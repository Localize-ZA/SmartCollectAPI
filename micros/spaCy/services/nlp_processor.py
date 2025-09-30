import spacy
import time
from typing import List, Dict, Optional
from models.document import Document, NLPAnalysis, Entity, Classification, KeyPhrase
from services.entity_extractor import EntityExtractor
from services.text_classifier import TextClassifier
from config import config
import logging

logger = logging.getLogger(__name__)

class NLPProcessor:
    def __init__(self):
        self.nlp = None
        self.entity_extractor = None
        self.text_classifier = None
        self._load_models()
    
    def _load_models(self):
        """Load spaCy model and initialize components"""
        try:
            logger.info(f"Loading spaCy model: {config.SPACY_MODEL}")
            self.nlp = spacy.load(config.SPACY_MODEL)
            
            # Initialize specialized processors
            self.entity_extractor = EntityExtractor(self.nlp)
            self.text_classifier = TextClassifier(self.nlp)
            
            logger.info("NLP models loaded successfully")
        except Exception as e:
            logger.error(f"Failed to load NLP models: {e}")
            raise
    
    def process_document(self, document: Document) -> NLPAnalysis:
        """Process a document and return NLP analysis"""
        start_time = time.time()
        
        try:
            # Process text with spaCy
            doc = self.nlp(document.content)
            
            analysis = NLPAnalysis()
            
            # Basic statistics
            analysis.word_count = len([token for token in doc if not token.is_space])
            analysis.sentence_count = len(list(doc.sents))
            
            # Named Entity Recognition
            if config.ENABLE_NER:
                analysis.entities = self.entity_extractor.extract_entities(doc)
            
            # Text Classification
            if config.ENABLE_CLASSIFICATION:
                analysis.classifications = self.text_classifier.classify_text(doc)
            
            # Key Phrase Extraction
            if config.ENABLE_KEY_PHRASES:
                analysis.key_phrases = self._extract_key_phrases(doc)
            
            # Language Detection
            if config.ENABLE_LANGUAGE_DETECTION:
                analysis.language, analysis.language_confidence = self._detect_language(doc)
            
            # Document Embeddings
            if config.ENABLE_EMBEDDINGS and doc.vector.any():
                analysis.embedding = doc.vector.tolist()
            
            # Sentiment Analysis (basic)
            analysis.sentiment = self._analyze_sentiment(doc)
            
            # Processing time
            analysis.processing_time_ms = (time.time() - start_time) * 1000
            
            logger.info(f"Document processed successfully in {analysis.processing_time_ms:.2f}ms")
            return analysis
            
        except Exception as e:
            logger.error(f"Error processing document: {e}")
            raise
    
    def _extract_key_phrases(self, doc) -> List[KeyPhrase]:
        """Extract key phrases using noun chunks and named entities"""
        phrases = []
        
        # Extract noun chunks
        for chunk in doc.noun_chunks:
            if len(chunk.text.strip()) > 2:  # Skip very short phrases
                phrases.append(KeyPhrase(
                    phrase=chunk.text.strip(),
                    score=0.8  # Simple scoring for now
                ))
        
        # Extract named entities as key phrases
        for ent in doc.ents:
            phrases.append(KeyPhrase(
                phrase=ent.text,
                score=0.9  # Higher score for named entities
            ))
        
        # Remove duplicates and sort by score
        unique_phrases = {}
        for phrase in phrases:
            if phrase.phrase.lower() not in unique_phrases:
                unique_phrases[phrase.phrase.lower()] = phrase
        
        return sorted(list(unique_phrases.values()), key=lambda x: x.score, reverse=True)[:10]
    
    def _detect_language(self, doc) -> tuple[Optional[str], Optional[float]]:
        """Detect document language - basic implementation"""
        # For now, assume English since we're using en_core_web_md
        # In production, you might want to use langdetect or similar
        return "en", 0.95
    
    def _analyze_sentiment(self, doc) -> Dict[str, float]:
        """Basic sentiment analysis using token sentiment scores"""
        if not hasattr(doc[0], 'sentiment'):
            return {"positive": 0.0, "negative": 0.0, "neutral": 1.0}
        
        # Simple sentiment calculation based on token polarity
        sentiments = [token.sentiment for token in doc if hasattr(token, 'sentiment')]
        
        if not sentiments:
            return {"positive": 0.0, "negative": 0.0, "neutral": 1.0}
        
        avg_sentiment = sum(sentiments) / len(sentiments)
        
        if avg_sentiment > 0.1:
            return {"positive": min(avg_sentiment, 1.0), "negative": 0.0, "neutral": 0.0}
        elif avg_sentiment < -0.1:
            return {"positive": 0.0, "negative": min(abs(avg_sentiment), 1.0), "neutral": 0.0}
        else:
            return {"positive": 0.0, "negative": 0.0, "neutral": 1.0}
    
    def process_batch(self, documents: List[Document]) -> List[NLPAnalysis]:
        """Process multiple documents efficiently"""
        results = []
        
        # Process documents in batches for better performance
        for doc in documents:
            try:
                analysis = self.process_document(doc)
                results.append(analysis)
            except Exception as e:
                logger.error(f"Failed to process document {doc.id}: {e}")
                # Create empty analysis for failed documents
                results.append(NLPAnalysis(processing_time_ms=0))
        
        return results
    
    def health_check(self) -> Dict[str, any]:
        """Health check for the NLP processor"""
        try:
            # Test processing with a simple document
            test_doc = Document(
                id="health_check",
                content="This is a test document for health checking."
            )
            
            start_time = time.time()
            analysis = self.process_document(test_doc)
            processing_time = (time.time() - start_time) * 1000
            
            return {
                "status": "healthy",
                "model": config.SPACY_MODEL,
                "test_processing_time_ms": processing_time,
                "features": {
                    "ner": config.ENABLE_NER,
                    "classification": config.ENABLE_CLASSIFICATION,
                    "key_phrases": config.ENABLE_KEY_PHRASES,
                    "embeddings": config.ENABLE_EMBEDDINGS,
                    "language_detection": config.ENABLE_LANGUAGE_DETECTION
                }
            }
        except Exception as e:
            return {
                "status": "unhealthy",
                "error": str(e)
            }