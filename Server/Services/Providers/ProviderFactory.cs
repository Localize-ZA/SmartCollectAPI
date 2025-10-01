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

public class ProviderFactory(IServiceProvider serviceProvider, IOptions<ServicesOptions> options, ILogger<ProviderFactory> logger) : IProviderFactory
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ServicesOptions _options = options.Value;
    private readonly ILogger<ProviderFactory> _logger = logger;

    public IAdvancedDocumentParser GetDocumentParser()
    {
        _logger.LogInformation($"ProviderFactory: Parser config = '{_options.Parser}', using provider: {_options.Parser?.ToUpperInvariant() ?? "DEFAULT"}");
        return _options.Parser?.ToUpperInvariant() switch
        {
            "OSS" => _serviceProvider.GetRequiredService<OssDocumentParser>(), // Composite parser with LibreOffice + PdfPig
            "PDFPIG" => _serviceProvider.GetRequiredService<PdfPigParser>(), // Direct PdfPig parser
            _ => _serviceProvider.GetRequiredService<OssDocumentParser>() // Default to OSS composite parser
        };
    }

    public IOcrService GetOcrService()
    {
        return _options.OCR?.ToUpperInvariant() switch
        {
            "EASYOCR" => _serviceProvider.GetRequiredService<EasyOcrService>(), // New advanced OCR
            "TESSERACT" => _serviceProvider.GetRequiredService<TesseractOcrService>(), // Fallback OCR
            _ => _serviceProvider.GetRequiredService<EasyOcrService>() // Default to EasyOCR when available
        };
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
    public string Parser { get; set; } = "OSS";  // Default to OSS parsers
    public string OCR { get; set; } = "OSS";
    public string Embeddings { get; set; } = "OSS";
    public string EntityExtraction { get; set; } = "OSS";
    public string Notifications { get; set; } = "OSS";
}
