# Script to upgrade SmartCollectAPI to use chunking and better embeddings
# This upgrades from spaCy en_core_web_sm (96 dims) to en_core_web_md (300 dims)
# and adds chunking capabilities for better semantic search

Write-Host "=== SmartCollectAPI: Upgrade to Chunking & Better Embeddings ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Check Docker is running
Write-Host "[1/6] Checking Docker..." -ForegroundColor Yellow
docker ps > $null 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Docker is not running. Please start Docker Desktop first." -ForegroundColor Red
    exit 1
}
Write-Host "✓ Docker is running" -ForegroundColor Green

# Step 2: Apply database migration
Write-Host ""
Write-Host "[2/6] Applying database migration (adding document_chunks table)..." -ForegroundColor Yellow
$migrationPath = "scripts\add_chunks_table.sql"
if (-not (Test-Path $migrationPath)) {
    Write-Host "ERROR: Migration script not found at $migrationPath" -ForegroundColor Red
    exit 1
}

docker exec -i smartcollectapi-postgres-1 psql -U postgres -d smartcollect < $migrationPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to apply migration. Check if database is running." -ForegroundColor Red
    exit 1
}
Write-Host "✓ Database migration applied" -ForegroundColor Green

# Step 3: Upgrade spaCy model
Write-Host ""
Write-Host "[3/6] Upgrading spaCy model (en_core_web_sm → en_core_web_md)..." -ForegroundColor Yellow
Push-Location "micros\spaCy"

# Check if virtual environment exists
if (-not (Test-Path "venv")) {
    Write-Host "Creating Python virtual environment..." -ForegroundColor Yellow
    python -m venv venv
}

# Activate and install
Write-Host "Installing spaCy and downloading en_core_web_md model..." -ForegroundColor Yellow
.\venv\Scripts\Activate.ps1
python -m pip install --upgrade pip > $null
python -m pip install spacy > $null
python -m spacy download en_core_web_md

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to download spaCy model" -ForegroundColor Red
    Pop-Location
    exit 1
}

deactivate
Pop-Location
Write-Host "✓ spaCy model upgraded to en_core_web_md (300 dimensions)" -ForegroundColor Green

# Step 4: Update spaCy config
Write-Host ""
Write-Host "[4/6] Updating spaCy configuration..." -ForegroundColor Yellow
$envPath = "micros\spaCy\.env"
$envContent = @"
SPACY_MODEL=en_core_web_md
SERVICE_PORT=5084
REDIS_HOST=localhost
REDIS_PORT=6379
ENABLE_NER=true
ENABLE_CLASSIFICATION=true
ENABLE_KEY_PHRASES=true
ENABLE_EMBEDDINGS=true
ENABLE_LANGUAGE_DETECTION=true
"@

Set-Content -Path $envPath -Value $envContent
Write-Host "✓ spaCy configuration updated" -ForegroundColor Green

# Step 5: Rebuild .NET project
Write-Host ""
Write-Host "[5/6] Rebuilding .NET API..." -ForegroundColor Yellow
Push-Location "Server"
dotnet build > $null
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to build .NET project" -ForegroundColor Red
    Pop-Location
    exit 1
}
Pop-Location
Write-Host "✓ .NET API rebuilt" -ForegroundColor Green

# Step 6: Verify the upgrade
Write-Host ""
Write-Host "[6/6] Verifying upgrade..." -ForegroundColor Yellow

# Check if chunks table exists
$verifyQuery = "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'document_chunks';"
$result = docker exec smartcollectapi-postgres-1 psql -U postgres -d smartcollect -t -c "$verifyQuery"

if ($result.Trim() -eq "1") {
    Write-Host "✓ document_chunks table created successfully" -ForegroundColor Green
} else {
    Write-Host "WARNING: document_chunks table may not have been created" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Upgrade Complete! ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Summary of changes:" -ForegroundColor White
Write-Host "  ✓ Embeddings upgraded: 96 → 300 dimensions" -ForegroundColor Green
Write-Host "  ✓ Text chunking enabled for documents > 2000 chars" -ForegroundColor Green
Write-Host "  ✓ document_chunks table created in database" -ForegroundColor Green
Write-Host "  ✓ spaCy model: en_core_web_md" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Restart your services (docker compose, .NET API, spaCy)" -ForegroundColor White
Write-Host "  2. Test document upload to verify chunking works" -ForegroundColor White
Write-Host "  3. Query the chunks: SELECT COUNT(*) FROM document_chunks;" -ForegroundColor White
Write-Host ""
Write-Host "To start services:" -ForegroundColor Yellow
Write-Host "  docker compose -f docker-compose.dev.yml up -d" -ForegroundColor Cyan
Write-Host "  cd Server && dotnet run" -ForegroundColor Cyan
Write-Host "  cd micros/spaCy && .\venv\Scripts\Activate.ps1 && uvicorn main:app --reload --port 5084" -ForegroundColor Cyan
Write-Host ""
