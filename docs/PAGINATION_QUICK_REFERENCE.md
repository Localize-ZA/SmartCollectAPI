# 🚀 Pagination Quick Reference Guide

Quick guide for using the pagination feature in SmartCollectAPI.

---

## 📋 TL;DR

```csharp
// Call pagination method
var result = await _apiClient.FetchAllPagesAsync(
    source,
    PaginationType.Offset,
    paginationConfig,
    cancellationToken
);

// Process all pages
foreach (var page in result.Pages)
{
    // page.RawResponse contains JSON data
    // page.RecordCount contains number of records
}

// Check metrics
_logger.LogInformation(
    "Fetched {Pages} pages, {Records} records in {Ms}ms",
    result.TotalPages,
    result.TotalRecords,
    result.TotalTimeMs
);
```

---

## 🎯 Pagination Types

### 1. Offset-based Pagination
**Use when**: API uses `offset` and `limit` parameters  
**Example API**: `https://api.example.com/users?offset=0&limit=100`

```json
{
  "pagination_type": "Offset",
  "pagination_config": {
    "limit": 100,
    "offsetParam": "offset",
    "limitParam": "limit",
    "maxPages": 50
  }
}
```

**Request sequence**:
```
Page 1: ?offset=0&limit=100
Page 2: ?offset=100&limit=100
Page 3: ?offset=200&limit=100
```

---

### 2. Page-based Pagination
**Use when**: API uses `page` and `per_page` parameters  
**Example API**: `https://api.example.com/products?page=1&per_page=50`

```json
{
  "pagination_type": "Page",
  "pagination_config": {
    "limit": 50,
    "pageParam": "page",
    "limitParam": "per_page",
    "startPage": 1,
    "maxPages": 20
  }
}
```

**Request sequence**:
```
Page 1: ?page=1&per_page=50
Page 2: ?page=2&per_page=50
Page 3: ?page=3&per_page=50
```

---

### 3. Cursor-based Pagination
**Use when**: API returns cursor tokens in response  
**Example API**: `https://api.example.com/posts?cursor=abc123&limit=25`

```json
{
  "pagination_type": "Cursor",
  "pagination_config": {
    "limit": 25,
    "cursorParam": "cursor",
    "limitParam": "limit",
    "cursorPath": "$.pagination.next_cursor",
    "maxPages": 100
  }
}
```

**Request sequence**:
```
Page 1: ?limit=25
        Response: {"pagination": {"next_cursor": "abc123"}}
        
Page 2: ?cursor=abc123&limit=25
        Response: {"pagination": {"next_cursor": "xyz789"}}
        
Page 3: ?cursor=xyz789&limit=25
        Response: {"pagination": {"next_cursor": null}}  ← STOP
```

---

### 4. LinkHeader Pagination
**Use when**: API returns `Link` header with `rel="next"`  
**Example API**: GitHub API, GitLab API

```json
{
  "pagination_type": "LinkHeader",
  "pagination_config": {
    "maxPages": 10
  }
}
```

**Response headers**:
```
Link: <https://api.github.com/repos?page=2>; rel="next",
      <https://api.github.com/repos?page=10>; rel="last"
```

---

## ⚙️ Configuration Options

### PaginationConfig Properties

| Property | Default | Description |
|----------|---------|-------------|
| `Limit` | 100 | Records per page |
| `MaxPages` | 50 | Maximum pages to fetch (hard cap: 1000) |
| `DelayMs` | 1000 | Delay between requests (milliseconds) |
| `PageParam` | "page" | Query param for page number |
| `LimitParam` | "limit" | Query param for limit/per_page |
| `OffsetParam` | "offset" | Query param for offset |
| `CursorParam` | "cursor" | Query param for cursor |
| `CursorPath` | "$.nextCursor" | JSONPath to extract cursor |
| `TotalCountPath` | "$.total" | JSONPath to extract total count |
| `LinkRelation` | "next" | Link header relation name |
| `StartPage` | 1 | Starting page number (some APIs use 0) |
| `StopOnEmpty` | true | Stop when receiving empty results |
| `StopOnPartial` | true | Stop when results < limit (last page) |

---

## 🚦 Rate Limiting

### Priority Order
1. **Source Rate Limit** (highest priority)
   ```csharp
   source.RateLimitPerMinute = 30;  // 30 req/min = 2s delay
   ```

2. **Config Delay** (fallback)
   ```json
   {
     "pagination_config": {
       "delayMs": 1000  // 1s delay
     }
   }
   ```

### Calculation
```csharp
// Source rate limit
delay = 60000 / RateLimitPerMinute

// Examples
RateLimitPerMinute = 60  → delay = 1000ms (1 req/s)
RateLimitPerMinute = 30  → delay = 2000ms (0.5 req/s)
RateLimitPerMinute = 120 → delay = 500ms (2 req/s)
```

---

## 📊 Result Metrics

### PaginatedFetchResult

```csharp
public class PaginatedFetchResult
{
    public List<ApiResponse> Pages { get; set; }       // All pages
    public int TotalPages { get; set; }                // Count of pages
    public int TotalRecords { get; set; }              // Total records
    public bool Success { get; set; }                  // Overall success
    public string? ErrorMessage { get; set; }          // Error if failed
    public long TotalTimeMs { get; set; }              // Total duration
    public bool MaxPagesReached { get; set; }          // Hit max limit?
    public List<long> PageFetchTimes { get; set; }     // Per-page times
}
```

