# SmartCollectAPI Usage Guide

This document provides examples of how to use the SmartCollectAPI endpoints for document processing.

## Base URL
```
http://localhost:5000 (Development)
https://your-domain.com (Production)
```

## Authentication
Currently, the API doesn't require authentication, but this should be added for production use.

## Endpoints

### 1. Health Check

Check if the API and its dependencies are healthy.

```bash
GET /health
```

**Response:**
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.1234567",
  "entries": {
    "postgres": {
      "status": "Healthy",
      "duration": "00:00:00.0234567"
    },
    "redis": {
      "status": "Healthy", 
      "duration": "00:00:00.0123456"
    }
  }
}
```

### 2. Document Ingestion

Upload and process a document through the intelligent pipeline.

```bash
POST /api/ingest
Content-Type: multipart/form-data

file: [your-file.pdf]
notify_email: user@example.com (optional)
```

**Example using curl:**
```bash
curl -X POST http://localhost:5000/api/ingest \
  -F "file=@document.pdf" \
  -F "notify_email=john@example.com"
```

**Response:**
```json
{
  "job_id": "123e4567-e89b-12d3-a456-426614174000",
  "sha256": "abc123def456...",
  "source_uri": "uploads/123e4567-e89b-12d3-a456-426614174000.pdf"
}
```

The document will be processed asynchronously through the pipeline:
1. **Type Detection** - Automatically detect file type
2. **Parsing** - Extract text using Google Document AI or appropriate parser
3. **Entity Extraction** - Identify people, organizations, locations using Google Natural Language API
4. **Vectorization** - Generate embeddings using Vertex AI
5. **Storage** - Save canonical JSON + vectors to PostgreSQL
6. **Notification** - Send email summary if notify_email provided

### 3. Get Documents

Retrieve processed documents with pagination.

```bash
GET /api/documents?page=1&pageSize=20
```

**Response:**
```json
{
  "items": [
    {
      "id": "123e4567-e89b-12d3-a456-426614174000",
      "sourceUri": "uploads/document.pdf",
      "mime": "application/pdf",
      "sha256": "abc123def456...",
      "createdAt": "2025-01-24T12:00:00Z",
      "hasEmbedding": true
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

### 4. Get Specific Document

Retrieve full document details including canonical JSON and embeddings.

```bash
GET /api/documents/{id}
```

**Response:**
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "sourceUri": "uploads/document.pdf",
  "mime": "application/pdf",
  "sha256": "abc123def456...",
  "canonical": {
    "id": "123e4567-e89b-12d3-a456-426614174000",
    "source_uri": "uploads/document.pdf",
    "ingest_ts": "2025-01-24T12:00:00Z",
    "mime": "application/pdf",
    "structured": false,
    "extracted_text": "This is the extracted text from the document...",
    "entities": [
      {
        "name": "Acme Corporation",
        "type": "ORGANIZATION",
        "salience": 0.95,
        "mentions": [
          {
            "text": "Acme Corporation",
            "start_offset": 10,
            "end_offset": 26
          }
        ]
      }
    ],
    "tables": [],
    "sections": [
      {
        "title": "Page 1",
        "content": "Document content...",
        "pageNumber": 1
      }
    ],
    "embedding_dim": 1536,
    "processing_status": "processed",
    "schema_version": "v1"
  },
  "createdAt": "2025-01-24T12:00:00Z",
  "updatedAt": "2025-01-24T12:00:00Z",
  "embedding": [0.123, -0.456, 0.789, ...] // 1536 dimensions
}
```

### 5. Get Processing Status

Check the status of documents currently being processed.

```bash
GET /api/documents/staging?status=processing
```

**Response:**
```json
[
  {
    "id": "456e7890-e89b-12d3-a456-426614174001",
    "jobId": "789e4567-e89b-12d3-a456-426614174002",
    "sourceUri": "uploads/another-document.pdf",
    "mime": "application/pdf",
    "sha256": "def456ghi789...",
    "status": "processing",
    "attempts": 1,
    "createdAt": "2025-01-24T12:05:00Z",
    "updatedAt": "2025-01-24T12:05:30Z"
  }
]
```

**Status values:**
- `pending` - Queued for processing
- `processing` - Currently being processed
- `done` - Successfully processed
- `failed` - Processing failed after retries

### 6. Get Processing Statistics

Get overall system processing statistics.

```bash
GET /api/documents/stats
```

**Response:**
```json
{
  "totalDocuments": 150,
  "documentsWithEmbeddings": 147,
  "processedToday": 23,
  "stagingStatus": {
    "pending": 2,
    "processing": 1,
    "done": 145,
    "failed": 2
  }
}
```

### 7. Search Documents

Search for similar documents using text-based search (vector search placeholder).

```bash
POST /api/documents/search
Content-Type: application/json

{
  "query": "contract agreement terms",
  "limit": 10
}
```

**Response:**
```json
[
  {
    "id": "123e4567-e89b-12d3-a456-426614174000",
    "sourceUri": "uploads/contract.pdf",
    "mime": "application/pdf", 
    "sha256": "abc123def456...",
    "createdAt": "2025-01-24T12:00:00Z",
    "hasEmbedding": true
  }
]
```

## File Type Support

### Structured Data (Native Parsing)
- **JSON** (`application/json`) - Preserved hierarchy
- **XML** (`application/xml`, `text/xml`) - Converted to hierarchical JSON
- **CSV** (`text/csv`) - Converted to JSON with row/column structure

### Documents (Google Document AI)
- **PDF** (`application/pdf`) - Text extraction, OCR, layout preservation, table extraction
- **Word** (`application/msword`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document`)
- **Excel** (`application/vnd.ms-excel`, `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`)
- **PowerPoint** (`application/vnd.ms-powerpoint`, `application/vnd.openxmlformats-officedocument.presentationml.presentation`)

### Images (Google Vision AI)
- **JPEG/JPG** (`image/jpeg`) - OCR + object detection
- **PNG** (`image/png`) - OCR + object detection  
- **GIF** (`image/gif`) - OCR + object detection
- **TIFF** (`image/tiff`) - OCR + object detection
- **WEBP** (`image/webp`) - OCR + object detection

## Error Handling

### HTTP Status Codes
- `200` - Success
- `202` - Accepted (for async processing)
- `400` - Bad Request (invalid parameters)
- `404` - Not Found
- `429` - Too Many Requests (rate limiting)
- `500` - Internal Server Error

### Error Response Format
```json
{
  "error": "Description of the error",
  "details": "Additional error details if available"
}
```

## Processing Pipeline Details

### 1. Ingestion
- File uploaded and stored locally or in cloud storage
- SHA256 hash computed for deduplication
- Job enqueued to Redis for async processing

### 2. Type Detection
- MIME type detection from headers and content sniffing
- Route to appropriate parser based on detected type

### 3. Parsing
- **Structured data**: Direct JSON conversion preserving hierarchy
- **Documents**: Google Document AI for advanced text/layout extraction
- **Images**: Google Vision AI for OCR and object detection

### 4. Entity Extraction
- Google Natural Language API identifies:
  - People, organizations, locations
  - Sentiment analysis
  - Entity salience scores
  - Mention locations in text

### 5. Vectorization
- Vertex AI text embeddings (1536 dimensions)
- Text chunking for long documents
- Fallback to hash-based embeddings if needed

### 6. Storage
- Canonical JSON stored in PostgreSQL JSONB column
- Vector embeddings stored in pgvector column for similarity search
- Full-text search capabilities

### 7. Notification
- Gmail API sends structured summary email
- Includes processing statistics, top entities, full JSON attachment
- SMTP fallback if Gmail unavailable

## Configuration Examples

### Google Cloud Services (Recommended)
```json
{
  "Services": {
    "Parser": "Google",
    "OCR": "Google", 
    "Embeddings": "Google",
    "EntityExtraction": "Google",
    "Notifications": "Google"
  }
}
```

### OSS Fallback Mode
```json
{
  "Services": {
    "Parser": "OSS",
    "OCR": "OSS",
    "Embeddings": "OSS", 
    "EntityExtraction": "OSS",
    "Notifications": "OSS"
  }
}
```

## Rate Limits & Quotas

### Google Cloud API Limits (Default)
- Document AI: 600 requests/minute
- Vision API: 1800 requests/minute
- Natural Language API: 600 requests/minute
- Vertex AI: Varies by region/model

### Best Practices
- Implement client-side rate limiting
- Use exponential backoff for retries
- Monitor quota usage and set alerts
- Consider batching requests where possible

## Monitoring & Debugging

### Health Monitoring
- `/health` endpoint checks all dependencies
- Redis queue depth monitoring
- Database connection health
- Failed job tracking in DLQ

### Logging
- Structured logging with correlation IDs
- Processing stage tracking
- Error details with stack traces
- Performance metrics

### Debugging Failed Jobs
1. Check `/api/documents/staging?status=failed`
2. Review Redis DLQ for poison messages
3. Check logs for detailed error information
4. Verify Google Cloud API quotas and permissions

This completes the API usage guide. The system provides a comprehensive, intelligent document processing pipeline with Google Cloud AI integration and robust fallback mechanisms.