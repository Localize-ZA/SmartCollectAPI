# Document Processing Pipeline Test

This is a test markdown file to verify the complete processing pipeline:

## Features to Test
- **MIME Type Detection**: Should detect as `text/markdown`
- **Text Extraction**: Should extract plain text content
- **Embedding Generation**: Should generate embeddings using SimpleEmbeddingService
- **Entity Extraction**: Should extract entities using SimpleEntityExtractionService
- **Status Update**: Should move from staging to done status

## Content
This document contains various markdown elements:
- Lists (like this one)
- Headers at different levels
- **Bold text** and *italic text*
- Code blocks and inline `code`

```javascript
// Sample code block
function processDocument(doc) {
    return doc.process();
}
```

## Expected Pipeline Flow
1. Upload → Staging (with correct MIME type detection)
2. Processing → Text extraction, embedding generation, entity extraction
3. Completion → Move to done status

This test will verify that our enhanced SimpleContentDetector correctly identifies markdown files and that the complete processing pipeline works end-to-end.