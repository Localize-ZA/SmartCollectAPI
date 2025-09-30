import asyncio
import json
import logging
import signal
import sys
from datetime import datetime
from typing import Dict, Any

from models.document import Document, ProcessedDocument
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

class RedisConsumer:
    def __init__(self):
        self.nlp_processor = NLPProcessor()
        self.redis_service = RedisService()
        self.running = False
        self.processed_count = 0
        
    async def start(self):
        """Start the Redis consumer"""
        self.running = True
        logger.info("Starting NLP Redis consumer...")
        
        # Set up signal handlers for graceful shutdown
        signal.signal(signal.SIGINT, self._signal_handler)
        signal.signal(signal.SIGTERM, self._signal_handler)
        
        while self.running:
            try:
                await self._process_next_job()
            except Exception as e:
                logger.error(f"Error in consumer loop: {e}")
                await asyncio.sleep(5)  # Wait before retrying
        
        logger.info("Redis consumer stopped")
    
    async def _process_next_job(self):
        """Process the next job from the queue"""
        try:
            # Get job from Redis queue (blocking with timeout)
            job_data = await asyncio.to_thread(self.redis_service.get_processing_job)
            
            if not job_data:
                return  # No job available, continue loop
            
            # Parse job data
            job = ProcessingJob(**job_data)
            logger.info(f"Processing job {job.id} for document {job.document_id}")
            
            # Update job status to processing
            job.start_processing()
            self.redis_service.update_job_status(job.id, JobStatus.PROCESSING.value, 0.0)
            
            try:
                # Extract document from job data
                document_data = job.metadata.get("document")
                if not document_data:
                    raise ValueError("No document data in job")
                
                document = Document(**document_data)
                
                # Process document with NLP
                logger.debug(f"Running NLP analysis on document {document.id}")
                analysis = await asyncio.to_thread(self.nlp_processor.process_document, document)
                
                # Create processed document result
                processed_doc = ProcessedDocument(
                    document=document,
                    analysis=analysis,
                    processed_at=datetime.utcnow(),
                    processing_version=config.SERVICE_VERSION,
                    model_used=config.SPACY_MODEL
                )
                
                # Update job status to completed
                job.complete_processing()
                self.redis_service.update_job_status(job.id, JobStatus.COMPLETED.value, 100.0)
                
                # Publish result
                result = {
                    "job_id": job.id,
                    "document_id": document.id,
                    "processed_document": processed_doc.dict(),
                    "status": "completed",
                    "processed_at": datetime.utcnow().isoformat()
                }
                
                success = self.redis_service.publish_job_result(job.id, result)
                
                if success:
                    self.processed_count += 1
                    logger.info(f"Successfully processed job {job.id} (total: {self.processed_count})")
                else:
                    logger.error(f"Failed to publish result for job {job.id}")
                
            except Exception as e:
                # Handle processing errors
                error_msg = f"Failed to process document: {str(e)}"
                logger.error(f"Job {job.id} failed: {error_msg}")
                
                job.fail_processing(error_msg)
                self.redis_service.update_job_status(job.id, JobStatus.FAILED.value, 0.0, error_msg)
                
                # Publish failure result
                result = {
                    "job_id": job.id,
                    "document_id": job.document_id,
                    "status": "failed",
                    "error": error_msg,
                    "processed_at": datetime.utcnow().isoformat()
                }
                
                self.redis_service.publish_job_result(job.id, result)
                
        except Exception as e:
            logger.error(f"Error processing job: {e}")
    
    def _signal_handler(self, signum, frame):
        """Handle shutdown signals"""
        logger.info(f"Received signal {signum}, shutting down gracefully...")
        self.running = False
    
    async def stop(self):
        """Stop the consumer"""
        self.running = False
        logger.info("Consumer stopped")
    
    def get_status(self) -> Dict[str, Any]:
        """Get consumer status"""
        return {
            "running": self.running,
            "processed_count": self.processed_count,
            "nlp_processor_status": self.nlp_processor.health_check(),
            "redis_status": self.redis_service.health_check(),
            "queue_length": self.redis_service.get_queue_length(config.NLP_QUEUE)
        }

async def main():
    """Main function to run the consumer"""
    consumer = RedisConsumer()
    
    try:
        await consumer.start()
    except KeyboardInterrupt:
        logger.info("Received keyboard interrupt")
    except Exception as e:
        logger.error(f"Consumer error: {e}")
    finally:
        await consumer.stop()

if __name__ == "__main__":
    asyncio.run(main())