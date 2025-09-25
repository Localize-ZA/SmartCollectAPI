using Microsoft.Extensions.Logging;
using Pgvector;
using System.Security.Cryptography;
using System.Text;

namespace SmartCollectAPI.Services.Providers;

public class SimpleEmbeddingService : IEmbeddingService
{
    private readonly ILogger<SimpleEmbeddingService> _logger;

    public int EmbeddingDimensions => 1536; // Match Vertex AI dimensions for compatibility
    public int MaxTokens => 8192;

    public SimpleEmbeddingService(ILogger<SimpleEmbeddingService> logger)
    {
        _logger = logger;
    }

    public async Task<EmbeddingResult> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new EmbeddingResult(
                    Embedding: new Vector(new float[EmbeddingDimensions]),
                    Success: false,
                    ErrorMessage: "Text cannot be empty"
                );
            }

            _logger.LogInformation("Generating simple hash-based embedding for text of length: {TextLength}", text.Length);

            // This is a very basic fallback that creates a deterministic embedding
            // based on text content. In production, you'd want to use a proper
            // embedding model like sentence-transformers
            await Task.Delay(50, cancellationToken); // Simulate processing time

            var embedding = GenerateHashBasedEmbedding(text);

            return new EmbeddingResult(
                Embedding: new Vector(embedding),
                Success: true
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating simple embedding");
            return new EmbeddingResult(
                Embedding: new Vector(new float[EmbeddingDimensions]),
                Success: false,
                ErrorMessage: ex.Message
            );
        }
    }

    public async Task<BatchEmbeddingResult> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        try
        {
            var textList = texts.ToList();
            if (!textList.Any())
            {
                return new BatchEmbeddingResult(
                    Embeddings: new List<Vector>(),
                    Success: true
                );
            }

            _logger.LogInformation("Generating {Count} simple embeddings", textList.Count);

            var embeddings = new List<Vector>();
            foreach (var text in textList)
            {
                var result = await GenerateEmbeddingAsync(text, cancellationToken);
                embeddings.Add(result.Embedding);
            }

            return new BatchEmbeddingResult(
                Embeddings: embeddings,
                Success: true
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating batch simple embeddings");
            return new BatchEmbeddingResult(
                Embeddings: new List<Vector>(),
                Success: false,
                ErrorMessage: ex.Message
            );
        }
    }

    private float[] GenerateHashBasedEmbedding(string text)
    {
        // Create a deterministic but distributed embedding based on text content
        var embedding = new float[EmbeddingDimensions];
        
        // Use SHA256 to create multiple hash seeds
        using var sha256 = SHA256.Create();
        var textBytes = Encoding.UTF8.GetBytes(text.ToLowerInvariant().Trim());
        
        // Generate multiple hash values to fill the embedding space
        for (int i = 0; i < EmbeddingDimensions; i += 32) // SHA256 produces 32 bytes
        {
            var seedBytesRaw = BitConverter.GetBytes(i);
            var seedBytes = new byte[seedBytesRaw.Length + textBytes.Length];
            Buffer.BlockCopy(seedBytesRaw, 0, seedBytes, 0, seedBytesRaw.Length);
            Buffer.BlockCopy(textBytes, 0, seedBytes, seedBytesRaw.Length, textBytes.Length);
            var hash = sha256.ComputeHash(seedBytes);
            
            // Convert hash bytes to floats in range [-1, 1]
            for (int j = 0; j < Math.Min(32, EmbeddingDimensions - i); j++)
            {
                // Normalize byte value (0-255) to float range (-1, 1)
                embedding[i + j] = (hash[j] - 127.5f) / 127.5f;
            }
        }

        // Normalize the vector to unit length
        var magnitude = Math.Sqrt(embedding.Sum(x => x * x));
        if (magnitude > 0)
        {
            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] = (float)(embedding[i] / magnitude);
            }
        }

        return embedding;
    }
}