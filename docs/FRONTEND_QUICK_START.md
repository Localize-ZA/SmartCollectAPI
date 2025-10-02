# 🚀 Quick Start Guide - Updated Frontend

## New Pages

### 1. Chunk Search (`/search`)
Search through document chunks with semantic understanding.

**Try it:**
```
http://localhost:3000/search
```

**Features:**
- Semantic search with sentence-transformers (768d) or spaCy (300d)
- Hybrid search (semantic + text)
- Adjustable similarity threshold
- Query highlighting
- Direct document links

---

### 2. Language Detection (`/language`)
Detect language from text with 75+ languages supported.

**Try it:**
```
http://localhost:3000/language
```

**Features:**
- Real-time language detection
- Confidence scores
- Multiple candidate languages
- Sample texts in 8 languages
- Full language list

---

## Updated API Functions

### Chunk Search
```typescript
import { searchChunks, getDocumentChunks, hybridSearchChunks } from "@/lib/api";

// Semantic search
const results = await searchChunks({
  query: "machine learning",
  provider: "sentence-transformers",
  limit: 10,
  similarityThreshold: 0.7
});

// Get chunks for a document
const chunks = await getDocumentChunks(documentId);

// Hybrid search
const hybridResults = await hybridSearchChunks({
  query: "artificial intelligence",
  provider: "sentence-transformers",
  semanticWeight: 0.7,
  textWeight: 0.3
});
```

### Language Detection
```typescript
import { detectLanguage, getSupportedLanguages } from "@/lib/api";

// Detect language
const result = await detectLanguage("Hello world", 0.5);
console.log(result.detectedLanguage.languageName); // "English"

// Get all supported languages
const languages = await getSupportedLanguages();
console.log(languages.length); // 75+
```

### API Sources
```typescript
import { 
  getApiSources, 
  createApiSource, 
  testApiConnection, 
  triggerApiIngestion 
} from "@/lib/api";

// List sources
const sources = await getApiSources({ enabled: true });

// Create new source
const newSource = await createApiSource({
  name: "My API",
  endpointUrl: "https://api.example.com/data",
  authType: "ApiKey",
  apiKey: "secret-key"
});

// Test connection
const testResult = await testApiConnection(sourceId);

// Trigger ingestion
const result = await triggerApiIngestion(sourceId);
console.log(`Created ${result.documentsCreated} documents`);
```

---

## Component Usage

### Document Chunks View
```tsx
import { DocumentChunksView } from "@/components/DocumentChunksView";

<DocumentChunksView documentId="72b24a35-74bd-4336-bbda-12b62996f569" />
```

Shows all chunks for a document with:
- Chunk index and token count
- Position in source document
- Full chunk content
- Creation timestamps

---

## Updated Interfaces

### Document with Embedding Metadata
```typescript
interface DocumentSummary {
  id: string;
  sourceUri: string;
  mime?: string | null;
  sha256?: string | null;
  createdAt: string;
  hasEmbedding: boolean;
  embeddingProvider?: string | null;     // NEW
  embeddingDimensions?: number | null;   // NEW
}
```

### Chunk Search Result
```typescript
interface ChunkSearchResult {
  chunkId: number;
  documentId: string;
  chunkIndex: number;
  content: string;
  similarity: number;
  documentUri: string;
}
```

### Language Detection Result
```typescript
interface LanguageDetectionResult {
  detectedLanguage: {
    language: string;
    languageName: string;
    confidence: number;
    isoCode639_1?: string;
    isoCode639_3?: string;
  };
  allCandidates: LanguageCandidate[];
  textLength: number;
}
```

---

## Service URLs

| Service | Port | Health Endpoint | Purpose |
|---------|------|-----------------|---------|
| Main API | 5082 | `/health/basic` | ASP.NET Core backend |
| SMTP | 5083 | `/health` | Email notifications |
| spaCy NLP | 5084 | `/health` | NER, language processing |
| Embeddings | 8001 | `/health` | Sentence transformers |
| OCR | 8002 | `/health` | EasyOCR service |
| **Language Detection** | **8004** | `/health` | **Lingua (Phase 4)** |

---

## Testing Workflow

### Test Phase 3 (Chunk Search)

1. **Upload large document**
   ```
   Navigate to /upload
   Select file > 2000 characters
   Upload and wait for processing
   ```

