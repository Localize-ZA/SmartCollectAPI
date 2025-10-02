# Language Detection Microservice

High-accuracy language detection service using the lingua library.

## Features

- **75+ Languages**: Supports detection of 75+ languages
- **High Accuracy**: Uses statistical models for precise detection
- **Confidence Scores**: Returns confidence values for all detected languages
- **Fast**: Optimized for performance
- **RESTful API**: Simple HTTP endpoints

## Supported Languages

English, Spanish, French, German, Italian, Portuguese, Russian, Chinese, Japanese, Korean, Arabic, Hindi, and 60+ more.

## Installation

### Windows

```powershell
# Run setup script
.\setup.ps1

# Or manually:
python -m venv venv
.\venv\Scripts\Activate.ps1
pip install -r requirements.txt
```

### Linux/Mac

```bash
python3 -m venv venv
source venv/bin/activate
pip install -r requirements.txt
```

## Running the Service

### Development

```bash
python app.py
```

The service will start on http://localhost:8004

### Docker

```bash
docker build -t language-detection .
docker run -p 8004:8004 language-detection
```

## API Endpoints

### POST /detect

Detect the language of text.

**Request:**
```json
{
  "text": "Hello, how are you?",
  "min_confidence": 0.5
}
```

**Response:**
```json
{
  "detected_language": {
    "language": "ENGLISH",
    "language_name": "English",
    "confidence": 0.999,
    "iso_code_639_1": "EN",
    "iso_code_639_3": "ENG"
  },
  "all_candidates": [
    {
      "language": "ENGLISH",
      "language_name": "English",
      "confidence": 0.999,
      "iso_code_639_1": "EN",
      "iso_code_639_3": "ENG"
    }
  ],
  "text_length": 19
}
```

### GET /health

Check service health.

**Response:**
```json
{
  "status": "healthy",
  "service": "language-detection",
  "version": "1.0.0",
  "languages_supported": 75
}
```

### GET /languages

List all supported languages.

**Response:**
```json
{
  "total": 75,
  "languages": [
    {
      "name": "ENGLISH",
      "display_name": "English",
      "iso_639_1": "EN",
      "iso_639_3": "ENG"
    },
    ...
  ]
}
```

## Integration with SmartCollectAPI

The language detection service is called by `LanguageDetectionService.cs` in the main API:

```csharp
var result = await _languageDetectionService.DetectLanguageAsync(text);
// result.Language = "ENGLISH"
// result.Confidence = 0.999
```

## Performance

- **Average Response Time**: < 50ms for texts up to 1000 characters
- **Memory Usage**: ~200MB
- **Concurrent Requests**: Supports 100+ concurrent requests

## Error Handling

- **400**: Text is empty
- **422**: Could not detect language or confidence below threshold
- **500**: Internal server error

## Configuration

The service runs on port 8004 by default. To change:

```python
uvicorn.run(app, host="0.0.0.0", port=YOUR_PORT)
```

Or use environment variable:
```bash
PORT=8005 python app.py
```

## Testing

```bash
# Test with curl
curl -X POST http://localhost:8004/detect \
  -H "Content-Type: application/json" \
  -d '{"text": "Bonjour le monde"}'

# Test with PowerShell
$body = @{ text = "Hola mundo" } | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:8004/detect -Method Post -Body $body -ContentType "application/json"
```

## Dependencies

- **fastapi**: Web framework
- **uvicorn**: ASGI server
- **pydantic**: Data validation
- **lingua-language-detector**: Core language detection library

## License

Part of SmartCollectAPI project.
