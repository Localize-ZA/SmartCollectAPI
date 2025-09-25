using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

    public ProviderFactory(IServiceProvider serviceProvider, IOptions<ServicesOptions> options)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    public IAdvancedDocumentParser GetDocumentParser()
    {
        return _options.Parser?.ToUpperInvariant() switch
        {
            "GOOGLE" => _serviceProvider.GetRequiredService<GoogleDocAiParser>(),
            "OSS" => _serviceProvider.GetRequiredService<SimplePdfParser>(),
            _ => _serviceProvider.GetRequiredService<GoogleDocAiParser>() // Default to Google
        };
    }

    public IOcrService GetOcrService()
    {
        return _options.OCR?.ToUpperInvariant() switch
        {
            "GOOGLE" => _serviceProvider.GetRequiredService<GoogleVisionOcrService>(),
            "OSS" => throw new NotImplementedException("OSS OCR service not implemented yet"),
            _ => _serviceProvider.GetRequiredService<GoogleVisionOcrService>() // Default to Google
        };
    }

    public IEmbeddingService GetEmbeddingService()
    {
        return _options.Embeddings?.ToUpperInvariant() switch
        {
            "GOOGLE" => _serviceProvider.GetRequiredService<VertexEmbeddingService>(),
            "OSS" => _serviceProvider.GetRequiredService<SimpleEmbeddingService>(),
            _ => _serviceProvider.GetRequiredService<VertexEmbeddingService>() // Default to Google
        };
    }

    public IEntityExtractionService GetEntityExtractionService()
    {
        return _options.EntityExtraction?.ToUpperInvariant() switch
        {
            "GOOGLE" => _serviceProvider.GetRequiredService<GoogleEntityExtractionService>(),
            "OSS" => throw new NotImplementedException("OSS entity extraction service not implemented yet"),
            _ => _serviceProvider.GetRequiredService<GoogleEntityExtractionService>() // Default to Google
        };
    }

    public INotificationService GetNotificationService()
    {
        return _options.Notifications?.ToUpperInvariant() switch
        {
            "GOOGLE" => _serviceProvider.GetRequiredService<GmailNotificationService>(),
            "OSS" => _serviceProvider.GetRequiredService<SmtpNotificationService>(),
            _ => _serviceProvider.GetRequiredService<GmailNotificationService>() // Default to Google
        };
    }
}

public class ServicesOptions
{
    public string Parser { get; set; } = "Google";
    public string OCR { get; set; } = "Google";
    public string Embeddings { get; set; } = "Google";
    public string EntityExtraction { get; set; } = "Google";
    public string Notifications { get; set; } = "Google";
}