# Phase 1 Days 6-7: GraphQL Client Integration Test
# Tests GraphQL query execution and cursor-based pagination

$baseUrl = "https://localhost:5001"
$apiUrl = "$baseUrl/api"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "🧪 Testing Days 6-7: GraphQL Client" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Test GraphQL API endpoint (using GitHub GraphQL API as example)
# NOTE: You'll need a GitHub personal access token for this to work
$githubToken = $env:GITHUB_TOKEN
if ([string]::IsNullOrEmpty($githubToken)) {
    Write-Host "⚠️  GITHUB_TOKEN environment variable not set" -ForegroundColor Yellow
    Write-Host "   Set it with: `$env:GITHUB_TOKEN = 'your_token_here'" -ForegroundColor Yellow
    Write-Host "   Or create one at: https://github.com/settings/tokens" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "   For this test, using a public GraphQL API instead..." -ForegroundColor Yellow
    Write-Host ""
}

# ===================================================================
# Test 1: Create GraphQL API Source with Simple Query
# ===================================================================

Write-Host "Test 1: Create GraphQL API Source (SpaceX API)" -ForegroundColor Cyan
Write-Host "---------------------------------------------------" -ForegroundColor Cyan

$sourcePayload1 = @{
    name = "SpaceX Launches - GraphQL"
    description = "Fetches SpaceX launch data via GraphQL API"
    apiType = "GraphQL"
    endpointUrl = "https://spacex-production.up.railway.app/"
    httpMethod = "POST"
    authType = "None"
    graphQLQuery = @"
query GetLaunches {
  launches(limit: 5) {
    id
    mission_name
    launch_date_utc
    rocket {
      rocket_name
      rocket_type
    }
    launch_site {
      site_name
    }
    launch_success
  }
}
"@
    responsePath = "launches"
    paginationType = "None"
    enabled = $true
} | ConvertTo-Json -Depth 10

