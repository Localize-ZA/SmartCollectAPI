# SmartCollectAPI - Intelligent Document Processing Pipeline

A comprehensive document processing pipeline that transforms raw documents into structured, intelligent data using modern NLP and AI services. The system features a microservices architecture with specialized processing components.

## 🎯 System Overview

SmartCollectAPI transforms raw documents (PDFs, Word, images, JSON, XML, CSV) into structured, intelligent data using a combination of specialized microservices and AI-powered analysis. The system follows the **ingest → detect → parse → normalize → enrich → vectorize → persist → notify** pipeline.

## 🛠️ Tech Stack

### Core Services
- **.NET 9** - Main API with Minimal API + Background Services
- **Redis** - Queue management with streams and DLQ
- **PostgreSQL + pgvector** - Structured storage with vector similarity search
- **Next.js 14+** - Modern React-based frontend with TypeScript

### Microservices Architecture
- **spaCy NLP Service** - Python FastAPI microservice for advanced NLP processing
  - Entity extraction and recognition
  - Text classification and categorization
  - Key phrase extraction
  - Sentiment analysis
  - Word embeddings and similarity
- **Document Processing Pipeline** - Specialized parsers for various formats
- **Notification Service** - SMTP-based email notifications

## 🏗️ Architecture

### Microservices Architecture
The system is built with a modular microservices approach:
- **Main API (.NET 9)** - Document ingestion, orchestration, and data persistence
- **spaCy NLP Service (Python FastAPI)** - Advanced natural language processing
- **Redis Queue System** - Asynchronous job processing and message queuing
- **PostgreSQL Database** - Document storage with vector search capabilities
- **Next.js Frontend** - Modern web interface with real-time updates

## 🧠 spaCy NLP Microservice

The spaCy NLP service provides comprehensive natural language processing capabilities through a FastAPI-based microservice architecture.

### Features
- **Named Entity Recognition (NER)** - Extract persons, organizations, locations, dates, etc.
- **Text Classification** - Categorize documents by content type (financial, legal, technical, etc.)
- **Key Phrase Extraction** - Identify important terms and concepts
- **Sentiment Analysis** - Determine emotional tone and sentiment
- **Word Embeddings** - Generate semantic vector representations
- **Async Processing** - Background job processing with Redis integration

### API Endpoints
- `GET /health` - Service health check
- `POST /process` - Process document text with full NLP pipeline
- `POST /extract-entities` - Extract named entities only
- `POST /classify` - Classify document content
- `POST /analyze-sentiment` - Analyze text sentiment
- `GET /jobs/{job_id}` - Check processing job status

### Technical Stack
- **FastAPI** - Modern Python web framework with automatic OpenAPI docs
- **spaCy 3.8+** - Industrial-strength NLP library
- **Redis** - Job queue and result caching
- **Pydantic** - Data validation and serialization
- **Docker** - Containerized deployment

### Processing Pipeline
1. **Ingestion** - File upload → storage → SHA256 deduplication → Redis queue
2. **Type Detection** - MIME type detection with content sniffing
3. **Parsing** - Route to appropriate parser based on content type
4. **NLP Processing** - spaCy microservice for entity extraction, classification, and sentiment analysis
5. **Vectorization** - Text embeddings and semantic analysis
6. **Persistence** - Canonical JSON + vectors stored in PostgreSQL
7. **Notification** - SMTP email delivery with processing summary

### Database Schema
```sql
-- Staging table for job tracking
CREATE TABLE staging_documents (
  id UUID PRIMARY KEY,
  job_id TEXT NOT NULL,
  source_uri TEXT NOT NULL,
  mime TEXT,
  sha256 TEXT,
  raw_metadata JSONB,
  normalized JSONB,
  status TEXT DEFAULT 'pending',
  attempts INT DEFAULT 0,
  created_at TIMESTAMPTZ DEFAULT NOW(),
  updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Final documents table with vector embeddings
CREATE TABLE documents (
  id UUID PRIMARY KEY,
  source_uri TEXT NOT NULL,
  mime TEXT,
  sha256 TEXT UNIQUE,
  canonical JSONB NOT NULL,
  created_at TIMESTAMPTZ DEFAULT NOW(),
  updated_at TIMESTAMPTZ DEFAULT NOW(),
  embedding VECTOR -- Vector dimensions depend on NLP service configuration
);
```

## ⚙️ Configuration

### Main API Configuration (`appsettings.json`)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=SmartCollectDB;Username=postgres;Password=yourpassword",
    "Redis": "localhost:6379"
  },
  "Services": {
    "SpacyNlpService": {
      "BaseUrl": "http://localhost:5084",
      "Timeout": 30000
    }
  },
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "EnableSsl": true,
    "Username": "your-email@gmail.com",
    "Password": "your-app-password",
    "FromEmail": "noreply@yourcompany.com"
  }
}
```

### spaCy NLP Service Configuration (`.env`)
```env
# Service Settings
SERVICE_PORT=5084
SERVICE_VERSION=1.0.0

