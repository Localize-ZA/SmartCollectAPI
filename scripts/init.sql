-- SmartCollectAPI Database Initialization Script
-- Creates the necessary tables and extensions for document processing

-- Enable pgvector extension for vector similarity search
CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Create staging_documents table for processing pipeline
CREATE TABLE IF NOT EXISTS staging_documents (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    job_id TEXT NOT NULL,
    source_uri TEXT NOT NULL,
    mime TEXT,
    sha256 TEXT,
    raw_metadata JSONB,
    normalized JSONB,
    status TEXT DEFAULT 'pending',
    attempts INT DEFAULT 0,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Create documents table for final processed documents
CREATE TABLE IF NOT EXISTS documents (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    source_uri TEXT NOT NULL,
    mime TEXT,
    sha256 TEXT UNIQUE,
    canonical JSONB NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    embedding VECTOR(300) -- Using 300 dimensions for spaCy embeddings
);

-- Create indexes for better performance
CREATE INDEX IF NOT EXISTS idx_staging_documents_job_id ON staging_documents(job_id);
CREATE INDEX IF NOT EXISTS idx_staging_documents_status ON staging_documents(status);
CREATE INDEX IF NOT EXISTS idx_staging_documents_sha256 ON staging_documents(sha256);
CREATE INDEX IF NOT EXISTS idx_staging_documents_created_at ON staging_documents(created_at);

CREATE INDEX IF NOT EXISTS idx_documents_sha256 ON documents(sha256);
CREATE INDEX IF NOT EXISTS idx_documents_created_at ON documents(created_at);
CREATE INDEX IF NOT EXISTS idx_documents_mime ON documents(mime);

-- Create vector similarity search index for embeddings
CREATE INDEX IF NOT EXISTS idx_documents_embedding ON documents USING hnsw (embedding vector_cosine_ops);

-- Create a function to update the updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Create triggers to automatically update updated_at timestamps
DROP TRIGGER IF EXISTS update_staging_documents_updated_at ON staging_documents;
CREATE TRIGGER update_staging_documents_updated_at
    BEFORE UPDATE ON staging_documents
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_documents_updated_at ON documents;
CREATE TRIGGER update_documents_updated_at
    BEFORE UPDATE ON documents
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Insert some sample data for testing (optional)
-- You can remove this section if you don't want sample data
INSERT INTO staging_documents (job_id, source_uri, mime, status) VALUES
('test-job-1', '/uploads/sample1.pdf', 'application/pdf', 'pending'),
('test-job-2', '/uploads/sample2.docx', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 'processed')
ON CONFLICT DO NOTHING;

-- Display initialization completion message
DO $$
BEGIN
    RAISE NOTICE 'SmartCollectAPI database initialization completed successfully!';
    RAISE NOTICE 'Tables created: staging_documents, documents';
    RAISE NOTICE 'Extensions enabled: vector, uuid-ossp';
    RAISE NOTICE 'Indexes and triggers configured for optimal performance';
END $$;