# Services Upgrade - Testing Guide

**Date:** October 1, 2025  
**Status:** Ready for Testing

---

## 🎯 What Was Changed

### Deleted Services (Useless Placeholders)
- ❌ SimplePdfParser.cs
- ❌ SimpleEntityExtractionService.cs  
- ❌ SimpleOcrService.cs
- ❌ SimpleEmbeddingService.cs

### New Microservices Created

#### 1. EasyOCR Service (Port 5085)
**Location:** `micros/ocr/`
**Purpose:** Deep learning OCR with 80+ languages
**Files Created:**
- app.py (FastAPI server)
- services/ocr_processor.py (EasyOCR wrapper)
- requirements.txt
- setup.ps1 / setup.sh
- README.md

**C# Integration:** `Server/Services/Providers/EasyOcrService.cs`

#### 2. Sentence-Transformers Service (Port 5086)
**Location:** `micros/embeddings/`
**Purpose:** High-quality 768-dimensional embeddings
**Files Created:**
- app.py (FastAPI server)
- services/embedding_service.py (SentenceTransformer wrapper)
- requirements.txt
- setup.ps1 / setup.sh
- README.md

**C# Integration:** `Server/Services/Providers/SentenceTransformerService.cs`

### Modified Files
- `Server/Program.cs` - Updated service registrations
- `Server/Services/Providers/ProviderFactory.cs` - Updated routing to use new services
- `Server/appsettings.json` - Added microservices configuration

---

## 🚀 Quick Start Testing

### Step 1: Setup Python Microservices

```powershell
# Terminal 1: spaCy (if not already running)
cd micros/spaCy
.\setup.ps1
# Wait for "Application startup complete"

# Terminal 2: EasyOCR
cd micros/ocr
.\setup.ps1
# This will download ~500MB of models on first run
# Wait for "Application startup complete"

# Terminal 3: Sentence-Transformers
cd micros/embeddings
.\setup.ps1
# This will download ~420MB model on first run
# Wait for "Application startup complete"
```

**Note:** First-time setup downloads models. Subsequent runs are instant.

### Step 2: Verify Health Checks

```powershell
# Check all services are running
curl http://localhost:5084/health  # spaCy
curl http://localhost:5085/health  # EasyOCR
curl http://localhost:5086/health  # Embeddings
```

Expected response from each:
```json
{
  "status": "healthy",
  "service": "...",
  "version": "1.0.0"
}
```

### Step 3: Start Main API

```powershell
# Terminal 4: Main .NET API
cd Server
dotnet run
# Wait for "Now listening on: http://localhost:5082"
```

### Step 4: Test Document Upload

Use the client app or curl:

```powershell
# Upload a PDF
curl -X POST http://localhost:5082/api/documents/upload `
  -F "file=@test-file.pdf" `
  -F "metadata={\"title\":\"Test Document\"}"

# Upload an image (tests OCR)
curl -X POST http://localhost:5082/api/documents/upload `
  -F "file=@test-image.png" `
  -F "metadata={\"title\":\"Test Image\"}"

# Upload text file (tests embeddings)
curl -X POST http://localhost:5082/api/documents/upload `
  -F "file=@test.txt" `
  -F "metadata={\"title\":\"Test Text\"}"
```

---

## ✅ Test Cases

### Test 1: EasyOCR Integration
**Goal:** Verify OCR service extracts text from images

1. Upload image file (PNG, JPG, JPEG)
2. Check logs for "Calling EasyOCR service"
3. Verify document appears in /api/documents
4. Verify extracted text is stored in database

**Expected:**
- OCR extracts text with 95%+ accuracy
- Bounding boxes detected
- No "SimpleOcrService" in logs (deleted)

### Test 2: Sentence-Transformers Embeddings
**Goal:** Verify 768-dimensional embeddings are generated

1. Upload text file (> 2000 chars to trigger chunking)
2. Check logs for "Calling sentence-transformers service"
3. Query database: `SELECT embedding FROM document_chunks LIMIT 1;`
4. Verify vector has 768 dimensions

**Expected:**
- Embeddings are 768-dimensional (not 300 or 96)
- document_chunks table has entries
- Embeddings are normalized

### Test 3: Entity Extraction (Existing - spaCy)
**Goal:** Verify entity extraction still works

1. Upload document with entities (names, dates, orgs)
2. Check response includes entities
3. Verify entities stored in metadata

**Expected:**
- Entities extracted: PERSON, ORG, DATE, GPE, etc.
- spaCy service called (not SimpleEntityExtractionService)

### Test 4: End-to-End Pipeline
**Goal:** Full document processing workflow

