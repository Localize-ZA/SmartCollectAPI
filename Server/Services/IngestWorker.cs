using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using SmartCollectAPI.Models;
using SmartCollectAPI.Data;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace SmartCollectAPI.Services;

public class IngestWorker : BackgroundService
{
    private readonly ILogger<IngestWorker> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RedisJobQueue _jobQueue;

    private const string StreamName = "ingest-stream";
    private const int MaxRetryAttempts = 3;
    private readonly string _processedRoot;

    public IngestWorker(
        ILogger<IngestWorker> logger,
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopeFactory,
        IJobQueue jobQueue)
    {
        _logger = logger;
        _redis = redis;
        _scopeFactory = scopeFactory;
        _jobQueue = jobQueue as RedisJobQueue ?? throw new ArgumentException("JobQueue must be RedisJobQueue");
        
        // For backup/debugging purposes
        _processedRoot = Path.Combine(AppContext.BaseDirectory, "processed");
        Directory.CreateDirectory(_processedRoot);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var db = _redis.GetDatabase();
        var group = "worker-group";
        var consumer = Environment.MachineName;

        // Create consumer group if missing
        try { await db.StreamCreateConsumerGroupAsync(StreamName, group, "$", true); }
        catch { /* ignore if exists */ }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var entries = await db.StreamReadGroupAsync(StreamName, group, consumer, ">", count: 1, noAck: false);
                if (entries is null || entries.Length == 0)
                {
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }

                foreach (var e in entries)
                {
                    StagingDocument? stagingDoc = null;
                    try
                    {
                        var fields = e.Values.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
                        if (!fields.TryGetValue("job", out var jobJson))
                        {
                            _logger.LogWarning("Entry {Id} has no job field", e.Id);
                            await db.StreamAcknowledgeAsync(StreamName, group, e.Id);
                            continue;
                        }
                        var job = JsonSerializer.Deserialize<JobEnvelope>(jobJson);
                        if (job is null)
                        {
                            _logger.LogWarning("Entry {Id} job payload invalid", e.Id);
                            await db.StreamAcknowledgeAsync(StreamName, group, e.Id);
                            continue;
                        }

                        _logger.LogInformation("Processing job {JobId} for file {SourceUri}", job.JobId, job.SourceUri);

                        // Create or update staging document
                        using var scope = _scopeFactory.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<SmartCollectDbContext>();
                        var pipeline = scope.ServiceProvider.GetRequiredService<IDocumentProcessingPipeline>();
                        
                        stagingDoc = await dbContext.StagingDocuments
                            .FirstOrDefaultAsync(sd => sd.JobId == job.JobId.ToString(), stoppingToken);
                        
                        if (stagingDoc == null)
                        {
                            stagingDoc = new StagingDocument
                            {
                                JobId = job.JobId.ToString(),
                                SourceUri = job.SourceUri,
                                Mime = job.MimeType,
                                Sha256 = job.Sha256,
                                Status = "processing",
                                Attempts = 1,
                                RawMetadata = JsonSerializer.SerializeToNode(job)
                            };
                            dbContext.StagingDocuments.Add(stagingDoc);
                        }
                        else
                        {
                            stagingDoc.Status = "processing";
                            stagingDoc.Attempts++;
                            stagingDoc.UpdatedAt = DateTimeOffset.UtcNow;
                        }
                        
                        await dbContext.SaveChangesAsync(stoppingToken);

                        // Check if document already exists (idempotency by SHA256)
                        var existingDoc = await dbContext.Documents
                            .FirstOrDefaultAsync(d => d.Sha256 == job.Sha256, stoppingToken);
                        
                        if (existingDoc != null)
                        {
                            _logger.LogInformation("Document with SHA256 {Sha256} already exists, skipping processing", job.Sha256);
                            
                            // Mark staging as done and ack the message
                            stagingDoc.Status = "done";
                            stagingDoc.UpdatedAt = DateTimeOffset.UtcNow;
                            await dbContext.SaveChangesAsync(stoppingToken);
                            await db.StreamAcknowledgeAsync(StreamName, group, e.Id);
                            continue;
                        }

                        // Process the document using the enhanced pipeline
                        var pipelineResult = await pipeline.ProcessDocumentAsync(job, stoppingToken);

                        if (pipelineResult.Success && pipelineResult.CanonicalDocument != null)
                        {
                            // Create document record with embedding (let Id auto-generate)
                            var document = new Document
                            {
                                SourceUri = job.SourceUri,
                                Mime = job.MimeType,
                                Sha256 = job.Sha256,
                                Canonical = JsonSerializer.SerializeToNode(pipelineResult.CanonicalDocument)!,
                                CreatedAt = DateTimeOffset.UtcNow,
                                UpdatedAt = DateTimeOffset.UtcNow,
                                Embedding = null // Will be set if embedding was successful
                            };

                            // Try to get embedding from the canonical document
                            if (pipelineResult.CanonicalDocument.EmbeddingDim > 0)
                            {
                                // Set the embedding vector from the pipeline result, if available
                                if (pipelineResult.CanonicalDocument.Embedding != null)
                                {
                                    // Convert float[] to Pgvector.Vector if necessary
                                    document.Embedding = new Vector(pipelineResult.CanonicalDocument.Embedding);
                                }
                                _logger.LogInformation("Document processed with {Dimensions} embedding dimensions", 
                                    pipelineResult.CanonicalDocument.EmbeddingDim);
                            }

                            dbContext.Documents.Add(document);
                            await dbContext.SaveChangesAsync(stoppingToken); // Save to get the document ID

                            // Save chunks if available
                            if (pipelineResult.ChunkEmbeddings != null && pipelineResult.ChunkEmbeddings.Any())
                            {
                                _logger.LogInformation("Saving {Count} chunks for document {DocumentId}", 
                                    pipelineResult.ChunkEmbeddings.Count, document.Id);
                                
                                foreach (var chunkEmb in pipelineResult.ChunkEmbeddings)
                                {
                                    var chunk = new DocumentChunk
                                    {
                                        DocumentId = document.Id,
                                        ChunkIndex = chunkEmb.ChunkIndex,
                                        Content = chunkEmb.Content,
                                        StartOffset = chunkEmb.StartOffset,
                                        EndOffset = chunkEmb.EndOffset,
                                        Embedding = chunkEmb.Embedding,
                                        Metadata = JsonSerializer.Serialize(chunkEmb.Metadata),
                                        CreatedAt = DateTime.UtcNow
                                    };
                                    
                                    dbContext.DocumentChunks.Add(chunk);
                                }
                                
                                await dbContext.SaveChangesAsync(stoppingToken);
                                _logger.LogInformation("Successfully saved {Count} chunks", pipelineResult.ChunkEmbeddings.Count);
                            }

                            // Update staging document
                            stagingDoc.Normalized = JsonSerializer.SerializeToNode(pipelineResult.CanonicalDocument);
                            stagingDoc.Status = "done";
                            stagingDoc.UpdatedAt = DateTimeOffset.UtcNow;

                            await dbContext.SaveChangesAsync(stoppingToken);

                            // Save processed document for backup/debugging
                            await SaveProcessedDocument(pipelineResult.CanonicalDocument, stoppingToken);

                            _logger.LogInformation("Successfully processed job {JobId}. Notification sent: {NotificationSent}", 
                                job.JobId, pipelineResult.NotificationResult?.Success == true);
                        }
                        else
                        {
                            // Mark as failed
                            stagingDoc.Status = "failed";
                            stagingDoc.UpdatedAt = DateTimeOffset.UtcNow;
                            await dbContext.SaveChangesAsync(stoppingToken);

                            _logger.LogError("Pipeline processing failed for job {JobId}: {Error}", 
                                job.JobId, pipelineResult.ErrorMessage);
                        }

                        await db.StreamAcknowledgeAsync(StreamName, group, e.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed processing entry {Id}", e.Id);
                        
                        // Check retry count from Redis message
                        var fields = e.Values.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
                        var retryCount = fields.TryGetValue("retry_count", out var retryCountStr) && 
                                        int.TryParse(retryCountStr, out var count) ? count : 0;
                        
                        // Update staging document status to failed if we have it
                        if (stagingDoc != null)
                        {
                            try
                            {
                                using var scope = _scopeFactory.CreateScope();
                                var dbContext = scope.ServiceProvider.GetRequiredService<SmartCollectDbContext>();
                                
                                var existingStagingDoc = await dbContext.StagingDocuments
                                    .FirstOrDefaultAsync(sd => sd.Id == stagingDoc.Id, stoppingToken);
                                    
                                if (existingStagingDoc != null)
                                {
                                    existingStagingDoc.Status = retryCount >= MaxRetryAttempts ? "failed" : "pending";
                                    existingStagingDoc.UpdatedAt = DateTimeOffset.UtcNow;
                                    await dbContext.SaveChangesAsync(stoppingToken);
                                }
                            }
                            catch (Exception dbEx)
                            {
                                _logger.LogError(dbEx, "Failed to update staging document status");
                            }
                        }
                        
                        // If max retries exceeded, move to DLQ
                        if (retryCount >= MaxRetryAttempts)
                        {
                            await _jobQueue.MoveToDeadLetterQueueAsync(StreamName, group, e.Id.ToString(), 
                                $"Max retries ({MaxRetryAttempts}) exceeded: {ex.Message}", stoppingToken);
                        }
                        else
                        {
                            // Don't ack; it can be retried later
                            _logger.LogInformation("Job {MessageId} will be retried. Current retry count: {RetryCount}", 
                                e.Id, retryCount);
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // Graceful shutdown
                break;
            }
            catch (Exception ex)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                _logger.LogError(ex, "Worker loop error");
                try { await Task.Delay(2000, stoppingToken); } catch (TaskCanceledException) { break; }
            }
        }
    }

    private async Task SaveProcessedDocument(CanonicalDocument canonical, CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTime.UtcNow;
            var outDir = Path.Combine(_processedRoot, now.ToString("yyyy"), now.ToString("MM"), now.ToString("dd"));
            Directory.CreateDirectory(outDir);
            var outFile = Path.Combine(outDir, canonical.Id + ".json");
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(canonical, options);
            await File.WriteAllTextAsync(outFile, json, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save processed document backup for {Id}", canonical.Id);
        }
    }
}
