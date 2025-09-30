import redis
import json
import logging
from typing import Optional, Dict, Any
from config import config

logger = logging.getLogger(__name__)

class RedisService:
    def __init__(self):
        self.redis_client = None
        self._connect()
    
    def _connect(self):
        """Connect to Redis server"""
        try:
            self.redis_client = redis.Redis(
                host=config.REDIS_HOST,
                port=config.REDIS_PORT,
                db=config.REDIS_DB,
                password=config.REDIS_PASSWORD,
                decode_responses=True,
                socket_connect_timeout=5,
                socket_timeout=5
            )
            
            # Test connection
            self.redis_client.ping()
            logger.info(f"Connected to Redis at {config.REDIS_HOST}:{config.REDIS_PORT}")
        except Exception as e:
            logger.error(f"Failed to connect to Redis: {e}")
            raise
    
    def publish_job_result(self, job_id: str, result: Dict[str, Any]) -> bool:
        """Publish job result to results queue"""
        try:
            message = {
                "job_id": job_id,
                "result": result,
                "timestamp": result.get("processed_at")
            }
            
            self.redis_client.lpush(
                config.NLP_RESULTS_QUEUE,
                json.dumps(message, default=str)
            )
            
            logger.debug(f"Published result for job {job_id}")
            return True
        except Exception as e:
            logger.error(f"Failed to publish job result: {e}")
            return False
    
    def get_processing_job(self) -> Optional[Dict[str, Any]]:
        """Get next job from processing queue (blocking)"""
        try:
            # Blocking pop with timeout
            result = self.redis_client.brpop(config.NLP_QUEUE, timeout=30)
            
            if result:
                queue_name, message = result
                job_data = json.loads(message)
                logger.debug(f"Retrieved job from queue: {job_data.get('id', 'unknown')}")
                return job_data
                
            return None
        except Exception as e:
            logger.error(f"Failed to get processing job: {e}")
            return None
    
    def update_job_status(self, job_id: str, status: str, progress: float = 0.0, error: Optional[str] = None):
        """Update job status in Redis"""
        try:
            job_key = f"nlp:job:{job_id}"
            job_data = {
                "status": status,
                "progress": progress,
                "updated_at": None  # Will be set by json.dumps default
            }
            
            if error:
                job_data["error"] = error
            
            self.redis_client.setex(
                job_key,
                3600,  # Expire after 1 hour
                json.dumps(job_data, default=str)
            )
            
            logger.debug(f"Updated job {job_id} status to {status}")
        except Exception as e:
            logger.error(f"Failed to update job status: {e}")
    
    def get_job_status(self, job_id: str) -> Optional[Dict[str, Any]]:
        """Get job status from Redis"""
        try:
            job_key = f"nlp:job:{job_id}"
            job_data = self.redis_client.get(job_key)
            
            if job_data:
                return json.loads(job_data)
            return None
        except Exception as e:
            logger.error(f"Failed to get job status: {e}")
            return None
    
    def set_cache(self, key: str, value: Any, expire: int = 3600):
        """Set cached value with expiration"""
        try:
            self.redis_client.setex(
                key,
                expire,
                json.dumps(value, default=str)
            )
        except Exception as e:
            logger.error(f"Failed to set cache: {e}")
    
    def get_cache(self, key: str) -> Optional[Any]:
        """Get cached value"""
        try:
            data = self.redis_client.get(key)
            if data:
                return json.loads(data)
            return None
        except Exception as e:
            logger.error(f"Failed to get cache: {e}")
            return None
    
    def health_check(self) -> Dict[str, Any]:
        """Health check for Redis connection"""
        try:
            # Test connection
            latency = self.redis_client.ping()
            
            # Get some stats
            info = self.redis_client.info()
            
            return {
                "status": "healthy",
                "latency_ms": latency if isinstance(latency, (int, float)) else 0,
                "connected_clients": info.get("connected_clients", 0),
                "used_memory_human": info.get("used_memory_human", "unknown"),
                "redis_version": info.get("redis_version", "unknown")
            }
        except Exception as e:
            return {
                "status": "unhealthy",
                "error": str(e)
            }
    
    def get_queue_length(self, queue_name: str) -> int:
        """Get length of a queue"""
        try:
            return self.redis_client.llen(queue_name)
        except Exception as e:
            logger.error(f"Failed to get queue length: {e}")
            return 0
    
    def clear_queue(self, queue_name: str) -> bool:
        """Clear all items from a queue"""
        try:
            self.redis_client.delete(queue_name)
            logger.info(f"Cleared queue: {queue_name}")
            return True
        except Exception as e:
            logger.error(f"Failed to clear queue: {e}")
            return False