# Redis Configuration
REDIS_HOST=localhost
REDIS_PORT=6379
REDIS_DB=0

# spaCy Configuration
SPACY_MODEL=en_core_web_sm
BATCH_SIZE=10

# Feature Flags
ENABLE_NER=true
ENABLE_CLASSIFICATION=true
ENABLE_KEY_PHRASES=true
ENABLE_EMBEDDINGS=true
ENABLE_SENTIMENT=true
```
```

## 📄 Canonical Document Schema

Each processed document is normalized to this schema:

```json
{
  "id": "uuid4",
  "source_uri": "path/to/file.pdf",
  "ingest_ts": "2025-01-24T12:00:00Z",
  "mime": "application/pdf",
  "structured": false,
  "structured_payload": null,
  "extracted_text": "Full document text for search and embedding",
  "entities": [
    {
      "name": "Acme Corp",
      "type": "ORGANIZATION", 
      "salience": 0.96,
      "mentions": [{"text": "Acme Corp", "start_offset": 10, "end_offset": 19}]
    }
  ],
  "tables": [],
  "sections": [],
  "embedding_dim": 300,
  "processing_status": "processed",
  "processing_errors": null,
  "schema_version": "v1"
}
```

## 🚀 Quick Start

### 1. Infrastructure Setup
```bash
# Start Redis and PostgreSQL
docker compose -f docker-compose.dev.yml up -d
```

### 2. Start spaCy NLP Service
```bash
cd micros/spacy
# Windows
.\run.bat

# Linux/macOS
./run.sh
```

### 3. Run the Main API
```bash
cd Server
dotnet run
```

### 4. Start the Frontend (Optional)
```bash
cd client
npm install
npm run dev
```

### 5. Test Document Processing
```bash
# Upload a document for processing
curl -X POST http://localhost:5000/api/ingest \
  -F "file=@document.pdf" \
  -F "notify_email=your-email@gmail.com"
```

### 6. Monitor Processing
- Main API health: `GET /health`
- spaCy NLP service health: `GET http://localhost:5084/health`
- View Redis queues and DLQ for job status
- Check PostgreSQL `staging_documents` and `documents` tables
- Frontend dashboard: `http://localhost:3000` (if running)

## 🔄 Error Handling & Reliability

### Retry Mechanism
- **Exponential backoff** for API rate limits (429 errors)
- **Dead Letter Queue** for poison messages after 3 retry attempts
- **Partial failure recovery** - persist successful stages, retry failed ones

### Idempotency
- **SHA256 deduplication** prevents duplicate document processing
- **Job idempotency tokens** prevent double-submit on HTTP uploads
- **Database constraints** ensure single canonical record per file hash

### Monitoring
- Structured logging with correlation IDs
- Health checks for Redis and PostgreSQL dependencies
- Processing metrics and error tracking
- DLQ monitoring and alerting

## 📊 Performance Features

- **Batch NLP processing** to optimize spaCy processing performance
- **Connection pooling** for database and Redis
- **Vector similarity search** with HNSW indexing via pgvector  
- **Async processing** throughout the pipeline
- **Horizontal worker scaling** support

## 🎯 Demo Flow

1. **Upload** a complex document through the API or web interface
2. **Monitor** job progression through Redis streams and frontend dashboard
3. **Observe** spaCy NLP service extract entities, classify content, and generate embeddings
4. **Receive** email notification with structured summary and JSON attachment
5. **Query** vector database for semantic similarity searches
6. **Explore** results through the modern Next.js frontend

The system showcases **"raw chaos in, structured intelligence out"** using modern microservices architecture and advanced NLP capabilities.

## 🔧 Development

### Build & Test
```bash
cd Server
dotnet build
dotnet test
```

### Frontend Development
```bash
cd client  
npm install
npm run dev
```

### spaCy NLP Service Development
```bash
cd micros/spacy
# Activate virtual environment
.\.venv\Scripts\Activate.ps1  # Windows
source .venv/bin/activate     # Linux/macOS

# Install dependencies
pip install -r requirements.txt

# Download spaCy model
python -m spacy download en_core_web_sm

# Run service
python -m uvicorn app:app --host 0.0.0.0 --port 5084 --reload
```

## 📈 Production Considerations

### Infrastructure
- Deploy spaCy NLP service with proper containerization (Docker)
- Set up load balancing for multiple spaCy service instances
- Configure Redis clustering for high availability
- Set up PostgreSQL replication and backup strategies
- Implement proper secret management (Azure Key Vault, etc.)

### Monitoring & Observability
- Set up monitoring and alerting (Prometheus + Grafana)
- Configure log aggregation with structured logging
- Implement health checks for all microservices
- Monitor NLP processing performance and accuracy
- Track queue depths and processing latencies

### Security & Performance
- Implement proper authentication and authorization
- Configure HTTPS/TLS for all service communications
- Set up rate limiting and request validation
- Optimize spaCy model loading and caching
- Configure connection pooling for databases
