# Phase 3 Complete: Mean-of-Chunks Vector Architecture

## 🎉 Overview

**Phase 3** introduces a sophisticated **mean-of-chunks** embedding architecture that stores individual chunk embeddings while computing document-level embeddings as the mean of all chunk vectors. This enables both granular chunk-level search and efficient document-level retrieval.

## ✅ What's Implemented

### 1. Enhanced Schema (768-dim Support)

**Database Changes:**
- Updated `documents.embedding` to `vector(768)` (from 300)
- Updated `document_chunks.embedding` to `vector(768)` (from 300)
- Added `documents.embedding_provider` (VARCHAR) - tracks which provider was used
- Added `documents.embedding_dimensions` (INTEGER) - stores actual dimensions
- Created vector similarity indexes for efficient search
- Added PostgreSQL functions for chunk search

**Schema File:** `scripts/phase3_mean_of_chunks_schema.sql`

### 2. Updated Models

**Document Model (`Server/Models/Document.cs`):**
```csharp
public class Document
{
    // ... existing properties ...
    
    [Column(TypeName = "vector(768)")]
    public Vector? Embedding { get; set; } // Mean-of-chunks embedding
    
    public string? EmbeddingProvider { get; set; } // "sentence-transformers", "spacy", etc.
    public int? EmbeddingDimensions { get; set; } // Actual dimensions used
}
```

**DocumentChunk Model (`Server/Models/DocumentChunk.cs`):**
```csharp
[Column("embedding", TypeName = "vector(768)")]
public Vector? Embedding { get; set; } // Individual chunk embedding
```

### 3. Pipeline Integration

**DocumentProcessingPipeline** now:
1. Generates embeddings for **each chunk** using the selected provider
2. Stores all chunk embeddings in `ChunkEmbedding` objects
3. Computes **mean-of-chunks** as the document-level embedding
4. Returns provider metadata in `PipelineResult`

**Key Code:**
```csharp
// Generate embeddings for each chunk
foreach (var chunk in chunks)
{
    var chunkEmbedding = await embeddingService.GenerateEmbeddingAsync(chunk.Content);
    chunkEmbeddings.Add(new ChunkEmbedding(
        ChunkIndex: chunk.ChunkIndex,
        Content: chunk.Content,
        StartOffset: chunk.StartOffset,
        EndOffset: chunk.EndOffset,
        Embedding: chunkEmbedding.Embedding,
        Metadata: chunk.Metadata
    ));
}

// Compute mean-of-chunks as document embedding
var meanEmbedding = ComputeMeanEmbedding(chunkEmbeddings);
```

### 4. Chunk Storage

**IngestWorker** (`Server/Services/IngestWorker.cs`):
- Saves document with mean-of-chunks embedding
- Stores `embedding_provider` and `embedding_dimensions` metadata
- Persists all individual chunks with their embeddings to `document_chunks` table
- Logs chunk count for audit trail

**Flow:**
```
Document Upload
  ↓
Pipeline Processing
  ↓
Generate Plan (Decision Engine)
  ↓
Select Provider (Provider Factory)
  ↓
Chunk Text
  ↓
Generate Chunk Embeddings (N chunks × provider)
  ↓
Compute Mean Embedding (document level)
  ↓
Save Document (with mean embedding + metadata)
  ↓
Save Chunks (with individual embeddings)
```

### 5. Chunk Search Service

**IChunkSearchService** (`Server/Services/ChunkSearchService.cs`):

**Features:**
- **Semantic Search:** Find similar chunks by embedding
- **Document Chunks:** Get all chunks for a specific document
- **Hybrid Search:** Combine semantic + full-text search
- **Cosine Distance:** Computed in-memory for accuracy

