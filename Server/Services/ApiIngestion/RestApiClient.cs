using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SmartCollectAPI.Models;

namespace SmartCollectAPI.Services.ApiIngestion;

public interface IApiClient
{
    Task<ApiResponse> FetchAsync(ApiSource source, CancellationToken cancellationToken = default);
    Task<PaginatedFetchResult> FetchAllPagesAsync(ApiSource source, PaginationType paginationType, PaginationConfig? paginationConfig = null, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(ApiSource source, CancellationToken cancellationToken = default);
}

public class ApiResponse
{
    public bool Success { get; set; }
    public string? RawResponse { get; set; }
    public object? ParsedData { get; set; }
    public int RecordCount { get; set; }
    public int HttpStatusCode { get; set; }
    public long ResponseSizeBytes { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class RestApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<RestApiClient> logger,
    IAuthenticationManager authManager) : IApiClient
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<RestApiClient> _logger = logger;
    private readonly IAuthenticationManager _authManager = authManager;

    public async Task<ApiResponse> FetchAsync(ApiSource source, CancellationToken cancellationToken = default)
    {
        var response = new ApiResponse
        {
            Metadata = []
        };

        try
        {
            using var httpClient = _httpClientFactory.CreateClient("ApiIngestion");
            var request = await BuildRequestAsync(source, cancellationToken);

            _logger.LogInformation(
                "Fetching data from {ApiType} endpoint: {Method} {Url}",
                source.ApiType,
                source.HttpMethod,
                source.EndpointUrl
            );

            using var httpResponse = await httpClient.SendAsync(request, cancellationToken);

            response.HttpStatusCode = (int)httpResponse.StatusCode;
            response.RawResponse = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            response.ResponseSizeBytes = response.RawResponse?.Length ?? 0;

            if (httpResponse.IsSuccessStatusCode)
            {
                response.Success = true;

                // Parse JSON response
                if (!string.IsNullOrEmpty(response.RawResponse))
                {
                    try
                    {
                        response.ParsedData = JsonSerializer.Deserialize<object>(response.RawResponse);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse JSON response from {Endpoint}", source.EndpointUrl);
                        // Keep raw response, don't fail the request
                    }
                }

                // Extract metadata from response headers
                foreach (var header in httpResponse.Headers)
                {
                    response.Metadata[$"header_{header.Key}"] = string.Join(", ", header.Value);
                }

                _logger.LogInformation(
                    "Successfully fetched data from {Endpoint}. Status: {StatusCode}, Size: {Size} bytes",
                    source.EndpointUrl,
                    response.HttpStatusCode,
                    response.ResponseSizeBytes
                );
            }
            else
            {
                response.Success = false;
                response.ErrorMessage = $"HTTP {response.HttpStatusCode}: {httpResponse.ReasonPhrase}";

                _logger.LogWarning(
                    "Failed to fetch data from {Endpoint}. Status: {StatusCode}, Response: {Response}",
                    source.EndpointUrl,
                    response.HttpStatusCode,
                    response.RawResponse
                );
            }
        }
        catch (HttpRequestException ex)
        {
            response.Success = false;
            response.ErrorMessage = $"HTTP request failed: {ex.Message}";
            _logger.LogError(ex, "HTTP request failed for {Endpoint}", source.EndpointUrl);
        }
        catch (TaskCanceledException ex)
        {
            response.Success = false;
            response.ErrorMessage = "Request timed out";
            _logger.LogError(ex, "Request timed out for {Endpoint}", source.EndpointUrl);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.ErrorMessage = $"Unexpected error: {ex.Message}";
            _logger.LogError(ex, "Unexpected error fetching from {Endpoint}", source.EndpointUrl);
        }

        return response;
    }

