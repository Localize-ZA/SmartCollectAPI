from fastapi import FastAPI, UploadFile, File, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from services.ocr_processor import OCRProcessor
from pydantic import BaseModel
from typing import List, Optional
import logging

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(
    title="EasyOCR Service",
    version="1.0.0",
    description="Advanced OCR service using EasyOCR for text extraction from images"
)

# Add CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Initialize OCR processor (loads model at startup)
ocr_processor: Optional[OCRProcessor] = None

@app.on_event("startup")
async def startup_event():
    """Initialize OCR processor on startup"""
    global ocr_processor
    logger.info("Initializing EasyOCR processor...")
    ocr_processor = OCRProcessor()
    logger.info("EasyOCR processor ready!")

class OCRResponse(BaseModel):
    text: str
    confidence: float
    bounding_boxes: List[dict]
    language_detected: List[str]
    success: bool
    error_message: Optional[str] = None

@app.post("/api/v1/ocr/extract", response_model=OCRResponse)
async def extract_text(file: UploadFile = File(...)):
    """
    Extract text from an image using EasyOCR
    
    Supports: JPEG, PNG, GIF, BMP, TIFF, PDF (first page)
    """
    try:
        if not ocr_processor:
            raise HTTPException(status_code=503, detail="OCR processor not initialized")
        
        logger.info(f"Processing file: {file.filename}, content_type: {file.content_type}")
        
        # Read file contents
        contents = await file.read()
        
        # Process with EasyOCR
        result = ocr_processor.extract_text(contents, file.content_type or "image/jpeg")
        
        return OCRResponse(
            text=result["text"],
            confidence=result["confidence"],
            bounding_boxes=result["bounding_boxes"],
            language_detected=result["languages"],
            success=True
        )
    
    except Exception as e:
        logger.error(f"OCR extraction failed: {str(e)}", exc_info=True)
        return OCRResponse(
            text="",
            confidence=0.0,
            bounding_boxes=[],
            language_detected=[],
            success=False,
            error_message=str(e)
        )

class BatchOCRRequest(BaseModel):
    """Request for batch OCR processing"""
    image_urls: List[str]

@app.post("/api/v1/ocr/batch")
async def batch_extract(files: List[UploadFile] = File(...)):
    """
    Extract text from multiple images in batch
    """
    try:
        if not ocr_processor:
            raise HTTPException(status_code=503, detail="OCR processor not initialized")
        
        results = []
        for file in files:
            contents = await file.read()
            result = ocr_processor.extract_text(contents, file.content_type or "image/jpeg")
            results.append({
                "filename": file.filename,
                "text": result["text"],
                "confidence": result["confidence"],
                "success": True
            })
        
        return {
            "results": results,
            "total_processed": len(results),
            "success": True
        }
    
    except Exception as e:
        logger.error(f"Batch OCR failed: {str(e)}", exc_info=True)
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/health")
async def health_check():
    """Health check endpoint"""
    return {
        "status": "healthy" if ocr_processor else "initializing",
        "service": "easyocr",
        "version": "1.0.0",
        "languages_supported": ["en"] if ocr_processor else [],
        "gpu_available": ocr_processor.gpu_available if ocr_processor else False
    }

@app.get("/api/v1/languages")
async def list_languages():
    """List all supported languages"""
    if not ocr_processor:
        raise HTTPException(status_code=503, detail="OCR processor not initialized")
    
    return {
        "languages": ocr_processor.supported_languages,
        "default": "en"
    }

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=5085)
