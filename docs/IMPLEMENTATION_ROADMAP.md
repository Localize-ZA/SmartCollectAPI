# Decision Engine Implementation Roadmap

## 🗺️ Complete Implementation Plan

### Phase 1: Foundation ✅ COMPLETE
**Status:** 100% Complete  
**Duration:** Completed  
**Files Created:** 7 files, 1,022 lines of code

**Deliverables:**
- ✅ PipelinePlan model
- ✅ IDecisionEngine interface
- ✅ RuleBasedDecisionEngine (9 rule categories)
- ✅ DecisionEngineController with test endpoints
- ✅ Service registration
- ✅ Comprehensive documentation
- ✅ Test scripts

**Key Features:**
- Document type detection (8+ types)
- Chunking strategy selection (8 strategies)
- Language detection (8+ languages)
- Priority assignment
- Cost estimation
- Reranking decisions

---

### Phase 2: Integration & Provider Factory
**Status:** Ready to Start  
**Estimated Duration:** 3-4 days  

#### 2.1 ProviderFactory (Day 1)
**Goal:** Abstract embedding services for dynamic provider selection

**Files to Create:**
```
Server/Services/Embeddings/IEmbeddingProviderFactory.cs
Server/Services/Embeddings/EmbeddingProviderFactory.cs
Server/Services/Embeddings/IEmbeddingService.cs (common interface)
```

**Tasks:**
- [ ] Create IEmbeddingService interface (common abstraction)
- [ ] Update SentenceTransformerService to implement IEmbeddingService
- [ ] Update SpacyNlpService to implement IEmbeddingService
- [ ] Create IEmbeddingProviderFactory interface
- [ ] Implement EmbeddingProviderFactory with provider key lookup
- [ ] Register factory in Program.cs
- [ ] Add tests for provider resolution

**Expected Code:**
```csharp
public interface IEmbeddingService
{
    Task<float[]> EmbedAsync(string text);
    Task<List<float[]>> EmbedBatchAsync(List<string> texts);
    int DimensionCount { get; }
    string ProviderName { get; }
}

public interface IEmbeddingProviderFactory
{
    IEmbeddingService GetProvider(string providerKey);
    IEmbeddingService GetDefaultProvider();
}
```

#### 2.2 Pipeline Integration (Day 2-3)
**Goal:** Wire DecisionEngine into DocumentProcessingPipeline

**Files to Modify:**
```
Server/Services/IngestWorker.cs
Server/Services/DocumentProcessor.cs (if exists)
Server/Controllers/DocumentsController.cs
```

**Tasks:**
- [ ] Inject IDecisionEngine into IngestWorker
- [ ] Generate plan before processing staging documents
- [ ] Pass plan to chunking logic
- [ ] Pass plan.EmbeddingProvider to ProviderFactory
- [ ] Log plan decisions
- [ ] Add plan to document metadata
- [ ] Test end-to-end with different document types

**Expected Changes:**
```csharp
// In IngestWorker
var plan = await _decisionEngine.GeneratePlanForStagingAsync(stagingDoc);
_logger.LogInformation("Generated plan: {Strategy} chunking, {Provider} embeddings", 
    plan.ChunkingStrategy, plan.EmbeddingProvider);

// Apply plan to processing
var chunks = await _chunkingService.ChunkAsync(content, plan);
var embeddingService = _providerFactory.GetProvider(plan.EmbeddingProvider);
var embeddings = await embeddingService.EmbedBatchAsync(chunks);
```

---

### Phase 3: Mean-of-Chunks & Schema Update
**Status:** Pending Phase 2  
**Estimated Duration:** 3-4 days

#### 3.1 Schema Update (Day 1)
**Goal:** Add chunks table for granular embeddings

**Files to Create:**
```
Server/Models/DocumentChunk.cs
scripts/create_chunks_table.sql
scripts/migrate_to_chunks.ps1
```

**New Schema:**
```sql
CREATE TABLE document_chunks (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    document_id UUID NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
    chunk_index INT NOT NULL,
    content TEXT NOT NULL,
    embedding vector(768) NOT NULL,
    token_count INT,
    metadata JSONB,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(document_id, chunk_index)
);

CREATE INDEX idx_chunks_document ON document_chunks(document_id);
CREATE INDEX idx_chunks_embedding ON document_chunks USING ivfflat (embedding vector_cosine_ops);
```

**Tasks:**
- [ ] Create DocumentChunk model
- [ ] Add DbSet<DocumentChunk> to SmartCollectDbContext
- [ ] Create migration SQL script
- [ ] Update DocumentProcessingPipeline to save chunks
- [ ] Keep document embedding as mean-of-chunks

#### 3.2 Mean-of-Chunks Implementation (Day 2)
**Goal:** Calculate document vector as mean of chunk embeddings

**Files to Modify:**
```
Server/Services/IngestWorker.cs
Server/Services/VectorService.cs (if exists)
```