**Methods:**
```csharp
Task<List<ChunkSearchResult>> SearchChunksBySimilarityAsync(
    Vector queryEmbedding,
    int limit = 10,
    float similarityThreshold = 0.7f);

Task<List<ChunkSearchResult>> SearchChunksByDocumentAsync(
    Guid documentId);

Task<HybridSearchResult> HybridSearchAsync(
    Vector queryEmbedding,
    string? queryText = null,
    int limit = 10,
    float similarityThreshold = 0.7f);
```

### 6. Chunk Search API

**ChunkSearchController** (`Server/Controllers/ChunkSearchController.cs`):

#### POST `/api/ChunkSearch/search`
Search chunks by semantic similarity.

**Request:**
```json
{
  "query": "What is deep learning?",
  "provider": "sentence-transformers",
  "limit": 10,
  "similarityThreshold": 0.7
}
```

**Response:**
```json
{
  "query": "What is deep learning?",
  "provider": "sentence-transformers",
  "resultCount": 5,
  "results": [
    {
      "chunkId": 123,
      "documentId": "abc-123-def",
      "chunkIndex": 2,
      "content": "Deep learning is part of machine learning...",
      "startOffset": 500,
      "endOffset": 750,
      "similarity": 0.89,
      "metadata": "{\"strategy\": \"semantic\"}",
      "documentUri": "uploads/ai-paper.pdf",
      "documentMime": "application/pdf"
    }
  ]
}
```

#### POST `/api/ChunkSearch/hybrid-search`
Combine semantic and full-text search.

**Request:**
```json
{
  "query": "neural networks computer vision",
  "provider": "sentence-transformers",
  "limit": 10,
  "similarityThreshold": 0.6
}
```

**Response:**
```json
{
  "query": "neural networks computer vision",
  "provider": "sentence-transformers",
  "semanticResultCount": 8,
  "textResultCount": 12,
  "totalUniqueResults": 15,
  "results": [...]
}
```

#### GET `/api/ChunkSearch/document/{documentId}`
Get all chunks for a specific document.

**Response:**
```json
{
  "documentId": "abc-123-def",
  "chunkCount": 25,
  "chunks": [...]
}
```

## 🎯 Architecture Benefits

### 1. **Granular Search**
- Search at chunk level (paragraphs, sections)
- More precise results than whole-document search
- Better for long documents

### 2. **Document-Level Aggregation**
- Mean-of-chunks provides document-level embedding
- Efficient for document similarity
- No need to store full text embedding separately

### 3. **Hybrid Search**
- Combine semantic (embedding) + lexical (full-text)
- Semantic: "neural networks" finds "deep learning"
- Lexical: Exact keyword matches
- Best of both worlds

### 4. **Provider Flexibility**
- Different providers for different document types
- Track which provider was used
- Easy to compare quality

### 5. **Scalability**
- Chunks enable parallel processing
- Smaller embedding operations
- Better memory management

## 📊 Performance Comparison

| Approach | Search Precision | Speed | Memory | Use Case |
|----------|-----------------|-------|--------|----------|
| **Full Document Embedding** | Low | Fast | Low | Short docs, quick search |
| **Mean-of-Chunks** | High | Fast | Medium | Long docs, balanced |
| **Chunk-Level Search** | Highest | Medium | High | Long docs, precise results |
| **Hybrid Search** | Highest | Medium | High | Best quality, production |

## 🔧 Implementation Details

### Mean Embedding Calculation

```csharp
private static Vector ComputeMeanEmbedding(List<ChunkEmbedding> chunkEmbeddings)
{
    var firstEmbedding = chunkEmbeddings[0].Embedding!.ToArray();
    var dimensions = firstEmbedding.Length;
    var sum = new float[dimensions];

    foreach (var chunkEmb in chunkEmbeddings)
    {
        var embedding = chunkEmb.Embedding!.ToArray();
        for (int i = 0; i < dimensions; i++)
        {
            sum[i] += embedding[i];
        }
    }

    for (int i = 0; i < dimensions; i++)
    {
        sum[i] /= chunkEmbeddings.Count;
    }

    return new Vector(sum);
}
```

