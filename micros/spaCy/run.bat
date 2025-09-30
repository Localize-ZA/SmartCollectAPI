@echo off
REM spaCy NLP Service Setup and Run Script for Windows

echo Setting up spaCy NLP Service...

REM Check if Python is installed
python --version >nul 2>&1
if errorlevel 1 (
    echo Python is required but not installed.
    pause
    exit /b 1
)

REM Create virtual environment if it doesn't exist
if not exist ".venv" (
    echo Creating virtual environment...
    python -m venv .venv
    if errorlevel 1 (
        echo Failed to create virtual environment
        pause
        exit /b 1
    )
)

REM Activate virtual environment
echo Activating virtual environment...
call .venv\Scripts\activate.bat
if errorlevel 1 (
    echo Failed to activate virtual environment
    pause
    exit /b 1
)

REM Install dependencies with fallback approach
echo Installing dependencies...
python -m pip install --upgrade pip

echo Attempting installation with pip...
pip install --no-cache-dir -r requirements.txt
if errorlevel 1 (
    echo Main installation failed. Trying alternative approach...
    echo Installing core packages with binary wheels only...
    pip install --only-binary=:all: fastapi uvicorn redis python-multipart pydantic python-dotenv httpx
    
    echo Installing core packages...
    pip install --only-binary=:all: fastapi uvicorn redis python-multipart pydantic python-dotenv httpx
    
    echo Installing spaCy...
    pip install --only-binary=:all: "spacy>=3.8.0"
    
    if errorlevel 1 (
        echo Package installation failed. This may be due to missing C++ build tools.
        echo Please install Microsoft Visual C++ Build Tools from:
        echo https://visualstudio.microsoft.com/visual-cpp-build-tools/
        pause
        exit /b 1
    )
)

REM Download spaCy language model (using smaller model)
echo Downloading spaCy language model...
python -m spacy download en_core_web_sm
if errorlevel 1 (
    echo Warning: Failed to download spaCy model automatically.
    echo You can download it manually later with: python -m spacy download en_core_web_sm
    echo Continuing anyway...
)

echo Setup complete!
echo Starting spaCy NLP Service on port 5084...
uvicorn app:app --host 0.0.0.0 --port 5084 --reload

pause