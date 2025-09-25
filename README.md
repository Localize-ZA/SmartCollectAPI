# SmartCollectAPI - Google-First Document Processing Pipeline

A comprehensive document processing pipeline that leverages Google Cloud services for intelligent document parsing, entity extraction, vectorization, and notification delivery.

## 🎯 System Overview

SmartCollectAPI transforms raw documents (PDFs, Word, images, JSON, XML, CSV) into structured, intelligent data using Google Cloud AI services while providing fallback OSS alternatives. The system follows the **ingest → detect → parse → normalize → enrich → vectorize → persist → notify** pipeline.

## 🛠️ Tech Stack

- **.NET 9** - Minimal API + Background Services
- **Redis** - Queue management with streams and DLQ
- **PostgreSQL + pgvector** - Structured storage with vector similarity search
- **Google Cloud Services:**
  - Document AI - PDF/Word parsing with OCR and layout preservation
  - Vision AI - Image OCR and object detection
  - Natural Language AI - Entity extraction and sentiment analysis
  - Vertex AI - Text embeddings (1536 dimensions)
  - Gmail API - Notification delivery
- **OSS Fallbacks** - SMTP, simple embeddings, basic PDF parsing

## 🏗️ Architecture

### Provider-Based Architecture
All Google Cloud services are wrapped behind interfaces with OSS fallbacks:
- `IAdvancedDocumentParser` → GoogleDocAiParser | SimplePdfParser
- `IOcrService` → GoogleVisionOcrService | (Future: TesseractOcrService)
- `IEmbeddingService` → VertexEmbeddingService | SimpleEmbeddingService
- `IEntityExtractionService` → GoogleEntityExtractionService
- `INotificationService` → GmailNotificationService | SmtpNotificationService

### Processing Pipeline
1. **Ingestion** - File upload → storage → SHA256 deduplication → Redis queue
2. **Type Detection** - MIME type detection with content sniffing
3. **Parsing** - Route to appropriate parser based on content type
4. **Entity Extraction** - Google Natural Language API for entities and sentiment
5. **Vectorization** - Vertex AI embeddings with text chunking
6. **Persistence** - Canonical JSON + vectors stored in PostgreSQL
7. **Notification** - Gmail/SMTP delivery with processing summary

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
  embedding VECTOR(1536)
);
```

## ⚙️ Configuration

Configure provider selection in `appsettings.json`:

```json
{
  "Services": {
    "Parser": "Google",         // Google | OSS
    "OCR": "Google",           // Google | OSS  
    "Embeddings": "Google",    // Google | OSS
    "EntityExtraction": "Google", // Google | OSS
    "Notifications": "Google"   // Google | OSS
  },
  "GoogleCloud": {
    "ProjectId": "your-project-id",
    "Location": "us-central1",
    "ProcessorId": "your-processor-id",
    "CredentialsPath": "path/to/service-account.json"
  },
  "Gmail": {
    "CredentialsPath": "path/to/gmail-credentials.json",
    "FromEmail": "noreply@yourcompany.com"
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
  "embedding_dim": 1536,
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

### 2. Google Cloud Setup (Optional but Recommended)
1. Create a Google Cloud project
2. Enable APIs: Document AI, Vision AI, Natural Language AI, Vertex AI, Gmail API
3. Create service account with appropriate permissions
4. Download credentials JSON file
5. Update `appsettings.json` with project details

### 3. Run the API
```bash
cd Server
dotnet run
```

### 4. Test Document Processing
```bash
# Upload a document for processing
curl -X POST http://localhost:5000/api/ingest \
  -F "file=@document.pdf" \
  -F "notify_email=your-email@gmail.com"
```

### 5. Monitor Processing
- Check health: `GET /health`
- View Redis queues and DLQ for job status
- Check PostgreSQL `staging_documents` and `documents` tables

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

- **Batch embedding processing** to optimize Vertex AI API usage
- **Connection pooling** for database and Redis
- **Vector similarity search** with HNSW indexing via pgvector  
- **Async processing** throughout the pipeline
- **Horizontal worker scaling** support

## 🎯 Demo Flow

1. **Upload** a complex PDF through the API
2. **Monitor** job progression through Redis streams
3. **Observe** Google Cloud APIs extract text, entities, and generate embeddings
4. **Receive** email notification with structured summary and JSON attachment
5. **Query** vector database for semantic similarity searches

The system showcases **"raw chaos in, structured intelligence out"** - perfect for demonstrating Google Cloud AI capabilities in a hackathon setting.

## 🔧 Development

### Build & Test
```bash
cd Server
dotnet build
dotnet test
```

### Client Dashboard (Optional)
```bash
cd client  
npm install
npm run dev
```

## 📈 Production Considerations

- Configure proper Google Cloud authentication (Workload Identity)
- Set up monitoring and alerting (Cloud Monitoring + Grafana)
- Implement proper secret management (Azure Key Vault, etc.)
- Configure log aggregation (Structured logging → Cloud Logging)
- Set up backup strategies for PostgreSQL
- Configure Redis clustering for high availability
