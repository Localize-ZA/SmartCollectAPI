using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCollectAPI.Data;
using SmartCollectAPI.Models;
using SmartCollectAPI.Services.ApiIngestion;

namespace SmartCollectAPI.Controllers;

[ApiController]
[Route("api/sources")]
public class ApiSourcesController : ControllerBase
{
    private readonly SmartCollectDbContext _context;
    private readonly ILogger<ApiSourcesController> _logger;
    private readonly IAuthenticationManager _authManager;
    private readonly IApiClient _apiClient;
    private readonly IDataTransformer _transformer;
    private readonly IApiIngestionService _ingestionService;

    public ApiSourcesController(
        SmartCollectDbContext context,
        ILogger<ApiSourcesController> logger,
        IAuthenticationManager authManager,
        IApiClient apiClient,
        IDataTransformer transformer,
        IApiIngestionService ingestionService)
    {
        _context = context;
        _logger = logger;
        _authManager = authManager;
        _apiClient = apiClient;
        _transformer = transformer;
        _ingestionService = ingestionService;
    }

    /// <summary>
    /// Get all API sources
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ApiSourceDto>>> GetAllSources(
        [FromQuery] string? apiType = null,
        [FromQuery] bool? enabled = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var query = _context.ApiSources.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(apiType))
            {
                query = query.Where(s => s.ApiType == apiType);
            }

            if (enabled.HasValue)
            {
                query = query.Where(s => s.Enabled == enabled.Value);
            }

            // Pagination
            var total = await query.CountAsync();
            var sources = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var dtos = sources.Select(s => MapToDto(s, includeSecrets: false)).ToList();