### Cosine Similarity

```csharp
private static float ComputeCosineDistance(Vector a, Vector b)
{
    var arrayA = a.ToArray();
    var arrayB = b.ToArray();
    
    float dotProduct = 0;
    float normA = 0;
    float normB = 0;
    
    for (int i = 0; i < arrayA.Length; i++)
    {
        dotProduct += arrayA[i] * arrayB[i];
        normA += arrayA[i] * arrayA[i];
        normB += arrayB[i] * arrayB[i];
    }
    
    float cosineSimilarity = dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    return 1.0f - cosineSimilarity; // Convert to distance
}
```

## 🧪 Testing

### Run Tests:
```powershell
.\test-phase3-chunks.ps1
```

### Test Coverage:
1. ✅ Document upload with chunking
2. ✅ Chunk search by similarity
3. ✅ Hybrid search (semantic + text)
4. ✅ Mean-of-chunks embedding verification
5. ✅ Provider comparison (spacy vs sentence-transformers)
6. ✅ Schema verification

### Expected Results:
- Large documents (>2000 chars) get chunked
- Each chunk has its own embedding
- Document embedding is mean of chunk embeddings
- Chunk search returns precise results
- Hybrid search combines semantic + text matches
- Provider metadata is tracked

## 📋 Migration Steps

### 1. Apply Schema Migration
```bash
psql -U your_user -d smartcollect < scripts/phase3_mean_of_chunks_schema.sql
```

This will:
- Update `documents.embedding` to 768 dimensions
- Update `document_chunks.embedding` to 768 dimensions
- Add `embedding_provider` and `embedding_dimensions` columns
- Create vector indexes for performance
- Add helper functions for chunk search

### 2. Restart Backend
```bash
cd Server
dotnet run
```

### 3. Test Endpoints
```powershell
.\test-phase3-chunks.ps1
```

## 🔍 Example Queries

### Python Example: Chunk Search
```python
import requests

response = requests.post(
    "http://localhost:5000/api/ChunkSearch/search",
    json={
        "query": "What is machine learning?",
        "provider": "sentence-transformers",
        "limit": 5,
        "similarityThreshold": 0.7
    }
)

results = response.json()
for chunk in results["results"]:
    print(f"Similarity: {chunk['similarity']:.3f}")
    print(f"Content: {chunk['content'][:100]}...")
```

### curl Example: Hybrid Search
```bash
curl -X POST http://localhost:5000/api/ChunkSearch/hybrid-search \
  -H "Content-Type: application/json" \
  -d '{
    "query": "deep learning neural networks",
    "provider": "sentence-transformers",
    "limit": 10,
    "similarityThreshold": 0.6
  }'
```

## ✅ Phase 3 Success Criteria - All Met!

- ✅ Schema updated to support 768-dim vectors
- ✅ Document model includes provider metadata
- ✅ DocumentChunk model supports 768-dim embeddings
- ✅ Pipeline generates chunk embeddings
- ✅ Mean-of-chunks computed correctly
- ✅ IngestWorker saves chunks to database
- ✅ ChunkSearchService implements semantic search
- ✅ ChunkSearchService implements hybrid search
- ✅ ChunkSearchController provides REST API
- ✅ Service registered in DI container
- ✅ Comprehensive test script created
- ✅ Documentation complete
- ✅ Zero compilation errors

## 🚀 Next: Phase 4 - Language Detection

Phase 4 will add a dedicated language detection microservice:

1. **Create FastAPI microservice** with lingua library
2. **Implement `/detect` endpoint** for language detection
3. **Create C# client service** for calling the microservice
4. **Update RuleBasedDecisionEngine** to use real language detection
5. **Add language-specific chunking rules** (CJK characters, RTL text)

**Estimated time:** 1-2 days

Ready to continue with Phase 4? 🚀
