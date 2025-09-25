# Google-First Data Processing Pipeline Blueprint

## 🎯 Project Overview & Constraints

Perfect, that changes the game entirely — 6 hours, hackathon, Google sponsor means the architecture must:

- **Lean heavily on Google Cloud tools** (to score points).
- **Be demoable fast** with an end-to-end pipeline (ingest → process → Gmail).
- **Still look scalable** on the whiteboard pitch so judges see the long-term potential.

**Hackathon window:** 6 hours end-to-end build from scaffold to demo.
**Sponsor priority:** highlight Google Cloud tooling wherever possible.
**Core stack:** .NET 8 (Minimal API + worker services) with Redis, Postgres, pgvector.
**Mandatory flow:** ingest → detect → parse → normalize → enrich → vectorize → persist → Gmail export.
**Demo deliverable:** end-to-end run that lands a polished email in the judges' inbox.

## 🔄 High Level Flow (Single Sentence)

Client uploads file or stream → ASP.NET ingest API stores raw to GCS (or local store) and enqueues a Redis job → .NET Worker pulls job → type detection → parsing (structured stays structured; unstructured → text) → Document AI / Vision / NL / Vertex embeddings applied → normalize into standard schema → stage row in Postgres staging table → write final record + embedding into Postgres+pgvector → trigger Gmail export → mark job complete (or DLQ/retry on failure).

## 📋 Lifecycle Snapshot
1. Client uploads a file or stream into the ingest API.
2. API stores the raw payload (GCS or local) and enqueues a Redis job containing metadata.
3. Worker pulls the job, performs type detection, and dispatches to the appropriate parser.
4. Document AI, Vision AI, or native parsers extract text, structure, and metadata.
5. Natural Language API derives entities; Vertex AI produces embeddings.
6. Canonical results + vectors are written to Postgres with pgvector.
7. Gmail API assembles and sends the notification email with structured summary + attachments.

## 🏗️ Architecture (Hackathon-Ready)

### Input Layer
- **REST API** (ASP.NET Core minimal API).
- Accepts files + streams.
- Pushes raw payload into Redis (queue) for processing.
- ASP.NET Core Minimal API endpoint for multipart uploads, JSON bodies, and streamed payloads.
- Persists raw artifacts to Google Cloud Storage (`gs://uploads/{jobId}`) with a SHA-256 checksum for dedupe.
- Enqueues job metadata (URI, MIME, checksum, timestamp, notifier) to Redis Streams/Lists.

### Processing Layer
- **Worker service in .NET** that pulls from Redis.
- Detects type (JSON, XML, CSV, PDF, Word, Excel).
- JSON/XML/CSV: parse and preserve hierarchy.
- PDF/Word: use Google Document AI (OCR + layout + entity extraction).
- Images in docs/PDF: Google Vision AI (OCR, labeling).
- Hosted worker service in .NET subscribed to the Redis queue.
- Idempotency check: skip processing when checksum already exists in Postgres.
- MIME/type detection via request hints + magic-byte sniffing (first 4 KB).
- Dispatch rules:
  - JSON/XML/CSV → native parsing (`System.Text.Json`, `XDocument`, `CsvHelper`).
  - PDF/Word → Google Document AI for text, layout, tables.
  - Embedded images → Google Vision AI for OCR + labeling.
- Normalize all outputs into a canonical schema while maintaining hierarchy.

### Entity Extraction
For unstructured text: call Google Cloud Natural Language API to extract entities, syntax, sentiment.

Normalize into a standard schema:
```json
{
  "source": "filename.pdf",
  "content_type": "pdf",
  "extracted_text": "...",
  "entities": [...],
  "structured_data": {...},
  "vector": [...]
}
```

- Forward consolidated text to Google Cloud Natural Language API.
- Capture entity `name`, `type`, `salience`, `mentions`, and offsets.
- Optional enrichment step to canonicalize entities with domain lookup tables.

### Vectorization
- Use Google GenAI embeddings via Vertex AI.
- Store vectors + normalized schema into Postgres + pgvector.
- Cache intermediate steps in a dedicated Postgres table.

### Export Layer
- Once structured data is finalized, trigger Gmail API:
- Send an email with structured summary + attachments.
- Judges will see raw-to-clean transformation in their inbox.
- Primary embeddings provider: Vertex AI text embeddings (pluggable interface for Hugging Face fallback).
- Chunk long documents prior to embedding to respect model token limits.
- Persist results to Postgres:
  - `staging_documents` for in-flight metadata, retries, and audit history.
  - `documents` with canonical JSONB and `vector(1536)` column (pgvector).
