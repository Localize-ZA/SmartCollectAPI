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

### Phase 2: Integration & Provider Factory ✅ COMPLETE
**Status:** 100% Complete  
**Duration:** Completed  
**Files Created/Modified:** 5 files

#### 2.1 ProviderFactory ✅ COMPLETE
**Goal:** Abstract embedding services for dynamic provider selection

**Files Created:**
```
Server/Services/Providers/IEmbeddingProviderFactory.cs
Server/Services/Providers/EmbeddingProviderFactory.cs
Server/Controllers/DecisionEngineController.cs (3 new endpoints)
test-provider-factory.ps1
docs/PHASE2_1_PROVIDER_FACTORY.md
```

**Completed Tasks:**
- ✅ Create IEmbeddingProviderFactory interface (4 methods)
- ✅ Implement EmbeddingProviderFactory with provider key lookup
- ✅ Support sentence-transformers (768-dim) and spaCy (300-dim)
- ✅ Register factory in Program.cs
- ✅ Add 3 test endpoints to DecisionEngineController
- ✅ Create comprehensive test script (6 scenarios)
- ✅ Documentation complete

**Key Features:**
- Provider resolution by key
- Default provider fallback
- Case-insensitive matching
- Easy extensibility for new providers (OpenAI, Cohere)
- Performance comparison endpoint

#### 2.2 Pipeline Integration ✅ COMPLETE
**Goal:** Wire DecisionEngine into DocumentProcessingPipeline

**Files Modified:**
```
Server/Services/DocumentProcessingPipeline.cs
test-pipeline-integration.ps1
docs/PHASE2_2_PIPELINE_INTEGRATION.md
```

**Completed Tasks:**
- ✅ Inject IDecisionEngine into DocumentProcessingPipeline
- ✅ Inject IEmbeddingProviderFactory into pipeline
- ✅ Generate plan before processing documents
- ✅ Extract content preview for plan generation
- ✅ Use plan parameters for chunking (strategy, size, overlap)
- ✅ Select embedding provider from plan
- ✅ Implement mean-of-chunks document embedding
- ✅ Add ComputeMeanEmbedding helper method
- ✅ Comprehensive logging for audit trail
- ✅ Fallback to default provider on errors
- ✅ End-to-end integration test script
- ✅ Complete documentation

**Implemented Code:**
```csharp
// Generate plan with content preview
var processingPlan = await _decisionEngine.GeneratePlanAsync(
    fileName: fileInfo.Name,
    fileSize: fileInfo.Length,
    mimeType: detectedMimeType,
    contentPreview: contentPreview,
    metadata: null);

// Use plan for chunking
var chunkingOptions = new ChunkingOptions(
    MaxTokens: processingPlan.ChunkSize,
    OverlapTokens: processingPlan.ChunkOverlap,
    Strategy: parsedStrategy
);

// Select provider from plan
var embeddingService = _embeddingProviderFactory.GetProvider(processingPlan.EmbeddingProvider);

// Compute mean-of-chunks
var meanEmbedding = ComputeMeanEmbedding(chunkEmbeddings);
```

---

### Phase 3: Mean-of-Chunks & Schema Update ✅ COMPLETE
**Status:** 100% Complete  
**Duration:** Completed  
**Files Created/Modified:** 8 files, 800+ lines of code

#### 3.1 Schema Update ✅ COMPLETE
**Goal:** Support 768-dim vectors and add provider metadata

**Files Created:**
```
scripts/phase3_mean_of_chunks_schema.sql
Server/Services/ChunkSearchService.cs
Server/Controllers/ChunkSearchController.cs
test-phase3-chunks.ps1
docs/PHASE3_MEAN_OF_CHUNKS.md
docs/PHASE3_COMPLETE.md
```

**Files Modified:**
```
Server/Models/Document.cs (added provider metadata)
Server/Models/DocumentChunk.cs (updated to 768-dim)
Server/Data/SmartCollectDbContext.cs (added provider columns)
Server/Services/IngestWorker.cs (saves provider metadata)
Server/Services/DocumentProcessingPipeline.cs (returns provider info)
Server/Program.cs (registered ChunkSearchService)
```

**Completed Tasks:**
- ✅ Updated schema to support 768-dim vectors
- ✅ Added embedding_provider and embedding_dimensions to documents
- ✅ Created DocumentChunk model (already existed, updated to 768-dim)
- ✅ Added DbSet<DocumentChunk> to SmartCollectDbContext
- ✅ Created comprehensive migration SQL script
- ✅ Updated DocumentProcessingPipeline to compute mean-of-chunks
- ✅ Updated IngestWorker to save chunks and provider metadata
- ✅ Created ChunkSearchService (semantic, hybrid, document chunks)
- ✅ Created ChunkSearchController (3 REST endpoints)
- ✅ Registered services in Program.cs
- ✅ Comprehensive test script (6 scenarios)
- ✅ Complete documentation (2 files)
- ✅ Zero compilation errors

**Key Features:**
- Individual chunk embeddings (768-dim)
- Document-level mean-of-chunks embedding
- Provider metadata tracking (provider, dimensions)
- Semantic chunk search
- Hybrid search (semantic + full-text)
- Cosine similarity computation
- PostgreSQL full-text search integration

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
