using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SmartCollectAPI.Models;

namespace SmartCollectAPI.Services.ApiIngestion;

/// <summary>
/// GraphQL-specific API client implementation
/// Supports GraphQL queries, mutations, cursor-based pagination, and error handling
/// </summary>
public class GraphQLClient(
    IHttpClientFactory httpClientFactory,
    ILogger<GraphQLClient> logger,
    IAuthenticationManager authManager) : IApiClient
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<GraphQLClient> _logger = logger;
    private readonly IAuthenticationManager _authManager = authManager;

    /// <summary>
    /// Fetch a single GraphQL query response
    /// </summary>
    public async Task<ApiResponse> FetchAsync(ApiSource source, CancellationToken cancellationToken = default)
    {
        var response = new ApiResponse
        {
            Metadata = new Dictionary<string, string>()
        };

        try
        {
            using var httpClient = _httpClientFactory.CreateClient("ApiIngestion");
            var request = await BuildGraphQLRequestAsync(source, null, cancellationToken);

            _logger.LogInformation(
                "Fetching data from GraphQL endpoint: {Url}",
                source.EndpointUrl
            );

            using var httpResponse = await httpClient.SendAsync(request, cancellationToken);

            response.HttpStatusCode = (int)httpResponse.StatusCode;
            response.RawResponse = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            response.ResponseSizeBytes = response.RawResponse?.Length ?? 0;

            if (httpResponse.IsSuccessStatusCode)
            {
                // Parse GraphQL response
                var graphQLResponse = ParseGraphQLResponse(response.RawResponse);
                
                if (graphQLResponse.Errors != null && graphQLResponse.Errors.Count > 0)
                {
                    response.Success = false;
                    response.ErrorMessage = string.Join("; ", graphQLResponse.Errors.Select(e => e.Message));
                    _logger.LogWarning("GraphQL errors: {Errors}", response.ErrorMessage);
                }
                else
                {
                    response.Success = true;
                    response.ParsedData = graphQLResponse.Data;
                    
                    // Extract data from response path if specified
                    if (!string.IsNullOrEmpty(source.ResponsePath) && graphQLResponse.Data != null)
                    {
                        response.ParsedData = ExtractDataFromPath(graphQLResponse.Data, source.ResponsePath);
                    }
                    
                    response.RecordCount = CountRecords(response.ParsedData);
                }

                // Extract metadata from response headers
                foreach (var header in httpResponse.Headers)
                {
                    response.Metadata[header.Key] = string.Join(", ", header.Value);
                }
            }
            else
            {
                response.Success = false;
                response.ErrorMessage = $"HTTP {response.HttpStatusCode}: {httpResponse.ReasonPhrase}";
            }
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.ErrorMessage = $"GraphQL request failed: {ex.Message}";
            _logger.LogError(ex, "Failed to fetch GraphQL data from {Endpoint}", source.EndpointUrl);
        }

        return response;
    }

    /// <summary>
    /// Fetch all pages from a paginated GraphQL API using cursor-based pagination
    /// </summary>
    public async Task<PaginatedFetchResult> FetchAllPagesAsync(
        ApiSource source,
        PaginationType paginationType,
        PaginationConfig? paginationConfig = null,
        CancellationToken cancellationToken = default)
    {
        if (paginationType == PaginationType.None)
        {
            // No pagination - single fetch
            var singleResponse = await FetchAsync(source, cancellationToken);
            return new PaginatedFetchResult
            {
                Pages = [singleResponse],
                TotalPages = 1,
                TotalRecords = singleResponse.RecordCount,
                Success = singleResponse.Success,
                ErrorMessage = singleResponse.ErrorMessage,
                TotalTimeMs = 0
            };
        }

        // GraphQL typically uses cursor-based pagination
        if (paginationType == PaginationType.Cursor)
        {
            return await FetchCursorPagesAsync(source, paginationConfig, cancellationToken);
        }

        // Offset pagination can also be used with GraphQL
        if (paginationType == PaginationType.Offset)
        {
            return await FetchOffsetPagesAsync(source, paginationConfig, cancellationToken);
        }

        // Other pagination types not supported for GraphQL
        _logger.LogWarning("Pagination type {Type} not supported for GraphQL, using single fetch", paginationType);
        var response = await FetchAsync(source, cancellationToken);
        return new PaginatedFetchResult
        {
            Pages = [response],
            TotalPages = 1,
            TotalRecords = response.RecordCount,
            Success = response.Success,
            ErrorMessage = response.ErrorMessage
        };
    }

    /// <summary>
    /// Fetch all pages using cursor-based pagination (standard for GraphQL)
    /// </summary>
    private async Task<PaginatedFetchResult> FetchCursorPagesAsync(
        ApiSource source,
        PaginationConfig? config,
        CancellationToken cancellationToken)
    {
        config ??= new PaginationConfig();
        var result = new PaginatedFetchResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        string? cursor = null;
        var pageNumber = 0;
        var maxPages = config.MaxPages;
        var hasNextPage = true;

        _logger.LogInformation(
            "Starting cursor-based pagination for GraphQL: {Endpoint}, Limit: {Limit}, MaxPages: {MaxPages}",
            source.EndpointUrl,
            config.Limit,
            maxPages
        );

        while (hasNextPage && pageNumber < maxPages && !cancellationToken.IsCancellationRequested)
        {
            var pageStopwatch = System.Diagnostics.Stopwatch.StartNew();
            pageNumber++;

            try
            {
                // Add delay between requests (except first)
                if (pageNumber > 1 && config.DelayBetweenPagesMs > 0)
                {
                    await Task.Delay(config.DelayBetweenPagesMs, cancellationToken);
                }

                // Build request with cursor variable
                var variables = BuildCursorVariables(source, cursor, config);
                using var httpClient = _httpClientFactory.CreateClient("ApiIngestion");
                var request = await BuildGraphQLRequestAsync(source, variables, cancellationToken);

                _logger.LogDebug(
                    "Fetching GraphQL page {PageNumber} with cursor: {Cursor}",
                    pageNumber,
                    cursor ?? "(first page)"
                );

                using var httpResponse = await httpClient.SendAsync(request, cancellationToken);
                var rawResponse = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

                var apiResponse = new ApiResponse
                {
                    Success = httpResponse.IsSuccessStatusCode,
                    HttpStatusCode = (int)httpResponse.StatusCode,
                    RawResponse = rawResponse,
                    ResponseSizeBytes = rawResponse.Length,
                    Metadata = new Dictionary<string, string>()
                };

                if (httpResponse.IsSuccessStatusCode)
                {
                    var graphQLResponse = ParseGraphQLResponse(rawResponse);

                    if (graphQLResponse.Errors != null && graphQLResponse.Errors.Count > 0)
                    {
                        apiResponse.Success = false;
                        apiResponse.ErrorMessage = string.Join("; ", graphQLResponse.Errors.Select(e => e.Message));
                        result.Success = false;
                        result.ErrorMessage = apiResponse.ErrorMessage;
                        break;
                    }

                    // Extract data from response path
                    var data = graphQLResponse.Data;
                    if (!string.IsNullOrEmpty(source.ResponsePath) && data != null)
                    {
                        data = ExtractDataFromPath(data, source.ResponsePath);
                    }

                    apiResponse.ParsedData = data;
                    apiResponse.RecordCount = CountRecords(data);

                    // Extract pagination info
                    var paginationInfo = ExtractPaginationInfo(graphQLResponse.Data, config);
                    cursor = paginationInfo.EndCursor;
                    hasNextPage = paginationInfo.HasNextPage;

                    _logger.LogDebug(
                        "Page {PageNumber}: {RecordCount} records, HasNext: {HasNext}, NextCursor: {Cursor}",
                        pageNumber,
                        apiResponse.RecordCount,
                        hasNextPage,
                        cursor ?? "(none)"
                    );
                }
                else
                {
                    apiResponse.ErrorMessage = $"HTTP {apiResponse.HttpStatusCode}: {httpResponse.ReasonPhrase}";
                    result.Success = false;
                    result.ErrorMessage = apiResponse.ErrorMessage;
                    break;
                }

                pageStopwatch.Stop();
                result.Pages.Add(apiResponse);
                result.PageFetchTimes.Add(pageStopwatch.ElapsedMilliseconds);
                result.TotalRecords += apiResponse.RecordCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching GraphQL page {PageNumber}", pageNumber);
                result.Success = false;
                result.ErrorMessage = $"Page {pageNumber} failed: {ex.Message}";
                break;
            }
        }

        stopwatch.Stop();
        result.TotalPages = result.Pages.Count;
        result.TotalTimeMs = stopwatch.ElapsedMilliseconds;
        result.MaxPagesReached = pageNumber >= maxPages && hasNextPage;

        if (result.Success && result.Pages.Count == 0)
        {
            result.Success = false;
            result.ErrorMessage = "No pages fetched";
        }

        _logger.LogInformation(
            "Completed GraphQL cursor pagination: {Pages} pages, {Records} records in {Duration}ms, MaxReached: {MaxReached}",
            result.TotalPages,
            result.TotalRecords,
            result.TotalTimeMs,
            result.MaxPagesReached
        );

        return result;
    }

    /// <summary>
    /// Fetch all pages using offset-based pagination (less common for GraphQL)
    /// </summary>
    private async Task<PaginatedFetchResult> FetchOffsetPagesAsync(
        ApiSource source,
        PaginationConfig? config,
        CancellationToken cancellationToken)
    {
        config ??= new PaginationConfig();
        var result = new PaginatedFetchResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var limit = config.Limit;
        var offset = 0;
        var pageNumber = 0;
        var maxPages = config.MaxPages;

        _logger.LogInformation(
            "Starting offset-based pagination for GraphQL: {Endpoint}, Limit: {Limit}, MaxPages: {MaxPages}",
            source.EndpointUrl,
            limit,
            maxPages
        );

        while (pageNumber < maxPages && !cancellationToken.IsCancellationRequested)
        {
            var pageStopwatch = System.Diagnostics.Stopwatch.StartNew();
            pageNumber++;

            try
            {
                // Add delay between requests (except first)
                if (pageNumber > 1 && config.DelayBetweenPagesMs > 0)
                {
                    await Task.Delay(config.DelayBetweenPagesMs, cancellationToken);
                }

                // Build request with offset variables
                var variables = BuildOffsetVariables(source, offset, limit);
                using var httpClient = _httpClientFactory.CreateClient("ApiIngestion");
                var request = await BuildGraphQLRequestAsync(source, variables, cancellationToken);

                _logger.LogDebug(
                    "Fetching GraphQL page {PageNumber} with offset: {Offset}, limit: {Limit}",
                    pageNumber,
                    offset,
                    limit
                );

                using var httpResponse = await httpClient.SendAsync(request, cancellationToken);
                var rawResponse = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

                var apiResponse = new ApiResponse
                {
                    Success = httpResponse.IsSuccessStatusCode,
                    HttpStatusCode = (int)httpResponse.StatusCode,
                    RawResponse = rawResponse,
                    ResponseSizeBytes = rawResponse.Length,
                    Metadata = new Dictionary<string, string>()
                };

                if (httpResponse.IsSuccessStatusCode)
                {
                    var graphQLResponse = ParseGraphQLResponse(rawResponse);

                    if (graphQLResponse.Errors != null && graphQLResponse.Errors.Count > 0)
                    {
                        apiResponse.Success = false;
                        apiResponse.ErrorMessage = string.Join("; ", graphQLResponse.Errors.Select(e => e.Message));
                        result.Success = false;
                        result.ErrorMessage = apiResponse.ErrorMessage;
                        break;
                    }

                    // Extract data from response path
                    var data = graphQLResponse.Data;
                    if (!string.IsNullOrEmpty(source.ResponsePath) && data != null)
                    {
                        data = ExtractDataFromPath(data, source.ResponsePath);
                    }

                    apiResponse.ParsedData = data;
                    apiResponse.RecordCount = CountRecords(data);

                    _logger.LogDebug(
                        "Page {PageNumber}: {RecordCount} records",
                        pageNumber,
                        apiResponse.RecordCount
                    );

                    // Stop if no records returned (end of data)
                    if (apiResponse.RecordCount == 0)
                    {
                        _logger.LogDebug("No records in page {PageNumber}, stopping pagination", pageNumber);
                        break;
                    }
                }
                else
                {
                    apiResponse.ErrorMessage = $"HTTP {apiResponse.HttpStatusCode}: {httpResponse.ReasonPhrase}";
                    result.Success = false;
                    result.ErrorMessage = apiResponse.ErrorMessage;
                    break;
                }

                pageStopwatch.Stop();
                result.Pages.Add(apiResponse);
                result.PageFetchTimes.Add(pageStopwatch.ElapsedMilliseconds);
                result.TotalRecords += apiResponse.RecordCount;

                offset += limit;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching GraphQL page {PageNumber}", pageNumber);
                result.Success = false;
                result.ErrorMessage = $"Page {pageNumber} failed: {ex.Message}";
                break;
            }
        }

        stopwatch.Stop();
        result.TotalPages = result.Pages.Count;
        result.TotalTimeMs = stopwatch.ElapsedMilliseconds;
        result.MaxPagesReached = pageNumber >= maxPages;

        if (result.Success && result.Pages.Count == 0)
        {
            result.Success = false;
            result.ErrorMessage = "No pages fetched";
        }

        _logger.LogInformation(
            "Completed GraphQL offset pagination: {Pages} pages, {Records} records in {Duration}ms",
            result.TotalPages,
            result.TotalRecords,
            result.TotalTimeMs
        );

        return result;
    }

    /// <summary>
    /// Test connection to GraphQL endpoint
    /// </summary>
    public async Task<bool> TestConnectionAsync(ApiSource source, CancellationToken cancellationToken = default)
    {
        try
        {
            // For GraphQL, we'll send a simple introspection query to test the connection
            var testSource = new ApiSource
            {
                EndpointUrl = source.EndpointUrl,
                AuthType = source.AuthType,
                AuthConfigEncrypted = source.AuthConfigEncrypted,
                CustomHeaders = source.CustomHeaders,
                GraphQLQuery = source.GraphQLQuery ?? "{ __typename }",
                GraphQLVariables = source.GraphQLVariables
            };

            var response = await FetchAsync(testSource, cancellationToken);
            return response.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GraphQL connection test failed for {Endpoint}", source.EndpointUrl);
            return false;
        }
    }

    /// <summary>
    /// Build GraphQL HTTP request with query and variables
    /// </summary>
    private async Task<HttpRequestMessage> BuildGraphQLRequestAsync(
        ApiSource source,
        Dictionary<string, object>? variables,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, source.EndpointUrl);

        // Apply authentication
        await _authManager.ApplyAuthenticationAsync(request, source, cancellationToken);

        // Apply custom headers
        if (!string.IsNullOrEmpty(source.CustomHeaders))
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse custom headers for {Source}", source.Name);
            }
        }

        // Build GraphQL request body
        var graphQLRequest = new Dictionary<string, object>
        {
            ["query"] = source.GraphQLQuery ?? throw new InvalidOperationException("GraphQL query is required")
        };

        // Merge variables from source and pagination
        var allVariables = new Dictionary<string, object>();
        
        // Add variables from source configuration
        if (!string.IsNullOrEmpty(source.GraphQLVariables))
        {
            try
            {
                var sourceVars = JsonSerializer.Deserialize<Dictionary<string, object>>(source.GraphQLVariables);
                if (sourceVars != null)
                {
                    foreach (var v in sourceVars)
                    {
                        allVariables[v.Key] = v.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse GraphQL variables from source");
            }
        }

        // Add/override with pagination variables
        if (variables != null)
        {
            foreach (var v in variables)
            {
                allVariables[v.Key] = v.Value;
            }
        }

        if (allVariables.Count > 0)
        {
            graphQLRequest["variables"] = allVariables;
        }

        var json = JsonSerializer.Serialize(graphQLRequest);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return request;
    }

    /// <summary>
    /// Parse GraphQL response with errors and data
    /// </summary>
    private GraphQLResponse ParseGraphQLResponse(string? rawResponse)
    {
        if (string.IsNullOrEmpty(rawResponse))
        {
            return new GraphQLResponse();
        }

        try
        {
            var jsonDoc = JsonDocument.Parse(rawResponse);
            var root = jsonDoc.RootElement;

            var response = new GraphQLResponse();

            // Parse errors
            if (root.TryGetProperty("errors", out var errorsElement))
            {
                response.Errors = JsonSerializer.Deserialize<List<GraphQLError>>(errorsElement.GetRawText());
            }

            // Parse data
            if (root.TryGetProperty("data", out var dataElement))
            {
                response.Data = JsonSerializer.Deserialize<object>(dataElement.GetRawText());
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse GraphQL response");
            return new GraphQLResponse
            {
                Errors = new List<GraphQLError>
                {
                    new GraphQLError { Message = $"Failed to parse response: {ex.Message}" }
                }
            };
        }
    }

    /// <summary>
    /// Extract data from JSONPath-like expression
    /// </summary>
    private object? ExtractDataFromPath(object? data, string path)
    {
        if (data == null || string.IsNullOrEmpty(path))
        {
            return data;
        }

        try
        {
            var json = JsonSerializer.Serialize(data);
            var jsonNode = JsonNode.Parse(json);

            // Simple path traversal (e.g., "data.users.edges.node")
            var parts = path.TrimStart('$', '.').Split('.');
            JsonNode? current = jsonNode;

            foreach (var part in parts)
            {
                if (current is JsonObject obj && obj.ContainsKey(part))
                {
                    current = obj[part];
                }
                else
                {
                    _logger.LogWarning("Path segment '{Part}' not found in response", part);
                    return data;
                }
            }

            return current != null ? JsonSerializer.Deserialize<object>(current.ToJsonString()) : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract data from path '{Path}'", path);
            return data;
        }
    }

    /// <summary>
    /// Extract pagination info from GraphQL response (follows Relay Connection spec)
    /// </summary>
    private (string? EndCursor, bool HasNextPage) ExtractPaginationInfo(object? data, PaginationConfig config)
    {
        if (data == null)
        {
            return (null, false);
        }

        try
        {
            var json = JsonSerializer.Serialize(data);
            var jsonNode = JsonNode.Parse(json);

            // Try to find pageInfo using configured path or default
            var pageInfoPath = config.PageInfoPath ?? "pageInfo";
            var parts = pageInfoPath.TrimStart('$', '.').Split('.');
            JsonNode? current = jsonNode;

            foreach (var part in parts)
            {
                if (current is JsonObject obj && obj.ContainsKey(part))
                {
                    current = obj[part];
                }
                else
                {
                    // Path not found, return defaults
                    return (null, false);
                }
            }

            if (current is JsonObject pageInfo)
            {
                var endCursor = pageInfo["endCursor"]?.GetValue<string>();
                var hasNextPage = pageInfo["hasNextPage"]?.GetValue<bool>() ?? false;
                return (endCursor, hasNextPage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract pagination info from GraphQL response");
        }

        return (null, false);
    }

    /// <summary>
    /// Build variables for cursor-based pagination
    /// </summary>
    private Dictionary<string, object> BuildCursorVariables(ApiSource source, string? cursor, PaginationConfig config)
    {
        var variables = new Dictionary<string, object>();

        var firstParam = config.FirstParam ?? "first";
        var afterParam = config.AfterParam ?? "after";

        variables[firstParam] = config.Limit;

        if (!string.IsNullOrEmpty(cursor))
        {
            variables[afterParam] = cursor;
        }

        return variables;
    }

    /// <summary>
    /// Build variables for offset-based pagination
    /// </summary>
    private Dictionary<string, object> BuildOffsetVariables(ApiSource source, int offset, int limit)
    {
        var variables = new Dictionary<string, object>
        {
            ["offset"] = offset,
            ["limit"] = limit
        };

        return variables;
    }

    /// <summary>
    /// Count records in parsed data
    /// </summary>
    private int CountRecords(object? data)
    {
        if (data == null)
        {
            return 0;
        }

        try
        {
            var json = JsonSerializer.Serialize(data);
            var element = JsonDocument.Parse(json).RootElement;

            // If it's an array, count elements
            if (element.ValueKind == JsonValueKind.Array)
            {
                return element.GetArrayLength();
            }

            // If it's an object with an array property, try to find it
            if (element.ValueKind == JsonValueKind.Object)
            {
                // Try common array property names
                var arrayProps = new[] { "edges", "nodes", "items", "data", "results" };
                foreach (var prop in arrayProps)
                {
                    if (element.TryGetProperty(prop, out var arrayElement) &&
                        arrayElement.ValueKind == JsonValueKind.Array)
                    {
                        return arrayElement.GetArrayLength();
                    }
                }

                // If it's a single object, count as 1
                return 1;
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to count records in response");
            return 0;
        }
    }
}

/// <summary>
/// GraphQL response structure
/// </summary>
public class GraphQLResponse
{
    public object? Data { get; set; }
    public List<GraphQLError>? Errors { get; set; }
}

/// <summary>
/// GraphQL error structure
/// </summary>
public class GraphQLError
{
    public string Message { get; set; } = string.Empty;
    public List<GraphQLErrorLocation>? Locations { get; set; }
    public List<object>? Path { get; set; }
    public Dictionary<string, object>? Extensions { get; set; }
}

/// <summary>
/// GraphQL error location
/// </summary>
public class GraphQLErrorLocation
{
    public int Line { get; set; }
    public int Column { get; set; }
}