            Response.Headers.Append("X-Total-Count", total.ToString());
            Response.Headers.Append("X-Page", page.ToString());
            Response.Headers.Append("X-Page-Size", pageSize.ToString());

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve API sources");
            return StatusCode(500, new { error = "Failed to retrieve API sources" });
        }
    }

    /// <summary>
    /// Get a specific API source by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiSourceDto>> GetSource(Guid id)
    {
        try
        {
            var source = await _context.ApiSources.FindAsync(id);

            if (source == null)
            {
                return NotFound(new { error = "API source not found" });
            }

            return Ok(MapToDto(source, includeSecrets: false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve API source {SourceId}", id);
            return StatusCode(500, new { error = "Failed to retrieve API source" });
        }
    }

    /// <summary>
    /// Create a new API source
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiSourceDto>> CreateSource([FromBody] CreateApiSourceDto dto)
    {
        try
        {
            // Validate required fields
            if (string.IsNullOrEmpty(dto.Name) || string.IsNullOrEmpty(dto.EndpointUrl))
            {
                return BadRequest(new { error = "Name and EndpointUrl are required" });
            }

            var source = new ApiSource
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Description = dto.Description,
                ApiType = dto.ApiType ?? "REST",
                EndpointUrl = dto.EndpointUrl,
                HttpMethod = dto.HttpMethod ?? "GET",
                AuthType = dto.AuthType ?? "None",
                CustomHeaders = dto.CustomHeaders,
                RequestBody = dto.RequestBody,
                QueryParams = dto.QueryParams,
                ResponsePath = dto.ResponsePath ?? "$",
                FieldMappings = dto.FieldMappings,
                PaginationType = dto.PaginationType,
                PaginationConfig = dto.PaginationConfig,
                ScheduleCron = dto.ScheduleCron,
                Enabled = dto.Enabled ?? false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Encrypt authentication config
            if (dto.AuthConfig != null && dto.AuthConfig.Count > 0)
            {
                source.AuthConfigEncrypted = _authManager.EncryptCredentials(dto.AuthConfig);
            }

            _context.ApiSources.Add(source);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new API source: {SourceId} - {SourceName}", source.Id, source.Name);

            return CreatedAtAction(
                nameof(GetSource),
                new { id = source.Id },
                MapToDto(source, includeSecrets: false)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create API source");
            return StatusCode(500, new { error = "Failed to create API source" });
        }
    }

    /// <summary>
    /// Update an existing API source
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiSourceDto>> UpdateSource(Guid id, [FromBody] UpdateApiSourceDto dto)
    {
        try
        {
            var source = await _context.ApiSources.FindAsync(id);

            if (source == null)
            {
                return NotFound(new { error = "API source not found" });
            }

            // Update fields
            if (dto.Name != null) source.Name = dto.Name;
            if (dto.Description != null) source.Description = dto.Description;
            if (dto.ApiType != null) source.ApiType = dto.ApiType;
            if (dto.EndpointUrl != null) source.EndpointUrl = dto.EndpointUrl;
            if (dto.HttpMethod != null) source.HttpMethod = dto.HttpMethod;
            if (dto.AuthType != null) source.AuthType = dto.AuthType;
            if (dto.CustomHeaders != null) source.CustomHeaders = dto.CustomHeaders;
            if (dto.RequestBody != null) source.RequestBody = dto.RequestBody;
            if (dto.QueryParams != null) source.QueryParams = dto.QueryParams;
            if (dto.ResponsePath != null) source.ResponsePath = dto.ResponsePath;
            if (dto.FieldMappings != null) source.FieldMappings = dto.FieldMappings;
            if (dto.PaginationType != null) source.PaginationType = dto.PaginationType;
            if (dto.PaginationConfig != null) source.PaginationConfig = dto.PaginationConfig;
            if (dto.ScheduleCron != null) source.ScheduleCron = dto.ScheduleCron;
            if (dto.Enabled.HasValue) source.Enabled = dto.Enabled.Value;

            // Update authentication config if provided
            if (dto.AuthConfig != null && dto.AuthConfig.Count > 0)
            {
                source.AuthConfigEncrypted = _authManager.EncryptCredentials(dto.AuthConfig);
            }

            source.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated API source: {SourceId} - {SourceName}", source.Id, source.Name);

            return Ok(MapToDto(source, includeSecrets: false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update API source {SourceId}", id);
            return StatusCode(500, new { error = "Failed to update API source" });
        }
    }

    /// <summary>
    /// Delete an API source
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSource(Guid id)
    {
        try
        {
            var source = await _context.ApiSources.FindAsync(id);

            if (source == null)
            {
                return NotFound(new { error = "API source not found" });
            }

            _context.ApiSources.Remove(source);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted API source: {SourceId} - {SourceName}", source.Id, source.Name);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete API source {SourceId}", id);
            return StatusCode(500, new { error = "Failed to delete API source" });
        }
    }

    /// <summary>
    /// Get ingestion logs for a specific source
    /// </summary>
    [HttpGet("{id}/logs")]
    public async Task<ActionResult<List<ApiIngestionLogDto>>> GetSourceLogs(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var source = await _context.ApiSources.FindAsync(id);

            if (source == null)
            {
                return NotFound(new { error = "API source not found" });
            }

            var query = _context.ApiIngestionLogs
                .Where(l => l.SourceId == id)
                .OrderByDescending(l => l.StartedAt);

            var total = await query.CountAsync();
            var logs = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var dtos = logs.Select(MapLogToDto).ToList();

            Response.Headers.Append("X-Total-Count", total.ToString());
            Response.Headers.Append("X-Page", page.ToString());
            Response.Headers.Append("X-Page-Size", pageSize.ToString());

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve logs for source {SourceId}", id);
            return StatusCode(500, new { error = "Failed to retrieve logs" });
        }
    }

    /// <summary>
    /// Test connection to an API source
    /// </summary>
    [HttpPost("{id}/test-connection")]
    public async Task<ActionResult<TestConnectionResultDto>> TestConnection(Guid id)
    {
        try
        {
            var source = await _context.ApiSources.FindAsync(id);

            if (source == null)
            {
                return NotFound(new { error = "API source not found" });
            }

            _logger.LogInformation("Testing connection for source {SourceId} - {SourceName}", id, source.Name);

            var success = await _ingestionService.TestConnectionAsync(id);

            return Ok(new TestConnectionResultDto
            {
                Success = success,
                Message = success
                    ? "Connection successful"
                    : "Connection failed. Check logs for details.",
                TestedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test connection for source {SourceId}", id);
            return Ok(new TestConnectionResultDto
            {
                Success = false,
                Message = $"Connection test failed: {ex.Message}",
                TestedAt = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Manually trigger ingestion for a specific source
    /// </summary>
    [HttpPost("{id}/trigger")]
    public async Task<ActionResult<TriggerIngestionResultDto>> TriggerIngestion(Guid id)
    {
        try
        {
            var source = await _context.ApiSources.FindAsync(id);

            if (source == null)
            {
                return NotFound(new { error = "API source not found" });
            }

            _logger.LogInformation(
                "Manual ingestion triggered for source {SourceId} - {SourceName}",
                id,
                source.Name
            );

            var result = await _ingestionService.ExecuteIngestionAsync(id);

            return Ok(new TriggerIngestionResultDto
            {
                Success = result.Success,
                LogId = result.LogId,
                RecordsFetched = result.RecordsFetched,
                DocumentsCreated = result.DocumentsCreated,
                DocumentsFailed = result.DocumentsFailed,
                ExecutionTimeMs = result.ExecutionTimeMs,
                ErrorMessage = result.ErrorMessage,
                Warnings = result.Warnings,
                TriggeredAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger ingestion for source {SourceId}", id);
            return StatusCode(500, new
            {
                success = false,
                error = $"Failed to trigger ingestion: {ex.Message}"
            });
        }
    }

    private ApiSourceDto MapToDto(ApiSource source, bool includeSecrets = false)
    {
        return new ApiSourceDto
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description,
            ApiType = source.ApiType,
            EndpointUrl = source.EndpointUrl,
            HttpMethod = source.HttpMethod,
            AuthType = source.AuthType,
            CustomHeaders = source.CustomHeaders,
            RequestBody = source.RequestBody,
            QueryParams = source.QueryParams,
            ResponsePath = source.ResponsePath,
            FieldMappings = source.FieldMappings,
            PaginationType = source.PaginationType,
            PaginationConfig = source.PaginationConfig,
            ScheduleCron = source.ScheduleCron,
            Enabled = source.Enabled,
            LastRunAt = source.LastRunAt,
            NextRunAt = source.NextRunAt,
            LastStatus = source.LastStatus,
            ConsecutiveFailures = source.ConsecutiveFailures,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt
        };
    }

    private ApiIngestionLogDto MapLogToDto(ApiIngestionLog log)
    {
        return new ApiIngestionLogDto
        {
            Id = log.Id,
            SourceId = log.SourceId,
            StartedAt = log.StartedAt,
            CompletedAt = log.CompletedAt,
            Status = log.Status,
            RecordsFetched = log.RecordsFetched,
            DocumentsCreated = log.DocumentsCreated,
            DocumentsFailed = log.DocumentsFailed,
            ErrorsCount = log.ErrorsCount,
            ErrorMessage = log.ErrorMessage,
            HttpStatusCode = log.HttpStatusCode,
            ResponseSizeBytes = log.ResponseSizeBytes,
            ExecutionTimeMs = log.ExecutionTimeMs,
            PagesProcessed = log.PagesProcessed,
            TotalPages = log.TotalPages
        };
    }
}

// DTOs
public class ApiSourceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ApiType { get; set; } = string.Empty;
    public string EndpointUrl { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string? AuthType { get; set; }
    public string? CustomHeaders { get; set; }
    public string? RequestBody { get; set; }
    public string? QueryParams { get; set; }
    public string? ResponsePath { get; set; }
    public string? FieldMappings { get; set; }
    public string? PaginationType { get; set; }
    public string? PaginationConfig { get; set; }
    public string? ScheduleCron { get; set; }
    public bool Enabled { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public string? LastStatus { get; set; }
    public int ConsecutiveFailures { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateApiSourceDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ApiType { get; set; }
    public string EndpointUrl { get; set; } = string.Empty;
    public string? HttpMethod { get; set; }
    public string? AuthType { get; set; }
    public Dictionary<string, string>? AuthConfig { get; set; }
    public string? CustomHeaders { get; set; }
    public string? RequestBody { get; set; }
    public string? QueryParams { get; set; }
    public string? ResponsePath { get; set; }
    public string? FieldMappings { get; set; }
    public string? PaginationType { get; set; }
    public string? PaginationConfig { get; set; }
    public string? ScheduleCron { get; set; }
    public bool? Enabled { get; set; }
}

public class UpdateApiSourceDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? ApiType { get; set; }
    public string? EndpointUrl { get; set; }
    public string? HttpMethod { get; set; }
    public string? AuthType { get; set; }
    public Dictionary<string, string>? AuthConfig { get; set; }
    public string? CustomHeaders { get; set; }
    public string? RequestBody { get; set; }
    public string? QueryParams { get; set; }
    public string? ResponsePath { get; set; }
    public string? FieldMappings { get; set; }
    public string? PaginationType { get; set; }
    public string? PaginationConfig { get; set; }
    public string? ScheduleCron { get; set; }
    public bool? Enabled { get; set; }
}

public class ApiIngestionLogDto
{
    public Guid Id { get; set; }
    public Guid SourceId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Status { get; set; }
    public int RecordsFetched { get; set; }
    public int DocumentsCreated { get; set; }
    public int DocumentsFailed { get; set; }
    public int ErrorsCount { get; set; }
    public string? ErrorMessage { get; set; }
    public int? HttpStatusCode { get; set; }
    public long? ResponseSizeBytes { get; set; }
    public long? ExecutionTimeMs { get; set; }
    public int? PagesProcessed { get; set; }
    public int? TotalPages { get; set; }
}

public class TestConnectionResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime TestedAt { get; set; }
}

public class TriggerIngestionResultDto
{
    public bool Success { get; set; }
    public Guid LogId { get; set; }
    public int RecordsFetched { get; set; }
    public int DocumentsCreated { get; set; }
    public int DocumentsFailed { get; set; }
    public long ExecutionTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = new();
    public DateTime TriggeredAt { get; set; }
}
