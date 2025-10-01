# Test Provider Factory
$baseUrl = "http://localhost:5082"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Provider Factory Test Suite" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Test 1: Get available providers
Write-Host "`n1. Getting Available Providers..." -ForegroundColor Yellow
try {
    $providers = Invoke-RestMethod -Uri "$baseUrl/api/DecisionEngine/providers" -Method Get
    Write-Host "Success!" -ForegroundColor Green
    Write-Host "  Default Provider: $($providers.defaultProvider)" -ForegroundColor Cyan
    Write-Host "  Available Providers:" -ForegroundColor Cyan
    foreach ($provider in $providers.providers) {
        if ($provider.available) {
            Write-Host "    - $($provider.key): $($provider.dimensions) dimensions, $($provider.maxTokens) max tokens" -ForegroundColor Green
        } else {
            Write-Host "    - $($provider.key): UNAVAILABLE ($($provider.error))" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "Failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 2: Test sentence-transformers provider
Write-Host "`n2. Testing sentence-transformers Provider..." -ForegroundColor Yellow
$body1 = @{
    providerKey = "sentence-transformers"
    text = "Machine learning is a subset of artificial intelligence."
} | ConvertTo-Json

try {
    $result = Invoke-RestMethod -Uri "$baseUrl/api/DecisionEngine/test-provider" -Method Post -Body $body1 -ContentType "application/json"
    Write-Host "Success!" -ForegroundColor Green
    Write-Host "  Provider: $($result.providerKey)" -ForegroundColor Cyan
    Write-Host "  Dimensions: $($result.dimensions)" -ForegroundColor Cyan
    Write-Host "  Execution Time: $($result.executionTimeMs)ms" -ForegroundColor Cyan
    if ($result.sampleValues) {
        Write-Host "  Sample Values: $($result.sampleValues -join ', ')" -ForegroundColor Gray
    }
} catch {
    Write-Host "Failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host $_.ErrorDetails.Message -ForegroundColor Red
    }
}

# Test 3: Test spaCy provider
Write-Host "`n3. Testing spaCy Provider..." -ForegroundColor Yellow
$body2 = @{
    providerKey = "spacy"
    text = "The patient was diagnosed with Type 2 Diabetes in New York."
} | ConvertTo-Json

try {
    $result = Invoke-RestMethod -Uri "$baseUrl/api/DecisionEngine/test-provider" -Method Post -Body $body2 -ContentType "application/json"
    Write-Host "Success!" -ForegroundColor Green
    Write-Host "  Provider: $($result.providerKey)" -ForegroundColor Cyan
    Write-Host "  Dimensions: $($result.dimensions)" -ForegroundColor Cyan
    Write-Host "  Execution Time: $($result.executionTimeMs)ms" -ForegroundColor Cyan
    if ($result.sampleValues) {
        Write-Host "  Sample Values: $($result.sampleValues -join ', ')" -ForegroundColor Gray
    }
} catch {
    Write-Host "Failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host $_.ErrorDetails.Message -ForegroundColor Red
    }
}

# Test 4: Test invalid provider
Write-Host "`n4. Testing Invalid Provider (should fail gracefully)..." -ForegroundColor Yellow
$body3 = @{
    providerKey = "nonexistent-provider"
    text = "This should fail."
} | ConvertTo-Json

try {
    $result = Invoke-RestMethod -Uri "$baseUrl/api/DecisionEngine/test-provider" -Method Post -Body $body3 -ContentType "application/json"
    Write-Host "Unexpected success!" -ForegroundColor Yellow
} catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 400) {
        Write-Host "Failed as expected with 400 Bad Request" -ForegroundColor Green
        if ($_.ErrorDetails.Message) {
            $error = $_.ErrorDetails.Message | ConvertFrom-Json
            Write-Host "  Error: $($error.error)" -ForegroundColor Cyan
            Write-Host "  Available: $($error.availableProviders -join ', ')" -ForegroundColor Cyan
        }
    } else {
        Write-Host "Failed with unexpected error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Test 5: Compare all providers
Write-Host "`n5. Comparing All Providers..." -ForegroundColor Yellow
$compareBody = @{
    text = "Natural language processing enables computers to understand human language."
} | ConvertTo-Json

try {
    $results = Invoke-RestMethod -Uri "$baseUrl/api/DecisionEngine/compare-providers" -Method Post -Body $compareBody -ContentType "application/json"
    Write-Host "Success! Comparison results:" -ForegroundColor Green
    
    $table = @()
    foreach ($result in $results) {
        $table += [PSCustomObject]@{
            Provider = $result.providerKey
            Success = if ($result.success) { "✓" } else { "✗" }
            Dimensions = $result.dimensions
            TimeMs = $result.executionTimeMs
            Error = if ($result.errorMessage) { $result.errorMessage } else { "-" }
        }
    }
    
    $table | Format-Table -AutoSize
    
    # Show which is fastest
    $fastest = $results | Where-Object { $_.success } | Sort-Object executionTimeMs | Select-Object -First 1
    if ($fastest) {
        Write-Host "  Fastest: $($fastest.providerKey) ($($fastest.executionTimeMs)ms)" -ForegroundColor Green
    }
    
    # Show which has highest dimensions
    $highestDim = $results | Where-Object { $_.success } | Sort-Object dimensions -Descending | Select-Object -First 1
    if ($highestDim) {
        Write-Host "  Highest Quality: $($highestDim.providerKey) ($($highestDim.dimensions) dimensions)" -ForegroundColor Green
    }
    
} catch {
    Write-Host "Failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 6: Integration test - Generate plan and use recommended provider
Write-Host "`n6. Integration Test: Plan → Provider..." -ForegroundColor Yellow
$planBody = @{
    fileName = "research_paper.pdf"
    fileSize = 500000
    mimeType = "application/pdf"
    contentPreview = "Abstract: This paper presents a novel approach to deep learning."
} | ConvertTo-Json

try {
    # Step 1: Generate plan
    $plan = Invoke-RestMethod -Uri "$baseUrl/api/DecisionEngine/analyze" -Method Post -Body $planBody -ContentType "application/json"
    Write-Host "  Plan generated: $($plan.embeddingProvider) provider recommended" -ForegroundColor Cyan
    
    # Step 2: Use recommended provider
    $testBody = @{
        providerKey = $plan.embeddingProvider
        text = "Deep learning models use neural networks."
    } | ConvertTo-Json
    
    $embeddingResult = Invoke-RestMethod -Uri "$baseUrl/api/DecisionEngine/test-provider" -Method Post -Body $testBody -ContentType "application/json"
    Write-Host "  ✓ Successfully used recommended provider!" -ForegroundColor Green
    Write-Host "    Provider: $($embeddingResult.providerKey)" -ForegroundColor Cyan
    Write-Host "    Dimensions: $($embeddingResult.dimensions)" -ForegroundColor Cyan
    Write-Host "    Time: $($embeddingResult.executionTimeMs)ms" -ForegroundColor Cyan
    
} catch {
    Write-Host "Failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  All Tests Complete!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
