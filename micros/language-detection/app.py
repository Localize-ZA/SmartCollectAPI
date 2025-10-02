# Language Detection Microservice
# Uses lingua-py for accurate language detection

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from typing import Optional, List
from lingua import Language, LanguageDetectorBuilder
import logging

# Setup logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Initialize FastAPI app
app = FastAPI(
    title="Language Detection Service",
    description="Detects the language of text using lingua library",
    version="1.0.0"
)

# Initialize lingua detector with all languages
# This may take a few seconds on first start
logger.info("Initializing language detector...")
detector = LanguageDetectorBuilder.from_all_languages().build()
logger.info("Language detector initialized successfully")

# Request/Response Models
class DetectionRequest(BaseModel):
    text: str
    min_confidence: float = 0.0
    
    class Config:
        json_schema_extra = {
            "example": {
                "text": "Hello, how are you today?",
                "min_confidence": 0.5
            }
        }

class LanguageResult(BaseModel):
    language: str
    language_name: str
    confidence: float
    iso_code_639_1: str
    iso_code_639_3: str

class DetectionResponse(BaseModel):
    detected_language: LanguageResult
    all_candidates: List[LanguageResult]
    text_length: int

class HealthResponse(BaseModel):
    status: str
    service: str
    version: str
    languages_supported: int

# Endpoints
@app.get("/health", response_model=HealthResponse)
async def health():
    """Health check endpoint"""
    return HealthResponse(
        status="healthy",
        service="language-detection",
        version="1.0.0",
        languages_supported=len(Language)
    )

@app.post("/detect", response_model=DetectionResponse)
async def detect_language(request: DetectionRequest):
    """
    Detect the language of the provided text.
    
    Returns the most confident language along with all candidates.
    """
    if not request.text or len(request.text.strip()) == 0:
        raise HTTPException(status_code=400, detail="Text cannot be empty")
    
    try:
        # Get all confidence values
        confidence_values = detector.compute_language_confidence_values(request.text)
        
        if not confidence_values:
            raise HTTPException(
                status_code=422, 
                detail="Could not detect language from the provided text"
            )
        
        # Sort by confidence
        sorted_candidates = sorted(
            confidence_values, 
            key=lambda x: x.value, 
            reverse=True
        )
        
        # Get the most confident result
        top_result = sorted_candidates[0]
        
        # Check if confidence meets minimum threshold
        if top_result.value < request.min_confidence:
            raise HTTPException(
                status_code=422,
                detail=f"Confidence {top_result.value:.3f} is below minimum {request.min_confidence}"
            )
        
        # Build response
        detected_language = LanguageResult(
            language=top_result.language.name,
            language_name=get_language_name(top_result.language),
            confidence=top_result.value,
            iso_code_639_1=top_result.language.iso_code_639_1.name,
            iso_code_639_3=top_result.language.iso_code_639_3.name
        )
        
        # Build all candidates list (top 5)
        all_candidates = [
            LanguageResult(
                language=candidate.language.name,
                language_name=get_language_name(candidate.language),
                confidence=candidate.value,
                iso_code_639_1=candidate.language.iso_code_639_1.name,
                iso_code_639_3=candidate.language.iso_code_639_3.name
            )
            for candidate in sorted_candidates[:5]
        ]
        
        logger.info(
            f"Detected language: {detected_language.language} "
            f"(confidence: {detected_language.confidence:.3f})"
        )
        
        return DetectionResponse(
            detected_language=detected_language,
            all_candidates=all_candidates,
            text_length=len(request.text)
        )
        
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error detecting language: {str(e)}")
        raise HTTPException(status_code=500, detail=f"Internal error: {str(e)}")

def get_language_name(language: Language) -> str:
    """Get human-readable language name"""
    language_names = {
        Language.ENGLISH: "English",
        Language.SPANISH: "Spanish",
        Language.FRENCH: "French",
        Language.GERMAN: "German",
        Language.ITALIAN: "Italian",
        Language.PORTUGUESE: "Portuguese",
        Language.RUSSIAN: "Russian",
        Language.CHINESE: "Chinese",
        Language.JAPANESE: "Japanese",
        Language.KOREAN: "Korean",
        Language.ARABIC: "Arabic",
        Language.HINDI: "Hindi",
        Language.DUTCH: "Dutch",
        Language.POLISH: "Polish",
        Language.TURKISH: "Turkish",
        Language.VIETNAMESE: "Vietnamese",
        Language.THAI: "Thai",
        Language.SWEDISH: "Swedish",
        Language.NORWEGIAN: "Norwegian",
        Language.DANISH: "Danish",
        Language.FINNISH: "Finnish",
        Language.GREEK: "Greek",
        Language.HEBREW: "Hebrew",
        Language.INDONESIAN: "Indonesian",
        Language.MALAY: "Malay",
    }
    return language_names.get(language, language.name.title())

@app.get("/languages")
async def list_supported_languages():
    """List all supported languages"""
    languages = [
        {
            "name": lang.name,
            "display_name": get_language_name(lang),
            "iso_639_1": lang.iso_code_639_1.name,
            "iso_639_3": lang.iso_code_639_3.name
        }
        for lang in Language
    ]
    return {
        "total": len(languages),
        "languages": sorted(languages, key=lambda x: x["display_name"])
    }

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8004, log_level="info")
