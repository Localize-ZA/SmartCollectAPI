# Language Detection Microservice Setup Script

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Language Detection Service Setup" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Check if Python is installed
Write-Host "Checking Python installation..." -ForegroundColor Yellow
try {
    $pythonVersion = python --version 2>&1
    Write-Host "  Found: $pythonVersion" -ForegroundColor Green
} catch {
    Write-Host "  ERROR: Python not found. Please install Python 3.9 or higher." -ForegroundColor Red
    exit 1
}

# Create virtual environment
Write-Host "`nCreating virtual environment..." -ForegroundColor Yellow
if (Test-Path "venv") {
    Write-Host "  Virtual environment already exists" -ForegroundColor Gray
} else {
    python -m venv venv
    Write-Host "  Virtual environment created" -ForegroundColor Green
}

# Activate virtual environment and install dependencies
Write-Host "`nInstalling dependencies..." -ForegroundColor Yellow
& "venv\Scripts\Activate.ps1"
pip install -r requirements.txt
Write-Host "  Dependencies installed" -ForegroundColor Green

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Setup Complete!" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "To start the service:" -ForegroundColor Yellow
Write-Host "  1. Activate environment: .\venv\Scripts\Activate.ps1" -ForegroundColor White
Write-Host "  2. Run service: python app.py" -ForegroundColor White
Write-Host "  3. Visit: http://localhost:8004/docs`n" -ForegroundColor White
