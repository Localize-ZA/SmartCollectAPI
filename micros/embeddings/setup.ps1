# Setup script for Sentence-Transformers microservice (Windows)

Write-Host "Setting up Sentence-Transformers embedding service..." -ForegroundColor Cyan

# Create virtual environment
Write-Host "Creating virtual environment..." -ForegroundColor Yellow
python -m venv venv

# Activate virtual environment
Write-Host "Activating virtual environment..." -ForegroundColor Yellow
.\venv\Scripts\Activate.ps1

# Upgrade pip
Write-Host "Upgrading pip..." -ForegroundColor Yellow
python -m pip install --upgrade pip

# Install requirements
Write-Host "Installing dependencies (this may take a few minutes)..." -ForegroundColor Yellow
pip install -r requirements.txt

Write-Host ""
Write-Host "Setup complete!" -ForegroundColor Green
Write-Host ""
Write-Host "The model will be downloaded on first run (~400MB for all-mpnet-base-v2)" -ForegroundColor Yellow
Write-Host ""
Write-Host "To run the service:" -ForegroundColor Cyan
Write-Host "  1. Activate virtual environment:" -ForegroundColor White
Write-Host "     .\venv\Scripts\Activate.ps1" -ForegroundColor Yellow
Write-Host "  2. Start the service:" -ForegroundColor White
Write-Host "     uvicorn app:app --reload --port 5086" -ForegroundColor Yellow
Write-Host ""
