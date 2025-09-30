using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;
using SmartCollectAPI.Data;
using SmartCollectAPI.Models;

namespace SmartCollectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly ILogger<DocumentsController> _logger;
    private readonly SmartCollectDbContext _context;

    public DocumentsController(ILogger<DocumentsController> logger, SmartCollectDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Get all processed documents with pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<DocumentSummary>>> GetDocuments(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var skip = (page - 1) * pageSize;
        var totalCount = await _context.Documents.CountAsync(cancellationToken);

        var documents = await _context.Documents
            .OrderByDescending(d => d.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .Select(d => new DocumentSummary
            {
                Id = d.Id,
                SourceUri = d.SourceUri,
                Mime = d.Mime,
                Sha256 = d.Sha256,
                CreatedAt = d.CreatedAt,
                HasEmbedding = d.Embedding != null
            })
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<DocumentSummary>
        {
            Items = documents,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    /// <summary>
    /// Get a specific document by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Document>> GetDocument(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (document == null)
        {
            return NotFound();
        }

        return Ok(document);
    }

    /// <summary>
    /// Get staging documents (processing status)
    /// </summary>
    [HttpGet("staging")]
    public async Task<ActionResult<List<StagingDocument>>> GetStagingDocuments(
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.StagingDocuments.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(sd => sd.Status == status);
        }

        var stagingDocs = await query
            .OrderByDescending(sd => sd.UpdatedAt)
            .Take(50) // Limit to recent 50
            .ToListAsync(cancellationToken);

        return Ok(stagingDocs);
    }

    /// <summary>
    /// Get processing statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<ProcessingStats>> GetStats(CancellationToken cancellationToken = default)
    {
        var totalDocuments = await _context.Documents.CountAsync(cancellationToken);
        var documentsWithEmbeddings = await _context.Documents.CountAsync(d => d.Embedding != null, cancellationToken);
        
        var stagingStats = await _context.StagingDocuments
            .GroupBy(sd => sd.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var todayStart = DateTime.UtcNow.Date;
        var todayProcessed = await _context.Documents.CountAsync(d => d.CreatedAt >= todayStart, cancellationToken);

        return Ok(new ProcessingStats
        {
            TotalDocuments = totalDocuments,
            DocumentsWithEmbeddings = documentsWithEmbeddings,
            ProcessedToday = todayProcessed,
            StagingStatus = stagingStats.ToDictionary(s => s.Status ?? "unknown", s => s.Count)
        });
    }

    /// <summary>
    /// Search for similar documents using vector similarity (if embeddings exist)
    /// </summary>
    [HttpPost("search")]
    public async Task<ActionResult<List<DocumentSummary>>> SearchSimilar(
        [FromBody] SearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest("Search query is required.");
        }

        // This is a placeholder for vector similarity search
        // In a full implementation, you'd use pgvector's <-> operator
        _logger.LogInformation("Vector similarity search requested for: {Query}", request.Query);

        var limit = request.Limit ?? 10;
        if (limit < 1) limit = 10;
        if (limit > 100) limit = 100;

        var pattern = $"%{request.Query.Trim()}%";

        var documentsQuery = _context.Documents
            .FromSqlInterpolated($@"SELECT id, source_uri, mime, sha256, canonical, created_at, updated_at, embedding
                FROM documents
                WHERE canonical::text ILIKE {pattern}
                ORDER BY created_at DESC
                LIMIT {limit}")
            .AsNoTracking();

        var documents = await documentsQuery
            .Select(d => new DocumentSummary
            {
                Id = d.Id,
                SourceUri = d.SourceUri,
                Mime = d.Mime,
                Sha256 = d.Sha256,
                CreatedAt = d.CreatedAt,
                HasEmbedding = d.Embedding != null
            })
            .ToListAsync(cancellationToken);

        return Ok(documents);
    }

    /// <summary>
    /// Return documents most similar to the supplied document (vector similarity)
    /// </summary>
    [HttpGet("{id}/similar")]
    public async Task<ActionResult<List<DocumentSummary>>> GetSimilarDocuments(
        Guid id,
        [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        if (limit < 1) limit = 5;
        if (limit > 50) limit = 50;

        var source = await _context.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (source is null)
        {
            return NotFound();
        }

        if (source.Embedding is null)
        {
            return Ok(new List<DocumentSummary>());
        }

        var connectionString = _context.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogError("Database connection string not available when attempting similarity search.");
            return StatusCode(500, "Database connection is not configured.");
        }

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(@"
            SELECT id, source_uri, mime, sha256, created_at, embedding IS NOT NULL AS has_embedding
            FROM documents
            WHERE embedding IS NOT NULL AND id <> @id
            ORDER BY embedding <-> @embedding
            LIMIT @limit;", conn);

    cmd.Parameters.AddWithValue("id", id);
    var embeddingParameter = new NpgsqlParameter<Vector>("embedding", source.Embedding);
    cmd.Parameters.Add(embeddingParameter);
    cmd.Parameters.AddWithValue("limit", limit);

        var results = new List<DocumentSummary>();

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var createdAt = reader.GetDateTime(4);
            if (createdAt.Kind == DateTimeKind.Unspecified)
            {
                createdAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc);
            }

            var createdAtOffset = createdAt.Kind == DateTimeKind.Utc
                ? new DateTimeOffset(createdAt, TimeSpan.Zero)
                : new DateTimeOffset(createdAt);

            results.Add(new DocumentSummary
            {
                Id = reader.GetGuid(0),
                SourceUri = reader.GetString(1),
                Mime = reader.IsDBNull(2) ? null : reader.GetString(2),
                Sha256 = reader.IsDBNull(3) ? null : reader.GetString(3),
                CreatedAt = createdAtOffset,
                HasEmbedding = reader.GetBoolean(5)
            });
        }

        return Ok(results);
    }

    /// <summary>
    /// Get detailed document data including vector and normalized content
    /// </summary>
    [HttpGet("{id}/details")]
    public async Task<ActionResult<DocumentDetails>> GetDocumentDetails(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (document == null)
        {
            return NotFound();
        }

        // Get the corresponding staging document for processing info
        var stagingDoc = await _context.StagingDocuments
            .Where(sd => sd.Sha256 == document.Sha256)
            .OrderByDescending(sd => sd.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new DocumentDetails
        {
            Id = document.Id,
            SourceUri = document.SourceUri,
            Mime = document.Mime,
            Sha256 = document.Sha256,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt,
            Canonical = document.Canonical,
            HasEmbedding = document.Embedding != null,
            EmbeddingDimensions = document.Embedding?.ToArray().Length ?? 0,
            EmbeddingPreview = document.Embedding?.ToArray().Take(10).ToArray(), // First 10 dimensions for preview
            ProcessingInfo = stagingDoc != null ? new ProcessingInfo
            {
                Status = stagingDoc.Status,
                Attempts = stagingDoc.Attempts,
                RawMetadata = stagingDoc.RawMetadata,
                Normalized = stagingDoc.Normalized,
                ProcessedAt = stagingDoc.UpdatedAt
            } : null
        });
    }

    /// <summary>
    /// Send mock data to Redis for processing
    /// </summary>
    [HttpPost("redis/send")]
    public ActionResult SendMockDataToRedis([FromBody] MockDataRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("Content is required");
        }

        try
        {
            // We'll need to inject IConnectionMultiplexer to send to Redis
            // For now, return success and log the request
            var streamName = "mock-data-stream";
            var messageId = $"api-{Guid.NewGuid()}";
            
            _logger.LogInformation("Received mock data for Redis processing: Content='{Content}', Type='{ContentType}', Filename='{Filename}'", 
                request.Content.Substring(0, Math.Min(100, request.Content.Length)), 
                request.ContentType ?? "text/plain", 
                request.Filename ?? "mock-data.txt");

            var result = new
            {
                Message = "Mock data received and will be processed",
                StreamName = streamName,
                ContentLength = request.Content.Length,
                ContentType = request.ContentType ?? "text/plain",
                Filename = request.Filename ?? "mock-data.txt",
                Note = "To actually send to Redis, you can use: XADD mock-data-stream * content \"your content\" content_type \"text/plain\" filename \"test.txt\""
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process mock data request");
            return StatusCode(500, "Failed to process mock data");
        }
    }

    /// <summary>
    /// Get vector analysis for all documents with embeddings
    /// </summary>
    [HttpGet("vectors/analysis")]
    public async Task<ActionResult<VectorAnalysis>> GetVectorAnalysis(CancellationToken cancellationToken = default)
    {
        var documentsWithVectors = await _context.Documents
            .Where(d => d.Embedding != null)
            .Select(d => new { d.Id, d.Mime, d.CreatedAt, d.Embedding })
            .ToListAsync(cancellationToken);

        if (!documentsWithVectors.Any())
        {
            return Ok(new VectorAnalysis
            {
                TotalDocumentsWithVectors = 0,
                AverageDimensions = 0,
                MimeTypeDistribution = new Dictionary<string, int>(),
                DimensionStats = null
            });
        }

        var mimeDistribution = documentsWithVectors
            .GroupBy(d => d.Mime ?? "unknown")
            .ToDictionary(g => g.Key, g => g.Count());

        var dimensions = documentsWithVectors.Select(d => d.Embedding!.ToArray().Length).ToList();
        var avgDimensions = dimensions.Average();

        return Ok(new VectorAnalysis
        {
            TotalDocumentsWithVectors = documentsWithVectors.Count,
            AverageDimensions = avgDimensions,
            MimeTypeDistribution = mimeDistribution,
            DimensionStats = new DimensionStats
            {
                Min = dimensions.Min(),
                Max = dimensions.Max(),
                Average = avgDimensions,
                StandardDimension = dimensions.GroupBy(d => d).OrderByDescending(g => g.Count()).First().Key
            }
        });
    }
}

public record DocumentSummary
{
    public Guid Id { get; init; }
    public string SourceUri { get; init; } = string.Empty;
    public string? Mime { get; init; }
    public string? Sha256 { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public bool HasEmbedding { get; init; }
}

public record PagedResult<T>
{
    public List<T> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

public record ProcessingStats
{
    public int TotalDocuments { get; init; }
    public int DocumentsWithEmbeddings { get; init; }
    public int ProcessedToday { get; init; }
    public Dictionary<string, int> StagingStatus { get; init; } = new();
}

public record SearchRequest
{
    public string Query { get; init; } = string.Empty;
    public int? Limit { get; init; } = 10;
}

public record DocumentDetails
{
    public Guid Id { get; init; }
    public string SourceUri { get; init; } = string.Empty;
    public string? Mime { get; init; }
    public string? Sha256 { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public object Canonical { get; init; } = new();
    public bool HasEmbedding { get; init; }
    public int EmbeddingDimensions { get; init; }
    public float[]? EmbeddingPreview { get; init; }
    public ProcessingInfo? ProcessingInfo { get; init; }
}

public record ProcessingInfo
{
    public string? Status { get; init; }
    public int Attempts { get; init; }
    public object? RawMetadata { get; init; }
    public object? Normalized { get; init; }
    public DateTimeOffset ProcessedAt { get; init; }
}

public record VectorAnalysis
{
    public int TotalDocumentsWithVectors { get; init; }
    public double AverageDimensions { get; init; }
    public Dictionary<string, int> MimeTypeDistribution { get; init; } = new();
    public DimensionStats? DimensionStats { get; init; }
}

public record DimensionStats
{
    public int Min { get; init; }
    public int Max { get; init; }
    public double Average { get; init; }
    public int StandardDimension { get; init; }
}

public record MockDataRequest
{
    public string Content { get; init; } = string.Empty;
    public string? ContentType { get; init; }
    public string? Filename { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}