- Cache intermediate API responses for auditability and replay.

## 🛠️ Tech Stack (Google-First + .NET)

- **.NET 8** Minimal API → ingestion + orchestration.
- **Redis** → queue for active processing.
- **Postgres + pgvector** → structured + vector storage.
- **Google Cloud:**
  - Document AI → PDF/Word parsing (text + formatting + OCR).
  - Vision AI → image OCR.
  - Natural Language AI → entity extraction.
  - Vertex AI embeddings → vectors.
  - Gmail API → export.

## ⏱️ Development Strategy (6 hours)

- **Hour 1** – Scaffold .NET API + Redis queue. File upload endpoint.
- **Hour 2** – Implement type detection + JSON/XML/CSV parsers.
- **Hour 3** – Wire Google Document AI + Vision AI for PDFs/Word.
- **Hour 4** – Connect Natural Language API + embeddings → pgvector.
- **Hour 5** – Build Gmail export (send processed results).
- **Hour 6** – Demo polish: dashboard or CLI to show ingestion → Gmail mail arrives.

## 🎬 Demo Flow for Judges

1. Upload a messy PDF or CSV through your API.
2. Redis kicks off pipeline → Document AI/Vision AI → entities extracted → vectors generated.
3. Within seconds, Gmail inbox shows:
   - Original doc attached.
   - Structured JSON summary in body.
   - Entities and key metadata clearly presented.

They'll see: **raw chaos in, structured intelligence out, emailed neatly via Google.**

Here's the fork:
- I can map out the exact .NET libraries, Google SDKs, and Redis schema so you just copy/paste skeleton code during hackathon.
- Or I can give you a lightweight blueprint deck outline (for presentation) that frames it as scalable Google-native ingestion.

Do you want me to focus your 6-hour prep on **code scaffolding** (so you can sprint-build), or on **pitch framing** (so judges see the bigger picture)?

## 📋 Components & Responsibilities

### Ingress (API Gateway)
ASP.NET 8 Minimal API handling:
- Multipart file uploads
- Bare JSON/stream POSTs
- Webhook endpoints for streamed sources
- Stores raw payload to Google Cloud Storage (GCS) or local disk (hackathon fallback).
- Enqueues a job (Redis list / stream) with metadata + storage URI.

### Queue / Orchestrator
- Redis Streams or Redis Lists (StackExchange.Redis).
- Job format is small (uri, mime, upload_ts, md5/sha256, request_id).

### Processing Worker(s)
- .NET background service(s) (Dockerizable).
- Pull job → idempotency check → perform detection → call parsers/Google APIs → normalize → vectorize → persist → notify.

### Parsers / Feature Extractors
- **JSON/XML/CSV:** native parse in .NET (System.Text.Json, XmlDocument/XDocument, CsvHelper) — preserve hierarchy.
- **Office / PDF:** Google Document AI (preferred), fallback to Tika or Apache POI via an external utility.
- **Images:** Google Vision API (OCR + labeling).
- **Metadata extraction:** file name, pages, languages, tables, attachments, embedded images.

### NLP / Entity Extraction
- Google Natural Language API for entities. Optionally supplement with Vertex generative prompts for structured extraction.

### Embedding Service
- **Primary:** Vertex AI embeddings (Google) — for highest sponsor points and simplified quotas.
- **Optional/backup:** Hugging Face API or local GenKit model via ONNX (if you want HF bragging rights). Make embedding provider pluggable.

### Storage
**Postgres (main):** two tables:
- `staging_documents` — temporary (raw → normalized), status column
- `documents` — final canonical JSONB + embedding vector(dim) via pgvector

**Redis** — queue + short TTL cache for in-flight processing.

### Export
- Gmail API (OAuth2) to send summary + attachments to judges/demo accounts.

### Observability
- Cloud Logging (Stackdriver) or local console logs; metrics via Prometheus/Cloud Monitoring if time allows.

## 📄 Data Contracts / Schemas

### Job Envelope (Redis payload)
```json
{
  "job_id": "uuid4",
  "source_uri": "gs://my-bucket/uploads/abc.pdf",
  "mime_type": "application/pdf",
  "sha256": "deadbeef..",
  "received_at": "2025-09-24T18:12:34+02:00",
  "origin": "web",          // or "webhook", "api"
  "notify_email": "demo@you.com"
}
```