    /// <summary>
    /// Fetches all pages from a paginated API endpoint.
    /// Supports Offset, Page, Cursor, and LinkHeader pagination.
    /// </summary>
    public async Task<PaginatedFetchResult> FetchAllPagesAsync(
        ApiSource source, 
        PaginationType paginationType,
        PaginationConfig? paginationConfig = null,
        CancellationToken cancellationToken = default)
    {
        var result = new PaginatedFetchResult();
        paginationConfig ??= new PaginationConfig();

        // Apply safety limit
        var maxPages = Math.Min(paginationConfig.MaxPages, 1000);

        // Rate limiting setup
        var rateLimitDelay = source.RateLimitPerMinute.HasValue && source.RateLimitPerMinute.Value > 0
            ? TimeSpan.FromMilliseconds(60000.0 / source.RateLimitPerMinute.Value)
            : TimeSpan.FromMilliseconds(paginationConfig.DelayMs);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            switch (paginationType)
            {
                case PaginationType.Offset:
                    await FetchOffsetPagesAsync(source, paginationConfig, result, maxPages, rateLimitDelay, cancellationToken);
                    break;

                case PaginationType.Page:
                    await FetchPageNumberPagesAsync(source, paginationConfig, result, maxPages, rateLimitDelay, cancellationToken);
                    break;

                case PaginationType.Cursor:
                    await FetchCursorPagesAsync(source, paginationConfig, result, maxPages, rateLimitDelay, cancellationToken);
                    break;

                case PaginationType.LinkHeader:
                    await FetchLinkHeaderPagesAsync(source, paginationConfig, result, maxPages, rateLimitDelay, cancellationToken);
                    break;

                case PaginationType.None:
                default:
                    // Single page fetch
                    var response = await FetchAsync(source, cancellationToken);
                    if (response.Success)
                    {
                        result.Pages.Add(response);
                        result.TotalRecords = response.RecordCount;
                    }
                    else
                    {
                        result.ErrorMessage = response.ErrorMessage;
                    }
                    break;
            }

            stopwatch.Stop();
            result.TotalTimeMs = stopwatch.ElapsedMilliseconds;
            result.TotalPages = result.Pages.Count;
            result.Success = result.Pages.Count > 0;
            result.MaxPagesReached = result.Pages.Count >= maxPages;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Pagination failed: {ex.Message}";
            _logger.LogError(ex, "Failed to fetch paginated data from {Endpoint}", source.EndpointUrl);
        }

        return result;
    }

    private async Task FetchOffsetPagesAsync(
        ApiSource source,
        PaginationConfig config,
        PaginatedFetchResult result,
        int maxPages,
        TimeSpan rateLimitDelay,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        var limit = config.Limit;
        var pageFetchStopwatch = new System.Diagnostics.Stopwatch();

        for (int page = 0; page < maxPages; page++)
        {
            if (page > 0 && rateLimitDelay > TimeSpan.Zero)
            {
                await Task.Delay(rateLimitDelay, cancellationToken);
            }

            pageFetchStopwatch.Restart();

            // Create modified source with pagination params
            var paginatedSource = CloneSourceWithPaginationParams(source, new Dictionary<string, string>
            {
                [config.OffsetParam] = offset.ToString(),
                [config.LimitParam] = limit.ToString()
            });

            var response = await FetchAsync(paginatedSource, cancellationToken);
            pageFetchStopwatch.Stop();
            result.PageFetchTimes.Add(pageFetchStopwatch.ElapsedMilliseconds);
            
            if (!response.Success)
            {
                if (page == 0)
                {
                    result.ErrorMessage = response.ErrorMessage;
                }
                break;
            }

            result.Pages.Add(response);

            // Count records in response
            var recordCount = CountRecordsInResponse(response.RawResponse ?? "", source.ResponsePath);
            result.TotalRecords += recordCount;

            if (config.StopOnEmpty && recordCount == 0)
            {
                break;
            }

            if (config.StopOnPartial && recordCount < limit)
            {
                break; // Last page
            }

            offset += limit;
        }
    }

    private async Task FetchPageNumberPagesAsync(
        ApiSource source,
        PaginationConfig config,
        PaginatedFetchResult result,
        int maxPages,
        TimeSpan rateLimitDelay,
        CancellationToken cancellationToken)
    {
        var pageNumber = config.StartPage;
        var pageFetchStopwatch = new System.Diagnostics.Stopwatch();

        for (int page = 0; page < maxPages; page++)
        {
            if (page > 0 && rateLimitDelay > TimeSpan.Zero)
            {
                await Task.Delay(rateLimitDelay, cancellationToken);
            }

            pageFetchStopwatch.Restart();

            var paginatedSource = CloneSourceWithPaginationParams(source, new Dictionary<string, string>
            {
                [config.PageParam] = pageNumber.ToString(),
                [config.LimitParam] = config.Limit.ToString()
            });

            var response = await FetchAsync(paginatedSource, cancellationToken);
            pageFetchStopwatch.Stop();
            result.PageFetchTimes.Add(pageFetchStopwatch.ElapsedMilliseconds);
            
            if (!response.Success)
            {
                if (page == 0)
                {
                    result.ErrorMessage = response.ErrorMessage;
                }
                break;
            }

            result.Pages.Add(response);

            var recordCount = CountRecordsInResponse(response.RawResponse ?? "", source.ResponsePath);
            result.TotalRecords += recordCount;

            if (config.StopOnEmpty && recordCount == 0)
            {
                break;
            }

            if (config.StopOnPartial && recordCount < config.Limit)
            {
                break; // Last page
            }

            pageNumber++;
        }
    }

