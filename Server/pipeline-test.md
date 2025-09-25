# Complete Pipeline Test Document

This markdown document will test the **complete processing pipeline**:

## MIME Type Detection ✅
- File extension: `.md`
- Content: Contains markdown syntax
- Expected: `text/markdown` instead of `application/octet-stream`

## Text Extraction 
- **Headers** with `#` symbols
- **Bold text** and *italic text*
- Lists and bullet points
- Code blocks and inline `code`

```python
def test_processing():
    return "This should be extracted as plain text"
```

## Entity Extraction
This document mentions **OpenAI**, **Microsoft**, and **Google Cloud** as technology entities that should be detected by the entity extraction service.

## Embedding Generation
The SimpleEmbeddingService should generate vector embeddings for this content to enable semantic search and similarity matching.

## Expected Result
- Status: `done` (instead of `failed`)
- MIME: `text/markdown`
- Text extracted successfully
- Embeddings generated
- Entities extracted (may be empty for simple OSS service)

Pipeline test completed successfully! 🎉