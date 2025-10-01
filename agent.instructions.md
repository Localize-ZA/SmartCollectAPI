# OSS-First Data Processing Pipeline Blueprint

## Project Overview & Constraints

- Build a demonstrable end-to-end document pipeline within a short hackathon-style window.
- Showcase an entirely open-source toolchain that runs well on Linux (containers or bare metal).
- Keep the architecture modular so providers can be swapped without rewriting core services.

**Focus areas:** ingestion throughput, parser coverage for common business formats, NLP enrichment, and developer ergonomics.

## High-Level Flow

Client uploads a document ? ASP.NET ingest API stores it locally (or S3-compatible storage) and enqueues metadata in Redis ? .NET workers detect the content type, parse/convert via OSS providers, and call the spaCy microservice for entities + embeddings ? canonical records and vectors are upserted into Postgres/pgvector ? optional SMTP notification summarizes the results.

## Architecture

### Input Layer
- **REST API** (ASP.NET Core minimal API) handles file uploads and JSON metadata.
- Files land in `LocalStorageService` (disk) or an object store implementing `IStorageService`.
- `RedisJobQueue` captures job envelopes for background workers.

### Processing Layer
- `IngestWorker` pulls jobs, performs MIME/type detection, and orchestrates downstream providers.
- `DocumentProcessingPipeline` coordinates parsing, enrichment, embeddings, and persistence.
- spaCy microservice (FastAPI) delivers entity extraction, classification, sentiment, and 96-dim embeddings.

### Persistence Layer
- PostgreSQL with pgvector stores staging artifacts, canonical JSON, and embeddings.
- Redis retains transient work queues and optional DLQ streams.

### Output Layer
- `DocumentsController` exposes REST endpoints for status, canonical payloads, and similarity search.
- `SmtpNotificationService` sends summary emails once SMTP credentials are supplied.

## Provider Strategy

| Capability | Default OSS Provider | Roadmap Enhancements |
| --- | --- | --- |
| Document parsing | `PdfPigParser` (`OSS`) or `SimplePdfParser` (`SIMPLE`) | LibreOffice / Apache Tika bridge for Office formats |
| OCR | `SimpleOcrService` stub | Tesseract + OpenCV-based pipeline |
| NLP / embeddings | `SpacyNlpService` (microservice) | Larger spaCy transformer models, sentence-transformer embeddings |
| Notifications | `SmtpNotificationService` | Optional webhook/export providers |
| Storage | `LocalStorageService` | MinIO / S3-compatible implementation |

Provider choices are controlled by `ServicesOptions` in configuration so new integrations stay pluggable.

## Linux-Friendly Dependencies

- **Parsing:** PdfPig (iText7) today; LibreOffice (via `soffice --headless`) planned for DOCX/XLSX/PPTX.
- **OCR:** Tesseract CLI + language packs; optional `ocrmypdf` for PDF cleanup.
- **NLP:** spaCy microservice packaged with Docker, Redis worker for async tasks.
- **Media tooling:** `ffmpeg` + Whisper/faster-whisper for audio transcription when needed.

Package everything with Docker Compose for local dev, mirroring production Linux deployments.

## Implementation Checklist

1. Remove retired cloud-specific packages, config, and documentation (complete).
2. Harden PdfPig parser and add LibreOffice conversion worker for Office docs.
3. Replace `SimpleOcrService` stub with Tesseract-backed implementation.
4. Expand spaCy sidecar to expose sentence embeddings and richer entity metadata.
5. Wire MinIO/S3 storage provider into `IStorageService` alongside local disk.
6. Add automated ingestion tests that cover PDFs, DOCX, CSV, images, and archives.
7. Update docs and dashboards to highlight the OSS-first toolchain.

## Operational Notes

- Containerize supporting services (Redis, Postgres, spaCy, LibreOffice sidecars) for easy Linux deployment.
- Centralize logging with Serilog + Seq or OpenTelemetry exporters.
- Monitor queue depth, ingestion latency, and spaCy throughput to tune worker counts.
- Plan for configuration-driven feature flags when experimental providers (OCR, audio) roll out.

## Demo Script

1. Upload a scanned PDF and show ingestion succeeds with dedupe + queueing.
2. Highlight parsed text, entity extraction, and embeddings retrieved via `/api/documents/{id}`.
3. Trigger similarity search against stored documents to demonstrate pgvector integration.
4. Show health dashboards (API + spaCy) and optional SMTP notification output.

Delivering the above demonstrates an end-to-end OSS alternative to the previous cloud-exclusive pipeline while leaving ample room for future provider additions.