try {
    $response1 = Invoke-RestMethod -Uri "$apiUrl/apisources" `
        -Method Post `
        -Body $sourcePayload1 `
        -ContentType "application/json" `
        -SkipCertificateCheck

    $sourceId1 = $response1.id
    Write-Host "✅ Created GraphQL source: $sourceId1" -ForegroundColor Green
    Write-Host "  Name: $($response1.name)" -ForegroundColor Gray
    Write-Host "  API Type: $($response1.apiType)" -ForegroundColor Gray
    Write-Host "  Endpoint: $($response1.endpointUrl)" -ForegroundColor Gray
    Write-Host ""
}
catch {
    Write-Host "❌ Failed to create GraphQL source" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

# ===================================================================
# Test 2: Test GraphQL Connection
# ===================================================================

Write-Host "Test 2: Test GraphQL Connection" -ForegroundColor Cyan
Write-Host "---------------------------------------------------" -ForegroundColor Cyan

try {
    $testResponse = Invoke-RestMethod -Uri "$apiUrl/apisources/$sourceId1/test" `
        -Method Post `
        -SkipCertificateCheck

    if ($testResponse.success -eq $true) {
        Write-Host "✅ GraphQL connection test passed!" -ForegroundColor Green
    }
    else {
        Write-Host "⚠️  GraphQL connection test failed" -ForegroundColor Yellow
        Write-Host "  Error: $($testResponse.message)" -ForegroundColor Yellow
    }
    Write-Host ""
}
catch {
    Write-Host "❌ Failed to test GraphQL connection" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}

# ===================================================================
# Test 3: Execute GraphQL Ingestion
# ===================================================================

Write-Host "Test 3: Execute GraphQL Ingestion" -ForegroundColor Cyan
Write-Host "---------------------------------------------------" -ForegroundColor Cyan

try {
    $ingestResponse = Invoke-RestMethod -Uri "$apiUrl/apisources/$sourceId1/ingest" `
        -Method Post `
        -SkipCertificateCheck

    Write-Host "✅ GraphQL ingestion completed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📊 Ingestion Results:" -ForegroundColor Cyan
    Write-Host "  Success: $($ingestResponse.success)" -ForegroundColor Gray
    Write-Host "  Log ID: $($ingestResponse.logId)" -ForegroundColor Gray
    Write-Host "  Records Fetched: $($ingestResponse.recordsFetched)" -ForegroundColor Gray
    Write-Host "  Documents Created: $($ingestResponse.documentsCreated)" -ForegroundColor Gray
    Write-Host "  Documents Failed: $($ingestResponse.documentsFailed)" -ForegroundColor Gray
    Write-Host "  Execution Time: $($ingestResponse.executionTimeMs)ms" -ForegroundColor Gray
    Write-Host "  Pages Processed: $($ingestResponse.pagesProcessed)" -ForegroundColor Gray
    
    if ($ingestResponse.warnings -and $ingestResponse.warnings.Count -gt 0) {
        Write-Host ""
        Write-Host "⚠️  Warnings:" -ForegroundColor Yellow
        foreach ($warning in $ingestResponse.warnings) {
            Write-Host "  - $warning" -ForegroundColor Yellow
        }
    }
    Write-Host ""

    $logId1 = $ingestResponse.logId
}
catch {
    Write-Host "❌ Failed to execute GraphQL ingestion" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}

# ===================================================================
# Test 4: Verify Ingestion Log
# ===================================================================

if ($logId1) {
    Write-Host "Test 4: Verify GraphQL Ingestion Log" -ForegroundColor Cyan
    Write-Host "---------------------------------------------------" -ForegroundColor Cyan

    try {
        $logResponse = Invoke-RestMethod -Uri "$apiUrl/api-ingestion-logs/$logId1" `
            -Method Get `
            -SkipCertificateCheck

        Write-Host "✅ Retrieved ingestion log!" -ForegroundColor Green
        Write-Host ""
        Write-Host "📋 Log Details:" -ForegroundColor Cyan
        Write-Host "  Log ID: $($logResponse.id)" -ForegroundColor Gray
        Write-Host "  Status: $($logResponse.status)" -ForegroundColor Gray
        Write-Host "  Started: $($logResponse.startedAt)" -ForegroundColor Gray
        Write-Host "  Completed: $($logResponse.completedAt)" -ForegroundColor Gray
        Write-Host "  Records Fetched: $($logResponse.recordsFetched)" -ForegroundColor Gray
        Write-Host "  Documents Created: $($logResponse.documentsCreated)" -ForegroundColor Gray
        Write-Host "  HTTP Status: $($logResponse.httpStatusCode)" -ForegroundColor Gray
        Write-Host "  Response Size: $($logResponse.responseSizeBytes) bytes" -ForegroundColor Gray
        Write-Host ""
    }
    catch {
        Write-Host "❌ Failed to retrieve log" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
    }
}

# ===================================================================
# Test 5: Create GraphQL Source with Cursor Pagination (GitHub API)
# ===================================================================

if (-not [string]::IsNullOrEmpty($githubToken)) {
    Write-Host "Test 5: Create GraphQL Source with Cursor Pagination (GitHub)" -ForegroundColor Cyan
    Write-Host "---------------------------------------------------" -ForegroundColor Cyan

    $sourcePayload2 = @{
        name = "GitHub Repositories - GraphQL Pagination"
        description = "Fetches GitHub repositories with cursor-based pagination"
        apiType = "GraphQL"
        endpointUrl = "https://api.github.com/graphql"
        httpMethod = "POST"
        authType = "Bearer"
        customHeaders = @{
            "User-Agent" = "SmartCollectAPI-Test"
        } | ConvertTo-Json
        graphQLQuery = @"
query GetRepositories(`$first: Int!, `$after: String) {
  search(query: "language:C# stars:>1000", type: REPOSITORY, first: `$first, after: `$after) {
    repositoryCount
    pageInfo {
      hasNextPage
      endCursor
    }
    edges {
      node {
        ... on Repository {
          id
          name
          owner {
            login
          }
          description
          stargazerCount
          url
        }
      }
    }
  }
}
"@
        graphQLVariables = @{
            first = 10
        } | ConvertTo-Json
        responsePath = "search.edges"
        paginationType = "Cursor"
        paginationConfig = @{
            limit = 10
            maxPages = 3
            delayMs = 2000
            pageInfoPath = "search.pageInfo"
            firstParam = "first"
            afterParam = "after"
        } | ConvertTo-Json
        enabled = $true
    } | ConvertTo-Json -Depth 10

    # Add Bearer token to auth config
    $authConfig = @{
        token = $githubToken
    } | ConvertTo-Json

    $sourcePayload2Obj = $sourcePayload2 | ConvertFrom-Json
    $sourcePayload2Obj | Add-Member -NotePropertyName "authConfigEncrypted" -NotePropertyValue $authConfig
    $sourcePayload2 = $sourcePayload2Obj | ConvertTo-Json -Depth 10

    try {
        $response2 = Invoke-RestMethod -Uri "$apiUrl/apisources" `
            -Method Post `
            -Body $sourcePayload2 `
            -ContentType "application/json" `
            -SkipCertificateCheck

        $sourceId2 = $response2.id
        Write-Host "✅ Created paginated GraphQL source: $sourceId2" -ForegroundColor Green
        Write-Host "  Name: $($response2.name)" -ForegroundColor Gray
        Write-Host "  Pagination Type: $($response2.paginationType)" -ForegroundColor Gray
        Write-Host ""

        # Execute ingestion with pagination
        Write-Host "Test 6: Execute GraphQL Cursor Pagination" -ForegroundColor Cyan
        Write-Host "---------------------------------------------------" -ForegroundColor Cyan

        $ingestResponse2 = Invoke-RestMethod -Uri "$apiUrl/apisources/$sourceId2/ingest" `
            -Method Post `
            -SkipCertificateCheck

        Write-Host "✅ Paginated GraphQL ingestion completed!" -ForegroundColor Green
        Write-Host ""
        Write-Host "📊 Pagination Results:" -ForegroundColor Cyan
        Write-Host "  Success: $($ingestResponse2.success)" -ForegroundColor Gray
        Write-Host "  Pages Processed: $($ingestResponse2.pagesProcessed)" -ForegroundColor Gray
        Write-Host "  Total Records: $($ingestResponse2.totalRecords)" -ForegroundColor Gray
        Write-Host "  Records Fetched: $($ingestResponse2.recordsFetched)" -ForegroundColor Gray
        Write-Host "  Documents Created: $($ingestResponse2.documentsCreated)" -ForegroundColor Gray
        Write-Host "  Pagination Time: $($ingestResponse2.paginationTimeMs)ms" -ForegroundColor Gray
        Write-Host "  Total Execution Time: $($ingestResponse2.executionTimeMs)ms" -ForegroundColor Gray
        Write-Host "  Max Pages Reached: $($ingestResponse2.maxPagesReached)" -ForegroundColor Gray
        Write-Host ""

        # Cleanup
        Write-Host "🧹 Cleaning up paginated source..." -ForegroundColor Gray
        Invoke-RestMethod -Uri "$apiUrl/apisources/$sourceId2" `
            -Method Delete `
            -SkipCertificateCheck | Out-Null
    }
    catch {
        Write-Host "❌ Failed with GitHub API" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
    }
}
else {
    Write-Host "Test 5: Skipped (GitHub pagination test - no token)" -ForegroundColor Yellow
    Write-Host "---------------------------------------------------" -ForegroundColor Yellow
    Write-Host "  Set GITHUB_TOKEN to test cursor pagination" -ForegroundColor Gray
    Write-Host ""
}

# ===================================================================
# Test 6: Create GraphQL Source with Variables
# ===================================================================

Write-Host "Test 6: GraphQL with Query Variables" -ForegroundColor Cyan
Write-Host "---------------------------------------------------" -ForegroundColor Cyan

$sourcePayload3 = @{
    name = "SpaceX Launches - With Variables"
    description = "GraphQL query using variables for limit"
    apiType = "GraphQL"
    endpointUrl = "https://spacex-production.up.railway.app/"
    httpMethod = "POST"
    authType = "None"
    graphQLQuery = @"
query GetLaunches(`$limit: Int!) {
  launches(limit: `$limit) {
    id
    mission_name
    launch_year
    launch_success
  }
}
"@
    graphQLVariables = @{
        limit = 10
    } | ConvertTo-Json
    responsePath = "launches"
    paginationType = "None"
    enabled = $true
} | ConvertTo-Json -Depth 10

