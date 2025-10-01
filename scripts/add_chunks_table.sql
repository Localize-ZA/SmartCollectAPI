-- Create document_chunks table for semantic search over chunks
CREATE TABLE IF NOT EXISTS document_chunks (
    id SERIAL PRIMARY KEY,
    document_id INTEGER NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
    chunk_index INTEGER NOT NULL,
    content TEXT NOT NULL,
    start_offset INTEGER NOT NULL,
    end_offset INTEGER NOT NULL,
    embedding vector(300), -- Updated for en_core_web_md (300 dims)
    metadata JSONB DEFAULT '{}',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    -- Create index for faster similarity search
    CONSTRAINT unique_document_chunk UNIQUE(document_id, chunk_index)
);

-- Create vector similarity search index
CREATE INDEX IF NOT EXISTS idx_document_chunks_embedding 
    ON document_chunks 
    USING ivfflat (embedding vector_cosine_ops)
    WITH (lists = 100);

-- Create index for document lookup
CREATE INDEX IF NOT EXISTS idx_document_chunks_document_id 
    ON document_chunks(document_id);

-- Full-text search index for hybrid search
CREATE INDEX IF NOT EXISTS idx_document_chunks_content_fts 
    ON document_chunks 
    USING gin(to_tsvector('english', content));

-- Add search configuration
COMMENT ON TABLE document_chunks IS 'Stores chunked text segments with embeddings for granular semantic search';
COMMENT ON COLUMN document_chunks.chunk_index IS 'Zero-based index of chunk within parent document';
COMMENT ON COLUMN document_chunks.embedding IS 'Vector embedding for semantic search (300 dimensions for en_core_web_md)';
COMMENT ON COLUMN document_chunks.metadata IS 'Strategy used, token counts, sentence counts, etc.';
