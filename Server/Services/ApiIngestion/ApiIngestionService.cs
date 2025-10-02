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
    public List<string> Warnings { get; set; } = [];
    
    // Pagination metrics
    public int PagesProcessed { get; set; }
    public int TotalRecords { get; set; }
    public long PaginationTimeMs { get; set; }
    public bool MaxPagesReached { get; set; }
}

public class ApiIngestionService(
    SmartCollectDbContext context,
    IApiClientFactory apiClientFactory,
    IDataTransformer transformer,
    ILogger<ApiIngestionService> logger) : IApiIngestionService
{
    private readonly SmartCollectDbContext _context = context;
    private readonly IApiClientFactory _apiClientFactory = apiClientFactory;
    private readonly IDataTransformer _transformer = transformer;
    private readonly ILogger<ApiIngestionService> _logger = logger;

    public async Task<bool> TestConnectionAsync(Guid sourceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var source = await _context.ApiSources.FindAsync([sourceId], cancellationToken);

            if (source == null)
            {
                _logger.LogWarning("API source {SourceId} not found", sourceId);
                return false;
            }

            var apiClient = _apiClientFactory.GetClient(source);
            return await apiClient.TestConnectionAsync(source, cancellationToken);
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
                .FirstOrDefaultAsync(s => s.Id == sourceId, cancellationToken) ?? throw new InvalidOperationException($"API source {sourceId} not found");
            _logger.LogInformation(
                "Starting ingestion for source {SourceId} - {SourceName}",
                sourceId,
                source.Name
            );

            // Step 1: Fetch data from API (with pagination support)
            var paginationStopwatch = Stopwatch.StartNew();
            
            // Parse pagination configuration
            PaginationType paginationType = PaginationType.None;
            PaginationConfig? paginationConfig = null;
            
            if (!string.IsNullOrEmpty(source.PaginationType))
            {
                if (Enum.TryParse<PaginationType>(source.PaginationType, out var parsedType))
                {
                    paginationType = parsedType;
                }
            }
            
            if (!string.IsNullOrEmpty(source.PaginationConfig))
            {
                try
                {
                    paginationConfig = System.Text.Json.JsonSerializer.Deserialize<PaginationConfig>(source.PaginationConfig);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse pagination config for {SourceName}, using defaults", source.Name);
                }
            }

            // Get appropriate API client (REST or GraphQL)
            var apiClient = _apiClientFactory.GetClient(source);
            
            // Fetch all pages
            var paginatedResult = await apiClient.FetchAllPagesAsync(
                source, 
                paginationType, 
                paginationConfig, 
                cancellationToken
            );
            
            paginationStopwatch.Stop();

            if (!paginatedResult.Success)
            {
                throw new InvalidOperationException($"API request failed: {paginatedResult.ErrorMessage}");
            }

            _logger.LogInformation(
                "Fetched {PageCount} pages from {SourceName} in {Duration}ms. Total records: {RecordCount}",
                paginatedResult.TotalPages,
                source.Name,
                paginatedResult.TotalTimeMs,
                paginatedResult.TotalRecords
            );

            if (paginatedResult.MaxPagesReached)
            {
                _logger.LogWarning(
                    "Max pages limit reached for {SourceName}. More data may be available.",
                    source.Name
                );
                result.Warnings.Add($"Max pages limit ({paginationConfig?.MaxPages ?? 50}) reached. More data may be available.");
            }

            // Update log with pagination metrics
            log.PagesProcessed = paginatedResult.TotalPages;
            log.TotalPages = paginatedResult.TotalPages;
            log.PaginationTimeMs = paginatedResult.TotalTimeMs;
            log.MaxPagesReached = paginatedResult.MaxPagesReached;
            log.HttpStatusCode = paginatedResult.Pages.FirstOrDefault()?.HttpStatusCode ?? 0;
            log.ResponseSizeBytes = paginatedResult.Pages.Sum(p => p.ResponseSizeBytes);
            
            // Store detailed pagination metrics as JSON
            if (paginatedResult.PageFetchTimes.Count > 0)
            {
                var paginationMetrics = new
                {
                    page_fetch_times = paginatedResult.PageFetchTimes,
                    avg_page_time_ms = paginatedResult.PageFetchTimes.Average(),
                    min_page_time_ms = paginatedResult.PageFetchTimes.Min(),
                    max_page_time_ms = paginatedResult.PageFetchTimes.Max(),
                    total_pages = paginatedResult.TotalPages,
                    total_records = paginatedResult.TotalRecords,
                    pagination_type = paginationType.ToString(),
                    max_pages_reached = paginatedResult.MaxPagesReached
                };
                log.PaginationMetrics = System.Text.Json.JsonSerializer.Serialize(paginationMetrics);
            }

            // Step 2: Transform data from all pages
            var allTransformedDocuments = new List<TransformedDocument>();
            
            foreach (var page in paginatedResult.Pages)
            {
                var transformedDocuments = await _transformer.TransformAsync(source, page, cancellationToken);
                allTransformedDocuments.AddRange(transformedDocuments);
                
                _logger.LogDebug(
                    "Transformed {Count} documents from page",
                    transformedDocuments.Count
                );
            }

            log.RecordsFetched = allTransformedDocuments.Count;
            result.RecordsFetched = allTransformedDocuments.Count;
            result.PagesProcessed = paginatedResult.TotalPages;
            result.TotalRecords = paginatedResult.TotalRecords;
            result.PaginationTimeMs = paginatedResult.TotalTimeMs;
            result.MaxPagesReached = paginatedResult.MaxPagesReached;

            _logger.LogInformation(
                "Transformed {Count} documents from {SourceName} ({Pages} pages)",
                allTransformedDocuments.Count,
                source.Name,
                paginatedResult.TotalPages
            );

            // Step 3: Create staging documents
            int created = 0;
            int failed = 0;
            var errors = new List<string>();

            foreach (var transformedDoc in allTransformedDocuments)
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
            var source = await _context.ApiSources.FindAsync([sourceId], cancellationToken);
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
