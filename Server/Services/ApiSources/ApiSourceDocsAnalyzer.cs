using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace SmartCollectAPI.Services.ApiSources;

public record ApiSourceFieldSuggestion(string Field, string? Value, double Confidence, string? Notes);

public record ApiSourceAutoFillResult(IReadOnlyCollection<ApiSourceFieldSuggestion> Suggestions, IReadOnlyCollection<string> Warnings, string? SampleSnippet);

public interface IApiSourceDocsAnalyzer
{
    Task<ApiSourceAutoFillResult> AnalyzeAsync(string docsUrl, CancellationToken cancellationToken = default);
}

public class ApiSourceDocsAnalyzer : IApiSourceDocsAnalyzer
{
    private const int MaxContentBytes = 1_000_000;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiSourceDocsAnalyzer> _logger;

    public ApiSourceDocsAnalyzer(IHttpClientFactory httpClientFactory, ILogger<ApiSourceDocsAnalyzer> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ApiSourceAutoFillResult> AnalyzeAsync(string docsUrl, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(docsUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Documentation URL must be a valid HTTPS address.", nameof(docsUrl));
        }

        var client = _httpClientFactory.CreateClient("ApiDocsAnalyzer");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("SmartCollectAPI-DocsAnalyzer/1.0");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Fetching documentation failed with status {(int)response.StatusCode} ({response.StatusCode}).");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: false);

        var contentBuilder = new StringBuilder();
        var buffer = new char[4096];
        var totalChars = 0;
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            totalChars += read;
            if (totalChars * sizeof(char) > MaxContentBytes)
            {
                throw new InvalidOperationException("Documentation is too large to analyze safely (limit: 1 MB).");
            }
            contentBuilder.Append(buffer, 0, read);
        }

        var rawContent = contentBuilder.ToString();
        var text = NormalizeText(rawContent);

        var suggestions = new List<ApiSourceFieldSuggestion>();
        var warnings = new List<string>();

        var name = ExtractName(rawContent, uri);
        if (!string.IsNullOrWhiteSpace(name))
        {
            suggestions.Add(new ApiSourceFieldSuggestion("name", name, 0.7, "Derived from documentation title."));
        }

        var baseUrlSuggestion = ExtractBaseUrl(text);
        if (!string.IsNullOrWhiteSpace(baseUrlSuggestion))
        {
            suggestions.Add(new ApiSourceFieldSuggestion("endpointUrl", baseUrlSuggestion, 0.75, "Most common host found in documentation."));
        }

        var authInfo = ExtractAuthHints(text);
        suggestions.AddRange(authInfo.Suggestions);
        warnings.AddRange(authInfo.Warnings);

        var sampleSnippet = ExtractSampleSnippet(text);

        return new ApiSourceAutoFillResult(suggestions, warnings, sampleSnippet);
    }

    private static string NormalizeText(string content)
    {
        var withoutScripts = Regex.Replace(content, @"<script[\s\S]*?</script>", string.Empty, RegexOptions.IgnoreCase);
        var withoutStyles = Regex.Replace(withoutScripts, @"<style[\s\S]*?</style>", string.Empty, RegexOptions.IgnoreCase);
        var stripped = Regex.Replace(withoutStyles, "<[^>]+>", " ");
        var decoded = WebUtility.HtmlDecode(stripped);
        return Regex.Replace(decoded ?? string.Empty, @"\s+", " ").Trim();
    }

    private static string? ExtractName(string rawContent, Uri uri)
    {
        var match = Regex.Match(rawContent, "<title>(?<title>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (match.Success)
        {
            var title = WebUtility.HtmlDecode(match.Groups["title"].Value)?.Trim();
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title.Length > 120 ? title[..120] : title;
            }
        }

        return uri.Host.Replace("www.", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractBaseUrl(string text)
    {
        var matches = Regex.Matches(text, "https://[a-zA-Z0-9./_-]+", RegexOptions.IgnoreCase);
        if (matches.Count == 0)
        {
            return null;
        }

        var hosts = matches
            .Select(m => m.Value)
            .Select(value =>
            {
                if (Uri.TryCreate(value, UriKind.Absolute, out var candidate))
                {
                    var builder = new UriBuilder(candidate.Scheme, candidate.Host, candidate.IsDefaultPort ? -1 : candidate.Port);
                    var segments = candidate.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length > 0)
                    {
                        builder.Path = "/" + segments[0];
                    }
                    else
                    {
                        builder.Path = string.Empty;
                    }
                    return builder.Uri.ToString().TrimEnd('/');
                }
                return null;
            })
            .Where(v => v != null)
            .GroupBy(v => v)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        return hosts;
    }

    private static (List<ApiSourceFieldSuggestion> Suggestions, List<string> Warnings) ExtractAuthHints(string text)
    {
        var suggestions = new List<ApiSourceFieldSuggestion>();
        var warnings = new List<string>();

        var lowered = text.ToLowerInvariant();
        if (lowered.Contains("api key") || lowered.Contains("x-api-key"))
        {
            suggestions.Add(new ApiSourceFieldSuggestion("authType", "ApiKey", 0.8, "Documentation mentions API key."));
            if (Regex.IsMatch(text, "x-api-key", RegexOptions.IgnoreCase))
            {
                suggestions.Add(new ApiSourceFieldSuggestion("headerName", "X-API-Key", 0.8, "Found header hint X-API-Key."));
                suggestions.Add(new ApiSourceFieldSuggestion("authLocation", "header", 0.7, "Likely header-based key."));
            }
            else if (Regex.IsMatch(text, "api_key", RegexOptions.IgnoreCase))
            {
                suggestions.Add(new ApiSourceFieldSuggestion("queryParam", "api_key", 0.6, "Found query parameter api_key."));
                suggestions.Add(new ApiSourceFieldSuggestion("authLocation", "query", 0.6, "API key appears in query string."));
            }
            else
            {
                warnings.Add("API key mentioned but header/parameter name could not be determined.");
            }
        }
        else if (lowered.Contains("bearer"))
        {
            suggestions.Add(new ApiSourceFieldSuggestion("authType", "Bearer", 0.6, "Bearer token referenced in docs."));
            suggestions.Add(new ApiSourceFieldSuggestion("authLocation", "header", 0.75, "Bearer tokens use Authorization header."));
            suggestions.Add(new ApiSourceFieldSuggestion("headerName", "Authorization", 0.75, "Bearer tokens typically rely on Authorization header."));
        }
        else if (lowered.Contains("oauth"))
        {
            suggestions.Add(new ApiSourceFieldSuggestion("authType", "OAuth2", 0.5, "OAuth2 flow mentioned."));
            warnings.Add("OAuth2 detected; manual configuration likely required (client ID/secret).");
        }
        else
        {
            suggestions.Add(new ApiSourceFieldSuggestion("authType", "None", 0.3, "No authentication hints detected."));
        }

        return (suggestions, warnings);
    }

    private static string? ExtractSampleSnippet(string text)
    {
        var match = Regex.Match(text, "(GET|POST|PUT|DELETE)\\s+https://[a-zA-Z0-9./?=_-]+", RegexOptions.IgnoreCase);
        return match.Success ? match.Value : null;
    }
}
