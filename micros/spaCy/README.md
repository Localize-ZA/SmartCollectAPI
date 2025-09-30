# spaCy NLP Microservice

This microservice provides natural language processing capabilities using spaCy for the SmartCollect API system.

## Features

- **Named Entity Recognition (NER)**: Extracts persons, organizations, locations, dates, money, etc.
- **Text Classification**: Categorizes documents into types (financial, legal, technical, etc.)
- **Key Phrase Extraction**: Identifies important terms and concepts
- **Language Detection**: Identifies document language
- **Document Embeddings**: Generates vector representations for semantic search
- **Sentiment Analysis**: Basic sentiment scoring
- **Async Processing**: Queue-based processing via Redis
- **Batch Processing**: Efficient batch document processing

## API Endpoints

### Health Check
- `GET /health` - Service health status

### Document Processing
- `POST /api/v1/process` - Process single document synchronously
- `POST /api/v1/process/batch` - Process multiple documents in batch

### Async Job Processing
- `POST /api/v1/jobs/submit` - Submit document for async processing
- `GET /api/v1/jobs/{job_id}` - Get job status

### Statistics & Admin
- `GET /api/v1/stats` - Service statistics
- `POST /api/v1/admin/clear-queue` - Clear processing queue

## Setup

### Windows
```bash
run.bat
```

### Linux/Mac
```bash
chmod +x run.sh
./run.sh
```

### Manual Setup
```bash
# Create virtual environment
python -m venv venv
source venv/bin/activate  # On Windows: venv\Scripts\activate

# Install dependencies
pip install -r requirements.txt

# Download spaCy model
python -m spacy download en_core_web_md

# Run service
python app.py
```

### Docker
```bash
docker build -t spacy-nlp-service .
docker run -p 5084:5084 --env-file .env spacy-nlp-service
```

## Configuration

Configure via `.env` file:

- `SPACY_MODEL`: spaCy language model (default: en_core_web_md)
- `REDIS_HOST`: Redis server host
- `REDIS_PORT`: Redis server port
- `BATCH_SIZE`: Maximum batch processing size
- Feature flags: `ENABLE_NER`, `ENABLE_CLASSIFICATION`, etc.

## Integration with SmartCollect API

The service integrates with the main SmartCollect API pipeline:

1. Documents are submitted to the processing queue
2. spaCy worker processes documents asynchronously
3. Results are published back to the results queue
4. Main API consumes results and updates document metadata

## Architecture

```
┌─────────────────┐    ┌──────────────┐    ┌─────────────────┐
│   FastAPI App   │    │ NLP Processor│    │ Redis Consumer  │
│   (API Layer)   │───▶│   (spaCy)    │◄───│   (Worker)      │
└─────────────────┘    └──────────────┘    └─────────────────┘
         │                       │                    │
         ▼                       ▼                    ▼
┌─────────────────┐    ┌──────────────┐    ┌─────────────────┐
│ Redis Queues    │    │ NLP Models   │    │ Job Processing  │
│ - Processing    │    │ - NER        │    │ - Status Update │
│ - Results       │    │ - Classifier │    │ - Error Handle  │
└─────────────────┘    └──────────────┘    └─────────────────┘
```

## Service Status

The service runs on port 5084 and provides:
- Health checks at `/health`
- API documentation at `/docs`
- Monitoring via Redis queue lengths
- Processing statistics and performance metrics