### Standard Normalized Schema (Postgres JSONB)
```json
{
  "id": "uuid4",
  "source_uri":"gs://.../file.pdf",
  "ingest_ts":"2025-09-24T18:12:34+02:00",
  "mime":"application/pdf",
  "structured": false,
  "structured_payload": null,
  "extracted_text": "full extracted text for vectorization and search",
  "entities": [
    {"name":"Acme Corp","type":"ORGANIZATION","salience":0.96},
    {"name":"John Doe","type":"PERSON","salience":0.12}
  ],
  "tables":[ /* optional table extractions */ ],
  "sections":[ /* layout-preserved sections if available */ ],
  "embedding_dim":1536,
  "embedding":[0.00123, -0.2334, ...],
  "processing_status":"processed",
  "processing_errors": null,
  "schema_version": "v1"
}
```

### Postgres Tables (DDL sketch)
```sql
CREATE TABLE staging_documents (
  id uuid PRIMARY KEY,
  job_id text,
  source_uri text,
  mime text,
  sha256 text,
  raw_metadata jsonb,
  normalized jsonb,
  status text, -- pending, processing, failed, done
  attempts int default 0,
  created_at timestamptz default now()
);

-- Requires pgvector extension
CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE documents (
  id uuid PRIMARY KEY,
  source_uri text,
  mime text,
  sha256 text UNIQUE,
  canonical jsonb,
  created_at timestamptz default now(),
  updated_at timestamptz default now(),
  embedding vector(1536)
);
```

**Note:** `embedding vector(1536)` dimension is configurable; pick 1536 as a safe default (changeable to match Vertex/HF model).

## 🔄 Processing Flow (Detailed Step-by-Step)

### 1. Ingest
- Client POSTs file or stream to `/api/ingest`.
- Server writes file to GCS `uploads/{jobId}` and computes SHA256 hash.
- If sha256 already in documents (dedupe), short-circuit: return existing doc id and optionally re-trigger export.
- Create job envelope and push to Redis stream: `XADD ingest-stream * job_id ...`

### 2. Worker Picks Up Job
- Mark `staging_documents.status = 'processing'`, insert job meta.
- Read MIME type from stored file or HTTP header. If unknown, run a quick sniffer (first 4KB).

### 3. Type Detection
- If `application/json` → parse as JSON.
- If `text/csv` → parse CSV (CsvHelper) and convert to JSON preserving nested rows/tables.
- If `application/xml` or `text/xml` or `< hint` → parse XML with XDocument and convert to hierarchical JSON.
- If `application/pdf`, `application/msword`, `application/vnd.openxmlformats-officedocument.*` → route to Document AI.
- If `image/*` → Vision API OCR.

### 4. Parsing & Normalization

#### Structured (JSON/XML/CSV):
- Convert to canonical JSON with preserved hierarchy (i.e., no aggressive flattening).
- Extract top-level fields that map to standard schema: title, author, date, table-of-contents if present.
- Keep `structured: true` and store `structured_payload` with JSONB.

#### Unstructured (free text from docs/PDF/images):
- Use Document AI to return text, pages, layout, tables, entities (if the chosen processor supports this).
- If Document AI not available, fallback to Vision OCR → plain text.
- Clean text (normalize whitespace, remove invisible chars), detect language.

### 5. Entity Extraction & Enrichment
- Call Google Natural Language on `extracted_text` (or pass Document AI entities).
- Produce normalized list of entities with type, salience, mentions, span_offsets.
- Optionally map or canonicalize entities using a small lookup table (e.g., normalize country names).

### 6. Vectorization
- Send `text_to_embed` to Vertex AI embeddings endpoint.
- `text_to_embed = [concatenate metadata like title + extracted_text truncated to N tokens]` or break into chunks if very long (chunk size tied to embedding model limit).
- Receive embedding vector(s).

**Strategy:**
- **Short docs** → single embedding stored in `documents.embedding`.
- **Long docs** → chunk & store several embeddings in a related table `document_chunks` with `doc_id, chunk_index, embedding vector(1536)` to support granular retrieval.

### 7. Persist
- Save canonical JSON to `documents.canonical` (JSONB) and embedding to `embedding`.
- Upsert by sha256 to ensure idempotency.

