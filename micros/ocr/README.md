# EasyOCR Microservice

Advanced OCR service using EasyOCR for text extraction from images.

## Features

- ✅ Deep learning-based OCR (better than Tesseract)
- ✅ 80+ languages supported
- ✅ GPU acceleration (optional)
- ✅ Batch processing
- ✅ Bounding box detection
- ✅ Confidence scores

## Setup

### Windows
```powershell
.\setup.ps1
```

### Linux/Mac
```bash
chmod +x setup.sh
./setup.sh
```

### Manual Setup
```bash
python -m venv venv
source venv/bin/activate  # Windows: .\venv\Scripts\Activate.ps1
pip install -r requirements.txt
```

## Running

```bash
# Activate virtual environment
.\venv\Scripts\Activate.ps1  # Windows
source venv/bin/activate     # Linux/Mac

# Start the service
uvicorn app:app --reload --port 5085
```

## API Endpoints

### Extract Text from Image
```bash
POST http://localhost:5085/api/v1/ocr/extract
Content-Type: multipart/form-data

file: <image file>
```

**Response:**
```json
{
  "text": "Extracted text here",
  "confidence": 0.95,
  "bounding_boxes": [
    {
      "text": "word",
      "confidence": 0.98,
      "bbox": {
        "top_left": {"x": 10, "y": 20},
        "top_right": {"x": 50, "y": 20},
        "bottom_right": {"x": 50, "y": 40},
        "bottom_left": {"x": 10, "y": 40}
      }
    }
  ],
  "language_detected": ["en"],
  "success": true
}
```

### Batch OCR
```bash
POST http://localhost:5085/api/v1/ocr/batch
Content-Type: multipart/form-data

files: <multiple image files>
```

### Health Check
```bash
GET http://localhost:5085/health
```

**Response:**
```json
{
  "status": "healthy",
  "service": "easyocr",
  "version": "1.0.0",
  "languages_supported": ["en"],
  "gpu_available": false
}
```

### List Supported Languages
```bash
GET http://localhost:5085/api/v1/languages
```

## Configuration

Edit `services/ocr_processor.py` to change:

- **Languages:** Default is English only. Add more languages:
  ```python
  OCRProcessor(languages=['en', 'fr', 'es'])
  ```

- **GPU:** Enable/disable GPU acceleration:
  ```python
  OCRProcessor(gpu=True)  # Requires CUDA + PyTorch with CUDA
  ```

## GPU Support

For GPU acceleration:

1. Install CUDA toolkit
2. Install PyTorch with CUDA:
   ```bash
   pip install torch torchvision --index-url https://download.pytorch.org/whl/cu118
   ```

## Supported Image Formats

- JPEG / JPG
- PNG
- GIF
- BMP
- TIFF / TIF

## Language Codes

Common languages:
- `en` - English
- `fr` - French
- `es` - Spanish
- `de` - German
- `zh` - Chinese
- `ja` - Japanese
- `ko` - Korean
- `ar` - Arabic
- `hi` - Hindi
- `ru` - Russian

[Full list](https://www.jaided.ai/easyocr/)

## Performance

- **CPU:** ~2-5 seconds per image
- **GPU:** ~0.5-1 second per image
- **Memory:** ~1-2 GB RAM (increases with more languages)

## Troubleshooting

**Issue:** Out of memory
- **Fix:** Process images in smaller batches or reduce image resolution

**Issue:** Slow performance
- **Fix:** Enable GPU acceleration or reduce image size

**Issue:** Poor accuracy
- **Fix:** Ensure image quality is good, try different languages

## Integration

See `Server/Services/Providers/EasyOcrService.cs` for .NET integration.
