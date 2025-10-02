# System Startup Script
# Ensures all components are running

Write-Host "`n===================================================" -ForegroundColor Cyan
Write-Host "     SMARTCOLLECT API - SYSTEM STARTUP" -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan

$projectRoot = "c:\Users\colin\dev\OperationLocal\SmartCollectAPI"

Write-Host "`n1. Checking Docker Containers..." -ForegroundColor Yellow

# Check PostgreSQL
Write-Host "  PostgreSQL..." -NoNewline
$pgStatus = docker ps --filter "name=smartcollect-postgres" --format "{{.Status}}"
if ($pgStatus -match "Up") {
    Write-Host " RUNNING" -ForegroundColor Green
} else {
    Write-Host " STARTING..." -ForegroundColor Yellow
    docker start smartcollect-postgres | Out-Null
    Start-Sleep -Seconds 3
}

# Check Redis
Write-Host "  Redis..." -NoNewline
$redisStatus = docker ps --filter "name=smartcollect-redis" --format "{{.Status}}"
if ($redisStatus -match "Up") {
    Write-Host " RUNNING" -ForegroundColor Green
} else {
    Write-Host " STARTING..." -ForegroundColor Yellow
    docker start smartcollect-redis | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "`n2. Checking Backend API..." -ForegroundColor Yellow

# Check if backend is running
$backendProcess = Get-Process | Where-Object { $_.ProcessName -eq "SmartCollectAPI" }
if ($backendProcess) {
    Write-Host "  Backend API is RUNNING (PID: $($backendProcess.Id))" -ForegroundColor Green
} else {
    Write-Host "  Backend API is NOT RUNNING" -ForegroundColor Red
    Write-Host "  To start: cd Server; dotnet run" -ForegroundColor Yellow
}

Write-Host "`n3. Checking Microservices..." -ForegroundColor Yellow

# Check spaCy microservice
Write-Host "  spaCy NLP..." -NoNewline
try {
    $spacyResponse = Invoke-RestMethod -Uri "http://localhost:8001/health" -TimeoutSec 2
    Write-Host " RUNNING" -ForegroundColor Green
} catch {
    Write-Host " OFFLINE" -ForegroundColor Red
    Write-Host "    To start: cd micros/spaCy; python app.py" -ForegroundColor Yellow
}

# Check embeddings microservice
Write-Host "  Embeddings..." -NoNewline
try {
    $embeddingsResponse = Invoke-RestMethod -Uri "http://localhost:8002/health" -TimeoutSec 2
    Write-Host " RUNNING" -ForegroundColor Green
} catch {
    Write-Host " OFFLINE" -ForegroundColor Red
    Write-Host "    To start: cd micros/embeddings; python app.py" -ForegroundColor Yellow
}

# Check language detection microservice
Write-Host "  Language Detection..." -NoNewline
try {
    $langResponse = Invoke-RestMethod -Uri "http://localhost:8004/health" -TimeoutSec 2
    Write-Host " RUNNING" -ForegroundColor Green
} catch {
    Write-Host " OFFLINE (Optional)" -ForegroundColor Yellow
    Write-Host "    To start: cd micros/language-detection; .\venv\Scripts\Activate.ps1; python app.py" -ForegroundColor Gray
}

Write-Host "`n4. Checking Frontend..." -ForegroundColor Yellow

# Check if frontend is running
$frontendProcess = Get-Process | Where-Object { $_.ProcessName -eq "node" -and $_.CommandLine -match "next" }
if ($frontendProcess) {
    Write-Host "  Frontend is RUNNING" -ForegroundColor Green
} else {
    Write-Host "  Frontend is NOT RUNNING" -ForegroundColor Red
    Write-Host "  To start: cd client; npm run dev" -ForegroundColor Yellow
}

Write-Host "`n===================================================" -ForegroundColor Cyan
Write-Host "System Status Check Complete!" -ForegroundColor Cyan
Write-Host "`nTo test the system, run: .\test-complete-system.ps1" -ForegroundColor White
Write-Host "===================================================" -ForegroundColor Cyan
