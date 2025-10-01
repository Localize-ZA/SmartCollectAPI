# SmartCollectAPI Usage Guide

This document provides examples of how to use the SmartCollectAPI endpoints for document processing.

## Base URL
```
http://localhost:5082 (Development)
https://your-domain.com (Production)
```

## Authentication
Currently, the API does not require authentication. Add API keys, OAuth, or another auth mechanism before deploying to production.

## Endpoints

### 1. Health Check

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

```bash
POST /api/ingest
Content-Type: multipart/form-data

file: [your-file.pdf]
notify_email: user@example.com (optional)
```

**Example using curl:**
```bash
curl -X POST http://localhost:5082/api/ingest \
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

Pipeline stages:
1. **Type Detection** – MIME detection plus file sniffing determine the parser strategy.
2. **Parsing** – Structured formats (JSON, XML, CSV) are converted directly; PDFs default to the PdfPig parser; Office formats are converted to PDF via LibreOffice before parsing.
3. **Entity Extraction** – The spaCy microservice analyzes extracted text for entities, classifications, key phrases, and sentiment.
4. **Vectorization** – The same spaCy call returns 96-dimension embeddings that are stored alongside the document.
5. **Storage** – Canonical JSON plus vectors are persisted to PostgreSQL (pgvector).
6. **Notification** – Optional SMTP notifications are sent if the SMTP provider is configured and `notify_email` is supplied.

### 3. Get Documents

```bash
GET /api/documents?page=1&pageSize=20
```

### 4. Get Specific Document

```bash
GET /api/documents/{id}
```

### 5. Ingest Multiple Files

```bash
POST /api/ingest/bulk
Content-Type: multipart/form-data

files[]: [file1.pdf, file2.json]
notify_email: user@example.com (optional)
```

## File Type Support

### Structured Data (Native Parsing)
- **JSON** (`application/json`) – Preserved hierarchy
- **XML** (`application/xml`, `text/xml`) – Converted to hierarchical JSON
- **CSV** (`text/csv`) – Converted to JSON with row/column structure

### Documents (OSS parser stack)
- **PDF** (`application/pdf`) – Parsed with PdfPig for text extraction and layout hints
- **Plain Text** (`text/plain`) – Ingested as-is
- **Office formats** – DOC/DOCX/XLS/XLSX/PPT/PPTX/RTF/ODT/ODS/ODP are converted to PDF via LibreOffice (requires `soffice`) and then parsed with PdfPig. If LibreOffice is disabled or missing, the pipeline falls back to plain-text extraction.

### Images (Tesseract OCR)
- **JPEG/JPG**, **PNG**, **GIF**, **BMP**, **TIFF** – Processed through the Tesseract CLI (requires `tesseract` and language packs). Configure binary path and languages in `Tesseract` settings.

## Error Handling

See HTTP status code table and error schema in earlier versions—unchanged.

## Processing Pipeline Details

Parsing stage now includes LibreOffice conversion when available; OCR stage calls Tesseract via CLI.

## Configuration Examples

### Default OSS Mode
```json
{
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

### Lightweight Mode
```json
{
  "Services": {
    "Parser": "SIMPLE",
    "OCR": "SIMPLE",
    "Embeddings": "OSS",
    "EntityExtraction": "OSS",
    "Notifications": "OSS"
  },
  "LibreOffice": {
    "Enabled": false
  },
  "Tesseract": {
    "Enabled": false
  }
}
```

## Rate Limits & Quotas

The OSS pipeline (LibreOffice + PdfPig + Tesseract + spaCy) does not enforce explicit rate limits beyond infrastructure throughput. Monitor CPU/RAM usage for LibreOffice/Tesseract containers if you scale horizontally.

## Monitoring & Debugging

Same endpoints as before; ensure LibreOffice and Tesseract binaries are installed and on the PATH for the API container/VM. Logs include conversion and OCR timing plus failure details.

This completes the API usage guide. The system provides an OSS-first document processing pipeline with pluggable providers for future enhancements.
