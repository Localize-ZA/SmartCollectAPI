from typing import Optional, Dict, Any
from pydantic import BaseModel, Field
from datetime import datetime
from enum import Enum

class JobStatus(str, Enum):
    PENDING = "pending"
    PROCESSING = "processing"
    COMPLETED = "completed"
    FAILED = "failed"
    CANCELLED = "cancelled"

class ProcessingJob(BaseModel):
    id: str
    document_id: str
    status: JobStatus = JobStatus.PENDING
    created_at: datetime = Field(default_factory=datetime.utcnow)
    started_at: Optional[datetime] = None
    completed_at: Optional[datetime] = None
    error_message: Optional[str] = None
    progress: float = 0.0
    metadata: Optional[Dict[str, Any]] = {}
    
    def start_processing(self):
        self.status = JobStatus.PROCESSING
        self.started_at = datetime.utcnow()
    
    def complete_processing(self):
        self.status = JobStatus.COMPLETED
        self.completed_at = datetime.utcnow()
        self.progress = 100.0
    
    def fail_processing(self, error: str):
        self.status = JobStatus.FAILED
        self.completed_at = datetime.utcnow()
        self.error_message = error