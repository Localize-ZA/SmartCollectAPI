# Microservices Setup Guide

## Overview

SmartCollectAPI now uses three Python-based microservices for advanced NLP, OCR, and embedding capabilities:

1. **spaCy NLP Service** (Port 5084) - Entity extraction + 300-dim embeddings
2. **EasyOCR Service** (Port 5085) - Deep learning OCR with 80+ languages
3. **Sentence-Transformers Service** (Port 5086) - High-quality 768-dim embeddings

## Prerequisites

- Python 3.9 or higher
- pip package manager
- Virtual environment support (venv)
- 4GB+ RAM (8GB+ recommended with GPU)
- Optional: CUDA-capable GPU for acceleration

## Quick Start (Windows)

### 1. Setup spaCy NLP Service

```powershell
cd micros/spaCy
.\setup.ps1
```

This will:
- Create virtual environment
- Install dependencies (spaCy, FastAPI, etc.)
- Download en_core_web_md model (300-dim embeddings)
- Start service on http://localhost:5084

**Manual start (after setup):**
```powershell
cd micros/spaCy
.\.venv\Scripts\Activate.ps1
python app.py
```

### 2. Setup EasyOCR Service

```powershell
cd micros/ocr
.\setup.ps1
```

This will:
- Create virtual environment
- Install dependencies (EasyOCR, PyTorch, etc.)
- Download English OCR model (~100MB)
- Start service on http://localhost:5085

**Manual start (after setup):**
```powershell
cd micros/ocr
.\.venv\Scripts\Activate.ps1
python app.py
```

**Note:** First run downloads models (~500MB for CPU, ~1GB for GPU). This is a one-time download.

### 3. Setup Sentence-Transformers Service

```powershell
cd micros/embeddings
.\setup.ps1
```

This will:
- Create virtual environment
- Install dependencies (sentence-transformers, PyTorch, etc.)
- Download all-mpnet-base-v2 model (~420MB)
- Start service on http://localhost:5086

**Manual start (after setup):**
```powershell
cd micros/embeddings
.\.venv\Scripts\Activate.ps1
python app.py
```

## Quick Start (Linux/Mac)

### 1. Setup spaCy NLP Service

```bash
cd micros/spaCy
chmod +x setup.sh
./setup.sh
```

### 2. Setup EasyOCR Service

```bash
cd micros/ocr
chmod +x setup.sh
./setup.sh
```

### 3. Setup Sentence-Transformers Service

```bash
cd micros/embeddings
chmod +x setup.sh
./setup.sh
```

## Service Details

### spaCy NLP Service (localhost:5084)

**Capabilities:**
- Named Entity Recognition (PERSON, ORG, GPE, DATE, etc.)
- Part-of-speech tagging
- Sentiment analysis
- 300-dimensional embeddings (en_core_web_md)

**Endpoints:**
- `POST /api/v1/nlp/entities` - Extract entities
- `POST /api/v1/nlp/embed` - Generate embeddings
- `POST /api/v1/nlp/sentiment` - Analyze sentiment
- `GET /health` - Health check

**Configuration:**
- Model: en_core_web_md (50MB)
- Embedding dimensions: 300
- Max text length: ~1M characters
- Response time: ~50-200ms per document

### EasyOCR Service (localhost:5085)

**Capabilities:**
- Deep learning OCR (80+ languages)
- Bounding box detection
- Confidence scores per text region
- GPU acceleration support

**Endpoints:**
- `POST /api/v1/ocr/extract` - Extract text from image
- `POST /api/v1/ocr/batch` - Batch OCR processing
- `GET /api/v1/languages` - List supported languages
- `GET /health` - Health check

**Configuration:**
- Languages: English (default), add more in config
- GPU: Auto-detected (falls back to CPU)
- Timeout: 120 seconds
- Supported formats: PNG, JPG, JPEG, BMP, TIFF

**Performance:**
- CPU: ~2-5 seconds per image
- GPU: ~0.5-1 second per image

### Sentence-Transformers Service (localhost:5086)

