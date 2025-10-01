# Phase 1: Decision Engine Implementation

## 🎯 Overview

The Decision Engine is an intelligent system that analyzes documents and generates optimal processing plans. It determines the best chunking strategy, embedding provider, and processing parameters based on document characteristics.

## 📦 Components

### 1. PipelinePlan Model (`Models/PipelinePlan.cs`)

The processing plan contains all decisions about how to process a document:

```csharp
public class PipelinePlan
{
    public string ChunkingStrategy { get; set; }    // "semantic", "fixed", "markdown", "paragraph"
    public int ChunkSize { get; set; }              // Characters per chunk
    public int ChunkOverlap { get; set; }           // Overlap between chunks
    public string EmbeddingProvider { get; set; }   // "sentence-transformers", "spacy", "openai"
    public string Language { get; set; }            // ISO 639-1 code
    public bool RequiresOCR { get; set; }           // OCR needed?
    public bool RequiresNER { get; set; }           // Named Entity Recognition?
    public bool UseReranking { get; set; }          // Use cross-encoder reranking?
    public string DocumentType { get; set; }        // "legal", "medical", "code", etc.
    public string Priority { get; set; }            // "low", "normal", "high", "critical"
    public decimal EstimatedCost { get; set; }      // Processing cost estimate
    public List<string> DecisionReasons { get; set; } // Audit trail of decisions
}
```

### 2. IDecisionEngine Interface (`Services/Pipeline/IDecisionEngine.cs`)

Core interface for generating processing plans:

```csharp
public interface IDecisionEngine
{
    Task<PipelinePlan> GeneratePlanAsync(
        string fileName,
        long fileSize,
        string mimeType,
        string? contentPreview = null,
        Dictionary<string, object>? metadata = null);
    
    Task<PipelinePlan> GeneratePlanForStagingAsync(StagingDocument document);
}
```

### 3. RuleBasedDecisionEngine (`Services/Pipeline/RuleBasedDecisionEngine.cs`)

Implementation using heuristic rules to make decisions.

## 🧠 Decision Rules

### Document Type Detection

| Pattern | Type | Action |
|---------|------|--------|
| `.cs`, `.js`, `.py` files | `code` | Semantic chunking, smaller chunks |
| `.md`, `.markdown` files | `markdown` | Markdown-aware chunking |
| Contains "WHEREAS", "hereinafter" | `legal` | Larger chunks for context |
| Contains "patient", "diagnosis" | `medical` | Medium chunks, high priority |
| Contains "API", "documentation" | `technical` | Semantic chunking |
| `.json`, `.xml`, `.yaml` | `structured` | Small fixed chunks |

### Chunking Strategy Selection

| Document Type | Strategy | Chunk Size | Overlap |
|---------------|----------|------------|---------|
| code | semantic | 800 | 100 |
| markdown | markdown | 1500 | 200 |
| legal | paragraph | 2000 | 300 |
| medical | paragraph | 1500 | 250 |
| technical | semantic | 1200 | 200 |
| structured | fixed | 500 | 50 |
| tabular | fixed | 1000 | 0 |
| general | fixed | 1000 | 200 |

### Embedding Provider Selection

Currently defaults to `sentence-transformers` for all document types (free, high quality, 768 dimensions).

**Future expansion:**
- Legal/Medical → OpenAI (highest accuracy, premium)
- Multilingual → Cohere (better language support)
- Large documents → spaCy (faster, 300-dim)

### Language Detection (Basic)

| Pattern | Language |
|---------|----------|
| Chinese characters (U+4E00 to U+9FFF) | zh |
| Cyrillic (U+0400 to U+04FF) | ru |
| Arabic (U+0600 to U+06FF) | ar |
| Japanese Hiragana (U+3040 to U+309F) | ja |
| Korean Hangul (U+AC00 to U+D7AF) | ko |
| Common Spanish words | es |
| Common French words | fr |
| Common German words | de |
| Default | en |

### Priority Assignment

| Condition | Priority |
|-----------|----------|
| File size < 50 KB | high |
| Document type: legal or medical | high |
| File size > 5 MB | low |
| Default | normal |

### OCR Detection

| Condition | Requires OCR |
|-----------|--------------|
| MIME type starts with `image/` | Yes |
| PDF files | No (determined later) |
| All others | No |

### Reranking Decision

Reranking is enabled when:
- File size > 100 KB AND
- Priority is "high" or "critical"

### Cost Estimation

