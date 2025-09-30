from fastapi import FastAPI, HTTPException, BackgroundTasks
from fastapi.middleware.cors import CORSMiddleware
from contextlib import asynccontextmanager
import logging
import uuid
from datetime import datetime
from typing import List, Optional

from models.document import Document, ProcessedDocument, NLPAnalysis
from models.job import ProcessingJob, JobStatus
from services.nlp_processor import NLPProcessor
from services.redis_service import RedisService
from config import config

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

# Global services
nlp_processor = None
redis_service = None

@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan handler"""
    global nlp_processor, redis_service
    
    try:
        # Initialize services
        logger.info("Initializing spaCy NLP Service...")
        nlp_processor = NLPProcessor()
        redis_service = RedisService()
        logger.info("Services initialized successfully")
        
        yield
        
    except Exception as e:
        logger.error(f"Failed to initialize services: {e}")
        raise
    finally:
        logger.info("Shutting down spaCy NLP Service...")

# Create FastAPI app
app = FastAPI(
    title=config.SERVICE_NAME,
    version=config.SERVICE_VERSION,
    description="NLP processing service using spaCy for document analysis",
    lifespan=lifespan
)

# Add CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=config.CORS_ORIGINS,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# API Routes
@app.get("/")
async def root():
    """Root endpoint"""
    return {
        "service": config.SERVICE_NAME,
        "version": config.SERVICE_VERSION,
        "status": "running",
        "timestamp": datetime.utcnow().isoformat()
    }

@app.get("/health")
async def health_check():
    """Health check endpoint"""
    try:
        nlp_health = nlp_processor.health_check()
        redis_health = redis_service.health_check()
        
        overall_status = "healthy" if (
            nlp_health.get("status") == "healthy" and 
            redis_health.get("status") == "healthy"
        ) else "unhealthy"
        
        return {
            "status": overall_status,
            "timestamp": datetime.utcnow().isoformat(),
            "service": config.SERVICE_NAME,
            "version": config.SERVICE_VERSION,
            "components": {
                "nlp_processor": nlp_health,
                "redis": redis_health
            },
            "queues": {
                "processing": redis_service.get_queue_length(config.NLP_QUEUE),
                "results": redis_service.get_queue_length(config.NLP_RESULTS_QUEUE)
            }
        }
    except Exception as e:
        logger.error(f"Health check failed: {e}")
        raise HTTPException(status_code=503, detail=f"Service unhealthy: {str(e)}")

@app.post("/api/v1/process", response_model=ProcessedDocument)
async def process_document(document: Document):
    """Process a single document synchronously"""
    try:
        logger.info(f"Processing document {document.id}")
        
        # Process document with NLP
        analysis = nlp_processor.process_document(document)
        
        # Create result
        result = ProcessedDocument(
            document=document,
            analysis=analysis,
            processed_at=datetime.utcnow(),
            processing_version=config.SERVICE_VERSION,
            model_used=config.SPACY_MODEL
        )
        
        logger.info(f"Successfully processed document {document.id}")
        return result
        
    except Exception as e:
        logger.error(f"Failed to process document {document.id}: {e}")
        raise HTTPException(status_code=500, detail=f"Processing failed: {str(e)}")

@app.post("/api/v1/process/batch", response_model=List[ProcessedDocument])
async def process_documents_batch(documents: List[Document]):
    """Process multiple documents in batch"""
    try:
        if len(documents) > config.BATCH_SIZE:
            raise HTTPException(
                status_code=400, 
                detail=f"Batch size exceeds maximum of {config.BATCH_SIZE}"
            )
        
        logger.info(f"Processing batch of {len(documents)} documents")
        
        results = []
        for document in documents:
            try:
                analysis = nlp_processor.process_document(document)
                result = ProcessedDocument(
                    document=document,
                    analysis=analysis,
                    processed_at=datetime.utcnow(),
                    processing_version=config.SERVICE_VERSION,
                    model_used=config.SPACY_MODEL
                )
                results.append(result)
                
            except Exception as e:
                logger.error(f"Failed to process document {document.id} in batch: {e}")
                # Add failed result
                results.append(ProcessedDocument(
                    document=document,
                    analysis=NLPAnalysis(),
                    processed_at=datetime.utcnow(),
                    processing_version=config.SERVICE_VERSION,
                    model_used=config.SPACY_MODEL
                ))
        
        logger.info(f"Successfully processed batch of {len(results)} documents")
        return results
        
    except Exception as e:
        logger.error(f"Batch processing failed: {e}")
        raise HTTPException(status_code=500, detail=f"Batch processing failed: {str(e)}")

@app.post("/api/v1/jobs/submit")
async def submit_job(document: Document):
    """Submit a document for asynchronous processing"""
    try:
        job_id = str(uuid.uuid4())
        
        # Create processing job
        job = ProcessingJob(
            id=job_id,
            document_id=document.id,
            status=JobStatus.PENDING,
            metadata={"document": document.dict()}
        )
        
        # Add job to Redis queue
        job_data = job.dict()
        queue_length = redis_service.redis_client.lpush(
            config.NLP_QUEUE,
            job_data
        )
        
        # Update job status
        redis_service.update_job_status(job_id, JobStatus.PENDING.value)
        
        logger.info(f"Submitted job {job_id} for document {document.id} (queue length: {queue_length})")
        
        return {
            "job_id": job_id,
            "document_id": document.id,
            "status": JobStatus.PENDING.value,
            "submitted_at": datetime.utcnow().isoformat(),
            "queue_position": queue_length
        }
        
    except Exception as e:
        logger.error(f"Failed to submit job: {e}")
        raise HTTPException(status_code=500, detail=f"Job submission failed: {str(e)}")

@app.get("/api/v1/jobs/{job_id}")
async def get_job_status(job_id: str):
    """Get job status"""
    try:
        job_status = redis_service.get_job_status(job_id)
        
        if not job_status:
            raise HTTPException(status_code=404, detail="Job not found")
        
        return {
            "job_id": job_id,
            **job_status
        }
        
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Failed to get job status: {e}")
        raise HTTPException(status_code=500, detail=f"Failed to get job status: {str(e)}")

@app.get("/api/v1/stats")
async def get_service_stats():
    """Get service statistics"""
    try:
        return {
            "service": config.SERVICE_NAME,
            "version": config.SERVICE_VERSION,
            "uptime": datetime.utcnow().isoformat(),
            "configuration": {
                "spacy_model": config.SPACY_MODEL,
                "batch_size": config.BATCH_SIZE,
                "features": {
                    "ner": config.ENABLE_NER,
                    "classification": config.ENABLE_CLASSIFICATION,
                    "key_phrases": config.ENABLE_KEY_PHRASES,
                    "embeddings": config.ENABLE_EMBEDDINGS,
                    "language_detection": config.ENABLE_LANGUAGE_DETECTION
                }
            },
            "queues": {
                "processing_queue_length": redis_service.get_queue_length(config.NLP_QUEUE),
                "results_queue_length": redis_service.get_queue_length(config.NLP_RESULTS_QUEUE)
            },
            "redis": redis_service.health_check()
        }
    except Exception as e:
        logger.error(f"Failed to get stats: {e}")
        raise HTTPException(status_code=500, detail=f"Failed to get stats: {str(e)}")

@app.post("/api/v1/admin/clear-queue")
async def clear_processing_queue():
    """Clear the processing queue (admin endpoint)"""
    try:
        success = redis_service.clear_queue(config.NLP_QUEUE)
        
        if success:
            return {"message": "Processing queue cleared successfully"}
        else:
            raise HTTPException(status_code=500, detail="Failed to clear queue")
            
    except Exception as e:
        logger.error(f"Failed to clear queue: {e}")
        raise HTTPException(status_code=500, detail=f"Failed to clear queue: {str(e)}")

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(
        "app:app",
        host="0.0.0.0",
        port=config.SERVICE_PORT,
        reload=True,
        log_level="info"
    )