### 8. Export / Notification
- Construct a concise email summary:
  - **Subject:** `Ingested: {filename} — {top entities}`
  - **Body:** structured JSON summary + top 5 entities + top 5 extracted key values.
  - **Attach** canonical JSON as .json or CSV.
- Use Gmail API (via OAuth2 refresh token) to send mail to `notify_email`.
- Log email send result in DB.

### 9. Finishing
- Update `staging_documents.status = 'done'` or `failed` with `processing_errors`.
- Acknowledge Redis message (if using streams consumer groups).

## ⚠️ Failure Modes & Mitigation

### API Quotas / Rate Limits (Document AI, Vertex)
- Implement client-side rate limiter and exponential backoff for 429/5xx responses.
- Bulkify requests where possible (batch embedding calls).

### Poison Messages
- Track attempts; after N retries (e.g., 5), move job to DLQ queue `ingest-dlq` and notify developer email.

### Large Files / Timeouts
- Offload heavy processing to worker pool; keep API ingest lightweight (store and ack).
- Use chunking for large docs (paginate through Document AI).

### Duplicate Ingestion
- Dedupe by SHA256; use DB unique constraint to ensure single canonical record.

### Partial Failures (embedding OK but entity extraction fails)
- Persist what succeeded; mark `processing_errors` and allow later re-run of failed stages.

### Auth Failures for Gmail
- Graceful fallback: if Gmail fails, queue an email send retry and optionally store the export file in GCS with a link.

## 🔄 Idempotency, Dedupe, and Versioning

- **SHA256 content hash** always computed at ingest. Use as canonical dedupe key.
- **Job idempotency token** on HTTP upload to prevent double-submit.
- **Schema versioning** field in canonical JSON (v1, v2...). Keep backward compatibility by migrating or adding transforms when schema changes.
- **Audit trail** in `staging_documents` with attempts and `processing_errors`.

## 🔧 Detailed Implementation Notes

### Worker Process Management
- Use .NET's `IHostedService` or `BackgroundService` for Redis consumer.
- Implement graceful shutdown with cancellation tokens.
- Consider worker pool scaling based on queue depth.

### Redis Configuration
- Use Redis Streams for better reliability and consumer group support.
- Set appropriate TTL for completed jobs.
- Configure persistence for queue durability.

### Google Cloud API Integration
- Use service account keys or workload identity for authentication.
- Implement retry policies with exponential backoff.
- Cache API responses where appropriate (Document AI results).

### Database Optimization
- Index on `sha256` for fast dedupe lookups.
- Consider partitioning `staging_documents` by date for cleanup.
- Use connection pooling for high-throughput scenarios.

### Monitoring & Alerting
- Track queue depth, processing times, error rates.
- Set up alerts for failed jobs in DLQ.
- Monitor API quota usage for Google services.

## 🎯 End-to-End Testing Strategy

### Unit Tests
- Test individual parsers (JSON, XML, CSV).
- Mock Google API responses for entity extraction.
- Validate schema normalization logic.

### Integration Tests
- Test full pipeline with sample documents.
- Verify Gmail API integration with test accounts.
- Test error handling and retry mechanisms.

### Load Testing
- Simulate concurrent file uploads.
- Test worker scaling under load.
- Validate database performance with large documents.

## 🚀 Deployment Considerations

### Local Development
- Use Docker Compose for Redis and Postgres.
- Local file storage fallback for GCS.
- Mock Google APIs during development.

### Production Deployment
- Container orchestration (Kubernetes or Docker Swarm).
- Managed services for Redis and Postgres where possible.
- Proper secret management for API keys.
- Health checks and readiness probes.

## 📈 Scalability Roadmap

### Immediate Enhancements
- Multi-tenant support with workspace isolation.
- Role-based access control and user management.
- Web dashboard for job monitoring and document search.

### Medium-term Features
- Batch processing for large document sets.
- Webhook notifications for external systems.
- Document versioning and change tracking.

### Long-term Vision
- Machine learning pipeline for custom entity models.
- Advanced analytics and document insights.
- Integration with enterprise document management systems.

## 💡 Alternative Architecture Patterns

### Event-Driven Architecture
- Replace Redis with Google Cloud Pub/Sub.
- Use Cloud Functions for serverless processing.
- Event sourcing for complete audit trail.

### Microservices Approach
- Separate services for ingestion, processing, and export.
- API Gateway for service orchestration.
- Independent scaling of components.

### Hybrid Cloud Strategy
- Multi-cloud deployment for redundancy.
- Edge processing for latency-sensitive operations.
- Cost optimization across cloud providers.

