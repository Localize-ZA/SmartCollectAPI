using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using SmartCollectAPI.Models;
using SmartCollectAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace SmartCollectAPI.Services;

public class IngestWorker : BackgroundService
{
    private readonly ILogger<IngestWorker> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IContentDetector _detector;
    private readonly IJsonParser _jsonParser;
    private readonly IXmlParser _xmlParser;
    private readonly ICsvParser _csvParser;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RedisJobQueue _jobQueue;

    private const string StreamName = "ingest-stream";
    private const int MaxRetryAttempts = 3;
    private readonly string _uploadsRoot;
    private readonly string _processedRoot;

    public IngestWorker(
        ILogger<IngestWorker> logger,
        IConnectionMultiplexer redis,
        IContentDetector detector,
        IJsonParser jsonParser,
        IXmlParser xmlParser,
        ICsvParser csvParser,
        IServiceScopeFactory scopeFactory,
        IJobQueue jobQueue)
    {
        _logger = logger;
        _redis = redis;
        _detector = detector;
        _jsonParser = jsonParser;
        _xmlParser = xmlParser;
        _csvParser = csvParser;
        _scopeFactory = scopeFactory;
        _jobQueue = jobQueue as RedisJobQueue ?? throw new ArgumentException("JobQueue must be RedisJobQueue");

        // Assume LocalStorageService default path 'uploads' when local. For now derive processed beside it.
        _uploadsRoot = Path.Combine(AppContext.BaseDirectory, "uploads");
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

                        // Create or update staging document
                        using var scope = _scopeFactory.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<SmartCollectDbContext>();
                        
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

                        var localPath = job.SourceUri;
                        var absPath = Path.IsPathRooted(localPath) ? localPath : Path.Combine(AppContext.BaseDirectory, localPath);
                        if (!File.Exists(absPath))
                        {
                            _logger.LogError("Source file missing: {Path}", absPath);
                            
                            // Update staging document status to failed
                            if (stagingDoc != null)
                            {
                                stagingDoc.Status = "failed";
                                stagingDoc.UpdatedAt = DateTimeOffset.UtcNow;
                                await dbContext.SaveChangesAsync(stoppingToken);
                            }
                            
                            await db.StreamAcknowledgeAsync(StreamName, group, e.Id);
                            continue;
                        }

                        // Insert into staging as 'processing' if repo configured
                        if (_stagingRepo is not null)
                        {
                            try { await _stagingRepo.InsertAsync(job.JobId.ToString(), job.SourceUri, job.MimeType, job.Sha256, null, "processing", stoppingToken); }
                            catch (Exception ex) { _logger.LogWarning(ex, "Staging insert failed (may already exist) for job {JobId}", job.JobId); }
                        }

                        await using var fs = File.OpenRead(absPath);
                        var mime = await _detector.DetectMimeAsync(fs, job.MimeType, stoppingToken);
                        fs.Position = 0;

                        System.Text.Json.Nodes.JsonNode? parsed = null;
                        if (mime == "application/json")
                            parsed = await _jsonParser.ParseAsync(fs, stoppingToken);
                        else if (mime == "text/xml" || mime == "application/xml")
                            parsed = await _xmlParser.ParseAsync(fs, stoppingToken);
                        else if (mime == "text/csv")
                            parsed = await _csvParser.ParseAsync(fs, stoppingToken);

                        var canonical = new Models.CanonicalDocument
                        {
                            Id = job.JobId,
                            SourceUri = job.SourceUri,
                            IngestTs = DateTimeOffset.UtcNow,
                            Mime = mime,
                            Structured = parsed is not null,
                            StructuredPayload = parsed
                        };

                        // Update staging document with normalized data
                        stagingDoc!.Normalized = JsonSerializer.SerializeToNode(canonical);
                        stagingDoc.Status = "done";
                        stagingDoc.UpdatedAt = DateTimeOffset.UtcNow;

                        // Check if document already exists (idempotency by SHA256)
                        var existingDoc = await dbContext.Documents
                            .FirstOrDefaultAsync(d => d.Sha256 == job.Sha256, stoppingToken);
                        
                        if (existingDoc == null)
                        {
                            var document = new Document
                            {
                                Id = job.JobId,
                                SourceUri = job.SourceUri,
                                Mime = mime,
                                Sha256 = job.Sha256,
                                Canonical = JsonSerializer.SerializeToNode(canonical)!,
                                CreatedAt = DateTimeOffset.UtcNow,
                                UpdatedAt = DateTimeOffset.UtcNow
                                // Embedding will be added later when vectorization is implemented
                            };
                            dbContext.Documents.Add(document);
                        }
                        else
                        {
                            _logger.LogInformation("Document with SHA256 {Sha256} already exists, skipping", job.Sha256);
                        }

                        await dbContext.SaveChangesAsync(stoppingToken);

                        // Still save to file for backup/debugging purposes
                        var now = DateTime.UtcNow;
                        var outDir = Path.Combine(_processedRoot, now.ToString("yyyy"), now.ToString("MM"), now.ToString("dd"));
                        Directory.CreateDirectory(outDir);
                        var outFile = Path.Combine(outDir, job.JobId + ".json");
                        await File.WriteAllTextAsync(outFile, JsonSerializer.Serialize(canonical, new JsonSerializerOptions { WriteIndented = true }), stoppingToken);

                        // Upsert into documents if repo configured
                        if (_documentsRepo is not null)
                        {
                            try { await _documentsRepo.UpsertAsync(job.SourceUri, mime, job.Sha256, canonical.StructuredPayload ?? new System.Text.Json.Nodes.JsonObject(), stoppingToken); }
                            catch (Exception ex) { _logger.LogError(ex, "Documents upsert failed for job {JobId}", job.JobId); }
                        }

                        if (_stagingRepo is not null)
                        {
                            try { await _stagingRepo.UpdateStatusAsync(job.JobId.ToString(), "done", stoppingToken); }
                            catch (Exception ex) { _logger.LogWarning(ex, "Staging status update failed for job {JobId}", job.JobId); }
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
                            await _jobQueue.MoveToDeadLetterQueueAsync(StreamName, group, e.Id, 
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
}
