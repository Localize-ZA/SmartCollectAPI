from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from services.embedding_service import EmbeddingService
from pydantic import BaseModel
from typing import List, Optional
import logging

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(
    title="Sentence-Transformers Embedding Service",
    version="1.0.0",
    description="High-quality semantic embeddings using sentence-transformers"
)

# Add CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Initialize embedding service (loads model at startup)
embedding_service: Optional[EmbeddingService] = None

@app.on_event("startup")
async def startup_event():
    """Initialize embedding service on startup"""
    global embedding_service
    logger.info("Initializing Sentence-Transformers embedding service...")
    # Using all-mpnet-base-v2: 768 dimensions, best quality for general use
    embedding_service = EmbeddingService(model_name="all-mpnet-base-v2")
    logger.info("Embedding service ready!")

class EmbedRequest(BaseModel):
    text: str
    normalize: bool = True

class BatchEmbedRequest(BaseModel):
    texts: List[str]
    normalize: bool = True
    batch_size: int = 32

class EmbeddingResponse(BaseModel):
    embedding: List[float]
    dimensions: int
    model: str
    success: bool
    error_message: Optional[str] = None

class BatchEmbeddingResponse(BaseModel):
    embeddings: List[List[float]]
    dimensions: int
    count: int
    model: str
    success: bool
    error_message: Optional[str] = None

@app.post("/api/v1/embed/single", response_model=EmbeddingResponse)
async def embed_single(request: EmbedRequest):
    """
    Generate embedding for a single text
    
    Args:
        text: Text to embed
        normalize: Whether to normalize the embedding vector (recommended for cosine similarity)
    
    Returns:
        Embedding vector with metadata
    """
    try:
        if not embedding_service:
            raise HTTPException(status_code=503, detail="Embedding service not initialized")
        
        if not request.text or len(request.text.strip()) == 0:
            raise HTTPException(status_code=400, detail="Text cannot be empty")
        
        logger.info(f"Generating embedding for text of length: {len(request.text)}")
        
        embedding = embedding_service.encode(request.text, normalize=request.normalize)
        
        return EmbeddingResponse(
            embedding=embedding.tolist(),
            dimensions=len(embedding),
            model=embedding_service.model_name,
            success=True
        )
    
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Embedding generation failed: {str(e)}", exc_info=True)
        return EmbeddingResponse(
            embedding=[],
            dimensions=0,
            model="",
            success=False,
            error_message=str(e)
        )

@app.post("/api/v1/embed/batch", response_model=BatchEmbeddingResponse)
async def embed_batch(request: BatchEmbedRequest):
    """
    Generate embeddings for multiple texts in batch
    
    Args:
        texts: List of texts to embed
        normalize: Whether to normalize embedding vectors
        batch_size: Number of texts to process at once
    
    Returns:
        List of embedding vectors with metadata
    """
    try:
        if not embedding_service:
            raise HTTPException(status_code=503, detail="Embedding service not initialized")
        
        if not request.texts or len(request.texts) == 0:
            raise HTTPException(status_code=400, detail="Texts list cannot be empty")
        
        logger.info(f"Generating embeddings for {len(request.texts)} texts")
        
        embeddings = embedding_service.encode_batch(
            request.texts, 
            normalize=request.normalize,
            batch_size=request.batch_size
        )
        
        return BatchEmbeddingResponse(
            embeddings=[emb.tolist() for emb in embeddings],
            dimensions=len(embeddings[0]) if len(embeddings) > 0 else 0,
            count=len(embeddings),
            model=embedding_service.model_name,
            success=True
        )
    
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Batch embedding generation failed: {str(e)}", exc_info=True)
        return BatchEmbeddingResponse(
            embeddings=[],
            dimensions=0,
            count=0,
            model="",
            success=False,
            error_message=str(e)
        )

class SimilarityRequest(BaseModel):
    text1: str
    text2: str

class SimilarityResponse(BaseModel):
    similarity: float
    model: str
    success: bool

@app.post("/api/v1/similarity", response_model=SimilarityResponse)
async def compute_similarity(request: SimilarityRequest):
    """
    Compute cosine similarity between two texts
    
    Args:
        text1: First text
        text2: Second text
    
    Returns:
        Similarity score (0-1, higher is more similar)
    """
    try:
        if not embedding_service:
            raise HTTPException(status_code=503, detail="Embedding service not initialized")
        
        similarity = embedding_service.similarity(request.text1, request.text2)
        
        return SimilarityResponse(
            similarity=float(similarity),
            model=embedding_service.model_name,
            success=True
        )
    
    except Exception as e:
        logger.error(f"Similarity computation failed: {str(e)}", exc_info=True)
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/health")
async def health_check():
    """Health check endpoint"""
    return {
        "status": "healthy" if embedding_service else "initializing",
        "service": "sentence-transformers",
        "version": "1.0.0",
        "model": embedding_service.model_name if embedding_service else None,
        "dimensions": embedding_service.dimensions if embedding_service else None,
        "max_seq_length": embedding_service.max_seq_length if embedding_service else None
    }

@app.get("/api/v1/models")
async def list_models():
    """List available and current model information"""
    if not embedding_service:
        raise HTTPException(status_code=503, detail="Embedding service not initialized")
    
    available_models = [
        {
            "name": "all-MiniLM-L6-v2",
            "dimensions": 384,
            "max_tokens": 256,
            "description": "Fast and efficient, good for most use cases"
        },
        {
            "name": "all-mpnet-base-v2",
            "dimensions": 768,
            "max_tokens": 384,
            "description": "Best quality for general semantic search (current)"
        },
        {
            "name": "multi-qa-mpnet-base-dot-v1",
            "dimensions": 768,
            "max_tokens": 512,
            "description": "Optimized for question-answering and search"
        },
        {
            "name": "all-MiniLM-L12-v2",
            "dimensions": 384,
            "max_tokens": 256,
            "description": "Larger version of MiniLM, better quality"
        }
    ]
    
    return {
        "current_model": {
            "name": embedding_service.model_name,
            "dimensions": embedding_service.dimensions,
            "max_seq_length": embedding_service.max_seq_length
        },
        "available_models": available_models
    }

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=5086)
