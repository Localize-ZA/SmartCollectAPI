# Simple Decision Engine Test
$baseUrl = "http://localhost:5082"

Write-Host "Testing Decision Engine API" -ForegroundColor Cyan

# Test 1: Legal Document
Write-Host "`nTest 1: Legal Contract" -ForegroundColor Yellow
$body1 = @{
    fileName = "contract.pdf"
    fileSize = 150000
    mimeType = "application/pdf"
    contentPreview = "WHEREAS the parties agree to the following terms and conditions"
} | ConvertTo-Json

try {
    $result = Invoke-RestMethod -Uri "$baseUrl/api/DecisionEngine/analyze" -Method Post -Body $body1 -ContentType "application/json"
    Write-Host "Success!" -ForegroundColor Green
    Write-Host "  Document Type: $($result.documentType)"
    Write-Host "  Chunking: $($result.chunkingStrategy) ($($result.chunkSize) chars)"
    Write-Host "  Embedding: $($result.embeddingProvider)"
    Write-Host "  Priority: $($result.priority)"
    Write-Host "  Cost: $($result.estimatedCost) units"
} catch {
    Write-Host "Failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 2: Code File
Write-Host "`nTest 2: Python Code" -ForegroundColor Yellow
$body2 = @{
    fileName = "app.py"
    fileSize = 30000
    mimeType = "text/x-python"
    contentPreview = "from fastapi import FastAPI`napp = FastAPI()"
} | ConvertTo-Json

try {
    $result = Invoke-RestMethod -Uri "$baseUrl/api/DecisionEngine/analyze" -Method Post -Body $body2 -ContentType "application/json"
    Write-Host "Success!" -ForegroundColor Green
    Write-Host "  Document Type: $($result.documentType)"
    Write-Host "  Chunking: $($result.chunkingStrategy) ($($result.chunkSize) chars)"
    Write-Host "  Embedding: $($result.embeddingProvider)"
    Write-Host "  Priority: $($result.priority)"
} catch {
    Write-Host "Failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Medical Document
Write-Host "`nTest 3: Medical Record" -ForegroundColor Yellow
$body3 = @{
    fileName = "patient_record.txt"
    fileSize = 50000
    mimeType = "text/plain"
    contentPreview = "Patient Name: John Doe. Diagnosis: Type 2 Diabetes. Prescription: Metformin 500mg"
} | ConvertTo-Json

try {
    $result = Invoke-RestMethod -Uri "$baseUrl/api/DecisionEngine/analyze" -Method Post -Body $body3 -ContentType "application/json"
    Write-Host "Success!" -ForegroundColor Green
    Write-Host "  Document Type: $($result.documentType)"
    Write-Host "  Chunking: $($result.chunkingStrategy) ($($result.chunkSize) chars)"
    Write-Host "  Priority: $($result.priority)"
    Write-Host "  NER: $($result.requiresNER)"
} catch {
    Write-Host "Failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 4: Run all test cases
Write-Host "`nTest 4: Running All Built-in Tests" -ForegroundColor Yellow
try {
    $results = Invoke-RestMethod -Uri "$baseUrl/api/DecisionEngine/run-tests" -Method Get
    Write-Host "Total tests: $($results.Count)" -ForegroundColor Green
    $successful = ($results | Where-Object { $_.success }).Count
    Write-Host "Successful: $successful / $($results.Count)" -ForegroundColor Green
} catch {
    Write-Host "Failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nAll tests complete!" -ForegroundColor Cyan
