using System.Text.Json.Serialization;

namespace SmartCollectAPI.Services.ApiIngestion;

/// <summary>
/// Configuration for API pagination behavior
/// </summary>
public class PaginationConfig
{
    /// <summary>
    /// Number of records per page/request. Default: 100
    /// </summary>
    [JsonPropertyName("limit")]
    public int Limit { get; set; } = 100;

    /// <summary>
    /// Maximum number of pages to fetch (safety limit). Default: 50
    /// </summary>
    [JsonPropertyName("maxPages")]
    public int MaxPages { get; set; } = 50;

    /// <summary>
    /// Delay in milliseconds between requests (rate limiting). Default: 1000ms
    /// </summary>
    [JsonPropertyName("delayMs")]
    public int DelayMs { get; set; } = 1000;

    /// <summary>
    /// Query parameter name for page number (Page pagination). Default: "page"
    /// </summary>
    [JsonPropertyName("pageParam")]
    public string PageParam { get; set; } = "page";

    /// <summary>
    /// Query parameter name for limit/per_page. Default: "limit"
    /// </summary>
    [JsonPropertyName("limitParam")]
    public string LimitParam { get; set; } = "limit";

    /// <summary>
    /// Query parameter name for offset (Offset pagination). Default: "offset"
    /// </summary>
    [JsonPropertyName("offsetParam")]
    public string OffsetParam { get; set; } = "offset";

    /// <summary>
    /// Query parameter name for cursor (Cursor pagination). Default: "cursor"
    /// </summary>
    [JsonPropertyName("cursorParam")]
    public string CursorParam { get; set; } = "cursor";

    /// <summary>
    /// JSONPath to extract cursor from response. Default: "$.nextCursor"
    /// </summary>
    [JsonPropertyName("cursorPath")]
    public string? CursorPath { get; set; } = "$.nextCursor";

    /// <summary>
    /// JSONPath to extract total record count. Default: "$.total"
    /// </summary>
    [JsonPropertyName("totalCountPath")]
    public string? TotalCountPath { get; set; } = "$.total";

    /// <summary>
    /// For LinkHeader pagination: name of the "next" relation. Default: "next"
    /// </summary>
    [JsonPropertyName("linkRelation")]
    public string LinkRelation { get; set; } = "next";

    /// <summary>
    /// Starting page number (some APIs start at 0, others at 1). Default: 1
    /// </summary>
    [JsonPropertyName("startPage")]
    public int StartPage { get; set; } = 1;

    /// <summary>
    /// Whether to stop fetching when receiving empty results. Default: true
    /// </summary>
    [JsonPropertyName("stopOnEmpty")]
    public bool StopOnEmpty { get; set; } = true;

    /// <summary>
    /// Whether to stop when results are less than limit. Default: true
    /// </summary>
    [JsonPropertyName("stopOnPartial")]
    public bool StopOnPartial { get; set; } = true;

    // ============================================================
    // GraphQL-Specific Configuration
    // ============================================================

    /// <summary>
    /// JSONPath to extract pageInfo from GraphQL response. Default: "pageInfo"
    /// For nested paths use dot notation: "data.users.pageInfo"
    /// </summary>
    [JsonPropertyName("pageInfoPath")]
    public string? PageInfoPath { get; set; } = "pageInfo";

    /// <summary>
    /// GraphQL variable name for "first" parameter (cursor pagination). Default: "first"
    /// </summary>
    [JsonPropertyName("firstParam")]
    public string FirstParam { get; set; } = "first";

    /// <summary>
    /// GraphQL variable name for "after" parameter (cursor pagination). Default: "after"
    /// </summary>
    [JsonPropertyName("afterParam")]
    public string AfterParam { get; set; } = "after";

    /// <summary>
    /// GraphQL variable name for "last" parameter (backward pagination). Default: "last"
    /// </summary>
    [JsonPropertyName("lastParam")]
    public string LastParam { get; set; } = "last";

    /// <summary>
    /// GraphQL variable name for "before" parameter (backward pagination). Default: "before"
    /// </summary>
    [JsonPropertyName("beforeParam")]
    public string BeforeParam { get; set; } = "before";

    /// <summary>
    /// Delay between pagination requests in milliseconds. Default: 1000ms
    /// Alias for DelayMs for consistency
    /// </summary>
    [JsonPropertyName("delayBetweenPagesMs")]
    public int DelayBetweenPagesMs
    {
        get => DelayMs;
        set => DelayMs = value;
    }
}

/// <summary>
/// Types of pagination supported
/// </summary>
public enum PaginationType
{
    /// <summary>
    /// No pagination - single request
    /// </summary>
    None,

    /// <summary>
    /// Offset-based pagination: ?offset=0&limit=100
    /// </summary>
    Offset,

    /// <summary>
    /// Page number pagination: ?page=1&per_page=100
    /// </summary>
    Page,

    /// <summary>
    /// Cursor-based pagination: ?cursor=abc123
    /// </summary>
    Cursor,

    /// <summary>
    /// Link header pagination (GitHub style): Link: &lt;url&gt;; rel="next"
    /// </summary>
    LinkHeader
}

/// <summary>
/// Result of a paginated fetch operation
/// </summary>
public class PaginatedFetchResult
{
    /// <summary>
    /// All API responses from all pages
    /// </summary>
    public List<ApiResponse> Pages { get; set; } = [];

    /// <summary>
    /// Total number of pages fetched
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Total records across all pages
    /// </summary>
    public int TotalRecords { get; set; }

    /// <summary>
    /// Whether pagination completed successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Time taken for all requests
    /// </summary>
    public long TotalTimeMs { get; set; }

    /// <summary>
    /// Whether max pages limit was reached
    /// </summary>
    public bool MaxPagesReached { get; set; }

    /// <summary>
    /// Individual page fetch times
    /// </summary>
    public List<long> PageFetchTimes { get; set; } = [];
}
