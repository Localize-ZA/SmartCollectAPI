using Google.Cloud.Vision.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SmartCollectAPI.Services.Providers;

public class GoogleVisionOcrService : IOcrService
{
    private readonly ILogger<GoogleVisionOcrService> _logger;
    private readonly ImageAnnotatorClient _client;
    private readonly GoogleCloudOptions _options;

    private readonly HashSet<string> _supportedMimeTypes = new()
    {
        "image/jpeg",
        "image/jpg", 
        "image/png",
        "image/gif",
        "image/bmp",
        "image/webp",
        "image/tiff",
        "image/tif"
    };

    public GoogleVisionOcrService(ILogger<GoogleVisionOcrService> logger, IOptions<GoogleCloudOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        
        try
        {
            _client = ImageAnnotatorClient.Create();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Google Vision client");
            throw;
        }
    }

    public bool CanHandle(string mimeType)
    {
        return _supportedMimeTypes.Contains(mimeType.ToLowerInvariant());
    }

    public async Task<OcrResult> ExtractTextAsync(Stream imageStream, string mimeType, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing image with Google Vision OCR, MIME type: {MimeType}", mimeType);

            // Read the image content
            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream, cancellationToken);
            var imageBytes = memoryStream.ToArray();

            // Create Google Vision image
            var image = Google.Cloud.Vision.V1.Image.FromBytes(imageBytes);

            // Perform text detection
            var textDetectionResponse = await _client.DetectTextAsync(image);
            
            // Perform object detection 
            var objectDetectionResponse = await _client.DetectLocalizedObjectsAsync(image);

            // Extract full text
            var fullTextAnnotation = textDetectionResponse?.FirstOrDefault();
            var extractedText = fullTextAnnotation?.Description ?? string.Empty;

            // Extract text annotations with bounding boxes
            var annotations = textDetectionResponse?.Skip(1) // Skip the first one which is the full text
                .Select(annotation => new TextAnnotation(
                    Text: annotation.Description,
                    Confidence: annotation.Confidence,
                    BoundingBox: ConvertBoundingPoly(annotation.BoundingPoly)
                )).ToList() ?? new List<TextAnnotation>();

            // Extract detected objects
            var objects = objectDetectionResponse?.Select(obj => new DetectedObject(
                Label: obj.Name,
                Confidence: obj.Score,
                BoundingBox: ConvertBoundingPoly(obj.BoundingPoly)
            )).ToList() ?? new List<DetectedObject>();

            _logger.LogInformation("Successfully processed image with Google Vision. Text length: {TextLength}, Annotations: {AnnotationCount}, Objects: {ObjectCount}", 
                extractedText.Length, annotations.Count, objects.Count);

            return new OcrResult(
                ExtractedText: extractedText,
                Annotations: annotations,
                Objects: objects,
                Success: true
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing image with Google Vision OCR");
            return new OcrResult(
                ExtractedText: string.Empty,
                Success: false,
                ErrorMessage: ex.Message
            );
        }
    }

    private BoundingBox ConvertBoundingPoly(BoundingPoly? boundingPoly)
    {
        if (boundingPoly?.Vertices == null || !boundingPoly.Vertices.Any())
        {
            return new BoundingBox(0, 0, 0, 0);
        }

        var vertices = boundingPoly.Vertices;
        var minX = vertices.Min(v => v.X);
        var minY = vertices.Min(v => v.Y);
        var maxX = vertices.Max(v => v.X);
        var maxY = vertices.Max(v => v.Y);

        return new BoundingBox(
            X: minX,
            Y: minY,
            Width: maxX - minX,
            Height: maxY - minY
        );
    }
}