from typing import List, Dict, Optional, Any
from pydantic import BaseModel, Field
from datetime import datetime
from enum import Enum

class EntityType(str, Enum):
    PERSON = "PERSON"
    ORGANIZATION = "ORG"
    LOCATION = "LOC"
    DATE = "DATE"
    TIME = "TIME"
    MONEY = "MONEY"
    PERCENT = "PERCENT"
    PRODUCT = "PRODUCT"
    EVENT = "EVENT"
    WORK_OF_ART = "WORK_OF_ART"
    LAW = "LAW"
    LANGUAGE = "LANGUAGE"

class Entity(BaseModel):
    text: str
    type: str
    start: int
    end: int
    confidence: Optional[float] = None

class Classification(BaseModel):
    category: str
    confidence: float

class KeyPhrase(BaseModel):
    phrase: str
    score: float

class Document(BaseModel):
    id: str
    content: str
    metadata: Optional[Dict[str, Any]] = {}
    created_at: datetime = Field(default_factory=datetime.utcnow)

class NLPAnalysis(BaseModel):
    entities: List[Entity] = []
    classifications: List[Classification] = []
    key_phrases: List[KeyPhrase] = []
    language: Optional[str] = None
    language_confidence: Optional[float] = None
    embedding: Optional[List[float]] = None
    sentiment: Optional[Dict[str, float]] = None
    word_count: int = 0
    sentence_count: int = 0
    processing_time_ms: float = 0

class ProcessedDocument(BaseModel):
    document: Document
    analysis: NLPAnalysis
    processed_at: datetime = Field(default_factory=datetime.utcnow)
    processing_version: str = "1.0.0"
    model_used: str = "en_core_web_md"