## Preferred Tech Stack
- **Backend:** .NET 8 Minimal API, Worker Service (HostedService pattern).
- **Queue:** Redis (StackExchange.Redis) with DLQ stream for poison jobs.
- **Database:** Postgres 15+ with `pgvector`; optional materialized views for analytics.
- **Datastore Warehouse:** DuckDB for analystics, opensource.
- **Google Cloud Services:** Document AI, Vision AI, Natural Language AI, Vertex AI embeddings, Gmail API.
- **Observability:** Serilog + Cloud Logging; optional Prometheus export for metrics.

## Architecture Addenda — Providers, Export/Retry, Enrichment, Schema

This section refines the blueprint with (1) a provider strategy that balances Google-first with OSS fallbacks, (2) an export layer with REST + GraphQL and a non-blocking retry model, (3) enrichment workers that run post-ingestion, and (4) a stronger relational schema.

### 1) Google Reliance vs Reality (Pluggable Providers)

Principle: Every Google API call must sit behind a small interface with a local/OSS fallback. In “Hackathon” mode we default to Google providers; in “OSS” mode we run locally with sidecars or libraries.

Provider matrix (Google-first with OSS fallback):

- Document parsing
  - Google: Document AI (PDF/Word layout + OCR)
  - Fallbacks: Apache Tika Server, PyMuPDF/PdfPig, Apache POI for Office
- OCR (images or when doc text is image-based)
  - Google: Vision API (OCR, labels)
  - Fallback: Tesseract (native or container sidecar)
- Embeddings
  - Google: Vertex AI embeddings
  - Fallback: HuggingFace SentenceTransformers (e.g., all-MiniLM-L6-v2) via local HTTP server or hosted inference
- Notifications
  - Google: Gmail API
  - Fallback: SMTP or generic webhook exporter

Interfaces (example names; keep minimal contracts):
- IAdvancedDocumentParser → GoogleDocAiParser | TikaParser/PdfParser
- IOcrService → GoogleVisionOcrService | TesseractOcrService
- IEmbeddingService → VertexEmbeddingService | HFSentenceTransformerService
- INotificationService → GmailNotificationService | SmtpNotificationService/WebhookNotificationService

Configuration (select providers by environment):

```jsonc
// appsettings.json
{
  "Services": {
    "Parser": "Google",       // Google | OSS
    "OCR": "Google",          // Google | OSS
    "Embeddings": "Google",   // Google | OSS
    "Notifications": "Google"  // Google | OSS
  }
}
```

Notes:
- For the hackathon demo: default to Google across the board; the OSS providers remain wired and switchable without code changes.
- Log which provider handled each stage for traceability.

Google usage map (where Google tools are applied):
- Parsing of PDFs/Office → Document AI
- OCR of images/embedded scans → Vision API
- Entity extraction → Natural Language API
- Embeddings → Vertex AI embeddings
- Export/notification → Gmail API

### 2) Export Layer (REST + GraphQL + Retry without bottlenecks)

Surface:
- REST: Great for external integrations (stable endpoints)
- GraphQL: Great for dashboards and selective data fetching (only what consumers need)

Retry model (no tight loops, no bottlenecks):
- On export failure: set status=failed_export, compute exponential backoff, enqueue into export retry schedule with retry_at.
- Scheduling store:
  - Redis sorted set: key `export:retry`, score = retry_at (epoch ms)
  - Or a Postgres table with an index on retry_at
- Worker behavior:
  - Polls only jobs with retry_at <= now (ZRANGEBYSCORE or SELECT ... WHERE retry_at <= now())
  - On failure: increment attempt, compute backoff, re-enqueue
  - After N attempts: move to DLQ (`export:dlq` in Redis or a dedicated table) for manual intervention
- Isolation: Failed items never block healthy ones; no global locks; no retry-until-success loops.

Observability:
- Metrics: success/failure counts, DLQ size, average retry age
- Logs: export job id, attempt, chosen provider, error summary

GraphQL note:
- Add a GraphQL endpoint (e.g., Hot Chocolate) at `/graphql` with types for Document, Entity, Chunk, Provenance to power the dashboard.

### 3) Enrichment Workers (async, post-ingestion)

Purpose: Link and normalize entities after ingestion without blocking the ingest/export pipeline.

