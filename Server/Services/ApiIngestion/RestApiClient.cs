using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SmartCollectAPI.Models;

namespace SmartCollectAPI.Services.ApiIngestion;

public interface IApiClient
{
    Task<ApiResponse> FetchAsync(ApiSource source, CancellationToken cancellationToken = default);
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

public class RestApiClient : IApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RestApiClient> _logger;
    private readonly IAuthenticationManager _authManager;

    public RestApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<RestApiClient> logger,
        IAuthenticationManager authManager)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _authManager = authManager;
    }

    public async Task<ApiResponse> FetchAsync(ApiSource source, CancellationToken cancellationToken = default)
    {
        var response = new ApiResponse
        {
            Metadata = new Dictionary<string, string>()
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
