from sentence_transformers import SentenceTransformer, util
import numpy as np
import logging
from typing import List, Union
import torch

logger = logging.getLogger(__name__)

class EmbeddingService:
    """
    Wrapper for sentence-transformers that generates semantic embeddings
    """
    
    def __init__(self, model_name: str = "all-mpnet-base-v2", device: str = None):
        """
        Initialize SentenceTransformer model
        
        Args:
            model_name: Name of the model to load from sentence-transformers
                Popular options:
                - all-MiniLM-L6-v2: 384 dims, fast
                - all-mpnet-base-v2: 768 dims, best quality (default)
                - multi-qa-mpnet-base-dot-v1: 768 dims, optimized for Q&A
            device: Device to use ('cuda', 'cpu', or None for auto-detect)
        """
        self.model_name = model_name
        
        # Auto-detect device if not specified
        if device is None:
            device = 'cuda' if torch.cuda.is_available() else 'cpu'
        
        self.device = device
        
        logger.info(f"Loading sentence-transformers model: {model_name} on {device}")
        
        try:
            self.model = SentenceTransformer(model_name, device=device)
            self.dimensions = self.model.get_sentence_embedding_dimension()
            self.max_seq_length = self.model.max_seq_length
            
            logger.info(f"Model loaded successfully:")
            logger.info(f"  - Dimensions: {self.dimensions}")
            logger.info(f"  - Max sequence length: {self.max_seq_length}")
            logger.info(f"  - Device: {device}")
            logger.info(f"  - GPU available: {torch.cuda.is_available()}")
            
        except Exception as e:
            logger.error(f"Failed to load model {model_name}: {str(e)}")
            raise
    
    def encode(self, text: str, normalize: bool = True) -> np.ndarray:
        """
        Generate embedding for a single text
        
        Args:
            text: Text to embed
            normalize: Whether to normalize the embedding (recommended for cosine similarity)
        
        Returns:
            Numpy array of embedding vector
        """
        try:
            embedding = self.model.encode(
                text,
                convert_to_numpy=True,
                normalize_embeddings=normalize,
                show_progress_bar=False
            )
            return embedding
        
        except Exception as e:
            logger.error(f"Encoding failed: {str(e)}", exc_info=True)
            raise
    
    def encode_batch(
        self, 
        texts: List[str], 
        normalize: bool = True,
        batch_size: int = 32
    ) -> np.ndarray:
        """
        Generate embeddings for multiple texts
        
        Args:
            texts: List of texts to embed
            normalize: Whether to normalize embeddings
            batch_size: Number of texts to process at once
        
        Returns:
            Numpy array of shape (len(texts), embedding_dim)
        """
        try:
            embeddings = self.model.encode(
                texts,
                convert_to_numpy=True,
                normalize_embeddings=normalize,
                batch_size=batch_size,
                show_progress_bar=len(texts) > 100  # Show progress for large batches
            )
            return embeddings
        
        except Exception as e:
            logger.error(f"Batch encoding failed: {str(e)}", exc_info=True)
            raise
    
    def similarity(self, text1: str, text2: str) -> float:
        """
        Compute cosine similarity between two texts
        
        Args:
            text1: First text
            text2: Second text
        
        Returns:
            Similarity score (0-1, higher means more similar)
        """
        try:
            # Encode both texts
            emb1 = self.encode(text1, normalize=True)
            emb2 = self.encode(text2, normalize=True)
            
            # Compute cosine similarity
            similarity = util.cos_sim(emb1, emb2).item()
            
            return similarity
        
        except Exception as e:
            logger.error(f"Similarity computation failed: {str(e)}", exc_info=True)
            raise
    
    def find_most_similar(
        self, 
        query: str, 
        candidates: List[str], 
        top_k: int = 5
    ) -> List[dict]:
        """
        Find the most similar texts to a query
        
        Args:
            query: Query text
            candidates: List of candidate texts
            top_k: Number of top results to return
        
        Returns:
            List of dicts with 'text', 'score', and 'index'
        """
        try:
            # Encode query and candidates
            query_emb = self.encode(query, normalize=True)
            candidate_embs = self.encode_batch(candidates, normalize=True)
            
            # Compute similarities
            similarities = util.cos_sim(query_emb, candidate_embs)[0]
            
            # Get top-k indices
            top_indices = torch.topk(similarities, k=min(top_k, len(candidates))).indices
            
            # Build results
            results = []
            for idx in top_indices:
                idx = idx.item()
                results.append({
                    "text": candidates[idx],
                    "score": float(similarities[idx]),
                    "index": idx
                })
            
            return results
        
        except Exception as e:
            logger.error(f"Find most similar failed: {str(e)}", exc_info=True)
            raise
