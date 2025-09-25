namespace SmartCollectAPI.Services.Providers;

public interface IEntityExtractionService
{
    Task<EntityExtractionResult> ExtractEntitiesAsync(string text, CancellationToken cancellationToken = default);
}

public record EntityExtractionResult(
    List<ExtractedEntity> Entities,
    SentimentAnalysis? Sentiment = null,
    bool Success = true,
    string? ErrorMessage = null
);

public record SentimentAnalysis(
    float Score,
    float Magnitude,
    string Label // "POSITIVE", "NEGATIVE", "NEUTRAL", "MIXED"
);