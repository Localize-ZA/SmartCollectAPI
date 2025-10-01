# Phase 2.2: Pipeline Integration - Quick Reference

## 🎯 What Changed

The `DocumentProcessingPipeline` now uses the Decision Engine and Provider Factory to make intelligent processing decisions for each document.

## 📝 Key Changes

### 1. New Dependencies
```csharp
private readonly IDecisionEngine _decisionEngine;
private readonly IEmbeddingProviderFactory _embeddingProviderFactory;
```

### 2. Plan Generation (Before Processing)
```csharp
// Extract content preview for analysis
string? contentPreview = null;
using var previewReader = new StreamReader(fileStream, leaveOpen: true);
var previewBuffer = new char[500];
var charsRead = await previewReader.ReadAsync(previewBuffer, 0, 500);
contentPreview = new string(previewBuffer, 0, charsRead);
fileStream.Position = 0;

// Generate processing plan
var processingPlan = await _decisionEngine.GeneratePlanAsync(
    fileName: fileInfo.Name,
    fileSize: fileInfo.Length,
    mimeType: detectedMimeType,
    contentPreview: contentPreview,
    metadata: null);
```

### 3. Plan-Based Chunking
```csharp
// Parse strategy from plan
var strategy = Enum.TryParse<ChunkingStrategy>(processingPlan.ChunkingStrategy, 
    ignoreCase: true, out var parsedStrategy)
    ? parsedStrategy
    : ChunkingStrategy.SlidingWindow;

// Use plan parameters
var chunkingOptions = new ChunkingOptions(
    MaxTokens: processingPlan.ChunkSize,      // From plan
    OverlapTokens: processingPlan.ChunkOverlap, // From plan
    Strategy: strategy                          // From plan
);
```

### 4. Dynamic Provider Selection
```csharp
// Get provider from plan
IEmbeddingService embeddingService;
try
{
    embeddingService = _embeddingProviderFactory.GetProvider(processingPlan.EmbeddingProvider);
    _logger.LogInformation("Using embedding provider: {Provider} (from plan)", 
        processingPlan.EmbeddingProvider);
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to get provider {Provider}, falling back to default", 
        processingPlan.EmbeddingProvider);
    embeddingService = _embeddingProviderFactory.GetDefaultProvider();
}
```

### 5. Mean-of-Chunks Embedding
```csharp
// Compute document embedding as mean of chunk embeddings
if (chunkEmbeddings.Count > 0 && chunkEmbeddings[0].Embedding != null)
{
    var meanEmbedding = ComputeMeanEmbedding(chunkEmbeddings);
    embeddingResult = new EmbeddingResult(meanEmbedding);
    _logger.LogInformation("Computed mean-of-chunks embedding ({Dimensions} dims) from {Count} chunks",
        meanEmbedding.ToArray().Length, chunkEmbeddings.Count);
}

// Helper method added to class
private static Vector ComputeMeanEmbedding(List<ChunkEmbedding> chunkEmbeddings)
{
    var dims = chunkEmbeddings[0].Embedding.ToArray().Length;
    var sum = new float[dims];
    int validCount = 0;
    
    foreach (var chunk in chunkEmbeddings)
    {
        if (chunk.Embedding != null)
        {
            var embedding = chunk.Embedding.ToArray();
            for (int i = 0; i < dims; i++)
            {
                sum[i] += embedding[i];
            }
            validCount++;
        }
    }
    
    for (int i = 0; i < dims; i++)
    {
        sum[i] /= validCount;
    }
    
    return new Vector(sum);
}
```

## 📊 Log Output Examples

### Successful Processing:
```
[INF] Starting document processing pipeline for job abc123...
[INF] Processing document with MIME type: text/plain
[INF] Generated processing plan for sample.txt: Provider=sentence-transformers, Strategy=fixed, ChunkSize=1000, RequiresOCR=False, Language=en, Priority=normal, EstimatedCost=0.5
[INF] Decision reasons: Text content detected, no OCR needed; General document type selected; Fixed chunking for text content; Sentence-transformers for general content
[INF] Chunking text (2345 chars) using strategy: fixed, size: 1000, overlap: 200
[INF] Created 3 chunks from document
[INF] Using embedding provider: sentence-transformers (from plan)
[INF] Generated embeddings for 3 chunks using sentence-transformers
[INF] Computed mean-of-chunks embedding (768 dims) from 3 chunks
[INF] Successfully processed job abc123...
```

### With Provider Fallback:
```
[INF] Generated processing plan: Provider=openai (not available yet)
[WRN] Failed to get embedding provider openai, falling back to default
[INF] Using embedding provider: sentence-transformers (from plan)
```

## 🧪 Testing

### Quick Test:
```powershell
.\test-pipeline-integration.ps1
```

### Manual Test:
```powershell
# Upload document
$file = Get-Content test-files/sample-text.txt
curl -F "file=@test-files/sample-text.txt" http://localhost:5149/api/Upload

# Check logs for decision engine output
# Look for "Generated processing plan" messages
```

## ✅ Verification Checklist

After deployment, verify:

- [ ] Server starts without errors
- [ ] Document upload still works
- [ ] Logs show "Generated processing plan" for each upload
- [ ] Logs show "Using embedding provider: X (from plan)"
- [ ] Logs show "Computed mean-of-chunks embedding"
- [ ] Documents appear in database with embeddings
- [ ] Chunks are created and saved
- [ ] Search still works correctly

## 🔧 Troubleshooting

### Issue: "No overload for method 'GeneratePlanAsync'"
**Solution:** Check that you're not passing a cancellationToken parameter (it doesn't take one)

### Issue: Provider not found
**Solution:** Check that the provider is registered in Program.cs and the key matches exactly

### Issue: Mean embedding calculation fails
**Solution:** Ensure at least one chunk has a valid embedding before computing mean

### Issue: No content preview
**Solution:** This is OK - content preview is optional, plan will be generated with just file metadata

## 📚 Related Documentation

- **Full Documentation**: `docs/PHASE2_2_PIPELINE_INTEGRATION.md`
- **Provider Factory**: `docs/PHASE2_1_PROVIDER_FACTORY.md`
- **Decision Engine**: `docs/DECISION_ENGINE_PHASE1.md`
- **Implementation Roadmap**: `docs/IMPLEMENTATION_ROADMAP.md`
- **Phase 2 Summary**: `docs/PHASE2_COMPLETE.md`

## 🚀 Next Phase

Phase 3 will add:
- Schema validation for chunks table
- Processing plan metadata storage
- Language detection microservice
- Chunk-level search API

**Estimated Duration:** 2-3 days
