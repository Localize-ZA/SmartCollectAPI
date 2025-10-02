# Complete System Test Script
# Tests all components of SmartCollectAPI

Write-Host "`n===================================================" -ForegroundColor Cyan
Write-Host "     SMARTCOLLECT API - COMPLETE SYSTEM TEST" -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan

$baseUrl = "http://localhost:5082"
$testsPassed = 0
$testsFailed = 0
$testsSkipped = 0

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Method = "GET",
        [string]$Url,
        [object]$Body = $null,
        [string]$ContentType = "application/json",
        [switch]$Optional
    )
    
    Write-Host "`n[$Name]" -ForegroundColor Yellow
    
    try {
        $params = @{
            Uri = $Url
            Method = $Method
            TimeoutSec = 10
        }
        
        if ($Body) {
            $params.Body = $Body
            $params.ContentType = $ContentType
        }
        
        $response = Invoke-RestMethod @params
        Write-Host "  PASS" -ForegroundColor Green
        $script:testsPassed++
        return $response
    }
    catch {
        if ($Optional) {
            Write-Host "  SKIP (Optional service not running)" -ForegroundColor Gray
            $script:testsSkipped++
        } else {
            Write-Host "  FAIL: $($_.Exception.Message)" -ForegroundColor Red
            $script:testsFailed++
        }
        return $null
    }
}

Write-Host "`n1. INFRASTRUCTURE TESTS" -ForegroundColor Magenta
Write-Host "=================================" -ForegroundColor Magenta

# Test PostgreSQL
Write-Host "`n[PostgreSQL Connection]" -ForegroundColor Yellow
try {
    $pgTest = docker exec smartcollect-postgres psql -U postgres -d smartcollect -c "SELECT COUNT(*) FROM documents;" 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  PASS - Database accessible" -ForegroundColor Green
        $testsPassed++
    } else {
        throw "Connection failed"
    }
} catch {
    Write-Host "  FAIL - Database not accessible" -ForegroundColor Red
    $testsFailed++
}

# Test Redis
Write-Host "`n[Redis Connection]" -ForegroundColor Yellow
try {
    $redisTest = docker exec smartcollect-redis redis-cli PING 2>&1
    if ($redisTest -match "PONG") {
        Write-Host "  PASS - Redis accessible" -ForegroundColor Green
        $testsPassed++
    } else {
        throw "Connection failed"
    }
} catch {
    Write-Host "  FAIL - Redis not accessible" -ForegroundColor Red
    $testsFailed++
}

Write-Host "`n2. BACKEND API TESTS" -ForegroundColor Magenta
Write-Host "=================================" -ForegroundColor Magenta

# Basic health check
$health = Test-Endpoint -Name "API Health Check" -Url "$baseUrl/health"

# Get microservices status
$microservices = Test-Endpoint -Name "Microservices Status" -Url "$baseUrl/api/microservices/health"

if ($microservices) {
    Write-Host "  Services Status:" -ForegroundColor Cyan
    $microservices.PSObject.Properties | ForEach-Object {
        $status = if ($_.Value.isHealthy) { "ONLINE" } else { "OFFLINE" }
        $color = if ($_.Value.isHealthy) { "Green" } else { "Red" }
        Write-Host "    - $($_.Name): $status" -ForegroundColor $color
    }
}

# Test providers
$providers = Test-Endpoint -Name "Get Providers" -Url "$baseUrl/api/providers"

Write-Host "`n3. PHASE 1: DECISION ENGINE" -ForegroundColor Magenta
Write-Host "=================================" -ForegroundColor Magenta

# Test decision endpoint
$decisionRequest = @{
    content = "This is a sample document for testing the decision engine capabilities."
    fileType = "txt"
    fileSize = 1024
    language = "en"
} | ConvertTo-Json

$decision = Test-Endpoint -Name "Decision Engine" -Method "POST" -Url "$baseUrl/api/decision/analyze" -Body $decisionRequest

if ($decision) {
    Write-Host "  Decision Results:" -ForegroundColor Cyan
    Write-Host "    - Embedding Provider: $($decision.recommendedEmbeddingProvider)" -ForegroundColor Gray
    Write-Host "    - NLP Provider: $($decision.recommendedNlpProvider)" -ForegroundColor Gray
    Write-Host "    - Chunking: $($decision.shouldChunk)" -ForegroundColor Gray
    Write-Host "    - Confidence: $($decision.confidence)" -ForegroundColor Gray
}

Write-Host "`n4. PHASE 2: PROVIDER FACTORY & PIPELINE" -ForegroundColor Magenta
Write-Host "=================================" -ForegroundColor Magenta

