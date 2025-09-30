using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace SmartCollectAPI.Services.Providers;

public interface IProviderFactory
{
    IAdvancedDocumentParser GetDocumentParser();
    IOcrService GetOcrService();
    IEmbeddingService GetEmbeddingService();
    IEntityExtractionService GetEntityExtractionService();
    INotificationService GetNotificationService();
}

public class ProviderFactory : IProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ServicesOptions _options;
    private readonly ILogger<ProviderFactory> _logger;

    public ProviderFactory(IServiceProvider serviceProvider, IOptions<ServicesOptions> options, ILogger<ProviderFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    public IAdvancedDocumentParser GetDocumentParser()
    {
        _logger.LogInformation($"ProviderFactory: Parser config = '{_options.Parser}', using provider: {_options.Parser?.ToUpperInvariant() ?? "DEFAULT"}");
        return _options.Parser?.ToUpperInvariant() switch
        {
            "GOOGLE" => _serviceProvider.GetRequiredService<GoogleDocAiParser>(),
            "OSS" => _serviceProvider.GetRequiredService<PdfPigParser>(), // Use advanced PdfPig parser
            "SIMPLE" => _serviceProvider.GetRequiredService<SimplePdfParser>(), // Keep simple as option
            _ => _serviceProvider.GetRequiredService<PdfPigParser>() // Default to PdfPig for better parsing
        };
    }

    public IOcrService GetOcrService()
    {
        // Only OSS OCR service is supported
        return _serviceProvider.GetRequiredService<SimpleOcrService>();
    }

    public IEmbeddingService GetEmbeddingService()
    {
        // Use spaCy NLP service for embeddings
        return _serviceProvider.GetRequiredService<SpacyNlpService>();
    }

    public IEntityExtractionService GetEntityExtractionService()
    {
        // Use spaCy NLP service for entity extraction
        return _serviceProvider.GetRequiredService<SpacyNlpService>();
    }

    public INotificationService GetNotificationService()
    {
        // Only OSS notification service is supported
        return _serviceProvider.GetRequiredService<SmtpNotificationService>();
    }
}

public class ServicesOptions
{
    public string Parser { get; set; } = "Google";  // Keep Google Document AI only
    public string OCR { get; set; } = "OSS";
    public string Embeddings { get; set; } = "OSS";
    public string EntityExtraction { get; set; } = "OSS";
    public string Notifications { get; set; } = "OSS";
}