using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SmartCollectAPI.Data;
using SmartCollectAPI.Models;

namespace SmartCollectAPI.Services.ApiIngestion;

public interface IApiIngestionService
{
    Task<ApiIngestionResult> ExecuteIngestionAsync(Guid sourceId, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(Guid sourceId, CancellationToken cancellationToken = default);
}

public class ApiIngestionResult
{
    public bool Success { get; set; }
    public Guid LogId { get; set; }
    public int RecordsFetched { get; set; }
    public int DocumentsCreated { get; set; }
    public int DocumentsFailed { get; set; }
    public long ExecutionTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class ApiIngestionService : IApiIngestionService
{
    private readonly SmartCollectDbContext _context;
    private readonly IApiClient _apiClient;
    private readonly IDataTransformer _transformer;
    private readonly ILogger<ApiIngestionService> _logger;

    public ApiIngestionService(
        SmartCollectDbContext context,
        IApiClient apiClient,
        IDataTransformer transformer,
        ILogger<ApiIngestionService> logger)
    {
        _context = context;
        _apiClient = apiClient;
        _transformer = transformer;
        _logger = logger;
    }

    public async Task<bool> TestConnectionAsync(Guid sourceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var source = await _context.ApiSources.FindAsync(new object[] { sourceId }, cancellationToken);

            if (source == null)
            {
                _logger.LogWarning("API source {SourceId} not found", sourceId);
                return false;
            }

            return await _apiClient.TestConnectionAsync(source, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test connection for source {SourceId}", sourceId);
            return false;
        }
    }

    public async Task<ApiIngestionResult> ExecuteIngestionAsync(
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ApiIngestionResult();

        // Create log entry
        var log = new ApiIngestionLog
        {
            Id = Guid.NewGuid(),
            SourceId = sourceId,
            StartedAt = DateTime.UtcNow,
            Status = "Running"
        };

        try
        {
            // Get source configuration
            var source = await _context.ApiSources
                .Include(s => s.IngestionLogs)
                .FirstOrDefaultAsync(s => s.Id == sourceId, cancellationToken);

            if (source == null)
            {
                throw new InvalidOperationException($"API source {sourceId} not found");
            }

            _logger.LogInformation(
                "Starting ingestion for source {SourceId} - {SourceName}",
                sourceId,
                source.Name
            );

            // Step 1: Fetch data from API
            var apiResponse = await _apiClient.FetchAsync(source, cancellationToken);
            
            log.HttpStatusCode = apiResponse.HttpStatusCode;
            log.ResponseSizeBytes = apiResponse.ResponseSizeBytes;

            if (!apiResponse.Success)
            {
                throw new InvalidOperationException($"API request failed: {apiResponse.ErrorMessage}");
            }

            _logger.LogInformation(
                "Fetched data from {SourceName}. Status: {StatusCode}, Size: {Size} bytes",
                source.Name,
                apiResponse.HttpStatusCode,
                apiResponse.ResponseSizeBytes
            );

            // Step 2: Transform data
            var transformedDocuments = await _transformer.TransformAsync(source, apiResponse, cancellationToken);

            log.RecordsFetched = transformedDocuments.Count;
            result.RecordsFetched = transformedDocuments.Count;

            _logger.LogInformation(
                "Transformed {Count} documents from {SourceName}",
                transformedDocuments.Count,
                source.Name
            );

            // Step 3: Create staging documents
            int created = 0;
            int failed = 0;
            var errors = new List<string>();

            foreach (var transformedDoc in transformedDocuments)
            {
                try
                {
                    // Create metadata JSON
                    var metadata = new JsonObject
                    {
                        ["title"] = transformedDoc.Title,
                        ["description"] = transformedDoc.Description,
                        ["source"] = transformedDoc.Source,
                        ["sourceUrl"] = transformedDoc.SourceUrl,
                        ["fileType"] = transformedDoc.FileType,
                        ["publishedAt"] = transformedDoc.PublishedAt?.ToString("o")
                    };

                    // Add custom metadata fields
                    if (transformedDoc.Metadata != null)
                    {
                        foreach (var kvp in transformedDoc.Metadata)
                        {
                            metadata[$"custom_{kvp.Key}"] = JsonValue.Create(kvp.Value);
                        }
                    }

                    // Create normalized JSON
                    var normalized = new JsonObject
                    {
                        ["content"] = transformedDoc.Content,
                        ["title"] = transformedDoc.Title
                    };

                    var stagingDoc = new StagingDocument
                    {
                        Id = Guid.NewGuid(),
                        JobId = $"api_ingestion_{source.Name}_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                        SourceUri = source.EndpointUrl,
                        Mime = "application/json",
                        Status = "pending",
                        RawMetadata = metadata,
                        Normalized = normalized,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };

                    _context.StagingDocuments.Add(stagingDoc);
                    created++;

                    _logger.LogDebug("Created staging document from: {Title}", transformedDoc.Title);
                }
                catch (Exception ex)
                {
                    failed++;
                    var errorMsg = $"Failed to create staging document: {ex.Message}";
                    errors.Add(errorMsg);
                    _logger.LogWarning(ex, errorMsg);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            log.DocumentsCreated = created;
            log.DocumentsFailed = failed;
            log.ErrorsCount = errors.Count;
            
            result.DocumentsCreated = created;
            result.DocumentsFailed = failed;
            result.Warnings.AddRange(errors);

            // Update source status
            source.LastRunAt = DateTime.UtcNow;
            source.LastStatus = "Success";
            source.ConsecutiveFailures = 0;
            source.UpdatedAt = DateTime.UtcNow;

            // Complete log
            stopwatch.Stop();
            log.CompletedAt = DateTime.UtcNow;
            log.ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds;
            log.Status = "Success";

            result.Success = true;
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation(
                "Ingestion completed for {SourceName}. Created: {Created}, Failed: {Failed}, Time: {Time}ms",
                source.Name,
                created,
                failed,
                stopwatch.ElapsedMilliseconds
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Update log with error
            log.CompletedAt = DateTime.UtcNow;
            log.ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds;
            log.Status = "Failed";
            log.ErrorMessage = ex.Message;
            log.StackTrace = ex.StackTrace;
            log.ErrorsCount = 1;

            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            // Update source failure count
            var source = await _context.ApiSources.FindAsync(new object[] { sourceId }, cancellationToken);
            if (source != null)
            {
                source.LastRunAt = DateTime.UtcNow;
                source.LastStatus = "Failed";
                source.ConsecutiveFailures++;
                source.UpdatedAt = DateTime.UtcNow;
            }

            _logger.LogError(ex, "Ingestion failed for source {SourceId}", sourceId);
        }
        finally
        {
            // Save log
            _context.ApiIngestionLogs.Add(log);
            await _context.SaveChangesAsync(cancellationToken);
            result.LogId = log.Id;
        }

        return result;
    }
}