    private async Task FetchCursorPagesAsync(
        ApiSource source,
        PaginationConfig config,
        PaginatedFetchResult result,
        int maxPages,
        TimeSpan rateLimitDelay,
        CancellationToken cancellationToken)
    {
        string? cursor = null;
        var pageFetchStopwatch = new System.Diagnostics.Stopwatch();

        for (int page = 0; page < maxPages; page++)
        {
            if (page > 0 && rateLimitDelay > TimeSpan.Zero)
            {
                await Task.Delay(rateLimitDelay, cancellationToken);
            }

            pageFetchStopwatch.Restart();

            var paginationParams = new Dictionary<string, string>
            {
                [config.LimitParam] = config.Limit.ToString()
            };

            if (!string.IsNullOrEmpty(cursor))
            {
                paginationParams[config.CursorParam] = cursor;
            }

            var paginatedSource = CloneSourceWithPaginationParams(source, paginationParams);
            var response = await FetchAsync(paginatedSource, cancellationToken);
            pageFetchStopwatch.Stop();
            result.PageFetchTimes.Add(pageFetchStopwatch.ElapsedMilliseconds);
            
            if (!response.Success)
            {
                if (page == 0)
                {
                    result.ErrorMessage = response.ErrorMessage;
                }
                break;
            }

            result.Pages.Add(response);

            var recordCount = CountRecordsInResponse(response.RawResponse ?? "", source.ResponsePath);
            result.TotalRecords += recordCount;

            if (config.StopOnEmpty && recordCount == 0)
            {
                break;
            }

            // Extract next cursor from response
            cursor = ExtractCursorFromResponse(response.RawResponse ?? "", config.CursorPath);
            
            if (string.IsNullOrEmpty(cursor))
            {
                break; // No more pages
            }
        }
    }

    private async Task FetchLinkHeaderPagesAsync(
        ApiSource source,
        PaginationConfig config,
        PaginatedFetchResult result,
        int maxPages,
        TimeSpan rateLimitDelay,
        CancellationToken cancellationToken)
    {
        var currentUrl = source.EndpointUrl;
        var pageFetchStopwatch = new System.Diagnostics.Stopwatch();

        for (int page = 0; page < maxPages; page++)
        {
            if (page > 0 && rateLimitDelay > TimeSpan.Zero)
            {
                await Task.Delay(rateLimitDelay, cancellationToken);
            }

            pageFetchStopwatch.Restart();

            var sourceClone = CloneSource(source);
            sourceClone.EndpointUrl = currentUrl;

            var response = await FetchAsync(sourceClone, cancellationToken);
            pageFetchStopwatch.Stop();
            result.PageFetchTimes.Add(pageFetchStopwatch.ElapsedMilliseconds);
            
            if (!response.Success)
            {
                if (page == 0)
                {
                    result.ErrorMessage = response.ErrorMessage;
                }
                break;
            }

            result.Pages.Add(response);

            var recordCount = CountRecordsInResponse(response.RawResponse ?? "", source.ResponsePath);
            result.TotalRecords += recordCount;

            if (config.StopOnEmpty && recordCount == 0)
            {
                break;
            }

            // Extract next URL from Link header
            var nextUrl = ExtractNextLinkFromHeaders(response.Metadata);
            
            if (string.IsNullOrEmpty(nextUrl))
            {
                break; // No more pages
            }

            currentUrl = nextUrl;
        }
    }

    private ApiSource CloneSourceWithPaginationParams(ApiSource source, Dictionary<string, string> additionalParams)
    {
        var clone = CloneSource(source);

        // Merge with existing query params
        var existingParams = string.IsNullOrEmpty(clone.QueryParams)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(clone.QueryParams) ?? new Dictionary<string, string>();

        foreach (var param in additionalParams)
        {
            existingParams[param.Key] = param.Value;
        }

        clone.QueryParams = JsonSerializer.Serialize(existingParams);
        return clone;
    }