Flow:
- Trigger: document normalized and stored
- Extract entities from canonical JSON (or NL output)
- Resolve/normalize against domain registries (business, government, user accounts)
- Upsert entities and write `document_entities` linking rows (with salience/mentions/spans)
- Mark link_status on the document (resolved | needs_review) for ambiguous items

Properties:
- Idempotent and resumable; safe to re-run
- Backed by a small queue or DB poll of `link_status = 'pending'`
- Can feed `supply_chain_links` when relationships are identified

### 4) Schema Redesign (recommended)

Core relational tables to replace the too-flat staging/documents approach while preserving a staging view during transition:

```sql
CREATE TABLE documents (
  id UUID PRIMARY KEY,
  source_uri TEXT NOT NULL,
  domain_type TEXT NOT NULL,  -- 'government', 'business', 'user'
  mime TEXT,
  sha256 TEXT UNIQUE,
  canonical JSONB NOT NULL,
  ingest_ts TIMESTAMPTZ DEFAULT NOW(),
  updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE document_chunks (
  id UUID PRIMARY KEY,
  document_id UUID REFERENCES documents(id),
  chunk_index INT,
  text TEXT,
  embedding VECTOR(1536)
);

CREATE TABLE entities (
  id UUID PRIMARY KEY,
  name TEXT,
  type TEXT, -- PERSON, ORG, LOCATION, PRODUCT, etc.
  normalized_name TEXT,
  external_ref TEXT -- e.g. gov registry ID, company reg no.
);

CREATE TABLE document_entities (
  document_id UUID REFERENCES documents(id),
  entity_id UUID REFERENCES entities(id),
  salience FLOAT,
  mention_text TEXT,
  span JSONB,
  PRIMARY KEY (document_id, entity_id)
);

CREATE TABLE supply_chain_links (
  id UUID PRIMARY KEY,
  from_entity UUID REFERENCES entities(id),
  to_entity UUID REFERENCES entities(id),
  relationship TEXT, -- supplier, regulator, complainant, etc.
  source_document UUID REFERENCES documents(id)
);

CREATE TABLE provenance (
  id UUID PRIMARY KEY,
  document_id UUID REFERENCES documents(id),
  source_type TEXT,  -- 'api', 'upload', 'scrape'
  owner TEXT,
  pipeline_version TEXT,
  notes TEXT
);
```

Rationale:
- Domain separation: `documents.domain_type` keeps government/business/user clean while still joinable
- Entity relationships: `document_entities` + `supply_chain_links` provide relational power
- Unlinked data: enrichment writers update `document_entities`; unresolved items remain visible for manual review
- Chunk embeddings: first-class support via `document_chunks`
- Lineage: `provenance` records origins and pipeline versions

## Six-Hour Execution Plan
1. **Hour 1 — Ingestion Foundation**
   - Scaffold Minimal API, health endpoint, file upload handler, storage helper.
   - Compute SHA-256, enqueue Redis job metadata.
2. **Hour 2 — Structured Parsers**
   - Implement JSON/XML/CSV handlers with normalization into canonical DTOs.
   - Validate schema mapping to Postgres JSONB.
3. **Hour 3 — Document AI & Vision Integration**
   - Wire Document AI processors for PDF/Word; handle multi-page responses.
   - Integrate Vision AI OCR for embedded images.
4. **Hour 4 — NLP & Embeddings**
   - Call Natural Language API for entities, sentiment (optional), syntax.
   - Add Vertex AI embeddings client; store vectors in `vector(1536)` column.
5. **Hour 5 — Persistence & Gmail Export**
   - Finalize Postgres writes (staging + canonical tables, upsert by checksum).
   - Implement Gmail export with OAuth token cache + retry wrapper.
6. **Hour 6 — Demo Polish**
   - Build a lightweight dashboard/CLI to display ingest status and sent emails.
   - Add logging, DLQ monitor, and rehearse the end-to-end demo flow.

## Judge Demo Flow
1. Upload a messy PDF or CSV through the API/UI.
2. Dashboard shows the job entering and exiting the queue quickly.
3. Pipeline executes Document AI → Vision AI → NL → embeddings → Postgres.
4. Gmail inbox receives an email with structured summary, top entities, attachments.
5. Judges see raw chaos transformed into structured intelligence in real time.

