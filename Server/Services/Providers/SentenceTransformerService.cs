using Microsoft.Extensions.Logging;
using Pgvector;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartCollectAPI.Services.Providers;

/// <summary>
/// Embedding service using sentence-transformers microservice for high-quality semantic embeddings
/// Provides 768-dimensional embeddings (vs spaCy's 300) for better search quality
/// </summary>
public class SentenceTransformerService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SentenceTransformerService> _logger;
    private const string EMBEDDING_BASE_URL = "http://localhost:5086";

    public int EmbeddingDimensions => 768; // all-mpnet-base-v2 dimensions
    public int MaxTokens => 384; // Maximum sequence length for the model

    public SentenceTransformerService(HttpClient httpClient, ILogger<SentenceTransformerService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // Configure HTTP client for sentence-transformers service
        _httpClient.BaseAddress = new Uri(EMBEDDING_BASE_URL);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
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

            _logger.LogInformation("Calling sentence-transformers service for embedding, text length: {TextLength}", text.Length);

            // Truncate if too long (rough estimation: 1 token ≈ 4 chars)
            var maxChars = MaxTokens * 4;
            if (text.Length > maxChars)
            {
                _logger.LogWarning("Text too long ({Length} chars), truncating to {MaxChars}", text.Length, maxChars);
                text = text.Substring(0, maxChars);
            }

            var request = new EmbeddingRequest
            {
                Text = text,
                Normalize = true // Normalize for cosine similarity
            };

            var jsonContent = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/v1/embed/single", httpContent, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Sentence-transformers service returned error {StatusCode}: {Error}", 
                    response.StatusCode, errorContent);
                
                return new EmbeddingResult(
                    Embedding: new Vector(new float[EmbeddingDimensions]),
                    Success: false,
                    ErrorMessage: $"Embedding service error: {response.StatusCode}"
                );
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var embeddingResponse = JsonSerializer.Deserialize<EmbeddingResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (embeddingResponse == null || !embeddingResponse.Success)
            {
                _logger.LogWarning("Sentence-transformers service returned unsuccessful result");
                return new EmbeddingResult(
                    Embedding: new Vector(new float[EmbeddingDimensions]),
                    Success: false,
                    ErrorMessage: embeddingResponse?.ErrorMessage ?? "Unknown error"
                );
            }

            if (embeddingResponse.Embedding == null || embeddingResponse.Embedding.Count != EmbeddingDimensions)
            {
                _logger.LogError("Unexpected embedding dimensions: {Actual} vs {Expected}", 
                    embeddingResponse.Embedding?.Count ?? 0, EmbeddingDimensions);
                return new EmbeddingResult(
                    Embedding: new Vector(new float[EmbeddingDimensions]),
                    Success: false,
                    ErrorMessage: $"Invalid embedding dimensions: {embeddingResponse.Embedding?.Count ?? 0}"
                );
            }

            _logger.LogInformation("Generated {Dimensions}-dimensional embedding using {Model}", 
                embeddingResponse.Dimensions, embeddingResponse.Model);

            return new EmbeddingResult(
                Embedding: new Vector(embeddingResponse.Embedding.ToArray())
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling sentence-transformers service");
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

            _logger.LogInformation("Calling sentence-transformers service for {Count} embeddings", textList.Count);

            // Truncate texts if needed
            var maxChars = MaxTokens * 4;
            var processedTexts = textList.Select(t => 
                t.Length > maxChars ? t.Substring(0, maxChars) : t
            ).ToList();

            var request = new BatchEmbeddingRequest
            {
                Texts = processedTexts,
                Normalize = true,
                BatchSize = 32
            };

            var jsonContent = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/v1/embed/batch", httpContent, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Sentence-transformers batch service returned error {StatusCode}: {Error}", 
                    response.StatusCode, errorContent);
                
                return new BatchEmbeddingResult(
                    Embeddings: new List<Vector>(),
                    Success: false,
                    ErrorMessage: $"Embedding service error: {response.StatusCode}"
                );
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var embeddingResponse = JsonSerializer.Deserialize<BatchEmbeddingResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (embeddingResponse == null || !embeddingResponse.Success)
            {
                _logger.LogWarning("Sentence-transformers batch service returned unsuccessful result");
                return new BatchEmbeddingResult(
                    Embeddings: new List<Vector>(),
                    Success: false,
                    ErrorMessage: embeddingResponse?.ErrorMessage ?? "Unknown error"
                );
            }

            var embeddings = embeddingResponse.Embeddings?
                .Select(e => new Vector(e.ToArray()))
                .ToList() ?? new List<Vector>();

            _logger.LogInformation("Generated {Count} embeddings using {Model}", 
                embeddings.Count, embeddingResponse.Model);

            return new BatchEmbeddingResult(
                Embeddings: embeddings,
                Success: true
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling sentence-transformers batch service");
            return new BatchEmbeddingResult(
                Embeddings: new List<Vector>(),
                Success: false,
                ErrorMessage: ex.Message
            );
        }
    }
}

// Request/Response models for sentence-transformers API
internal class EmbeddingRequest
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("normalize")]
    public bool Normalize { get; set; } = true;
}

internal class BatchEmbeddingRequest
{
    [JsonPropertyName("texts")]
    public List<string> Texts { get; set; } = new();

    [JsonPropertyName("normalize")]
    public bool Normalize { get; set; } = true;

    [JsonPropertyName("batch_size")]
    public int BatchSize { get; set; } = 32;
}

internal class EmbeddingResponse
{
    [JsonPropertyName("embedding")]
    public List<float>? Embedding { get; set; }

    [JsonPropertyName("dimensions")]
    public int Dimensions { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}

internal class BatchEmbeddingResponse
{
    [JsonPropertyName("embeddings")]
    public List<List<float>>? Embeddings { get; set; }

    [JsonPropertyName("dimensions")]
    public int Dimensions { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}
