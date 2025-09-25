using Microsoft.Extensions.Logging;

namespace SmartCollectAPI.Services.Providers;

public class SimpleOcrService : IOcrService
{
    private readonly ILogger<SimpleOcrService> _logger;

    public SimpleOcrService(ILogger<SimpleOcrService> logger)
    {
        _logger = logger;
    }

    public async Task<OcrResult> ExtractTextAsync(Stream imageStream, string mimeType, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("OCR service not available - returning empty result");
        return await Task.FromResult(new OcrResult(
            ExtractedText: string.Empty,
            Annotations: new List<TextAnnotation>(),
            Objects: new List<DetectedObject>(),
            Success: true,
            ErrorMessage: null
        ));
    }

    public bool CanHandle(string mimeType)
    {
        // Simple OCR service can handle common image types
        return mimeType switch
        {
            "image/jpeg" or "image/jpg" or "image/png" or "image/gif" or "image/bmp" => true,
            _ => false
        };
    }
}