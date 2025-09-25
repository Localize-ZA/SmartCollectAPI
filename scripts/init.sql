-- Initialize database with pgvector extension and base schema
CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Create staging table for processing pipeline
CREATE TABLE staging_documents (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    job_id TEXT NOT NULL,
    source_uri TEXT NOT NULL,
    mime TEXT,
    sha256 TEXT,
    raw_metadata JSONB,
    normalized JSONB,
    status TEXT DEFAULT 'pending', -- pending, processing, failed, done
    attempts INT DEFAULT 0,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Create final documents table with vector embeddings
CREATE TABLE documents (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    source_uri TEXT NOT NULL,
    mime TEXT,
    sha256 TEXT UNIQUE,
    canonical JSONB NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    embedding VECTOR(1536) -- Default dimension for OpenAI/Vertex embeddings
);

-- Create indexes for performance
CREATE INDEX idx_staging_documents_status ON staging_documents(status);
CREATE INDEX idx_staging_documents_job_id ON staging_documents(job_id);
CREATE INDEX idx_staging_documents_sha256 ON staging_documents(sha256);
CREATE INDEX idx_documents_sha256 ON documents(sha256);
CREATE INDEX idx_documents_created_at ON documents(created_at);

-- Create vector similarity index (using HNSW for approximate nearest neighbor)
CREATE INDEX idx_documents_embedding ON documents USING hnsw (embedding vector_cosine_ops);