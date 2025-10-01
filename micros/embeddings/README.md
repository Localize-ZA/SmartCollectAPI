# Sentence-Transformers Embedding Service

High-quality semantic embeddings using sentence-transformers for advanced search and similarity.

## Features

- ✅ 768-dimensional embeddings (all-mpnet-base-v2)
- ✅ State-of-the-art semantic understanding
- ✅ Batch processing support
- ✅ GPU acceleration (optional)
- ✅ Multiple model options
- ✅ Cosine similarity computation

## Setup

### Windows
```powershell
.\setup.ps1
```

### Manual Setup
```bash
python -m venv venv
.\venv\Scripts\Activate.ps1  # Windows
pip install -r requirements.txt
```

**Note:** Model will be downloaded automatically on first run (~400MB for all-mpnet-base-v2)

## Running

```bash
# Activate virtual environment
.\venv\Scripts\Activate.ps1  # Windows

# Start the service
uvicorn app:app --reload --port 5086
```

## API Endpoints

### Generate Single Embedding
```bash
POST http://localhost:5086/api/v1/embed/single
Content-Type: application/json

{
  "text": "This is a sample text to embed",
  "normalize": true
}
```

**Response:**
```json
{
  "embedding": [0.123, -0.456, 0.789, ...],  // 768 floats
  "dimensions": 768,
  "model": "all-mpnet-base-v2",
  "success": true
}
```

### Generate Batch Embeddings
```bash
POST http://localhost:5086/api/v1/embed/batch
Content-Type: application/json

{
  "texts": [
    "First text to embed",
    "Second text to embed",
    "Third text to embed"
  ],
  "normalize": true,
  "batch_size": 32
}
```

**Response:**
```json
{
  "embeddings": [[...], [...], [...]],  // Array of 768-dim vectors
  "dimensions": 768,
  "count": 3,
  "model": "all-mpnet-base-v2",
  "success": true
}
```

### Compute Similarity
```bash
POST http://localhost:5086/api/v1/similarity
Content-Type: application/json

{
  "text1": "I love programming",
  "text2": "I enjoy coding"
}
```

**Response:**
```json
{
  "similarity": 0.85,
  "model": "all-mpnet-base-v2",
  "success": true
}
```

### Health Check
```bash
GET http://localhost:5086/health
```

**Response:**
```json
{
  "status": "healthy",
  "service": "sentence-transformers",
  "version": "1.0.0",
  "model": "all-mpnet-base-v2",
  "dimensions": 768,
  "max_seq_length": 384
}
```

### List Available Models
```bash
GET http://localhost:5086/api/v1/models
```

## Model Options

Edit `app.py` startup to change model:

```python
# Fast and efficient (384 dimensions)
embedding_service = EmbeddingService(model_name="all-MiniLM-L6-v2")

# Best quality (768 dimensions) - DEFAULT
embedding_service = EmbeddingService(model_name="all-mpnet-base-v2")

# Optimized for Q&A (768 dimensions)
embedding_service = EmbeddingService(model_name="multi-qa-mpnet-base-dot-v1")
```

### Model Comparison

| Model | Dimensions | Speed | Quality | Use Case |
|-------|------------|-------|---------|----------|
| all-MiniLM-L6-v2 | 384 | ⚡⚡⚡ | ⭐⭐⭐ | Fast search |
| all-mpnet-base-v2 | 768 | ⚡⚡ | ⭐⭐⭐⭐⭐ | Best general (default) |
| multi-qa-mpnet-base-dot-v1 | 768 | ⚡⚡ | ⭐⭐⭐⭐⭐ | Question answering |
| all-MiniLM-L12-v2 | 384 | ⚡⚡ | ⭐⭐⭐⭐ | Balanced |

## GPU Support

For GPU acceleration:

1. Install CUDA toolkit
2. Install PyTorch with CUDA:
   ```bash
   pip install torch torchvision --index-url https://download.pytorch.org/whl/cu118
   ```

GPU provides 5-10x speedup for batch processing.

## Performance

### CPU (typical laptop)
- Single embedding: ~50-100ms
- Batch (32 items): ~1-2 seconds
- Memory: ~1GB RAM

### GPU (NVIDIA)
- Single embedding: ~10-20ms
- Batch (32 items): ~200-400ms
- Memory: ~2GB VRAM

## Use Cases

### Semantic Search
```python
# Find documents similar to a query
query = "machine learning algorithms"
results = embedding_service.find_most_similar(query, documents, top_k=5)
```

### Document Clustering
```python
# Group similar documents
embeddings = embedding_service.encode_batch(documents)
# Use K-means or other clustering on embeddings
```

### Duplicate Detection
```python
# Find duplicate or near-duplicate texts
similarity = embedding_service.similarity(text1, text2)
if similarity > 0.9:
    print("Likely duplicates")
```

### Question Answering
```python
# Find best answer for a question
question = "What is machine learning?"
answers = ["ML is...", "AI refers to...", ...]
best_answer = embedding_service.find_most_similar(question, answers, top_k=1)
```

## Integration

See `Server/Services/Providers/SentenceTransformerService.cs` for .NET integration.

## Troubleshooting

**Issue:** Model download fails
- **Fix:** Check internet connection, models are downloaded from HuggingFace

**Issue:** Out of memory
- **Fix:** Reduce batch_size or switch to smaller model (MiniLM)

**Issue:** Slow on CPU
- **Fix:** Use GPU or smaller model, reduce batch size

**Issue:** Poor results
- **Fix:** Try different model (multi-qa for Q&A, mpnet for general)