**Tasks:**
- [ ] Save individual chunk embeddings to document_chunks table
- [ ] Calculate mean vector from all chunk embeddings
- [ ] Store mean vector in documents.embedding
- [ ] Update search to use chunks table
- [ ] Implement hybrid search (chunks + documents)

**Expected Code:**
```csharp
// Calculate mean embedding
var chunkEmbeddings = await embeddingService.EmbedBatchAsync(chunks);

// Save chunks
for (int i = 0; i < chunks.Count; i++)
{
    var chunk = new DocumentChunk
    {
        DocumentId = document.Id,
        ChunkIndex = i,
        Content = chunks[i],
        Embedding = chunkEmbeddings[i]
    };
    await _db.DocumentChunks.AddAsync(chunk);
}

// Calculate document mean embedding
var meanEmbedding = CalculateMean(chunkEmbeddings);
document.Embedding = meanEmbedding;
```

#### 3.3 Hybrid Search (Day 3)
**Goal:** Search both chunks and documents

**Files to Create:**
```
Server/Controllers/SearchController.cs (if doesn't exist)
Server/Services/HybridSearchService.cs
```

**Tasks:**
- [ ] Implement chunk-level search
- [ ] Implement document-level search
- [ ] Combine results (chunks → parent documents)
- [ ] Add relevance scoring
- [ ] Test search quality

---

### Phase 4: Language Detection Microservice
**Status:** Pending Phase 3  
**Estimated Duration:** 1-2 days

**Files to Create:**
```
micros/language-detection/app.py
micros/language-detection/requirements.txt
micros/language-detection/README.md
Server/Services/LanguageDetectionService.cs
```

**Tasks:**
- [ ] Create FastAPI microservice
- [ ] Install lingua-language-detector
- [ ] Implement /detect endpoint
- [ ] Create C# client service
- [ ] Update DecisionEngine to use real language detection
- [ ] Add language-based rules (CJK chunking, etc.)
- [ ] Test with multilingual documents

**Expected API:**
```python
@app.post("/detect")
async def detect_language(request: DetectRequest):
    language = detector.detect_language_of(request.text)
    confidence = detector.compute_language_confidence(request.text, language)
    return {
        "language": language.iso_code_639_1.name.lower(),
        "confidence": confidence,
        "alternatives": get_top_3_languages(request.text)
    }
```

---

### Phase 5: Cross-Encoder Reranker
**Status:** Pending Phase 4  
**Estimated Duration:** 2-3 days

**Files to Create:**
```
micros/reranker/app.py
micros/reranker/requirements.txt
micros/reranker/README.md
Server/Services/RerankerService.cs
```

**Tasks:**
- [ ] Create FastAPI microservice
- [ ] Install sentence-transformers (CrossEncoder)
- [ ] Load cross-encoder/ms-marco-MiniLM-L-6-v2 model
- [ ] Implement /rerank endpoint
- [ ] Create C# client service
- [ ] Integrate into search pipeline
- [ ] Benchmark quality improvement
- [ ] Add A/B testing capability

**Expected API:**
```python
@app.post("/rerank")
async def rerank(request: RerankRequest):
    pairs = [(request.query, doc) for doc in request.documents]
    scores = model.predict(pairs)
    ranked = sorted(zip(request.documents, scores), key=lambda x: x[1], reverse=True)
    return [{"text": doc, "score": float(score)} for doc, score in ranked]
```

**Integration:**
```csharp
// Search pipeline
var candidateChunks = await _vectorSearch.SearchChunksAsync(query, limit: 100);
var rerankedChunks = await _rerankerService.RerankAsync(query, candidateChunks);
return rerankedChunks.Take(10);
```

---

## 📊 Overall Timeline

| Phase | Duration | Complexity | Priority |
|-------|----------|------------|----------|
| Phase 1: Foundation | ✅ Complete | Low | Highest |
| Phase 2: Integration | 3-4 days | Medium | Highest |
| Phase 3: Mean-of-Chunks | 3-4 days | Medium | High |
| Phase 4: Language Detection | 1-2 days | Low | Medium |
| Phase 5: Reranker | 2-3 days | Low-Medium | Medium |
| **TOTAL** | **~2 weeks** | **Manageable** | - |

## 🎯 Success Metrics

### Phase 2 Success:
- [ ] Different document types use different embedding providers
- [ ] Plans are logged and auditable
- [ ] End-to-end test with legal, code, and medical documents

### Phase 3 Success:
- [ ] Chunks stored with individual embeddings
- [ ] Document embedding = mean of chunks
- [ ] Chunk-level search returns relevant results
- [ ] Search quality improves by 20%+

### Phase 4 Success:
- [ ] Accurate language detection for 20+ languages
- [ ] CJK documents use appropriate chunking
- [ ] Language-specific provider selection works

### Phase 5 Success:
- [ ] Reranking improves search quality by 10%+
- [ ] Reranking adds <100ms latency
- [ ] A/B tests show measurable improvement

## 🚦 Current Status

**✅ PHASE 1 COMPLETE**

**Next Action:** Start Phase 2.1 - Create ProviderFactory

Ready to proceed? Say "continue" to start Phase 2! 🚀
