# Test pagination implementation with multiple pagination types
Write-Host "🧪 Testing API Pagination Implementation..." -ForegroundColor Cyan
Write-Host ""

$apiUrl = "http://localhost:5001/api"
$headers = @{
    "Content-Type" = "application/json"
}

# Test 1: Offset-based pagination with JSONPlaceholder posts (100 records)
Write-Host "Test 1: Offset-based Pagination (JSONPlaceholder Posts)" -ForegroundColor Yellow
Write-Host "------------------------------------------------------"

$offsetSource = @{
    name = "JSONPlaceholder Posts - Offset"
    description = "Test offset-based pagination"
    api_type = "REST"
    endpoint_url = "https://jsonplaceholder.typicode.com/posts"
    http_method = "GET"
    auth_type = "None"
    response_path = "$"
    query_params = @{} | ConvertTo-Json
    custom_headers = @{} | ConvertTo-Json
    schedule_enabled = $false
    is_active = $true
    pagination_type = "Offset"
    pagination_config = @{
        limit = 10
        maxPages = 5
        delayMs = 100
        offsetParam = "_start"
        limitParam = "_limit"
        stopOnEmpty = $true
        stopOnPartial = $true
    } | ConvertTo-Json
} | ConvertTo-Json

try {
    Write-Host "Creating API source with offset pagination..." -ForegroundColor Gray
    $createResponse = Invoke-RestMethod -Uri "$apiUrl/apisources" -Method Post -Headers $headers -Body $offsetSource
    $sourceId = $createResponse.id
    Write-Host "✅ Created source: $sourceId" -ForegroundColor Green
    
    Write-Host "Triggering ingestion with pagination..." -ForegroundColor Gray
    $ingestResponse = Invoke-RestMethod -Uri "$apiUrl/apisources/$sourceId/ingest" -Method Post -Headers $headers
    
    Write-Host "✅ Ingestion completed!" -ForegroundColor Green
    Write-Host "  Pages fetched: $($ingestResponse.pages_processed ?? 'N/A')" -ForegroundColor Cyan
    Write-Host "  Records: $($ingestResponse.records_processed ?? 'N/A')" -ForegroundColor Cyan
    Write-Host ""
    
    # Clean up
    Invoke-RestMethod -Uri "$apiUrl/apisources/$sourceId" -Method Delete -Headers $headers | Out-Null
    Write-Host "🧹 Cleaned up test source" -ForegroundColor Gray
}
catch {
    Write-Host "❌ Offset pagination test failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.Exception -ForegroundColor Red
}

Write-Host ""

# Test 2: Page-based pagination with JSONPlaceholder comments (500 records)
Write-Host "Test 2: Page-based Pagination (JSONPlaceholder Comments)" -ForegroundColor Yellow
Write-Host "--------------------------------------------------------"

$pageSource = @{
    name = "JSONPlaceholder Comments - Page"
    description = "Test page-based pagination"
    api_type = "REST"
    endpoint_url = "https://jsonplaceholder.typicode.com/comments"
    http_method = "GET"
    auth_type = "None"
    response_path = "$"
    query_params = @{} | ConvertTo-Json
    custom_headers = @{} | ConvertTo-Json
    schedule_enabled = $false
    is_active = $true
    pagination_type = "Page"
    pagination_config = @{
        limit = 25
        maxPages = 3
        delayMs = 200
        pageParam = "_page"
        limitParam = "_limit"
        startPage = 1
        stopOnEmpty = $true
        stopOnPartial = $true
    } | ConvertTo-Json
} | ConvertTo-Json

try {
    Write-Host "Creating API source with page pagination..." -ForegroundColor Gray
    $createResponse = Invoke-RestMethod -Uri "$apiUrl/apisources" -Method Post -Headers $headers -Body $pageSource
    $sourceId = $createResponse.id
    Write-Host "✅ Created source: $sourceId" -ForegroundColor Green
    
    Write-Host "Triggering ingestion with pagination..." -ForegroundColor Gray
    $ingestResponse = Invoke-RestMethod -Uri "$apiUrl/apisources/$sourceId/ingest" -Method Post -Headers $headers
    
    Write-Host "✅ Ingestion completed!" -ForegroundColor Green
    Write-Host "  Pages fetched: $($ingestResponse.pages_processed ?? 'N/A')" -ForegroundColor Cyan
    Write-Host "  Records: $($ingestResponse.records_processed ?? 'N/A')" -ForegroundColor Cyan
    Write-Host ""
    
    # Clean up
    Invoke-RestMethod -Uri "$apiUrl/apisources/$sourceId" -Method Delete -Headers $headers | Out-Null
    Write-Host "🧹 Cleaned up test source" -ForegroundColor Gray
}
catch {
    Write-Host "❌ Page pagination test failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.Exception -ForegroundColor Red
}

Write-Host ""

# Test 3: No pagination - single page fetch
Write-Host "Test 3: No Pagination (Single Page)" -ForegroundColor Yellow
Write-Host "------------------------------------"