### Example Usage
```csharp
var result = await _apiClient.FetchAllPagesAsync(...);

if (result.Success)
{
    _logger.LogInformation(
        "Pagination complete: {Pages} pages, {Records} records, {Time}ms",
        result.TotalPages,
        result.TotalRecords,
        result.TotalTimeMs
    );
    
    // Calculate average page fetch time
    var avgTime = result.PageFetchTimes.Average();
    
    // Warn if max pages reached
    if (result.MaxPagesReached)
    {
        _logger.LogWarning(
            "Max pages limit reached! May have more data available."
        );
    }
}
else
{
    _logger.LogError("Pagination failed: {Error}", result.ErrorMessage);
}
```

---

## 🧪 Testing Examples

### Test with JSONPlaceholder

#### Offset Pagination
```powershell
curl -X POST http://localhost:5001/api/apisources -H "Content-Type: application/json" -d '{
  "name": "JSONPlaceholder Posts",
  "endpoint_url": "https://jsonplaceholder.typicode.com/posts",
  "http_method": "GET",
  "auth_type": "None",
  "pagination_type": "Offset",
  "pagination_config": "{\"limit\":10,\"maxPages\":5,\"offsetParam\":\"_start\",\"limitParam\":\"_limit\"}"
}'
```

#### Page Pagination
```powershell
curl -X POST http://localhost:5001/api/apisources -H "Content-Type: application/json" -d '{
  "name": "JSONPlaceholder Comments",
  "endpoint_url": "https://jsonplaceholder.typicode.com/comments",
  "http_method": "GET",
  "auth_type": "None",
  "pagination_type": "Page",
  "pagination_config": "{\"limit\":25,\"maxPages\":3,\"pageParam\":\"_page\",\"limitParam\":\"_limit\"}"
}'
```

---

## 🔄 Common Patterns

### Pattern 1: Standard Offset Pagination
```json
{
  "pagination_type": "Offset",
  "pagination_config": {
    "limit": 100,
    "offsetParam": "offset",
    "limitParam": "limit"
  }
}
```

### Pattern 2: GitHub-style Page Pagination
```json
{
  "pagination_type": "Page",
  "pagination_config": {
    "limit": 100,
    "pageParam": "page",
    "limitParam": "per_page",
    "startPage": 1
  }
}
```

### Pattern 3: Stripe-style Cursor Pagination
```json
{
  "pagination_type": "Cursor",
  "pagination_config": {
    "limit": 100,
    "cursorParam": "starting_after",
    "limitParam": "limit",
    "cursorPath": "$.data[-1].id"
  }
}
```

### Pattern 4: REST API with Link Headers
```json
{
  "pagination_type": "LinkHeader",
  "pagination_config": {
    "maxPages": 50
  }
}
```

---

## ⚠️ Common Issues

### Issue 1: API returns 429 Too Many Requests
**Solution**: Increase rate limit delay
```json
{
  "rate_limit_per_minute": 30,  // Reduce from 60 to 30
  "pagination_config": {
    "delayMs": 2000  // Or increase delay to 2s
  }
}
```

### Issue 2: Pagination stops after 1 page
**Solution**: Check stop conditions
```json
{
  "pagination_config": {
    "stopOnEmpty": false,    // Don't stop on empty
    "stopOnPartial": false   // Don't stop on partial
  }
}
```

### Issue 3: Can't extract cursor from response
**Solution**: Verify cursor path
```json
{
  "pagination_config": {
    "cursorPath": "$.paging.next"  // Match API response structure
  }
}
```

### Issue 4: Pagination takes too long
**Solution**: Reduce delay or max pages
```json
{
  "pagination_config": {
    "maxPages": 10,   // Limit pages
    "delayMs": 500    // Reduce delay
  }
}
```

---

## 📚 API Response Examples

### Offset Response
```json
{
  "data": [
    {"id": 1, "name": "Item 1"},
    {"id": 2, "name": "Item 2"}
  ],
  "total": 100,
  "offset": 0,
  "limit": 10
}
```

### Cursor Response
```json
{
  "data": [
    {"id": 1, "name": "Item 1"},
    {"id": 2, "name": "Item 2"}
  ],
  "pagination": {
    "next_cursor": "eyJpZCI6Mn0=",
    "has_more": true
  }
}
```

### Link Header Response
```
HTTP/1.1 200 OK
Link: <https://api.example.com/items?page=2>; rel="next",
      <https://api.example.com/items?page=10>; rel="last"

[
  {"id": 1, "name": "Item 1"},
  {"id": 2, "name": "Item 2"}
]
```

---

## 🎯 Best Practices

1. **Start with small limits**: Test with `maxPages: 5` first
2. **Use rate limiting**: Always set `rate_limit_per_minute` for external APIs
3. **Monitor metrics**: Log `PageFetchTimes` to identify slow pages
4. **Handle max pages**: Check `MaxPagesReached` and warn users
5. **Test pagination**: Use `test-pagination.ps1` for validation
6. **Choose correct type**: Match API's pagination style
7. **Configure stop conditions**: Use `StopOnPartial` for efficiency

---

## 🔗 Related Documentation

- Full Documentation: `docs/PHASE1_DAY3_4_PAGINATION_COMPLETE.md`
- Test Script: `test-pagination.ps1`
- Implementation: `Server/Services/ApiIngestion/RestApiClient.cs`
- Models: `Server/Services/ApiIngestion/PaginationModels.cs`

---

*Quick Reference - Phase 1 Day 3-4*  
*SmartCollectAPI Pagination Feature*
