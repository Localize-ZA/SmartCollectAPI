# SmartCollect API Development Setup

## Quick Start

1. **Start infrastructure:**
    ```powershell
    docker compose -f docker-compose.dev.yml up -d
    ```

2. **Verify core services:**
    - PostgreSQL: `localhost:5433` (user: `postgres`, password: `postgres`)
    - Redis: `localhost:6379`
    - Redis Commander UI: http://localhost:8081 (optional helper)

3. **Install document tooling (host or container image):**
    - LibreOffice (`soffice` CLI) – e.g. `sudo apt-get install -y libreoffice`
    - Tesseract OCR – e.g. `sudo apt-get install -y tesseract-ocr`
    - Additional language packs as required (e.g. `tesseract-ocr-eng`)

4. **Run the spaCy NLP microservice (required for embeddings/entities):**
    ```powershell
    cd micros\spaCy
    python -m venv .venv
    .\.venv\Scripts\Activate.ps1
    pip install -r requirements.txt
    python -m spacy download en_core_web_sm
    python -m uvicorn app:app --host 0.0.0.0 --port 5084 --reload
    ```

5. **Run the API:**
    ```powershell
    cd Server
    dotnet run
    ```

## Environment Configuration

Create or update `Server/appsettings.Development.json` with:

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379",
    "Postgres": "Host=localhost;Port=5433;Database=smartcollect;Username=postgres;Password=postgres"
  },
  "Storage": {
    "Provider": "Local",
    "LocalPath": "uploads"
  },
  "Services": {
    "Parser": "OSS",
    "OCR": "OSS",
    "Embeddings": "OSS",
    "EntityExtraction": "OSS",
    "Notifications": "OSS"
  },
  "LibreOffice": {
    "Enabled": true,
    "BinaryPath": "soffice",
    "TimeoutSeconds": 120
  },
  "Tesseract": {
    "Enabled": true,
    "BinaryPath": "tesseract",
    "Languages": "eng",
    "TimeoutSeconds": 120
  }
}
```

## Stop Infrastructure

```powershell
docker compose -f docker-compose.dev.yml down
```

## Clean Reset

```powershell
docker compose -f docker-compose.dev.yml down -v
docker compose -f docker-compose.dev.yml up -d
```
