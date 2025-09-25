using Microsoft.Extensions.Logging;

namespace SmartCollectAPI.Services.Providers;

public class SimpleEntityExtractionService : IEntityExtractionService
{
    private readonly ILogger<SimpleEntityExtractionService> _logger;

    public SimpleEntityExtractionService(ILogger<SimpleEntityExtractionService> logger)
    {
        _logger = logger;
    }

    public async Task<EntityExtractionResult> ExtractEntitiesAsync(string text, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Entity extraction service not available - returning empty result");
        return await Task.FromResult(new EntityExtractionResult(
            Entities: new List<ExtractedEntity>(),
            Sentiment: new SentimentAnalysis(0.0f, 0.0f, "NEUTRAL"),
            Success: true,
            ErrorMessage: null
        ));
    }
}