## Component Responsibilities
- **API Gateway:** Validate requests, store artifacts, compute checksum, enqueue job.
- **Worker Service:** Orchestrate parsing, enrichment, persistence, and notification.
- **Parsers:** Convert raw input into canonical schema while preserving hierarchy.
- **Embedding Service:** Provider-agnostic interface with batching and rate limiting.
- **Notifier:** Gmail exporter with exponential backoff and DLQ fallback.

## Data Contracts & Schemas
### Redis Job Envelope
```json
{
  "job_id": "UUID",
  "source_uri": "gs://uploads/abc.pdf",
  "mime_type": "application/pdf",
  "sha256": "deadbeef...",
  "received_at": "2025-09-24T18:12:34Z",
  "origin": "web",
  "notify_email": "demo@team.com"
}
```

### Canonical Document Schema (`documents.canonical`)
```json
{
  "id": "UUID",
  "source_uri": "gs://uploads/abc.pdf",
  "ingest_ts": "2025-09-24T18:12:34Z",
  "mime": "application/pdf",
  "structured": false,
  "structured_payload": null,
  "extracted_text": "...",
  "entities": [
    {"name": "Acme Corp", "type": "ORGANIZATION", "salience": 0.96}
  ],
  "tables": [],
  "sections": [],
  "embedding_dim": 1536,
  "embedding_strategy": "vertex-ai/text-embedding",
  "processing_status": "processed",
  "processing_errors": null,
  "schema_version": "v1"
}
```

### Core Tables (DDL Sketch)
```sql
CREATE TABLE staging_documents (
  id UUID PRIMARY KEY,
  job_id TEXT NOT NULL,
  source_uri TEXT NOT NULL,
  mime TEXT,
  sha256 TEXT,
  raw_metadata JSONB,
  normalized JSONB,
  status TEXT,
  attempts INT DEFAULT 0,
  created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE EXTENSION IF NOT EXISTS vector;

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

## Processing Pipeline (Detailed)
1. **Ingest & Queue** — Validate payload, store raw artifact, compute checksum, enqueue job.
2. **Stage Record** — Insert into `staging_documents` with status `pending` and raw metadata.
3. **Type Detection** — Branch logic based on MIME; fall back to heuristics for unknown types.
4. **Parsing & Normalization** — Structured data stays hierarchical; unstructured data yields cleaned text + layout metadata.
5. **Entity Extraction** — Call NL API; merge Document AI native entities when present.
6. **Vectorization** — Chunk text as needed, batch embed, persist chunk embeddings if applicable.
7. **Persistence** — Upsert canonical JSON + embeddings; mark staging status `done`.
8. **Export** — Compose Gmail message, attach assets, log response; on failure set status `failed` with errors.
9. **Cleanup** — Acknowledge Redis message, rotate/delete raw file after TTL or archive.

## Failure Modes & Mitigation
- **API quotas / 429s:** Apply client-side rate limiting and exponential backoff; batch embedding requests.
- **Poison messages:** Track attempts; move to `ingest-dlq` after N retries and alert team.
- **Large files / timeouts:** Stream uploads, chunk Document AI requests, and parallelize parsing.
- **Duplicate ingestion:** Enforce `UNIQUE(sha256)` and short-circuit replays with existing document reference.
- **Partial failures:** Persist successful stages, log granular errors, enable targeted replays for failed steps.
- **Auth failures (Gmail):** Store export payload in GCS and schedule retries; surface via monitoring alerts.

## Idempotency, Dedupe & Versioning
- Compute SHA-256 on ingest and use as the canonical dedupe key.
- Tag each pipeline stage with idempotency keys to avoid duplicate writes on retries.
- Include `schema_version` in canonical JSON; provide migration scripts for future changes.
- Maintain audit history in `staging_documents` (`status`, `attempts`, `processing_errors`).

## Optional Enhancements
- **Dashboard:** Simple web UI showing queue status, processing metrics, and email history.
- **Semantic search:** Demonstrate pgvector similarity for rapid retrieval.
- **Chunk storage:** Add `document_chunks` table with per-chunk embeddings for long-form documents.
- **Monitoring:** Prometheus exporters + Grafana dashboard or Cloud Monitoring alerts.
- **Roadmap extensions:** Downstream CRM webhooks, BigQuery sync, AutoML for domain-specific entity models.

## Implementation Forks
- **Code skeleton track:** Provide ready-to-run .NET templates (controllers, workers, Google API clients).
- **Pitch track:** Build a concise deck emphasizing scalability, cost control, compliance roadmap.
- Select the track that maximizes hackathon scoring or split responsibilities across the team.