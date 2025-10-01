using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartCollectAPI.Services.Providers;

/// <summary>
/// OCR service using EasyOCR microservice for text extraction from images
/// Provides better accuracy than Tesseract with GPU acceleration support
/// </summary>
public class EasyOcrService : IOcrService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EasyOcrService> _logger;
    private const string OCR_BASE_URL = "http://localhost:5085";

    public EasyOcrService(HttpClient httpClient, ILogger<EasyOcrService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Configure HTTP client for EasyOCR service
        _httpClient.BaseAddress = new Uri(OCR_BASE_URL);
        _httpClient.Timeout = TimeSpan.FromMinutes(2); // OCR can take time
    }

    public async Task<OcrResult> ExtractTextAsync(Stream imageStream, string mimeType, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Calling EasyOCR service for image OCR, mime type: {MimeType}", mimeType);

            // Prepare multipart form data
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(imageStream);

            streamContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            content.Add(streamContent, "file", "image" + GetExtension(mimeType));

            // Call EasyOCR API
            var response = await _httpClient.PostAsync("/api/v1/ocr/extract", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("EasyOCR service returned error {StatusCode}: {Error}", response.StatusCode, errorContent);

                return new OcrResult(
                    ExtractedText: string.Empty,
                    Annotations: [],
                    Objects: [],
                    Success: false,
                    ErrorMessage: $"EasyOCR service error: {response.StatusCode}"
                );
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var ocrResponse = JsonSerializer.Deserialize<EasyOcrResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (ocrResponse == null || !ocrResponse.Success)
            {
                _logger.LogWarning("EasyOCR service returned unsuccessful result");
                return new OcrResult(
                    ExtractedText: string.Empty,
                    Annotations: [],
                    Objects: [],
                    Success: false,
                    ErrorMessage: ocrResponse?.ErrorMessage ?? "Unknown error"
                );
            }

            // Convert EasyOCR bounding boxes to our format
            // EasyOCR returns 4 corner points, we need to convert to X, Y, Width, Height
            var annotations = ocrResponse.BoundingBoxes?.Select(bb =>
            {
                var topLeft = bb.Bbox?.TopLeft;
                var bottomRight = bb.Bbox?.BottomRight;

                var x = topLeft?.X ?? 0;
                var y = topLeft?.Y ?? 0;
                var width = (bottomRight?.X ?? 0) - x;
                var height = (bottomRight?.Y ?? 0) - y;

                return new TextAnnotation(
                    Text: bb.Text ?? "",
                    Confidence: bb.Confidence,
                    BoundingBox: new BoundingBox(x, y, width, height)
                );
            }).ToList() ?? [];

            _logger.LogInformation("EasyOCR extracted {Length} characters with {Count} text regions",
                ocrResponse.Text?.Length ?? 0, annotations.Count);

            return new OcrResult(
                ExtractedText: ocrResponse.Text ?? string.Empty,
                Annotations: annotations,
                Objects: [], // EasyOCR doesn't detect objects
                Success: true
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling EasyOCR service");
            return new OcrResult(
                ExtractedText: string.Empty,
                Annotations: [],
                Objects: [],
                Success: false,
                ErrorMessage: ex.Message
            );
        }
    }

    public bool CanHandle(string mimeType)
    {
        // EasyOCR supports common image formats
        return mimeType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" or "image/png" or "image/gif"
                or "image/bmp" or "image/tiff" or "image/tif" => true,
            _ => false
        };
    }

    private static string GetExtension(string mimeType)
    {
        return mimeType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/bmp" => ".bmp",
            "image/tiff" or "image/tif" => ".tiff",
            _ => ".jpg"
        };
    }
}

// Response models for EasyOCR API
internal class EasyOcrResponse
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("confidence")]
    public float Confidence { get; set; }

    [JsonPropertyName("bounding_boxes")]
    public List<EasyOcrBoundingBox>? BoundingBoxes { get; set; }

    [JsonPropertyName("language_detected")]
    public List<string>? LanguageDetected { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}

internal class EasyOcrBoundingBox
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("confidence")]
    public float Confidence { get; set; }

    [JsonPropertyName("bbox")]
    public EasyOcrBbox? Bbox { get; set; }
}

internal class EasyOcrBbox
{
    [JsonPropertyName("top_left")]
    public EasyOcrPoint? TopLeft { get; set; }

    [JsonPropertyName("top_right")]
    public EasyOcrPoint? TopRight { get; set; }

    [JsonPropertyName("bottom_right")]
    public EasyOcrPoint? BottomRight { get; set; }

    [JsonPropertyName("bottom_left")]
    public EasyOcrPoint? BottomLeft { get; set; }
}

internal class EasyOcrPoint
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }
}