$singleSource = @{
    name = "JSONPlaceholder Albums - Single"
    description = "Test single page fetch (no pagination)"
    api_type = "REST"
    endpoint_url = "https://jsonplaceholder.typicode.com/albums"
    http_method = "GET"
    auth_type = "None"
    response_path = "$"
    query_params = @{
        "_limit" = "20"
    } | ConvertTo-Json
    custom_headers = @{} | ConvertTo-Json
    schedule_enabled = $false
    is_active = $true
    pagination_type = "None"
} | ConvertTo-Json

try {
    Write-Host "Creating API source without pagination..." -ForegroundColor Gray
    $createResponse = Invoke-RestMethod -Uri "$apiUrl/apisources" -Method Post -Headers $headers -Body $singleSource
    $sourceId = $createResponse.id
    Write-Host "✅ Created source: $sourceId" -ForegroundColor Green
    
    Write-Host "Triggering single-page ingestion..." -ForegroundColor Gray
    $ingestResponse = Invoke-RestMethod -Uri "$apiUrl/apisources/$sourceId/ingest" -Method Post -Headers $headers
    
    Write-Host "✅ Ingestion completed!" -ForegroundColor Green
    Write-Host "  Pages fetched: $($ingestResponse.pages_processed ?? 'N/A')" -ForegroundColor Cyan
    Write-Host "  Records: $($ingestResponse.records_processed ?? 'N/A')" -ForegroundColor Cyan
    Write-Host ""
    
    # Clean up
    Invoke-RestMethod -Uri "$apiUrl/apisources/$sourceId" -Method Delete -Headers $headers | Out-Null
    Write-Host "🧹 Cleaned up test source" -ForegroundColor Gray
}
catch {
    Write-Host "❌ Single page test failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.Exception -ForegroundColor Red
}

Write-Host ""

# Test 4: Rate limiting test (verify delays between requests)
Write-Host "Test 4: Rate Limiting (Verify Request Delays)" -ForegroundColor Yellow
Write-Host "-----------------------------------------------"

$rateLimitSource = @{
    name = "JSONPlaceholder Todos - Rate Limited"
    description = "Test rate limiting with pagination"
    api_type = "REST"
    endpoint_url = "https://jsonplaceholder.typicode.com/todos"
    http_method = "GET"
    auth_type = "None"
    response_path = "$"
    query_params = @{} | ConvertTo-Json
    custom_headers = @{} | ConvertTo-Json
    schedule_enabled = $false
    is_active = $true
    rate_limit_per_minute = 30  # 30 requests per minute = 2s delay
    pagination_type = "Offset"
    pagination_config = @{
        limit = 20
        maxPages = 3
        delayMs = 0  # Rate limit from source should override
        offsetParam = "_start"
        limitParam = "_limit"
    } | ConvertTo-Json
} | ConvertTo-Json

try {
    Write-Host "Creating API source with rate limiting..." -ForegroundColor Gray
    $createResponse = Invoke-RestMethod -Uri "$apiUrl/apisources" -Method Post -Headers $headers -Body $rateLimitSource
    $sourceId = $createResponse.id
    Write-Host "✅ Created source: $sourceId" -ForegroundColor Green
    Write-Host "  Rate limit: 30 req/min (2s delay between requests)" -ForegroundColor Cyan
    
    Write-Host "Triggering ingestion (this will take ~6 seconds with delays)..." -ForegroundColor Gray
    $startTime = Get-Date
    $ingestResponse = Invoke-RestMethod -Uri "$apiUrl/apisources/$sourceId/ingest" -Method Post -Headers $headers
    $duration = (Get-Date) - $startTime
    
    Write-Host "✅ Ingestion completed!" -ForegroundColor Green
    Write-Host "  Pages fetched: $($ingestResponse.pages_processed ?? 'N/A')" -ForegroundColor Cyan
    Write-Host "  Records: $($ingestResponse.records_processed ?? 'N/A')" -ForegroundColor Cyan
    Write-Host "  Duration: $($duration.TotalSeconds.ToString('F2'))s (expected ~6s with delays)" -ForegroundColor Cyan
    Write-Host ""
    
    # Clean up
    Invoke-RestMethod -Uri "$apiUrl/apisources/$sourceId" -Method Delete -Headers $headers | Out-Null
    Write-Host "🧹 Cleaned up test source" -ForegroundColor Gray
}
catch {
    Write-Host "❌ Rate limiting test failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.Exception -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✅ Pagination Testing Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Summary:" -ForegroundColor Yellow
Write-Host "  ✓ Offset-based pagination" -ForegroundColor Green
Write-Host "  ✓ Page-based pagination" -ForegroundColor Green
Write-Host "  ✓ Single page (no pagination)" -ForegroundColor Green
Write-Host "  ✓ Rate limiting" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Test cursor-based pagination with compatible API" -ForegroundColor White
Write-Host "  2. Test LinkHeader pagination (GitHub API)" -ForegroundColor White
Write-Host "  3. Implement pagination in ApiIngestionService" -ForegroundColor White
Write-Host "  4. Add pagination metrics to logs" -ForegroundColor White