try {
    $response3 = Invoke-RestMethod -Uri "$apiUrl/apisources" `
        -Method Post `
        -Body $sourcePayload3 `
        -ContentType "application/json" `
        -SkipCertificateCheck

    $sourceId3 = $response3.id
    Write-Host "✅ Created GraphQL source with variables: $sourceId3" -ForegroundColor Green

    # Execute ingestion
    $ingestResponse3 = Invoke-RestMethod -Uri "$apiUrl/apisources/$sourceId3/ingest" `
        -Method Post `
        -SkipCertificateCheck

    Write-Host "✅ Ingestion completed!" -ForegroundColor Green
    Write-Host "  Records: $($ingestResponse3.recordsFetched)" -ForegroundColor Gray
    Write-Host "  Documents: $($ingestResponse3.documentsCreated)" -ForegroundColor Gray
    Write-Host ""

    # Cleanup
    Write-Host "🧹 Cleaning up test source..." -ForegroundColor Gray
    Invoke-RestMethod -Uri "$apiUrl/apisources/$sourceId3" `
        -Method Delete `
        -SkipCertificateCheck | Out-Null
}
catch {
    Write-Host "❌ Failed GraphQL variables test" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}

# ===================================================================
# Cleanup Test 1 Source
# ===================================================================

Write-Host "🧹 Cleaning up main test source..." -ForegroundColor Gray
try {
    Invoke-RestMethod -Uri "$apiUrl/apisources/$sourceId1" `
        -Method Delete `
        -SkipCertificateCheck | Out-Null
    Write-Host "✅ Cleaned up source: $sourceId1" -ForegroundColor Green
}
catch {
    Write-Host "⚠️  Failed to cleanup source" -ForegroundColor Yellow
}

# ===================================================================
# Summary
# ===================================================================

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✅ Days 6-7 GraphQL Testing Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "📝 Summary:" -ForegroundColor Cyan
Write-Host "  - GraphQL client implementation" -ForegroundColor Gray
Write-Host "  - Simple query execution" -ForegroundColor Gray
Write-Host "  - Query variables support" -ForegroundColor Gray
Write-Host "  - Cursor-based pagination (Relay spec)" -ForegroundColor Gray
Write-Host "  - Response path extraction" -ForegroundColor Gray
Write-Host "  - Error handling" -ForegroundColor Gray
Write-Host ""
Write-Host "🎯 Next Steps:" -ForegroundColor Cyan
Write-Host "  - Days 8-9: Webhook Receiver" -ForegroundColor Gray
Write-Host "  - Day 10: Incremental Sync" -ForegroundColor Gray
Write-Host ""
