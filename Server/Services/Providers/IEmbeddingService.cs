using Pgvector;

namespace SmartCollectAPI.Services.Providers;

public interface IEmbeddingService
{
    Task<EmbeddingResult> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    Task<BatchEmbeddingResult> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);
    int EmbeddingDimensions { get; }
    int MaxTokens { get; }
}

public record EmbeddingResult(
    Vector Embedding,
    bool Success = true,
    string? ErrorMessage = null
);

public record BatchEmbeddingResult(
    List<Vector> Embeddings,
    bool Success = true,
    string? ErrorMessage = null
);