| Component | Cost (units) |
|-----------|--------------|
| OCR Processing | 10.0 |
| OpenAI Embeddings | 5.0 |
| Cohere Embeddings | 3.0 |
| Sentence-Transformers | 0.1 |
| spaCy Embeddings | 0.05 |
| NER Processing | 2.0 |
| Reranking | 3.0 |

## 🔌 API Endpoints

### POST `/api/DecisionEngine/analyze`

Analyze a document and get a processing plan.

**Request:**
```json
{
  "fileName": "contract.pdf",
  "fileSize": 150000,
  "mimeType": "application/pdf",
  "contentPreview": "WHEREAS the parties agree...",
  "metadata": {
    "department": "legal"
  }
}
```

**Response:**
```json
{
  "chunkingStrategy": "paragraph",
  "chunkSize": 2000,
  "chunkOverlap": 300,
  "embeddingProvider": "sentence-transformers",
  "language": "en",
  "requiresOCR": false,
  "requiresNER": true,
  "useReranking": true,
  "documentType": "legal",
  "priority": "high",
  "estimatedCost": 2.1,
  "decisionReasons": [
    "Document type: legal",
    "Chunking: paragraph (size: 2000, overlap: 300)",
    "Embedding provider: sentence-transformers",
    "NER enabled",
    "Priority: high",
    "Reranking enabled for large/important document"
  ]
}
```

### GET `/api/DecisionEngine/test-cases`

Get predefined test cases for the decision engine.

### GET `/api/DecisionEngine/run-tests`

Run all test cases and return results.

## 🧪 Testing

### Run the test script:

```powershell
.\test-decision-engine.ps1
```

### Manual testing with curl:

```bash
# Test legal document
curl -X POST http://localhost:5082/api/DecisionEngine/analyze \
  -H "Content-Type: application/json" \
  -d '{
    "fileName": "contract.pdf",
    "fileSize": 150000,
    "mimeType": "application/pdf",
    "contentPreview": "WHEREAS the parties agree..."
  }'

# Test code file
curl -X POST http://localhost:5082/api/DecisionEngine/analyze \
  -H "Content-Type: application/json" \
  -d '{
    "fileName": "app.py",
    "fileSize": 30000,
    "mimeType": "text/x-python",
    "contentPreview": "from fastapi import FastAPI..."
  }'
```

## 📊 Example Outputs

### Legal Document
```
Document Type: legal
Chunking: paragraph (2000 chars, 300 overlap)
Embedding: sentence-transformers
Priority: high
Cost: 2.1 units
```

### Medical Record
```
Document Type: medical
Chunking: paragraph (1500 chars, 250 overlap)
Embedding: sentence-transformers
Priority: high
Cost: 2.1 units
```

### Code File
```
Document Type: code
Chunking: semantic (800 chars, 100 overlap)
Embedding: sentence-transformers
Priority: high (small file)
Cost: 0.1 units
```

### Large Document
```
Document Type: general
Chunking: fixed (1000 chars, 200 overlap)
Embedding: sentence-transformers
Priority: low (large file)
Cost: 0.1 units
```

## 🔄 Integration Points

The Decision Engine is designed to be integrated into:

1. **Document Upload Flow**
   ```csharp
   var plan = await _decisionEngine.GeneratePlanAsync(fileName, fileSize, mimeType);
   // Use plan to configure processing
   ```

2. **Staging Document Processing**
   ```csharp
   var plan = await _decisionEngine.GeneratePlanForStagingAsync(stagingDoc);
   // Apply plan to pipeline
   ```

3. **API Ingestion**
   ```csharp
   var plan = await _decisionEngine.GeneratePlanAsync(sourceFile, size, mime);
   // Use plan for external API data
   ```

## ✅ Phase 1 Complete

**What's implemented:**
- ✅ PipelinePlan model with all decision parameters
- ✅ IDecisionEngine interface
- ✅ RuleBasedDecisionEngine with 9 rule categories
- ✅ DecisionEngineController with test endpoints
- ✅ Test script for validation
- ✅ Service registration in Program.cs

**What's next (Phase 2):**
- Wire plan into DocumentProcessingPipeline
- Implement ProviderFactory for embedding services
- Update schema for chunks table
- Implement mean-of-chunks calculation

## 🎉 Success Criteria

Phase 1 is complete when:
- ✅ DecisionEngine generates plans for various document types
- ✅ Rules correctly identify document types
- ✅ Chunking strategies vary by document type
- ✅ Cost estimation reflects processing complexity
- ✅ Test endpoint returns reasonable plans
- ✅ All tests pass successfully

Run `.\test-decision-engine.ps1` to verify!
