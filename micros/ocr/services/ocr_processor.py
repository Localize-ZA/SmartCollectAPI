import easyocr
import io
import logging
from PIL import Image
import numpy as np
from typing import Dict, List, Any
import os

logger = logging.getLogger(__name__)

class OCRProcessor:
    """
    Wrapper for EasyOCR that handles text extraction from images
    """
    
    def __init__(self, languages: List[str] = None, gpu: bool = True):
        """
        Initialize EasyOCR reader
        
        Args:
            languages: List of language codes (e.g., ['en', 'fr']). Defaults to English only.
            gpu: Whether to use GPU acceleration
        """
        if languages is None:
            languages = ['en']  # Default to English
        
        self.supported_languages = languages
        self.gpu_available = gpu and self._check_gpu()
        
        logger.info(f"Initializing EasyOCR with languages: {languages}, GPU: {self.gpu_available}")
        
        try:
            self.reader = easyocr.Reader(
                languages, 
                gpu=self.gpu_available,
                verbose=False
            )
            logger.info("EasyOCR reader initialized successfully")
        except Exception as e:
            logger.error(f"Failed to initialize EasyOCR: {str(e)}")
            # Fallback to CPU if GPU initialization fails
            if self.gpu_available:
                logger.warning("GPU initialization failed, falling back to CPU")
                self.gpu_available = False
                self.reader = easyocr.Reader(languages, gpu=False, verbose=False)
            else:
                raise
    
    def _check_gpu(self) -> bool:
        """Check if CUDA/GPU is available"""
        try:
            import torch
            return torch.cuda.is_available()
        except ImportError:
            return False
    
    def extract_text(self, image_bytes: bytes, mime_type: str = "image/jpeg") -> Dict[str, Any]:
        """
        Extract text from image bytes
        
        Args:
            image_bytes: Raw image bytes
            mime_type: MIME type of the image
        
        Returns:
            Dictionary containing:
                - text: Extracted text (concatenated)
                - confidence: Average confidence score
                - bounding_boxes: List of bounding boxes with text and confidence
                - languages: Detected languages
        """
        try:
            # Convert bytes to PIL Image
            image = Image.open(io.BytesIO(image_bytes))
            
            # Convert to RGB if necessary
            if image.mode not in ('RGB', 'L'):
                image = image.convert('RGB')
            
            # Convert PIL Image to numpy array
            image_array = np.array(image)
            
            # Perform OCR
            results = self.reader.readtext(image_array)
            
            # Process results
            extracted_texts = []
            bounding_boxes = []
            confidences = []
            
            for detection in results:
                bbox, text, confidence = detection
                
                extracted_texts.append(text)
                confidences.append(confidence)
                
                # Convert bbox coordinates to dict
                bounding_boxes.append({
                    "text": text,
                    "confidence": float(confidence),
                    "bbox": {
                        "top_left": {"x": float(bbox[0][0]), "y": float(bbox[0][1])},
                        "top_right": {"x": float(bbox[1][0]), "y": float(bbox[1][1])},
                        "bottom_right": {"x": float(bbox[2][0]), "y": float(bbox[2][1])},
                        "bottom_left": {"x": float(bbox[3][0]), "y": float(bbox[3][1])}
                    }
                })
            
            # Combine all text
            full_text = "\n".join(extracted_texts)
            
            # Calculate average confidence
            avg_confidence = sum(confidences) / len(confidences) if confidences else 0.0
            
            return {
                "text": full_text,
                "confidence": avg_confidence,
                "bounding_boxes": bounding_boxes,
                "languages": self.supported_languages,
                "success": True
            }
        
        except Exception as e:
            logger.error(f"OCR extraction failed: {str(e)}", exc_info=True)
            return {
                "text": "",
                "confidence": 0.0,
                "bounding_boxes": [],
                "languages": [],
                "success": False,
                "error": str(e)
            }
    
    def extract_from_file(self, file_path: str) -> Dict[str, Any]:
        """
        Extract text from an image file
        
        Args:
            file_path: Path to the image file
        
        Returns:
            Same structure as extract_text()
        """
        with open(file_path, 'rb') as f:
            image_bytes = f.read()
        
        # Detect MIME type from extension
        ext = os.path.splitext(file_path)[1].lower()
        mime_map = {
            '.jpg': 'image/jpeg',
            '.jpeg': 'image/jpeg',
            '.png': 'image/png',
            '.gif': 'image/gif',
            '.bmp': 'image/bmp',
            '.tiff': 'image/tiff',
            '.tif': 'image/tiff'
        }
        mime_type = mime_map.get(ext, 'image/jpeg')
        
        return self.extract_text(image_bytes, mime_type)