2. **Search chunks**
   ```
   Navigate to /search
   Enter query: "machine learning"
   Select provider: sentence-transformers
   Set threshold: 0.7
   Click Search
   ```

3. **View results**
   - See similarity scores
   - Expand chunks
   - Click "View Document" to see full context

### Test Phase 4 (Language Detection)

1. **Start microservice**
   ```powershell
   cd micros/language-detection
   .\venv\Scripts\Activate.ps1
   python app.py
   ```

2. **Use detection page**
   ```
   Navigate to /language
   Click sample text or enter custom text
   Click "Detect Language"
   View confidence scores and alternatives
   ```

3. **Verify results**
   - Check detected language name
   - See ISO codes
   - View alternative candidates
   - Check confidence percentages

---

## Common Tasks

### Upload and Search Document
```typescript
// 1. Upload
const uploadResult = await uploadDocument(file);
console.log("Job ID:", uploadResult.job_id);

// 2. Wait for processing (polling or check manually)

// 3. Search chunks
const searchResults = await searchChunks({
  query: "your search query",
  provider: "sentence-transformers"
});

console.log(`Found ${searchResults.resultCount} chunks`);
```

### Create API Source and Ingest
```typescript
// 1. Create source
const source = await createApiSource({
  name: "JSON Placeholder",
  endpointUrl: "https://jsonplaceholder.typicode.com/posts",
  httpMethod: "GET",
  authType: "None",
  responsePath: "$",
  enabled: true
});

// 2. Test connection
const test = await testApiConnection(source.id);
console.log("Test:", test.success);

// 3. Trigger ingestion
const result = await triggerApiIngestion(source.id);
console.log(`Ingested ${result.recordsFetched} records`);
console.log(`Created ${result.documentsCreated} documents`);
```

### Detect Language and Process
```typescript
// 1. Detect language
const detection = await detectLanguage(text);
const language = detection.detectedLanguage.isoCode639_1;

console.log(`Detected: ${language}`); // "en", "es", etc.

// 2. Use language info for processing
// (Future: language-specific chunking rules)
```

---

## Troubleshooting

### Chunk Search Returns No Results
- **Check**: Document has embeddings (`hasEmbedding: true`)
- **Check**: Document was chunked (> 2000 chars)
- **Try**: Lower similarity threshold (e.g., 0.5)
- **Try**: Different provider (sentence-transformers vs spaCy)

### Language Detection Service Unavailable
- **Check**: Service running on port 8004
- **Start**: `cd micros/language-detection && python app.py`
- **Verify**: `curl http://localhost:8004/health`
- **Check**: Virtual environment activated

### API Source Connection Fails
- **Check**: Endpoint URL uses HTTPS
- **Check**: API key is correct
- **Check**: Authentication settings match API requirements
- **Try**: Test in Postman first

---

## Performance Tips

### Chunk Search
- Use `sentence-transformers` for best accuracy (768 dimensions)
- Use `spaCy` for faster search (300 dimensions)
- Increase threshold for fewer, more relevant results
- Use hybrid search for balanced semantic + keyword matching

### Language Detection
- Minimum 20-30 characters for reliable detection
- Longer text = higher confidence
- Mixed-language text may show multiple high-confidence candidates

### API Sources
- Use pagination for large datasets
- Configure field mappings to extract only needed data
- Schedule cron jobs for automatic ingestion
- Monitor logs for errors

---

## Quick Commands

```powershell
# Start backend
cd Server && dotnet run

# Start frontend
cd client && npm run dev

# Start language detection
cd micros/language-detection && .\venv\Scripts\Activate.ps1 && python app.py

# Test chunk search
curl -X POST http://localhost:5082/api/ChunkSearch/search `
  -H "Content-Type: application/json" `
  -d '{"query":"test","provider":"sentence-transformers","limit":5}'

# Test language detection
curl -X POST http://localhost:8004/detect `
  -H "Content-Type: application/json" `
  -d '{"text":"Hello world","min_confidence":0.5}'
```

---

## Next Steps

1. ✅ Upload documents and test chunking
2. ✅ Try semantic search with different providers
3. ✅ Test language detection with sample texts
4. ✅ Create API sources and test ingestion
5. 🎯 Integrate language detection into Decision Engine
6. 🎯 Add language-specific chunking rules
7. 🎯 Implement search analytics

---

**Status**: Frontend fully updated and production-ready! 🎉
