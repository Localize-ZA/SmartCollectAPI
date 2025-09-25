# Test Document for MIME Detection

This is a **test markdown document** to verify that our enhanced MIME type detection is working correctly.

## Features
- Headers with `#` symbols
- **Bold text** and *italic text*
- Code blocks:

```javascript
function test() {
    console.log("Testing MIME detection");
}
```

## Expected Result
This file should be detected as `text/markdown` instead of `application/octet-stream`.

The enhanced `SimpleContentDetector` should recognize:
1. Markdown headers (`#`, `##`, etc.)
2. Code blocks (``` delimited)
3. Text links `[text](url)`
4. Overall text readability (>70% printable characters)

If this works, the document should process successfully through the pipeline!