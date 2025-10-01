# Test JSONPlaceholder API Ingestion
$baseUrl = "http://localhost:5082"

Write-Host "=== Testing JSONPlaceholder Ingestion ===" -ForegroundColor Cyan

# Step 1: Create API Source
Write-Host "`n1. Creating JSONPlaceholder API Source..." -ForegroundColor Yellow
$createBody = @{
    name = "JSONPlaceholder Posts"
    description = "Test API for posts data"
    apiType = "REST"
    endpointUrl = "https://jsonplaceholder.typicode.com/posts"
    httpMethod = "GET"
    authType = "None"
    enabled = $true
    recordsPath = "$[*]"
    fieldMappings = @{
        title = "title"
        body = "body"
    }
} | ConvertTo-Json

try {
    $createResponse = Invoke-RestMethod -Uri "$baseUrl/api/sources" -Method Post -Body $createBody -ContentType "application/json"
    $sourceId = $createResponse.id
    Write-Host "✓ Source created with ID: $sourceId" -ForegroundColor Green
    Write-Host ($createResponse | ConvertTo-Json -Depth 5)
} catch {
    Write-Host "✗ Failed to create source" -ForegroundColor Red
    Write-Host $_.Exception.Message
    Write-Host $_.Exception.Response
    exit
}

# Step 2: Test Connection
Write-Host "`n2. Testing Connection..." -ForegroundColor Yellow
try {
    $testResponse = Invoke-RestMethod -Uri "$baseUrl/api/sources/$sourceId/test-connection" -Method Post
    Write-Host "✓ Connection test result:" -ForegroundColor Green
    Write-Host ($testResponse | ConvertTo-Json -Depth 5)
} catch {
    Write-Host "✗ Connection test failed" -ForegroundColor Red
    Write-Host $_.Exception.Message
    if ($_.ErrorDetails.Message) {
        Write-Host $_.ErrorDetails.Message
    }
}

# Step 3: Trigger Ingestion
Write-Host "`n3. Triggering Ingestion..." -ForegroundColor Yellow
try {
    $triggerResponse = Invoke-RestMethod -Uri "$baseUrl/api/sources/$sourceId/trigger" -Method Post
    Write-Host "✓ Ingestion triggered:" -ForegroundColor Green
    Write-Host ($triggerResponse | ConvertTo-Json -Depth 5)
    
    if ($triggerResponse.success) {
        Write-Host "`n✓✓✓ INGESTION SUCCESSFUL ✓✓✓" -ForegroundColor Green
        Write-Host "Records processed: $($triggerResponse.recordsProcessed)"
        Write-Host "Execution time: $($triggerResponse.executionTimeMs)ms"
    } else {
        Write-Host "`n✗✗✗ INGESTION FAILED ✗✗✗" -ForegroundColor Red
        Write-Host "Error: $($triggerResponse.errorMessage)"
    }
} catch {
    Write-Host "✗ Ingestion trigger failed" -ForegroundColor Red
    Write-Host "Status Code: $($_.Exception.Response.StatusCode.value__)"
    Write-Host "Message: $($_.Exception.Message)"
    
    if ($_.ErrorDetails.Message) {
        Write-Host "`nError Details:" -ForegroundColor Red
        Write-Host $_.ErrorDetails.Message
    }
    
    # Try to get response content
    if ($_.Exception.Response) {
        $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "`nResponse Body:" -ForegroundColor Red
        Write-Host $responseBody
    }
}

# Step 4: Check Logs
Write-Host "`n4. Checking Ingestion Logs..." -ForegroundColor Yellow
try {
    $logsResponse = Invoke-RestMethod -Uri "$baseUrl/api/sources/$sourceId/logs"
    Write-Host "✓ Recent logs:" -ForegroundColor Green
    Write-Host ($logsResponse | ConvertTo-Json -Depth 5)
} catch {
    Write-Host "✗ Failed to retrieve logs" -ForegroundColor Red
    Write-Host $_.Exception.Message
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Cyan
