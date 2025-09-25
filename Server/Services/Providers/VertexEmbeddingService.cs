using Google.Cloud.AIPlatform.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using Google.Protobuf.WellKnownTypes;

namespace SmartCollectAPI.Services.Providers;

public class VertexEmbeddingService : IEmbeddingService
{
    private readonly ILogger<VertexEmbeddingService> _logger;
    private readonly PredictionServiceClient _client;
    private readonly GoogleCloudOptions _options;
    private readonly string _endpointName;

    public int EmbeddingDimensions => 1536; // text-embedding-004 dimensions
    public int MaxTokens => 8192; // Maximum tokens for text-embedding-004

    public VertexEmbeddingService(ILogger<VertexEmbeddingService> logger, IOptions<GoogleCloudOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        
        try
        {
            _client = PredictionServiceClient.Create();
            
            // Construct endpoint name for text embeddings
            var location = _options.Location ?? "us-central1";
            _endpointName = $"projects/{_options.ProjectId}/locations/{location}/publishers/google/models/text-embedding-004";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Vertex AI client");
            throw;
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

            _logger.LogInformation("Generating embedding for text of length: {TextLength}", text.Length);

            // Truncate text if it exceeds max tokens (rough estimation: 1 token ≈ 4 characters)
            if (text.Length > MaxTokens * 4)
            {
                text = text.Substring(0, MaxTokens * 4);
                _logger.LogWarning("Text truncated to {MaxLength} characters to fit within token limit", MaxTokens * 4);
            }

            // Create the prediction request
            var instance = Google.Protobuf.WellKnownTypes.Value.ForStruct(new Struct
            {
                Fields =
                {
                    ["content"] = Google.Protobuf.WellKnownTypes.Value.ForString(text),
                    ["task_type"] = Google.Protobuf.WellKnownTypes.Value.ForString("RETRIEVAL_DOCUMENT") // Default task type
                }
            });

            var request = new PredictRequest
            {
                EndpointAsEndpointName = EndpointName.FromProjectLocationEndpoint(
                    _options.ProjectId, 
                    _options.Location ?? "us-central1", 
                    "text-embedding-004"),
                Instances = { instance }
            };

            // Make the prediction
            var response = await _client.PredictAsync(request, cancellationToken);

            if (!response.Predictions.Any())
            {
                return new EmbeddingResult(
                    Embedding: new Vector(new float[EmbeddingDimensions]),
                    Success: false,
                    ErrorMessage: "No embedding returned from Vertex AI"
                );
            }

            // Extract embedding from response
            var prediction = response.Predictions.First();
            var embeddingStruct = prediction.StructValue;
            
            if (!embeddingStruct.Fields.TryGetValue("embeddings", out var embeddingsValue))
            {
                return new EmbeddingResult(
                    Embedding: new Vector(new float[EmbeddingDimensions]),
                    Success: false,
                    ErrorMessage: "Embedding field not found in response"
                );
            }

            var embeddingArray = embeddingsValue.StructValue.Fields["values"].ListValue.Values;
            var embedding = embeddingArray.Select(v => (float)v.NumberValue).ToArray();

            if (embedding.Length != EmbeddingDimensions)
            {
                return new EmbeddingResult(
                    Embedding: new Vector(new float[EmbeddingDimensions]),
                    Success: false,
                    ErrorMessage: $"Expected {EmbeddingDimensions} dimensions, got {embedding.Length}"
                );
            }

            _logger.LogInformation("Successfully generated embedding with {Dimensions} dimensions", embedding.Length);

            return new EmbeddingResult(
                Embedding: new Vector(embedding),
                Success: true
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding with Vertex AI");
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

            _logger.LogInformation("Generating {Count} embeddings", textList.Count);

            var embeddings = new List<Vector>();

            // Process in batches to respect API limits
            const int batchSize = 5; // Conservative batch size for Vertex AI
            for (int i = 0; i < textList.Count; i += batchSize)
            {
                var batch = textList.Skip(i).Take(batchSize);
                var batchTasks = batch.Select(text => GenerateEmbeddingAsync(text, cancellationToken));
                var batchResults = await Task.WhenAll(batchTasks);

                foreach (var result in batchResults)
                {
                    if (!result.Success)
                    {
                        _logger.LogWarning("Failed to generate embedding: {Error}", result.ErrorMessage);
                        embeddings.Add(new Vector(new float[EmbeddingDimensions])); // Add zero vector as fallback
                    }
                    else
                    {
                        embeddings.Add(result.Embedding);
                    }
                }

                // Add delay between batches to respect rate limits
                if (i + batchSize < textList.Count)
                {
                    await Task.Delay(100, cancellationToken); // 100ms delay between batches
                }
            }

            _logger.LogInformation("Successfully generated {Count} embeddings", embeddings.Count);

            return new BatchEmbeddingResult(
                Embeddings: embeddings,
                Success: true
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating batch embeddings with Vertex AI");
            return new BatchEmbeddingResult(
                Embeddings: new List<Vector>(),
                Success: false,
                ErrorMessage: ex.Message
            );
        }
    }
}