    private ApiSource CloneSource(ApiSource source)
    {
        return new ApiSource
        {
            Id = source.Id,
            Name = source.Name,
            ApiType = source.ApiType,
            EndpointUrl = source.EndpointUrl,
            HttpMethod = source.HttpMethod,
            AuthType = source.AuthType,
            AuthConfigEncrypted = source.AuthConfigEncrypted,
            AuthLocation = source.AuthLocation,
            HeaderName = source.HeaderName,
            QueryParam = source.QueryParam,
            HasApiKey = source.HasApiKey,
            KeyVersion = source.KeyVersion,
            ApiKeyCiphertext = source.ApiKeyCiphertext,
            ApiKeyIv = source.ApiKeyIv,
            ApiKeyTag = source.ApiKeyTag,
            CustomHeaders = source.CustomHeaders,
            QueryParams = source.QueryParams,
            RequestBody = source.RequestBody,
            ResponsePath = source.ResponsePath
        };
    }

    private int CountRecordsInResponse(string jsonResponse, string? dataPath)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Navigate to data path if specified
            if (!string.IsNullOrEmpty(dataPath))
            {
                foreach (var segment in dataPath.Split('.'))
                {
                    if (root.TryGetProperty(segment, out var element))
                    {
                        root = element;
                    }
                    else
                    {
                        return 0;
                    }
                }
            }

            // Count array elements
            if (root.ValueKind == JsonValueKind.Array)
            {
                return root.GetArrayLength();
            }

            // Single object
            return root.ValueKind == JsonValueKind.Object ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    private string? ExtractCursorFromResponse(string jsonResponse, string? cursorPath)
    {
        if (string.IsNullOrEmpty(cursorPath))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            foreach (var segment in cursorPath.Split('.'))
            {
                if (root.TryGetProperty(segment, out var element))
                {
                    root = element;
                }
                else
                {
                    return null;
                }
            }

            return root.GetString();
        }
        catch
        {
            return null;
        }
    }

    private string? ExtractNextLinkFromHeaders(Dictionary<string, string>? metadata)
    {
        if (metadata == null)
        {
            return null;
        }

        // Look for Link header with "header_" prefix
        if (!metadata.TryGetValue("header_Link", out var linkHeader) && 
            !metadata.TryGetValue("header_link", out linkHeader))
        {
            return null;
        }

        // Parse GitHub-style Link header: <url>; rel="next"
        var links = linkHeader.Split(',');
        foreach (var link in links)
        {
            if (link.Contains("rel=\"next\"", StringComparison.OrdinalIgnoreCase))
            {
                var match = System.Text.RegularExpressions.Regex.Match(link, @"<([^>]+)>");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
        }

        return null;
    }

    public async Task<bool> TestConnectionAsync(ApiSource source, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await FetchAsync(source, cancellationToken);
            return response.Success;
        }
        catch
        {
            return false;
        }
    }

    private async Task<HttpRequestMessage> BuildRequestAsync(ApiSource source, CancellationToken cancellationToken)
    {
        // Build URL with query parameters
        var uriBuilder = new UriBuilder(source.EndpointUrl);

        if (!string.IsNullOrEmpty(source.QueryParams))
        {
            var queryParams = JsonSerializer.Deserialize<Dictionary<string, string>>(source.QueryParams);
            if (queryParams?.Count > 0)
            {
                var query = string.Join("&", queryParams.Select(kvp =>
                    $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

                uriBuilder.Query = string.IsNullOrEmpty(uriBuilder.Query)
                    ? query
                    : uriBuilder.Query.TrimStart('?') + "&" + query;
            }
        }

        var request = new HttpRequestMessage(
            new HttpMethod(source.HttpMethod),
            uriBuilder.Uri
        );

        // Add custom headers
        if (!string.IsNullOrEmpty(source.CustomHeaders))
        {
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(source.CustomHeaders);
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }

        // Apply authentication
        await _authManager.ApplyAuthenticationAsync(request, source, cancellationToken);

        // Add request body for POST/PUT/PATCH
        if (!string.IsNullOrEmpty(source.RequestBody) &&
            (source.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
             source.HttpMethod.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
             source.HttpMethod.Equals("PATCH", StringComparison.OrdinalIgnoreCase)))
        {
            request.Content = new StringContent(
                source.RequestBody,
                Encoding.UTF8,
                "application/json"
            );
        }

        return request;
    }
}