1. Upload large PDF (> 2000 chars)
2. Verify processing steps in logs:
   - PDF parsing (PdfPigParser)
   - Chunking (TextChunkingService)
   - Entity extraction (SpacyNlpService)
   - Embedding generation (SentenceTransformerService)
   - Storage (document + chunks)

**Expected:**
- All steps complete without errors
- document_chunks table populated
- Embeddings are 768-dimensional
- Entities extracted and stored

### Test 5: Service Fallback
**Goal:** Verify system handles microservice failures

1. Stop Sentence-Transformers service (Ctrl+C in terminal)
2. Upload document
3. Check if fallback to spaCy embeddings occurs

**Expected:**
- Error logged for SentenceTransformerService
- Document still processes (may use spaCy fallback)
- No crash or data loss

### Test 6: Concurrent Uploads
**Goal:** Test under load

1. Upload 10 documents simultaneously
2. Monitor all service logs
3. Verify all documents process successfully

**Expected:**
- All services handle concurrent requests
- No race conditions or deadlocks
- All documents appear in database

---

## 🔍 Log Monitoring

### Key Log Patterns to Look For

**SUCCESS Patterns:**
```
✅ "Calling EasyOCR service"
✅ "EasyOCR extracted X characters with Y text regions"
✅ "Calling sentence-transformers service"
✅ "Generated 768-dimensional embedding"
✅ "Saved X chunks for document"
```

**FAILURE Patterns (Should NOT appear):**
```
❌ "SimpleOcrService" (deleted)
❌ "SimpleEmbeddingService" (deleted)
❌ "SimplePdfParser" (deleted)
❌ "SimpleEntityExtractionService" (deleted)
❌ "Hash-based embedding" (removed)
```

### Where to Check Logs

**Microservices:** Check terminal windows where services are running
**Main API:** Check terminal running `dotnet run`
**Database:** Query logs table or use pgAdmin

---

## 🐛 Common Issues & Solutions

### Issue: "Port 5085 already in use"

**Solution:**
```powershell
# Find and kill process using port
netstat -ano | findstr :5085
taskkill /PID <PID> /F

# Or change port in micros/ocr/app.py
```

### Issue: "easyocr module not found"

**Solution:**
```powershell
cd micros/ocr
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
```

### Issue: "Model download timeout"

**Solution:**
- Check internet connection
- Models are large (420MB-500MB)
- Download happens on first run
- Retry if interrupted

### Issue: "GPU not detected"

**Solution:**
- Services will auto-fallback to CPU
- Slower but fully functional
- To force CPU: Edit service config files

### Issue: "Embedding dimensions mismatch"

**Solution:**
- Check appsettings.json has correct dimensions
- Verify Sentence-Transformers service is running
- May need to drop and recreate document_chunks table

---

## 📊 Performance Benchmarks

### Expected Performance (CPU)

| Operation | Before | After | Notes |
|-----------|--------|-------|-------|
| OCR (image) | 0ms (fake) | 2-5s | Real extraction now |
| Embedding (text) | 1ms (hash) | 50-100ms | Real semantic meaning |
| Entity extraction | 0ms (fake) | 50ms | Already using spaCy |
| Full pipeline (1 page) | ~100ms | ~3-5s | Worth the quality |

### Expected Performance (GPU)

| Operation | CPU Time | GPU Time | Speedup |
|-----------|----------|----------|---------|
| OCR | 2-5s | 0.5-1s | 5-10x |
| Embedding | 50-100ms | 10-20ms | 5x |

---

## ✅ Success Criteria

After testing, you should confirm:

- [ ] All 3 microservices start without errors
- [ ] Health checks return 200 OK
- [ ] PDF upload extracts text correctly
- [ ] Image upload performs OCR successfully
- [ ] Embeddings are 768-dimensional in database
- [ ] Entities are extracted from documents
- [ ] document_chunks table is populated
- [ ] No references to "Simple" services in logs
- [ ] System handles concurrent uploads
- [ ] No memory leaks or crashes after 100+ uploads

---

## 🎉 Next Steps After Testing

Once all tests pass:

1. **Update client app** to show enhanced metadata
2. **Implement hybrid search** endpoint (vector + BM25)
3. **Add monitoring** for microservices (health checks)
4. **Performance tuning** (batch sizes, timeouts)
5. **Production deployment** (Docker, Kubernetes)

---

## 📚 Reference Documentation

- [Microservices Setup Guide](MICROSERVICES_SETUP.md) - Detailed setup
- [Advanced Services Upgrade](ADVANCED_SERVICES_UPGRADE.md) - Architecture details
- [Chunking Implementation](CHUNKING_IMPLEMENTATION.md) - Chunking strategy
- [Architecture Overview](ARCHITECTURE.md) - System design

---

**Status:** Ready for Testing  
**Last Updated:** October 1, 2025
