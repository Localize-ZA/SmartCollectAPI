using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using Pgvector;

namespace SmartCollectAPI.Services.Providers;

/// <summary>
/// spaCy-powered NLP service that handles both entity extraction and embeddings
/// by calling the spaCy microservice at http://localhost:5084
/// </summary>
public class SpacyNlpService : IEntityExtractionService, IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SpacyNlpService> _logger;
    private const string SPACY_BASE_URL = "http://localhost:5084";

    public int EmbeddingDimensions => 96; // spaCy en_core_web_sm dimensions
    public int MaxTokens => 8192;

    public SpacyNlpService(HttpClient httpClient, ILogger<SpacyNlpService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // Configure HTTP client for spaCy service
        _httpClient.BaseAddress = new Uri(SPACY_BASE_URL);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<EntityExtractionResult> ExtractEntitiesAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Calling spaCy service for entity extraction on text of length: {TextLength}", text.Length);

            var spacyDocument = new
            {
                id = Guid.NewGuid().ToString(),
                content = text,
                source_uri = "pipeline_processing",
                metadata = new { }
            };

            var jsonContent = JsonSerializer.Serialize(spacyDocument);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/v1/process", httpContent, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("spaCy service returned error {StatusCode}: {Error}", response.StatusCode, errorContent);
                
                return new EntityExtractionResult(
                    Entities: new List<ExtractedEntity>(),
                    Sentiment: new SentimentAnalysis(0.0f, 0.0f, "NEUTRAL"),
                    Success: false,
                    ErrorMessage: $"spaCy service error: {response.StatusCode}"
                );
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var spacyResult = JsonSerializer.Deserialize<SpacyProcessedDocument>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (spacyResult?.Analysis == null)
            {
                _logger.LogWarning("spaCy service returned null analysis");
                return new EntityExtractionResult(
                    Entities: new List<ExtractedEntity>(),
                    Sentiment: new SentimentAnalysis(0.0f, 0.0f, "NEUTRAL"),
                    Success: false,
                    ErrorMessage: "spaCy returned null analysis"
                );
            }

            // Convert spaCy entities to our format
            var entities = spacyResult.Analysis.Entities?.Select(e => new ExtractedEntity(
                Name: e.Text ?? "",
                Type: e.Label ?? "UNKNOWN",
                Salience: (double)(e.Confidence ?? 0.5f),
                Mentions: new List<EntityMention>
                {
                    new EntityMention(
                        Text: e.Text ?? "",
                        StartOffset: 0, // spaCy doesn't provide this in our current format
                        EndOffset: (e.Text ?? "").Length
                    )
                }
            )).ToList() ?? new List<ExtractedEntity>();

            // Convert spaCy sentiment
            var sentiment = spacyResult.Analysis.Sentiment != null 
                ? new SentimentAnalysis(
                    Score: spacyResult.Analysis.Sentiment.Polarity ?? 0.0f,
                    Magnitude: Math.Abs(spacyResult.Analysis.Sentiment.Polarity ?? 0.0f),
                    Label: spacyResult.Analysis.Sentiment.Label ?? "NEUTRAL"
                )
                : new SentimentAnalysis(0.0f, 0.0f, "NEUTRAL");

            _logger.LogInformation("spaCy extracted {EntityCount} entities with sentiment: {Sentiment}", 
                entities.Count, sentiment.Label);

            return new EntityExtractionResult(
                Entities: entities,
                Sentiment: sentiment,
                Success: true,
                ErrorMessage: null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling spaCy service for entity extraction");
            return new EntityExtractionResult(
                Entities: new List<ExtractedEntity>(),
                Sentiment: new SentimentAnalysis(0.0f, 0.0f, "NEUTRAL"),
                Success: false,
                ErrorMessage: ex.Message
            );
        }
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

            _logger.LogInformation("Calling spaCy service for embedding generation on text of length: {TextLength}", text.Length);

            var spacyDocument = new
            {
                id = Guid.NewGuid().ToString(),
                content = text,
                source_uri = "embedding_generation",
                metadata = new { }
            };

            var jsonContent = JsonSerializer.Serialize(spacyDocument);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/v1/process", httpContent, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("spaCy service returned error {StatusCode}: {Error}", response.StatusCode, errorContent);
                
                return new EmbeddingResult(
                    Embedding: new Vector(new float[EmbeddingDimensions]),
                    Success: false,
                    ErrorMessage: $"spaCy service error: {response.StatusCode}"
                );
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var spacyResult = JsonSerializer.Deserialize<SpacyProcessedDocument>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (spacyResult?.Analysis?.Embedding == null || spacyResult.Analysis.Embedding.Count == 0)
            {
                _logger.LogWarning("spaCy service returned null or empty embedding");
                return new EmbeddingResult(
                    Embedding: new Vector(new float[EmbeddingDimensions]),
                    Success: false,
                    ErrorMessage: "spaCy returned null embedding"
                );
            }

            // Convert to our expected format
            var embedding = spacyResult.Analysis.Embedding.Take(EmbeddingDimensions).ToArray();
            if (embedding.Length < EmbeddingDimensions)
            {
                // Pad with zeros if needed
                var paddedEmbedding = new float[EmbeddingDimensions];
                Array.Copy(embedding, paddedEmbedding, embedding.Length);
                embedding = paddedEmbedding;
            }

            _logger.LogInformation("spaCy generated embedding with {Dimensions} dimensions", embedding.Length);

            return new EmbeddingResult(
                Embedding: new Vector(embedding),
                Success: true
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling spaCy service for embedding generation");
            return new EmbeddingResult(
                Embedding: new Vector(new float[EmbeddingDimensions]),
                Success: false,
                ErrorMessage: ex.Message
            );
        }
    }

    public async Task<BatchEmbeddingResult> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        if (!textList.Any())
        {
            return new BatchEmbeddingResult(
                Embeddings: new List<Vector>(),
                Success: false,
                ErrorMessage: "No texts provided"
            );
        }

        try
        {
            _logger.LogInformation("Generating embeddings for {TextCount} texts using spaCy service", textList.Count);

            var embeddings = new List<Vector>();
            var errors = new List<string>();

            // Process each text individually since our spaCy service processes one document at a time
            foreach (var text in textList)
            {
                var result = await GenerateEmbeddingAsync(text, cancellationToken);
                if (result.Success)
                {
                    embeddings.Add(result.Embedding);
                }
                else
                {
                    _logger.LogWarning("Failed to generate embedding for text: {Error}", result.ErrorMessage);
                    embeddings.Add(new Vector(new float[EmbeddingDimensions])); // Add zero vector as fallback
                    errors.Add(result.ErrorMessage ?? "Unknown error");
                }
            }

            var hasErrors = errors.Any();
            var errorMessage = hasErrors ? $"Errors occurred: {string.Join(", ", errors)}" : null;

            _logger.LogInformation("Generated {EmbeddingCount} embeddings with {ErrorCount} errors", 
                embeddings.Count, errors.Count);

            return new BatchEmbeddingResult(
                Embeddings: embeddings,
                Success: !hasErrors,
                ErrorMessage: errorMessage
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating batch embeddings");
            return new BatchEmbeddingResult(
                Embeddings: new List<Vector>(),
                Success: false,
                ErrorMessage: ex.Message
            );
        }
    }

    // Models to deserialize spaCy responses
    private class SpacyProcessedDocument
    {
        public SpacyAnalysis? Analysis { get; set; }
    }

    private class SpacyAnalysis
    {
        public List<SpacyEntity>? Entities { get; set; }
        public SpacySentiment? Sentiment { get; set; }
        public List<float>? Embedding { get; set; }
    }

    private class SpacyEntity
    {
        public string? Text { get; set; }
        public string? Label { get; set; }
        public float? Confidence { get; set; }
    }

    private class SpacySentiment
    {
        public float? Polarity { get; set; }
        public string? Label { get; set; }
    }
}