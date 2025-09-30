from typing import List
from models.document import Entity
import logging

logger = logging.getLogger(__name__)

class EntityExtractor:
    def __init__(self, nlp_model):
        self.nlp = nlp_model
        
        # Entity type mapping from spaCy to our format
        self.entity_mapping = {
            "PERSON": "PERSON",
            "ORG": "ORGANIZATION", 
            "GPE": "LOCATION",  # Geopolitical entity
            "LOC": "LOCATION",
            "DATE": "DATE",
            "TIME": "TIME",
            "MONEY": "MONEY",
            "PERCENT": "PERCENT",
            "PRODUCT": "PRODUCT",
            "EVENT": "EVENT",
            "WORK_OF_ART": "WORK_OF_ART",
            "LAW": "LAW",
            "LANGUAGE": "LANGUAGE",
            "NORP": "ORGANIZATION",  # Nationalities or religious groups
            "FAC": "LOCATION",  # Buildings, airports, highways, bridges, etc.
            "ORDINAL": "NUMBER",
            "CARDINAL": "NUMBER",
            "QUANTITY": "QUANTITY"
        }
    
    def extract_entities(self, doc) -> List[Entity]:
        """Extract named entities from spaCy doc"""
        entities = []
        
        try:
            for ent in doc.ents:
                # Map spaCy entity type to our format
                entity_type = self.entity_mapping.get(ent.label_, ent.label_)
                
                # Skip very short entities or common stop words
                if len(ent.text.strip()) < 2:
                    continue
                
                # Calculate confidence based on entity properties
                confidence = self._calculate_confidence(ent)
                
                entity = Entity(
                    text=ent.text.strip(),
                    type=entity_type,
                    start=ent.start_char,
                    end=ent.end_char,
                    confidence=confidence
                )
                
                entities.append(entity)
            
            # Remove duplicates while preserving order
            unique_entities = []
            seen_texts = set()
            
            for entity in entities:
                entity_key = (entity.text.lower(), entity.type)
                if entity_key not in seen_texts:
                    seen_texts.add(entity_key)
                    unique_entities.append(entity)
            
            logger.debug(f"Extracted {len(unique_entities)} unique entities")
            return unique_entities
            
        except Exception as e:
            logger.error(f"Error extracting entities: {e}")
            return []
    
    def _calculate_confidence(self, ent) -> float:
        """Calculate confidence score for an entity"""
        # Base confidence from spaCy (if available)
        base_confidence = 0.8
        
        # Adjust based on entity length (longer entities are often more reliable)
        length_bonus = min(len(ent.text) * 0.02, 0.15)
        
        # Adjust based on entity type (some types are more reliable)
        type_confidence = {
            "PERSON": 0.9,
            "ORG": 0.85,
            "GPE": 0.9,
            "DATE": 0.95,
            "MONEY": 0.95,
            "PERCENT": 0.95
        }
        
        type_bonus = type_confidence.get(ent.label_, 0.8) - 0.8
        
        # Final confidence score
        confidence = min(base_confidence + length_bonus + type_bonus, 1.0)
        return round(confidence, 3)
    
    def filter_entities_by_type(self, entities: List[Entity], entity_types: List[str]) -> List[Entity]:
        """Filter entities by specific types"""
        return [entity for entity in entities if entity.type in entity_types]
    
    def filter_entities_by_confidence(self, entities: List[Entity], min_confidence: float = 0.5) -> List[Entity]:
        """Filter entities by minimum confidence threshold"""
        return [entity for entity in entities if entity.confidence and entity.confidence >= min_confidence]