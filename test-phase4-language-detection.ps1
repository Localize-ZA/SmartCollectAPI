# Phase 4: Language Detection Test Script
# Tests language detection microservice and C# client integration

$baseUrl = "http://localhost:8004"
$apiUrl = "http://localhost:5082/api"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Phase 4: Language Detection Tests" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Test 1: Microservice Health Check
Write-Host "1. Testing Language Detection Service Health..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$baseUrl/health" -Method Get
    Write-Host "  Status: $($health.status)" -ForegroundColor Green
    Write-Host "  Service: $($health.service)" -ForegroundColor Green
    Write-Host "  Version: $($health.version)" -ForegroundColor Green
    Write-Host "  Languages Supported: $($health.languages_supported)" -ForegroundColor Green
} catch {
    Write-Host "  Failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  Make sure to start the language-detection service first!" -ForegroundColor Yellow
    Write-Host "  Run: cd micros\language-detection; python app.py" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Test 2: English Detection
Write-Host "2. Testing English Detection..." -ForegroundColor Yellow
try {
    $request = @{
        text = "Hello, how are you today? This is a test of the English language detection system."
        min_confidence = 0.5
    } | ConvertTo-Json

    $result = Invoke-RestMethod -Uri "$baseUrl/detect" -Method Post -Body $request -ContentType "application/json"
    
    Write-Host "  Detected: $($result.detected_language.language_name)" -ForegroundColor Green
    Write-Host "  Confidence: $([math]::Round($result.detected_language.confidence, 3))" -ForegroundColor Green
    Write-Host "  ISO 639-1: $($result.detected_language.iso_code_639_1)" -ForegroundColor Green
    Write-Host "  ISO 639-3: $($result.detected_language.iso_code_639_3)" -ForegroundColor Green
} catch {
    Write-Host "  Failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 3: Spanish Detection
Write-Host "3. Testing Spanish Detection..." -ForegroundColor Yellow
try {
    $request = @{
        text = "Hola, como estas? Esta es una prueba del sistema de deteccion de idiomas."
        min_confidence = 0.5
    } | ConvertTo-Json

    $result = Invoke-RestMethod -Uri "$baseUrl/detect" -Method Post -Body $request -ContentType "application/json"
    
    Write-Host "  Detected: $($result.detected_language.language_name)" -ForegroundColor Green
    Write-Host "  Confidence: $([math]::Round($result.detected_language.confidence, 3))" -ForegroundColor Green
} catch {
    Write-Host "  Failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 4: French Detection
Write-Host "4. Testing French Detection..." -ForegroundColor Yellow
try {
    $request = @{
        text = "Bonjour, comment allez-vous? Ceci est un test du systeme de detection de langue."
        min_confidence = 0.5
    } | ConvertTo-Json

    $result = Invoke-RestMethod -Uri "$baseUrl/detect" -Method Post -Body $request -ContentType "application/json"
    
    Write-Host "  Detected: $($result.detected_language.language_name)" -ForegroundColor Green
    Write-Host "  Confidence: $([math]::Round($result.detected_language.confidence, 3))" -ForegroundColor Green
} catch {
    Write-Host "  Failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 5: German Detection
Write-Host "5. Testing German Detection..." -ForegroundColor Yellow
try {
    $request = @{
        text = "Guten Tag, wie geht es Ihnen? Dies ist ein Test des Spracherkennungssystems."
        min_confidence = 0.5
    } | ConvertTo-Json

    $result = Invoke-RestMethod -Uri "$baseUrl/detect" -Method Post -Body $request -ContentType "application/json"
    
    Write-Host "  Detected: $($result.detected_language.language_name)" -ForegroundColor Green
    Write-Host "  Confidence: $([math]::Round($result.detected_language.confidence, 3))" -ForegroundColor Green
} catch {
    Write-Host "  Failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 6: Russian Detection
Write-Host "6. Testing Russian Detection..." -ForegroundColor Yellow
try {
    $request = @{
        text = "Привет, как дела сегодня? Это тест системы определения языка."
        min_confidence = 0.5
    } | ConvertTo-Json

    $result = Invoke-RestMethod -Uri "$baseUrl/detect" -Method Post -Body $request -ContentType "application/json"
    
    Write-Host "  Detected: $($result.detected_language.language_name)" -ForegroundColor Green
    Write-Host "  Confidence: $([math]::Round($result.detected_language.confidence, 3))" -ForegroundColor Green
} catch {
    Write-Host "  Failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 7: Italian Detection
Write-Host "7. Testing Italian Detection..." -ForegroundColor Yellow
try {
    $request = @{
        text = "Ciao, come stai oggi? Questo e un test del sistema di rilevamento della lingua."
        min_confidence = 0.5
    } | ConvertTo-Json

    $result = Invoke-RestMethod -Uri "$baseUrl/detect" -Method Post -Body $request -ContentType "application/json"
    
    Write-Host "  Detected: $($result.detected_language.language_name)" -ForegroundColor Green
    Write-Host "  Confidence: $([math]::Round($result.detected_language.confidence, 3))" -ForegroundColor Green
} catch {
    Write-Host "  Failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 8: Multiple Candidates
Write-Host "8. Testing Multiple Language Candidates..." -ForegroundColor Yellow
try {
    $request = @{
        text = "The quick brown fox jumps over the lazy dog."
        min_confidence = 0.0
    } | ConvertTo-Json

    $result = Invoke-RestMethod -Uri "$baseUrl/detect" -Method Post -Body $request -ContentType "application/json"
    
    Write-Host "  Top Candidates:" -ForegroundColor Cyan
    foreach ($candidate in $result.all_candidates) {
        Write-Host "    $($candidate.language_name): $([math]::Round($candidate.confidence, 3))" -ForegroundColor Gray
    }
} catch {
    Write-Host "  Failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 9: List Supported Languages
Write-Host "9. Testing Supported Languages Endpoint..." -ForegroundColor Yellow
try {
    $languages = Invoke-RestMethod -Uri "$baseUrl/languages" -Method Get
    Write-Host "  Total Languages: $($languages.total)" -ForegroundColor Green
    Write-Host "  Sample Languages:" -ForegroundColor Cyan
    foreach ($lang in $languages.languages | Select-Object -First 10) {
        Write-Host "    $($lang.display_name) ($($lang.iso_639_1))" -ForegroundColor Gray
    }
} catch {
    Write-Host "  Failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Phase 4 Tests Complete!" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "Summary:" -ForegroundColor White
Write-Host "  1. Service health check - TESTED" -ForegroundColor Green
Write-Host "  2. English detection - TESTED" -ForegroundColor Green
Write-Host "  3. Spanish detection - TESTED" -ForegroundColor Green
Write-Host "  4. French detection - TESTED" -ForegroundColor Green
Write-Host "  5. German detection - TESTED" -ForegroundColor Green
Write-Host "  6. Russian detection - TESTED" -ForegroundColor Green
Write-Host "  7. Italian detection - TESTED" -ForegroundColor Green
Write-Host "  8. Multiple candidates - TESTED" -ForegroundColor Green
Write-Host "  9. Supported languages - TESTED`n" -ForegroundColor Green

Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Update DecisionEngine to use LanguageDetectionService" -ForegroundColor White
Write-Host "  2. Add language-specific chunking rules" -ForegroundColor White
Write-Host "  3. Test with multilingual documents" -ForegroundColor White
Write-Host "  4. Add to docker-compose.dev.yml`n" -ForegroundColor White
