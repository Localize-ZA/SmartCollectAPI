using Microsoft.EntityFrameworkCore;
using Pgvector;
using SmartCollectAPI.Data;
using SmartCollectAPI.Models;

namespace SmartCollectAPI.Services;

public interface IChunkSearchService
{
    Task<List<ChunkSearchResult>> SearchChunksBySimilarityAsync(
        Vector queryEmbedding,
        int limit = 10,
        float similarityThreshold = 0.7f,
        CancellationToken cancellationToken = default);
    
    Task<List<ChunkSearchResult>> SearchChunksByDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);
    
    Task<HybridSearchResult> HybridSearchAsync(
        Vector queryEmbedding,
        string? queryText = null,
        int limit = 10,
        float similarityThreshold = 0.7f,
        CancellationToken cancellationToken = default);
}

public class ChunkSearchService : IChunkSearchService
{
    private readonly SmartCollectDbContext _dbContext;
    private readonly ILogger<ChunkSearchService> _logger;

    public ChunkSearchService(SmartCollectDbContext dbContext, ILogger<ChunkSearchService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<ChunkSearchResult>> SearchChunksBySimilarityAsync(
        Vector queryEmbedding,
        int limit = 10,
        float similarityThreshold = 0.7f,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching chunks by similarity (limit={Limit}, threshold={Threshold})", 
            limit, similarityThreshold);

        // Fetch all chunks with embeddings and compute similarity in memory
        // Note: In production, this should be optimized with proper pgvector operators
        var allChunks = await _dbContext.DocumentChunks
            .Where(dc => dc.Embedding != null)
            .Take(1000) // Limit to prevent memory issues
            .ToListAsync(cancellationToken);
        
        // Compute cosine distance in memory
        var results = allChunks
            .Select(dc => new
            {
                Chunk = dc,
                Distance = ComputeCosineDistance(dc.Embedding!, queryEmbedding)
            })
            .Where(x => 1 - x.Distance >= similarityThreshold)
            .OrderBy(x => x.Distance)
            .Take(limit)
            .ToList();

        var searchResults = new List<ChunkSearchResult>();
        foreach (var result in results)
        {
            var document = await _dbContext.Documents
                .FirstOrDefaultAsync(d => d.Id == result.Chunk.DocumentId, cancellationToken);

            searchResults.Add(new ChunkSearchResult
            {
                ChunkId = result.Chunk.Id,
                DocumentId = result.Chunk.DocumentId,
                ChunkIndex = result.Chunk.ChunkIndex,
                Content = result.Chunk.Content,
                StartOffset = result.Chunk.StartOffset,
                EndOffset = result.Chunk.EndOffset,
                Similarity = 1 - result.Distance,
                Metadata = result.Chunk.Metadata,
                DocumentUri = document?.SourceUri,
                DocumentMime = document?.Mime
            });
        }

        _logger.LogInformation("Found {Count} matching chunks", searchResults.Count);
        return searchResults;
    }

    public async Task<List<ChunkSearchResult>> SearchChunksByDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all chunks for document {DocumentId}", documentId);

        var chunks = await _dbContext.DocumentChunks
            .Where(dc => dc.DocumentId == documentId)
            .OrderBy(dc => dc.ChunkIndex)
            .ToListAsync(cancellationToken);

        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        var results = chunks.Select(chunk => new ChunkSearchResult
        {
            ChunkId = chunk.Id,
            DocumentId = chunk.DocumentId,
            ChunkIndex = chunk.ChunkIndex,
            Content = chunk.Content,
            StartOffset = chunk.StartOffset,
            EndOffset = chunk.EndOffset,
            Similarity = null, // No similarity since this is not a search
            Metadata = chunk.Metadata,
            DocumentUri = document?.SourceUri,
            DocumentMime = document?.Mime
        }).ToList();

