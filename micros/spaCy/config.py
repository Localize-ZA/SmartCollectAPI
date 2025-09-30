import os
from dotenv import load_dotenv

load_dotenv()

class Config:
    # Service Configuration
    SERVICE_NAME = "spaCy NLP Service"
    SERVICE_VERSION = "1.0.0"
    SERVICE_PORT = int(os.getenv("SERVICE_PORT", "5084"))
    
    # Redis Configuration
    REDIS_HOST = os.getenv("REDIS_HOST", "localhost")
    REDIS_PORT = int(os.getenv("REDIS_PORT", "6379"))
    REDIS_DB = int(os.getenv("REDIS_DB", "0"))
    REDIS_PASSWORD = os.getenv("REDIS_PASSWORD", None)
    
    # Queue Names
    NLP_QUEUE = "nlp:processing:queue"
    NLP_RESULTS_QUEUE = "nlp:results:queue"
    
    # spaCy Configuration
    SPACY_MODEL = os.getenv("SPACY_MODEL", "en_core_web_sm")
    BATCH_SIZE = int(os.getenv("BATCH_SIZE", "10"))
    
    # Processing Options
    ENABLE_NER = os.getenv("ENABLE_NER", "true").lower() == "true"
    ENABLE_CLASSIFICATION = os.getenv("ENABLE_CLASSIFICATION", "true").lower() == "true"
    ENABLE_KEY_PHRASES = os.getenv("ENABLE_KEY_PHRASES", "true").lower() == "true"
    ENABLE_EMBEDDINGS = os.getenv("ENABLE_EMBEDDINGS", "true").lower() == "true"
    ENABLE_LANGUAGE_DETECTION = os.getenv("ENABLE_LANGUAGE_DETECTION", "true").lower() == "true"
    
    # API Configuration
    API_PREFIX = "/api/v1"
    CORS_ORIGINS = os.getenv("CORS_ORIGINS", "*").split(",")
    
    # Health Check
    HEALTH_CHECK_INTERVAL = int(os.getenv("HEALTH_CHECK_INTERVAL", "30"))

config = Config()