# Test document ingestion
Write-Host "`n[Document Upload & Processing]" -ForegroundColor Yellow

# Create test file
$testContent = @"
Artificial Intelligence and Machine Learning

Artificial intelligence (AI) is revolutionizing how we process and analyze documents. 
Machine learning algorithms can now understand context, extract meaningful information, 
and generate high-quality embeddings for semantic search.

Natural Language Processing (NLP) enables systems to understand human language in ways 
that were previously impossible. With modern transformer models, we can achieve 
remarkable accuracy in document classification, entity extraction, and sentiment analysis.

The future of document processing lies in the intelligent combination of multiple AI 
technologies working together to provide comprehensive understanding and actionable insights.
"@

$testFile = "test-files/system-test-document.txt"
Set-Content -Path $testFile -Value $testContent

try {
    $boundary = [System.Guid]::NewGuid().ToString()
    $LF = "`r`n"
    
    $bodyLines = (
        "--$boundary",
        "Content-Disposition: form-data; name=`"file`"; filename=`"system-test-document.txt`"",
        "Content-Type: text/plain$LF",
        $testContent,
        "--$boundary--$LF"
    ) -join $LF
    
    $response = Invoke-RestMethod -Uri "$baseUrl/api/ingest" -Method Post -ContentType "multipart/form-data; boundary=`"$boundary`"" -Body $bodyLines
    
    Write-Host "  PASS - Document uploaded" -ForegroundColor Green
    Write-Host "    Job ID: $($response.jobId)" -ForegroundColor Gray
    $testsPassed++
    
    # Wait for processing
    Write-Host "  Waiting for processing (15 seconds)..." -ForegroundColor Gray
    Start-Sleep -Seconds 15
    
} catch {
    Write-Host "  FAIL - Upload failed: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

Write-Host "`n5. PHASE 3: CHUNK SEARCH & EMBEDDINGS" -ForegroundColor Magenta
Write-Host "=================================" -ForegroundColor Magenta

# Get recent documents
$documents = Test-Endpoint -Name "Get Documents" -Url "$baseUrl/api/Documents"

if ($documents -and $documents.Count -gt 0) {
    $latestDoc = $documents[0]
    Write-Host "  Latest Document:" -ForegroundColor Cyan
    Write-Host "    - ID: $($latestDoc.id)" -ForegroundColor Gray
    Write-Host "    - Provider: $($latestDoc.embeddingProvider)" -ForegroundColor Gray
    Write-Host "    - Dimensions: $($latestDoc.embeddingDimensions)" -ForegroundColor Gray
    
    # Test chunk search
    $searchRequest = @{
        query = "artificial intelligence"
        provider = "sentence-transformers"
        limit = 5
        similarityThreshold = 0.3
    } | ConvertTo-Json
    
    $searchResults = Test-Endpoint -Name "Semantic Chunk Search" -Method "POST" -Url "$baseUrl/api/ChunkSearch/search" -Body $searchRequest
    
    if ($searchResults) {
        Write-Host "  Search Results:" -ForegroundColor Cyan
        Write-Host "    - Query: $($searchResults.query)" -ForegroundColor Gray
        Write-Host "    - Results Found: $($searchResults.resultCount)" -ForegroundColor Gray
        if ($searchResults.results) {
            Write-Host "    - Top Match Similarity: $($searchResults.results[0].similarity)" -ForegroundColor Gray
        }
    }
    
    # Get document chunks
    $chunks = Test-Endpoint -Name "Get Document Chunks" -Url "$baseUrl/api/ChunkSearch/document/$($latestDoc.id)"
    
    if ($chunks) {
        Write-Host "  Document Chunks:" -ForegroundColor Cyan
        Write-Host "    - Total Chunks: $($chunks.Count)" -ForegroundColor Gray
    }
}

Write-Host "`n6. PHASE 4: LANGUAGE DETECTION" -ForegroundColor Magenta
Write-Host "=================================" -ForegroundColor Magenta

# Test language detection service (optional)
$langRequest = @{
    text = "Hello, how are you today?"
    min_confidence = 0.5
} | ConvertTo-Json

$langResult = Test-Endpoint -Name "Language Detection" -Method "POST" -Url "http://localhost:8004/detect" -Body $langRequest -Optional

if ($langResult) {
    Write-Host "  Detection Results:" -ForegroundColor Cyan
    Write-Host "    - Language: $($langResult.detected_language.language_name)" -ForegroundColor Gray
    Write-Host "    - Confidence: $([math]::Round($langResult.detected_language.confidence * 100, 1))%" -ForegroundColor Gray
}

# Test supported languages
$languages = Test-Endpoint -Name "Supported Languages" -Url "http://localhost:8004/languages" -Optional

if ($languages) {
    Write-Host "  Total Languages Supported: $($languages.Count)" -ForegroundColor Cyan
}

Write-Host "`n7. API SOURCES (API INGESTION)" -ForegroundColor Magenta
Write-Host "=================================" -ForegroundColor Magenta

# Get API sources
$apiSources = Test-Endpoint -Name "Get API Sources" -Url "$baseUrl/api/ApiSources"

# Test JSONPlaceholder API source
$jsonPlaceholderSource = @{
    name = "JSONPlaceholder Test"
    baseUrl = "https://jsonplaceholder.typicode.com"
    description = "Test API source"
    authenticationType = "none"
    isActive = $true
    endpoints = @(
        @{
            path = "/posts"
            method = "GET"
            isActive = $true
            dataPath = "$"
            transformationRules = @(
                @{
                    sourceField = "title"
                    targetField = "title"
                    transformationType = "direct"
                }
                @{
                    sourceField = "body"
                    targetField = "content"
                    transformationType = "direct"
                }
            )
        }
    )
} | ConvertTo-Json -Depth 10

Write-Host "`n[Create Test API Source]" -ForegroundColor Yellow
try {
    $newSource = Invoke-RestMethod -Uri "$baseUrl/api/ApiSources" -Method Post -Body $jsonPlaceholderSource -ContentType "application/json"
    Write-Host "  PASS - API Source created: $($newSource.id)" -ForegroundColor Green
    $testsPassed++
    
    # Test ingestion from the source
    Start-Sleep -Seconds 2
    
    Write-Host "`n[Ingest from API Source]" -ForegroundColor Yellow
    try {
        $ingestResult = Invoke-RestMethod -Uri "$baseUrl/api/ApiSources/$($newSource.id)/ingest" -Method Post
        Write-Host "  PASS - Ingestion triggered: $($ingestResult.jobId)" -ForegroundColor Green
        $testsPassed++
        
        Write-Host "  Waiting for API ingestion (10 seconds)..." -ForegroundColor Gray
        Start-Sleep -Seconds 10
        
    } catch {
        Write-Host "  FAIL - Ingestion failed: $($_.Exception.Message)" -ForegroundColor Red
        $testsFailed++
    }
    
} catch {
    Write-Host "  FAIL - Could not create API source: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

Write-Host "`n8. FRONTEND ACCESSIBILITY" -ForegroundColor Magenta
Write-Host "=================================" -ForegroundColor Magenta

# Check if frontend is running
Write-Host "`n[Frontend Server]" -ForegroundColor Yellow
try {
    $frontendResponse = Invoke-WebRequest -Uri "http://localhost:3000" -Method Head -TimeoutSec 5
    if ($frontendResponse.StatusCode -eq 200) {
        Write-Host "  PASS - Frontend accessible on port 3000" -ForegroundColor Green
        $testsPassed++
    }
} catch {
    Write-Host "  FAIL - Frontend not accessible" -ForegroundColor Red
    Write-Host "  Run: cd client; npm run dev" -ForegroundColor Yellow
    $testsFailed++
}

Write-Host "`n===================================================" -ForegroundColor Cyan
Write-Host "                  TEST SUMMARY" -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan

$total = $testsPassed + $testsFailed + $testsSkipped
$passRate = if ($total -gt 0) { [math]::Round(($testsPassed / $total) * 100, 1) } else { 0 }

Write-Host "`nTotal Tests: $total" -ForegroundColor White
Write-Host "Passed:  $testsPassed" -ForegroundColor Green
Write-Host "Failed:  $testsFailed" -ForegroundColor Red
Write-Host "Skipped: $testsSkipped" -ForegroundColor Gray
Write-Host "Pass Rate: $passRate%" -ForegroundColor $(if ($passRate -ge 80) { "Green" } elseif ($passRate -ge 60) { "Yellow" } else { "Red" })

if ($testsFailed -eq 0 -and $testsPassed -gt 0) {
    Write-Host "`nSYSTEM STATUS: OPERATIONAL" -ForegroundColor Green -BackgroundColor Black
} elseif ($testsFailed -lt 3) {
    Write-Host "`nSYSTEM STATUS: PARTIALLY OPERATIONAL" -ForegroundColor Yellow -BackgroundColor Black
} else {
    Write-Host "`nSYSTEM STATUS: ISSUES DETECTED" -ForegroundColor Red -BackgroundColor Black
}

Write-Host "`n===================================================" -ForegroundColor Cyan
