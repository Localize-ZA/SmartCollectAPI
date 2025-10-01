using Microsoft.AspNetCore.Mvc;
using Pgvector;
using SmartCollectAPI.Services;
using SmartCollectAPI.Services.Providers;

namespace SmartCollectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChunkSearchController : ControllerBase
{
    private readonly IChunkSearchService _chunkSearchService;
    private readonly IEmbeddingProviderFactory _embeddingProviderFactory;
    private readonly ILogger<ChunkSearchController> _logger;

    public ChunkSearchController(
        IChunkSearchService chunkSearchService,
        IEmbeddingProviderFactory embeddingProviderFactory,
        ILogger<ChunkSearchController> logger)
    {
        _chunkSearchService = chunkSearchService;
        _embeddingProviderFactory = embeddingProviderFactory;
        _logger = logger;
    }

    /// <summary>
    /// Search chunks by semantic similarity
    /// </summary>
    [HttpPost("search")]
    public async Task<ActionResult<ChunkSearchResponse>> SearchChunks([FromBody] ChunkSearchRequest request)
    {
        try
        {
            _logger.LogInformation("Chunk search request: query='{Query}', limit={Limit}", 
                request.Query, request.Limit);

            // Generate embedding for the query using specified or default provider
            var embeddingService = string.IsNullOrWhiteSpace(request.Provider)
                ? _embeddingProviderFactory.GetDefaultProvider()
                : _embeddingProviderFactory.GetProvider(request.Provider);

            var embeddingResult = await embeddingService.GenerateEmbeddingAsync(request.Query);
            
            if (!embeddingResult.Success || embeddingResult.Embedding == null)
            {
                return BadRequest(new { error = "Failed to generate embedding for query" });
            }

            // Search chunks
            var results = await _chunkSearchService.SearchChunksBySimilarityAsync(
                embeddingResult.Embedding,
                request.Limit,
                request.SimilarityThreshold
            );

            return Ok(new ChunkSearchResponse
            {
                Query = request.Query,
                Provider = request.Provider ?? "default",
                ResultCount = results.Count,
                Results = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching chunks");
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Hybrid search: semantic + full-text
    /// </summary>
    [HttpPost("hybrid-search")]
    public async Task<ActionResult<HybridSearchResponse>> HybridSearch([FromBody] HybridSearchRequest request)
    {
        try
        {
            _logger.LogInformation("Hybrid search request: query='{Query}', limit={Limit}", 
                request.Query, request.Limit);

            // Generate embedding for the query
            var embeddingService = string.IsNullOrWhiteSpace(request.Provider)
                ? _embeddingProviderFactory.GetDefaultProvider()
                : _embeddingProviderFactory.GetProvider(request.Provider);

            var embeddingResult = await embeddingService.GenerateEmbeddingAsync(request.Query);
            
            if (!embeddingResult.Success || embeddingResult.Embedding == null)
            {
                return BadRequest(new { error = "Failed to generate embedding for query" });
            }

            // Perform hybrid search
            var hybridResult = await _chunkSearchService.HybridSearchAsync(
                embeddingResult.Embedding,
                request.Query,
                request.Limit,
                request.SimilarityThreshold
            );

            return Ok(new HybridSearchResponse
            {
                Query = request.Query,
                Provider = request.Provider ?? "default",
                SemanticResultCount = hybridResult.SemanticResultCount,
                TextResultCount = hybridResult.TextResultCount,
                TotalUniqueResults = hybridResult.TotalUniqueResults,
                Results = hybridResult.Results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing hybrid search");
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Get all chunks for a specific document
    /// </summary>
    [HttpGet("document/{documentId}")]
    public async Task<ActionResult<DocumentChunksResponse>> GetDocumentChunks(Guid documentId)
    {
        try
        {
            _logger.LogInformation("Retrieving chunks for document {DocumentId}", documentId);

            var results = await _chunkSearchService.SearchChunksByDocumentAsync(documentId);

            return Ok(new DocumentChunksResponse
            {
                DocumentId = documentId,
                ChunkCount = results.Count,
                Chunks = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document chunks");
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }
}

// Request/Response Models
public class ChunkSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public int Limit { get; set; } = 10;
    public float SimilarityThreshold { get; set; } = 0.7f;
}

public class ChunkSearchResponse
{
    public string Query { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public int ResultCount { get; set; }
    public List<ChunkSearchResult> Results { get; set; } = new();
}

public class HybridSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public int Limit { get; set; } = 10;
    public float SimilarityThreshold { get; set; } = 0.7f;
}

public class HybridSearchResponse
{
    public string Query { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public int SemanticResultCount { get; set; }
    public int TextResultCount { get; set; }
    public int TotalUniqueResults { get; set; }
    public List<ChunkSearchResult> Results { get; set; } = new();
}

public class DocumentChunksResponse
{
    public Guid DocumentId { get; set; }
    public int ChunkCount { get; set; }
    public List<ChunkSearchResult> Chunks { get; set; } = new();
}