**Capabilities:**
- State-of-the-art semantic embeddings
- 768-dimensional vectors (vs spaCy's 300)
- Optimized for semantic search and similarity
- Batch processing support

**Endpoints:**
- `POST /api/v1/embed/single` - Single text embedding
- `POST /api/v1/embed/batch` - Batch embeddings
- `POST /api/v1/similarity` - Compute similarity between texts
- `GET /api/v1/models` - List available models
- `GET /health` - Health check

**Configuration:**
- Model: all-mpnet-base-v2 (420MB)
- Embedding dimensions: 768
- Max tokens: 384 (~1500 characters)
- Batch size: 32 (configurable)

**Performance:**
- CPU: ~50-100ms per text
- GPU: ~10-20ms per text
- Batch processing: ~2-5 seconds for 100 texts

## Health Checks

Verify all services are running:

```powershell
# spaCy
curl http://localhost:5084/health

# EasyOCR
curl http://localhost:5085/health

# Sentence-Transformers
curl http://localhost:5086/health
```

Expected response from all:
```json
{
  "status": "healthy",
  "service": "...",
  "version": "1.0.0"
}
```

## Troubleshooting

### Port Already in Use

If a port is already in use, you can change it in each service's `app.py`:

```python
# Change this line at the bottom of app.py
uvicorn.run(app, host="0.0.0.0", port=5084)  # Change port number
```

### GPU Not Detected

EasyOCR and Sentence-Transformers will automatically fall back to CPU if no GPU is detected. To force CPU mode:

**EasyOCR:**
```python
# In micros/ocr/services/ocr_processor.py
self.reader = easyocr.Reader(languages, gpu=False)
```

**Sentence-Transformers:**
```python
# In micros/embeddings/services/embedding_service.py
self.device = 'cpu'
```

### Model Download Issues

If model downloads fail, you can manually download them:

**spaCy:**
```powershell
python -m spacy download en_core_web_md
```

**Sentence-Transformers:**
Models are auto-downloaded on first use. If it fails, check internet connection and retry.

### Memory Issues

If you run out of memory:

1. **Reduce batch sizes** in Sentence-Transformers config
2. **Use smaller models:**
   - spaCy: Switch to `en_core_web_sm` (96-dim, 13MB)
   - Sentence-Transformers: Switch to `all-MiniLM-L6-v2` (384-dim, 80MB)

### Virtual Environment Not Activating

**PowerShell execution policy:**
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

**Or use CMD instead:**
```cmd
.venv\Scripts\activate.bat
```

## Production Deployment

### Docker Deployment

Each microservice includes a Dockerfile for containerization:

```bash
# Build EasyOCR image
cd micros/ocr
docker build -t smartcollect-ocr .
docker run -p 5085:5085 smartcollect-ocr

# Similar for other services
```

### Environment Variables

Configure services using environment variables:

**EasyOCR:**
- `OCR_PORT`: Service port (default: 5085)
- `OCR_LANGUAGES`: Comma-separated language codes (default: "en")
- `OCR_GPU`: Enable GPU (default: true)

**Sentence-Transformers:**
- `EMBED_PORT`: Service port (default: 5086)
- `EMBED_MODEL`: Model name (default: "all-mpnet-base-v2")
- `EMBED_BATCH_SIZE`: Batch size (default: 32)

### Scaling Recommendations

For production workloads:

1. **Run multiple instances** of each microservice behind a load balancer
2. **Use GPU instances** for EasyOCR and Sentence-Transformers (10x faster)
3. **Cache embeddings** in Redis to avoid recomputation
4. **Monitor memory usage** - each service needs 1-2GB RAM minimum

## API Integration

The .NET API automatically calls these services. No manual integration needed if services are running on default ports.

**Configuration in appsettings.json:**

```json
"Microservices": {
  "SpaCy": {
    "BaseUrl": "http://localhost:5084",
    "TimeoutSeconds": 30
  },
  "EasyOCR": {
    "BaseUrl": "http://localhost:5085",
    "TimeoutSeconds": 120
  },
  "SentenceTransformers": {
    "BaseUrl": "http://localhost:5086",
    "TimeoutSeconds": 30
  }
}
```

## Development Tips

### Hot Reload

All services support hot reload during development:

```powershell
# Add --reload flag
uvicorn app:app --reload --host 0.0.0.0 --port 5084
```

### Testing Endpoints

Use the included `.http` files or curl:

```bash
# Test EasyOCR
curl -X POST http://localhost:5085/api/v1/ocr/extract \
  -F "file=@test-image.png"

# Test embeddings
curl -X POST http://localhost:5086/api/v1/embed/single \
  -H "Content-Type: application/json" \
  -d '{"text": "Hello world", "normalize": true}'
```

### Logs

All services log to stdout. Check for errors:

```powershell
# Services started with setup scripts will show logs in the terminal
# Or run manually to see live logs:
cd micros/ocr
.\.venv\Scripts\Activate.ps1
python app.py
```

## Next Steps

1. ✅ Setup all three microservices
2. ✅ Verify health checks pass
3. 🔄 Start the main .NET API
4. 🔄 Test document upload and processing
5. 🔄 Monitor logs for any integration issues

For more details, see individual service README files:
- `micros/spaCy/README.md`
- `micros/ocr/README.md`
- `micros/embeddings/README.md`
