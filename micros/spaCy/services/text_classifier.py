from typing import List
from models.document import Classification
import logging

logger = logging.getLogger(__name__)

class TextClassifier:
    def __init__(self, nlp_model):
        self.nlp = nlp_model
        
        # Document classification categories based on content patterns
        self.classification_rules = {
            "financial": {
                "keywords": ["invoice", "payment", "receipt", "transaction", "financial", "accounting", "budget", "cost", "revenue", "profit"],
                "entities": ["MONEY", "PERCENT", "ORG"],
                "base_score": 0.1
            },
            "legal": {
                "keywords": ["contract", "agreement", "legal", "law", "court", "attorney", "clause", "terms", "conditions", "compliance"],
                "entities": ["LAW", "ORG", "PERSON"],
                "base_score": 0.1
            },
            "personal": {
                "keywords": ["personal", "private", "confidential", "individual", "employee", "staff", "personnel"],
                "entities": ["PERSON", "GPE"],
                "base_score": 0.1
            },
            "technical": {
                "keywords": ["technical", "specification", "manual", "documentation", "system", "software", "hardware", "API", "database"],
                "entities": ["PRODUCT", "ORG"],
                "base_score": 0.1
            },
            "communication": {
                "keywords": ["email", "message", "communication", "correspondence", "memo", "letter", "notification", "announcement"],
                "entities": ["PERSON", "ORG", "DATE"],
                "base_score": 0.1
            },
            "report": {
                "keywords": ["report", "analysis", "summary", "findings", "results", "conclusion", "recommendation", "overview"],
                "entities": ["DATE", "PERCENT", "CARDINAL"],
                "base_score": 0.1
            },
            "marketing": {
                "keywords": ["marketing", "advertising", "promotion", "campaign", "brand", "customer", "sales", "product"],
                "entities": ["ORG", "PRODUCT", "MONEY"],
                "base_score": 0.1
            },
            "administrative": {
                "keywords": ["administrative", "policy", "procedure", "guideline", "process", "workflow", "form", "application"],
                "entities": ["ORG", "DATE", "PERSON"],
                "base_score": 0.1
            }
        }
    
    def classify_text(self, doc) -> List[Classification]:
        """Classify document based on content analysis"""
        classifications = []
        text_lower = doc.text.lower()
        
        # Extract entities for classification
        entities = {ent.label_ for ent in doc.ents}
        
        try:
            for category, rules in self.classification_rules.items():
                score = self._calculate_category_score(text_lower, entities, rules)
                
                if score > 0.2:  # Minimum threshold for classification
                    classifications.append(Classification(
                        category=category,
                        confidence=round(score, 3)
                    ))
            
            # Sort by confidence
            classifications.sort(key=lambda x: x.confidence, reverse=True)
            
            # Return top 3 classifications
            result = classifications[:3]
            
            # If no strong classifications found, add a generic one
            if not result:
                result.append(Classification(
                    category="general",
                    confidence=0.5
                ))
            
            logger.debug(f"Document classified into {len(result)} categories")
            return result
            
        except Exception as e:
            logger.error(f"Error classifying text: {e}")
            return [Classification(category="unknown", confidence=0.1)]
    
    def _calculate_category_score(self, text: str, entities: set, rules: dict) -> float:
        """Calculate classification score for a category"""
        score = rules["base_score"]
        
        # Score based on keyword matches
        keyword_matches = sum(1 for keyword in rules["keywords"] if keyword in text)
        keyword_score = min(keyword_matches * 0.15, 0.6)  # Max 0.6 from keywords
        
        # Score based on entity types
        entity_matches = sum(1 for entity_type in rules["entities"] if entity_type in entities)
        entity_score = min(entity_matches * 0.1, 0.3)  # Max 0.3 from entities
        
        # Combine scores
        total_score = score + keyword_score + entity_score
        
        # Apply length penalty for very short documents
        if len(text.split()) < 10:
            total_score *= 0.5
        
        return min(total_score, 1.0)
    
    def get_category_explanation(self, category: str) -> str:
        """Get explanation for a classification category"""
        explanations = {
            "financial": "Document contains financial information such as invoices, payments, or accounting data",
            "legal": "Document contains legal content such as contracts, agreements, or legal documentation",
            "personal": "Document contains personal or personnel-related information",
            "technical": "Document contains technical specifications, manuals, or system documentation",
            "communication": "Document is a form of communication such as emails, memos, or letters",
            "report": "Document is a report containing analysis, findings, or recommendations",
            "marketing": "Document contains marketing or promotional content",
            "administrative": "Document contains administrative policies, procedures, or guidelines",
            "general": "Document does not fit into specific categories",
            "unknown": "Document classification could not be determined"
        }
        
        return explanations.get(category, "No explanation available")
    
    def add_custom_classification_rule(self, category: str, keywords: List[str], entities: List[str], base_score: float = 0.1):
        """Add a custom classification rule"""
        self.classification_rules[category] = {
            "keywords": keywords,
            "entities": entities,
            "base_score": base_score
        }
        logger.info(f"Added custom classification rule for category: {category}")