# Vectorization Roadmap - Making SmartCollectAPI State-of-the-Art

## Current State
- ✅ spaCy `en_core_web_sm` embeddings (96 dimensions)
- ✅ Basic entity extraction and sentiment
- ❌ No text chunking strategy
- ❌ No hybrid search
- ❌ No re-ranking
- ❌ Single embedding per document

## 🎯 Priority 1: Advanced Embedding Models

### Option A: OpenAI Embeddings (Recommended for Production)
**Model**: `text-embedding-3-large` or `text-embedding-3-small`
- **Dimensions**: 3072 (large) or 1536 (small) - configurable
- **Quality**: State-of-the-art semantic understanding
- **Cost**: ~$0.13 per 1M tokens (large), $0.02 per 1M tokens (small)
- **Latency**: ~200-500ms per request
- **Max Tokens**: 8,191 tokens

**Implementation Steps**:
1. Add OpenAI provider to `IEmbeddingService`
2. Support configurable dimensions via matryoshka representations
3. Batch processing for cost efficiency
4. Fallback to spaCy for offline scenarios

### Option B: Local Transformer Models (Best for Privacy/Cost)
**Models**: 
- `BAAI/bge-large-en-v1.5` (1024 dim, excellent quality)
- `mixedbread-ai/mxbai-embed-large-v1` (1024 dim, SOTA on MTEB)
- `sentence-transformers/all-MiniLM-L6-v2` (384 dim, fast)

**Infrastructure**:
- Run via FastAPI microservice (similar to spaCy)
- GPU acceleration recommended (NVIDIA T4 or better)
- ~2-5GB RAM per model

### Option C: Hybrid Approach
- **Primary**: OpenAI for production, high-value documents
- **Fallback**: Local transformers for bulk/low-priority
- **Baseline**: spaCy for offline/dev environments

## 🎯 Priority 2: Intelligent Text Chunking

### Strategy: Semantic Chunking
Instead of naive 512-token chunks, use:

1. **Sentence-aware chunking** 
   - Respect document structure (paragraphs, sections)
   - Use spaCy sentence boundaries
   - Target 256-512 tokens per chunk

2. **Recursive character splitting**
   - Split on: `\n\n` → `\n` → `. ` → ` `
   - Maintain overlap (50-100 tokens) for context

3. **Document-type specific strategies**:
   - **PDFs**: Page-based + semantic splitting
   - **JSON/XML**: Preserve hierarchical structure, chunk leaf nodes
   - **CSV**: Row-based with column metadata
   - **Code**: Function/class-level chunks

### Implementation Plan:
```csharp
public interface IChunkingStrategy
{
    Task<List<TextChunk>> ChunkAsync(string text, Dictionary<string, object> metadata);
}

public record TextChunk(
    string Content,
    int StartOffset,
    int EndOffset,
    int ChunkIndex,
    Dictionary<string, object> Metadata
);
```

## 🎯 Priority 3: Hybrid Search Architecture

### Components:

1. **Dense Vectors** (Current pgvector)
   - Semantic similarity via cosine distance
   - Good for conceptual matches

2. **Sparse Vectors** (BM25 via PostgreSQL FTS)
   - Keyword matching
   - Good for exact terms, names, codes

3. **Fusion Algorithm** (Reciprocal Rank Fusion)
   ```
   score(d) = Σ 1 / (k + rank_i(d))
   ```
   where k=60 (typical value)

### PostgreSQL Schema Updates:
```sql
-- Add full-text search
ALTER TABLE documents 
ADD COLUMN fts_vector tsvector 
GENERATED ALWAYS AS (to_tsvector('english', canonical->>'extracted_text')) STORED;

CREATE INDEX idx_documents_fts ON documents USING GIN(fts_vector);

-- Store multiple chunk embeddings
CREATE TABLE document_chunks (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    document_id UUID REFERENCES documents(id) ON DELETE CASCADE,
    chunk_index INT NOT NULL,
    content TEXT NOT NULL,
    start_offset INT,
    end_offset INT,
    embedding VECTOR(1536), -- OpenAI dimensions
    metadata JSONB,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    CONSTRAINT unique_document_chunk UNIQUE (document_id, chunk_index)
);

CREATE INDEX idx_chunks_embedding ON document_chunks 
USING ivfflat (embedding vector_cosine_ops) 
WITH (lists = 100);
```

## 🎯 Priority 4: Re-Ranking

After hybrid retrieval, re-rank top 20-50 results:

### Option A: Cross-Encoder Re-Ranker
**Model**: `cross-encoder/ms-marco-MiniLM-L-6-v2`
- Takes (query, document) pairs
- Returns relevance score 0-1
- Much slower but more accurate than bi-encoders

### Option B: Cohere Rerank API
- State-of-the-art commercial solution
- $2 per 1000 searches
- Simple API integration

## 🎯 Priority 5: Query Understanding

Before searching, enhance queries:

1. **Query Expansion**
   - Synonyms via WordNet or learned embeddings
   - Acronym expansion
   - Spell correction

2. **Query Classification**
   - Factual vs. conceptual
   - Time-sensitive vs. evergreen
   - Entity-focused vs. topic-focused

3. **Intent Detection**
   - Route to appropriate retrieval strategy
   - Adjust fusion weights based on intent

## 📊 Evaluation & Monitoring

### Metrics to Track:
1. **Retrieval Quality**
   - Mean Reciprocal Rank (MRR)
   - Normalized Discounted Cumulative Gain (NDCG@10)
   - Precision@K, Recall@K

2. **Embedding Quality**
   - Cosine similarity distribution
   - Cluster coherence
   - Outlier detection

3. **System Performance**
   - Indexing throughput (docs/second)
   - Query latency (p50, p95, p99)
   - Cache hit rates

### Logging Strategy:
```json
{
  "query": "user query text",
  "dense_results": [...],
  "sparse_results": [...],
  "fused_results": [...],
  "reranked_results": [...],
  "latency_ms": {
    "dense": 45,
    "sparse": 12,
    "fusion": 2,
    "rerank": 89,
    "total": 148
  },
  "user_feedback": "clicked_result_id"
}
```

## 🗓️ Implementation Timeline

### Week 1-2: Text Chunking
- [ ] Implement semantic chunking service
- [ ] Add `document_chunks` table
- [ ] Update pipeline to chunk before embedding
- [ ] Test with various document types

### Week 3-4: Better Embeddings
- [ ] Add OpenAI embedding provider
- [ ] OR Set up local transformer service
- [ ] Implement batch processing
- [ ] Migrate existing documents (backfill)

### Week 5-6: Hybrid Search
- [ ] Add PostgreSQL FTS indexes
- [ ] Implement RRF fusion algorithm
- [ ] Create unified search endpoint
- [ ] A/B test vs. vector-only search

### Week 7-8: Re-Ranking
- [ ] Deploy cross-encoder service
- [ ] OR Integrate Cohere Rerank API
- [ ] Update search pipeline
- [ ] Benchmark improvements

### Week 9-10: Query Enhancement
- [ ] Add query expansion
- [ ] Implement spell correction
- [ ] Build query classifier
- [ ] Fine-tune based on analytics

## 🔧 Quick Wins (Do These First!)

1. **Upgrade spaCy Model**
   ```python
   # In micros/spaCy/.env
   SPACY_MODEL=en_core_web_md  # 300 dimensions
   # or
   SPACY_MODEL=en_core_web_lg  # 300 dimensions, better accuracy
   ```

2. **Add Text Preprocessing**
   - Remove excessive whitespace
   - Normalize Unicode
   - Handle tables/code blocks specially

3. **Implement Basic Chunking**
   - Start with simple 512-token sliding window
   - 100-token overlap
   - Store chunks in separate table

4. **Enable PostgreSQL FTS**
   - Add tsvector column
   - Create GIN index
   - Combine with vector search

## 📚 Resources

### Papers
- "Attention Is All You Need" (Transformers)
- "BERT: Pre-training of Deep Bidirectional Transformers"
- "Sentence-BERT: Sentence Embeddings using Siamese BERT-Networks"
- "ColBERT: Efficient and Effective Passage Search via Contextualized Late Interaction"
- "Lost in the Middle: How Language Models Use Long Contexts"

### Libraries
- `sentence-transformers` - Easy local embeddings
- `llama-index` - Document chunking strategies
- `langchain` - Text splitters and retrievers
- `rank_bm25` - Python BM25 implementation

### Benchmarks
- MTEB (Massive Text Embedding Benchmark)
- BEIR (Benchmarking IR)
- MS MARCO ranking

## 🎓 Training & Fine-Tuning (Advanced)

For domain-specific excellence:

1. **Fine-tune embeddings** on your document corpus
2. **Collect user feedback** (clicks, dwell time)
3. **Build training pairs** (query, relevant_doc)
4. **Use contrastive learning** to improve retrieval

This is a longer-term investment but yields the best results for specialized domains.
