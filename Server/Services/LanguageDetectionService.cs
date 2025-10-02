using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartCollectAPI.Services;

/// <summary>
/// Interface for language detection service
/// </summary>
public interface ILanguageDetectionService
{
    Task<LanguageDetectionResult> DetectLanguageAsync(string text, float minConfidence = 0.0f, CancellationToken cancellationToken = default);
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Client for the language detection microservice
/// </summary>
public class LanguageDetectionService : ILanguageDetectionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LanguageDetectionService> _logger;
    private readonly string _serviceUrl;

    public LanguageDetectionService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<LanguageDetectionService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("LanguageDetection");
        _logger = logger;
        _serviceUrl = configuration["Services:LanguageDetection:Url"] ?? "http://localhost:8004";
        
        _httpClient.BaseAddress = new Uri(_serviceUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// Detect the language of the provided text
    /// </summary>
    public async Task<LanguageDetectionResult> DetectLanguageAsync(
        string text, 
        float minConfidence = 0.0f, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty", nameof(text));
        }

        try
        {
            var request = new LanguageDetectionRequest
            {
                Text = text,
                MinConfidence = minConfidence
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogDebug("Detecting language for text of length {Length}", text.Length);

            var response = await _httpClient.PostAsync("/detect", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Language detection failed with status {StatusCode}: {Error}",
                    response.StatusCode, errorContent);
                
                // Return English as fallback
                return new LanguageDetectionResult
                {
                    Language = "ENGLISH",
                    LanguageName = "English",
                    Confidence = 0.0f,
                    IsoCode639_1 = "EN",
                    IsoCode639_3 = "ENG",
                    TextLength = text.Length,
                    IsFallback = true
                };
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var detectionResponse = JsonSerializer.Deserialize<LanguageDetectionResponse>(
                responseJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (detectionResponse?.DetectedLanguage == null)
            {
                throw new InvalidOperationException("Invalid response from language detection service");
            }

            _logger.LogInformation(
                "Detected language: {Language} (confidence: {Confidence:F3})",
                detectionResponse.DetectedLanguage.LanguageName,
                detectionResponse.DetectedLanguage.Confidence);

            return new LanguageDetectionResult
            {
                Language = detectionResponse.DetectedLanguage.Language,
                LanguageName = detectionResponse.DetectedLanguage.LanguageName,
                Confidence = detectionResponse.DetectedLanguage.Confidence,
                IsoCode639_1 = detectionResponse.DetectedLanguage.IsoCode639_1,
                IsoCode639_3 = detectionResponse.DetectedLanguage.IsoCode639_3,
                TextLength = detectionResponse.TextLength,
                AllCandidates = detectionResponse.AllCandidates?.Select(c => new LanguageCandidate
                {
                    Language = c.Language,
                    LanguageName = c.LanguageName,
                    Confidence = c.Confidence
                }).ToList() ?? []
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling language detection service at {ServiceUrl}", _serviceUrl);
            return CreateFallbackResult(text);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Language detection request timed out");
            return CreateFallbackResult(text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during language detection");
            return CreateFallbackResult(text);
        }
    }

    /// <summary>
    /// Check if the language detection service is healthy
    /// </summary>
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed for language detection service");
            return false;
        }
    }

    private static LanguageDetectionResult CreateFallbackResult(string text)
    {
        return new LanguageDetectionResult
        {
            Language = "ENGLISH",
            LanguageName = "English",
            Confidence = 0.0f,
            IsoCode639_1 = "EN",
            IsoCode639_3 = "ENG",
            TextLength = text.Length,
            IsFallback = true
        };
    }
}

// Request/Response DTOs
internal class LanguageDetectionRequest
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("min_confidence")]
    public float MinConfidence { get; set; }
}

internal class LanguageDetectionResponse
{
    [JsonPropertyName("detected_language")]
    public LanguageInfo? DetectedLanguage { get; set; }

    [JsonPropertyName("all_candidates")]
    public List<LanguageInfo>? AllCandidates { get; set; }

    [JsonPropertyName("text_length")]
    public int TextLength { get; set; }
}

internal class LanguageInfo
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("language_name")]
    public string LanguageName { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public float Confidence { get; set; }

    [JsonPropertyName("iso_code_639_1")]
    public string IsoCode639_1 { get; set; } = string.Empty;

    [JsonPropertyName("iso_code_639_3")]
    public string IsoCode639_3 { get; set; } = string.Empty;
}

// Public result models
public class LanguageDetectionResult
{
    public string Language { get; set; } = string.Empty;
    public string LanguageName { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public string IsoCode639_1 { get; set; } = string.Empty;
    public string IsoCode639_3 { get; set; } = string.Empty;
    public int TextLength { get; set; }
    public bool IsFallback { get; set; }
    public List<LanguageCandidate> AllCandidates { get; set; } = [];
}

public class LanguageCandidate
{
    public string Language { get; set; } = string.Empty;
    public string LanguageName { get; set; } = string.Empty;
    public float Confidence { get; set; }
}
