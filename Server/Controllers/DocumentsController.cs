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
        // This is a placeholder for vector similarity search
        // In a full implementation, you'd use pgvector's <-> operator
        _logger.LogInformation("Vector similarity search requested for: {Query}", request.Query);

        // For now, return a simple text-based search
        var documents = await _context.Documents
            .Where(d => EF.Functions.ILike(d.Canonical.ToString(), $"%{request.Query}%"))
            .OrderByDescending(d => d.CreatedAt)
            .Take(request.Limit ?? 10)
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