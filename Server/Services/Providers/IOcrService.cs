namespace SmartCollectAPI.Services.Providers;

public interface IOcrService
{
    Task<OcrResult> ExtractTextAsync(Stream imageStream, string mimeType, CancellationToken cancellationToken = default);
    bool CanHandle(string mimeType);
}

public record OcrResult(
    string ExtractedText,
    List<TextAnnotation>? Annotations = null,
    List<DetectedObject>? Objects = null,
    bool Success = true,
    string? ErrorMessage = null
);

public record TextAnnotation(
    string Text,
    float Confidence,
    BoundingBox BoundingBox
);

public record DetectedObject(
    string Label,
    float Confidence,
    BoundingBox BoundingBox
);

public record BoundingBox(
    float X,
    float Y,
    float Width,
    float Height
);