        _logger.LogInformation("Retrieved {Count} chunks for document {DocumentId}", results.Count, documentId);
        return results;
    }

    public async Task<HybridSearchResult> HybridSearchAsync(
        Vector queryEmbedding,
        string? queryText = null,
        int limit = 10,
        float similarityThreshold = 0.7f,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Performing hybrid search (semantic + text, limit={Limit})", limit);

        // Semantic search results
        var semanticResults = await SearchChunksBySimilarityAsync(
            queryEmbedding, 
            limit * 2, // Get more for merging
            similarityThreshold, 
            cancellationToken);

        List<ChunkSearchResult>? textResults = null;

        // Text search results (if query text provided)
        if (!string.IsNullOrWhiteSpace(queryText))
        {
            // Use PostgreSQL full-text search
            var textMatches = await _dbContext.DocumentChunks
                .FromSqlRaw(@"
                    SELECT * FROM document_chunks
                    WHERE to_tsvector('english', content) @@ plainto_tsquery('english', {0})
                    ORDER BY ts_rank(to_tsvector('english', content), plainto_tsquery('english', {0})) DESC
                    LIMIT {1}",
                    queryText,
                    limit * 2)
                .ToListAsync(cancellationToken);

            textResults = new List<ChunkSearchResult>();
            foreach (var chunk in textMatches)
            {
                var document = await _dbContext.Documents
                    .FirstOrDefaultAsync(d => d.Id == chunk.DocumentId, cancellationToken);

                textResults.Add(new ChunkSearchResult
                {
                    ChunkId = chunk.Id,
                    DocumentId = chunk.DocumentId,
                    ChunkIndex = chunk.ChunkIndex,
                    Content = chunk.Content,
                    StartOffset = chunk.StartOffset,
                    EndOffset = chunk.EndOffset,
                    Similarity = null, // Text search doesn't have semantic similarity
                    Metadata = chunk.Metadata,
                    DocumentUri = document?.SourceUri,
                    DocumentMime = document?.Mime
                });
            }
        }

        // Merge and rank results (simple approach: combine unique chunks)
        var mergedResults = new Dictionary<int, ChunkSearchResult>();
        
        // Add semantic results first (higher priority)
        foreach (var result in semanticResults)
        {
            mergedResults[result.ChunkId] = result;
        }

        // Add text results if not already present
        if (textResults != null)
        {
            foreach (var result in textResults.Where(r => !mergedResults.ContainsKey(r.ChunkId)))
            {
                mergedResults[result.ChunkId] = result;
            }
        }

        var finalResults = mergedResults.Values
            .OrderByDescending(r => r.Similarity ?? 0) // Prioritize by similarity
            .Take(limit)
            .ToList();

        _logger.LogInformation("Hybrid search found {Count} results (semantic={SemanticCount}, text={TextCount})", 
            finalResults.Count, semanticResults.Count, textResults?.Count ?? 0);

        return new HybridSearchResult
        {
            Results = finalResults,
            SemanticResultCount = semanticResults.Count,
            TextResultCount = textResults?.Count ?? 0,
            TotalUniqueResults = mergedResults.Count
        };
    }

    private static float ComputeCosineDistance(Vector a, Vector b)
    {
        var arrayA = a.ToArray();
        var arrayB = b.ToArray();
        
        if (arrayA.Length != arrayB.Length)
            throw new ArgumentException("Vectors must have the same dimensions");
        
        float dotProduct = 0;
        float normA = 0;
        float normB = 0;
        
        for (int i = 0; i < arrayA.Length; i++)
        {
            dotProduct += arrayA[i] * arrayB[i];
            normA += arrayA[i] * arrayA[i];
            normB += arrayB[i] * arrayB[i];
        }
        
        normA = (float)Math.Sqrt(normA);
        normB = (float)Math.Sqrt(normB);
        
        if (normA == 0 || normB == 0)
            return 1.0f; // Maximum distance
        
        float cosineSimilarity = dotProduct / (normA * normB);
        return 1.0f - cosineSimilarity; // Convert to distance
    }
}

public class ChunkSearchResult
{
    public int ChunkId { get; set; }
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    public float? Similarity { get; set; }
    public string? Metadata { get; set; }
    public string? DocumentUri { get; set; }
    public string? DocumentMime { get; set; }
}

public class HybridSearchResult
{
    public List<ChunkSearchResult> Results { get; set; } = new();
    public int SemanticResultCount { get; set; }
    public int TextResultCount { get; set; }
    public int TotalUniqueResults { get; set; }
}
