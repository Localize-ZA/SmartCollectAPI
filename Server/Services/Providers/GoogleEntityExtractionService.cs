using Google.Cloud.Language.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SmartCollectAPI.Services.Providers;

public class GoogleEntityExtractionService : IEntityExtractionService
{
    private readonly ILogger<GoogleEntityExtractionService> _logger;
    private readonly LanguageServiceClient _client;
    private readonly GoogleCloudOptions _options;

    public GoogleEntityExtractionService(ILogger<GoogleEntityExtractionService> logger, IOptions<GoogleCloudOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        
        try
        {
            _client = LanguageServiceClient.Create();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Google Natural Language client");
            throw;
        }
    }

    public async Task<EntityExtractionResult> ExtractEntitiesAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new EntityExtractionResult(
                    Entities: new List<ExtractedEntity>(),
                    Success: true
                );
            }

            _logger.LogInformation("Extracting entities from text of length: {TextLength}", text.Length);

            // Create the document
            var document = new Document
            {
                Content = text,
                Type = Document.Types.Type.PlainText
            };

            // Extract entities
            var entitiesResponse = await _client.AnalyzeEntitiesAsync(document, cancellationToken: cancellationToken);
            
            // Analyze sentiment
            var sentimentResponse = await _client.AnalyzeSentimentAsync(document, cancellationToken: cancellationToken);

            // Convert entities
            var extractedEntities = entitiesResponse.Entities.Select(entity => new ExtractedEntity(
                Name: entity.Name,
                Type: entity.Type.ToString(),
                Salience: entity.Salience,
                Mentions: entity.Mentions?.Select(mention => new EntityMention(
                    Text: mention.Text.Content,
                    StartOffset: mention.Text.BeginOffset,
                    EndOffset: mention.Text.BeginOffset + mention.Text.Content.Length
                )).ToList()
            )).ToList();

            // Convert sentiment
            var sentiment = sentimentResponse.DocumentSentiment != null ? new SentimentAnalysis(
                Score: sentimentResponse.DocumentSentiment.Score,
                Magnitude: sentimentResponse.DocumentSentiment.Magnitude,
                Label: DetermineSentimentLabel(sentimentResponse.DocumentSentiment.Score)
            ) : null;

            _logger.LogInformation("Successfully extracted {EntityCount} entities from text", extractedEntities.Count);

            return new EntityExtractionResult(
                Entities: extractedEntities,
                Sentiment: sentiment,
                Success: true
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting entities with Google Natural Language");
            return new EntityExtractionResult(
                Entities: new List<ExtractedEntity>(),
                Success: false,
                ErrorMessage: ex.Message
            );
        }
    }

    private static string DetermineSentimentLabel(float score)
    {
        return score switch
        {
            >= 0.25f => "POSITIVE",
            <= -0.25f => "NEGATIVE", 
            _ => "NEUTRAL"
        };
    }
}