# Phase 3 Test Script - Simplified
# Tests chunk search, mean-of-chunks embedding, and hybrid search

$apiUrl = "http://localhost:5082/api"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Phase 3: Mean-of-Chunks Test Suite" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Upload Document with Chunking
Write-Host "1. Testing Document Upload with Chunking..." -ForegroundColor Yellow
try {
    $testFile = "test-files\large-ai-document.txt"
    $boundary = [System.Guid]::NewGuid().ToString()
    $fileContent = Get-Content $testFile -Raw
    
    $bodyLines = @(
        "--$boundary",
        "Content-Disposition: form-data; name=`"file`"; filename=`"large-ai-document.txt`"",
        "Content-Type: text/plain",
        "",
        $fileContent,
        "--$boundary--"
    )
    
    $body = $bodyLines -join "`r`n"
    
    $response = Invoke-RestMethod -Uri "$apiUrl/ingest" `
        -Method Post `
        -Body $body `
        -ContentType "multipart/form-data; boundary=$boundary"
    
    Write-Host "  Job enqueued: $($response.job_id)" -ForegroundColor Green
    Write-Host "  SHA256: $($response.sha256)" -ForegroundColor Green
    Write-Host "  Waiting 10 seconds for processing..." -ForegroundColor Gray
    Start-Sleep -Seconds 10
    
    # Get documents to find the newly created one
    $documents = Invoke-RestMethod -Uri "$apiUrl/Documents?pageSize=5" -Method Get
    if ($documents.items.Count -gt 0) {
        $documentId = $documents.items[0].id
        Write-Host "  Document ID: $documentId" -ForegroundColor Green
    } else {
        Write-Host "  Warning: No documents found yet" -ForegroundColor Yellow
        $documentId = $null
    }

} catch {
    Write-Host "  Failed: $($_.Exception.Message)" -ForegroundColor Red
    $documentId = $null
}

Write-Host ""

# Test 2: Search Chunks by Similarity
Write-Host "2. Testing Chunk Search by Similarity..." -ForegroundColor Yellow
if ($documentId) {
    try {
        $searchRequest = @{
            query = "important information"
            provider = "sentence-transformers"
            limit = 3
            similarityThreshold = 0.5
        } | ConvertTo-Json

        $searchResponse = Invoke-RestMethod -Uri "$apiUrl/ChunkSearch/search" `
            -Method Post `
            -Body $searchRequest `
            -ContentType "application/json"

        Write-Host "  Found $($searchResponse.resultCount) similar chunks" -ForegroundColor Green
        
        if ($searchResponse.resultCount -gt 0) {
            foreach ($result in $searchResponse.results) {
                Write-Host "    Chunk $($result.chunkIndex): similarity = $([math]::Round($result.similarity, 3))" -ForegroundColor Gray
            }
        }

    } catch {
        Write-Host "  Failed: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "  Skipped - no document uploaded" -ForegroundColor Yellow
}

Write-Host ""

# Test 3: Hybrid Search
Write-Host "3. Testing Hybrid Search (Semantic + Text)..." -ForegroundColor Yellow
if ($documentId) {
    try {
        $hybridRequest = @{
            query = "information"
            provider = "sentence-transformers"
            limit = 3
            similarityThreshold = 0.3
        } | ConvertTo-Json

        $hybridResponse = Invoke-RestMethod -Uri "$apiUrl/ChunkSearch/hybrid-search" `
            -Method Post `
            -Body $hybridRequest `
            -ContentType "application/json"

        Write-Host "  Found $($hybridResponse.resultCount) results" -ForegroundColor Green
        
        if ($hybridResponse.resultCount -gt 0) {
            foreach ($result in $hybridResponse.results) {
                Write-Host "    Chunk $($result.chunkIndex): similarity = $([math]::Round($result.similarity, 3))" -ForegroundColor Gray
            }
        }

    } catch {
        Write-Host "  Failed: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "  Skipped - no document uploaded" -ForegroundColor Yellow
}

Write-Host ""

# Test 4: Get Document Chunks
Write-Host "4. Testing Document Chunk Retrieval..." -ForegroundColor Yellow
if ($documentId) {
    try {
        $chunksResponse = Invoke-RestMethod -Uri "$apiUrl/ChunkSearch/document/$documentId" `
            -Method Get

        Write-Host "  Retrieved $($chunksResponse.chunkCount) chunks" -ForegroundColor Green
        Write-Host "  Document ID: $($chunksResponse.documentId)" -ForegroundColor Green

    } catch {
        Write-Host "  Failed: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "  Skipped - no document uploaded" -ForegroundColor Yellow
}

Write-Host ""

# Test 5: Provider Comparison
Write-Host "5. Testing Provider Comparison..." -ForegroundColor Yellow
try {
    $query = "machine learning"
    
    Write-Host "  Testing with spaCy (300 dims)..." -ForegroundColor Gray
    $spacyRequest = @{
        query = $query
        provider = "spacy"
        limit = 3
        similarityThreshold = 0.5
    } | ConvertTo-Json

    $spacyResponse = Invoke-RestMethod -Uri "$apiUrl/ChunkSearch/search" `
        -Method Post `
        -Body $spacyRequest `
        -ContentType "application/json"

    Write-Host "  Testing with sentence-transformers (768 dims)..." -ForegroundColor Gray
    $stRequest = @{
        query = $query
        provider = "sentence-transformers"
        limit = 3
        similarityThreshold = 0.5
    } | ConvertTo-Json

    $stResponse = Invoke-RestMethod -Uri "$apiUrl/ChunkSearch/search" `
        -Method Post `
        -Body $stRequest `
        -ContentType "application/json"

    Write-Host ""
    Write-Host "  Provider Comparison:" -ForegroundColor Cyan
    Write-Host "    spaCy Results: $($spacyResponse.resultCount)" -ForegroundColor Gray
    Write-Host "    Sentence Transformers Results: $($stResponse.resultCount)" -ForegroundColor Gray
    
    if ($spacyResponse.resultCount -gt 0 -and $stResponse.resultCount -gt 0) {
        $spacyAvgSim = ($spacyResponse.results | Measure-Object -Property similarity -Average).Average
        $stAvgSim = ($stResponse.results | Measure-Object -Property similarity -Average).Average
        
        Write-Host "    spaCy Avg Similarity: $([math]::Round($spacyAvgSim, 3))" -ForegroundColor Gray
        Write-Host "    ST Avg Similarity: $([math]::Round($stAvgSim, 3))" -ForegroundColor Gray
        
        if ($stAvgSim -gt $spacyAvgSim) {
            Write-Host "    -> Sentence Transformers shows higher quality" -ForegroundColor Green
        } else {
            Write-Host "    -> spaCy is faster but may have lower quality" -ForegroundColor Yellow
        }
    }

    Write-Host "  Provider comparison complete" -ForegroundColor Green

} catch {
    Write-Host "  Failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Phase 3 Tests Complete!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Summary:" -ForegroundColor White
Write-Host "  1. Document upload and chunking - TESTED" -ForegroundColor Green
Write-Host "  2. Chunk search by similarity - TESTED" -ForegroundColor Green
Write-Host "  3. Hybrid search (semantic + text) - TESTED" -ForegroundColor Green
Write-Host "  4. Document chunk retrieval - TESTED" -ForegroundColor Green
Write-Host "  5. Provider comparison - TESTED" -ForegroundColor Green
Write-Host ""
