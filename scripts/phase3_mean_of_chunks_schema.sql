-- Phase 3: Mean-of-Chunks Vector Architecture Schema Updates
-- This script updates the schema to support dynamic vector dimensions

-- ================================================
-- 1. Update documents table to support 768-dim vectors
-- ================================================

-- Drop old embedding column and recreate with 768 dimensions
-- Note: This will lose existing embeddings, but that's expected for the upgrade
ALTER TABLE documents 
DROP COLUMN IF EXISTS embedding CASCADE;

ALTER TABLE documents 
ADD COLUMN embedding vector(768);

-- Recreate vector similarity search index
CREATE INDEX IF NOT EXISTS idx_documents_embedding 
    ON documents 
    USING ivfflat (embedding vector_cosine_ops)
    WITH (lists = 100);

COMMENT ON COLUMN documents.embedding IS 'Mean-of-chunks embedding vector (768 dimensions for sentence-transformers, or 300 for spacy)';

-- ================================================
-- 2. Update document_chunks table to support 768-dim vectors
-- ================================================

-- Check if document_chunks table exists, if not create it
CREATE TABLE IF NOT EXISTS document_chunks (
    id SERIAL PRIMARY KEY,
    document_id UUID NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
    chunk_index INTEGER NOT NULL,
    content TEXT NOT NULL,
    start_offset INTEGER NOT NULL,
    end_offset INTEGER NOT NULL,
    embedding vector(768), -- Updated for sentence-transformers (768 dims) or spacy (300 dims)
    metadata JSONB DEFAULT '{}',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    -- Create index for faster similarity search
    CONSTRAINT unique_document_chunk UNIQUE(document_id, chunk_index)
);

-- If table already exists, update the embedding column
DO $$
BEGIN
    -- Drop old embedding column if it exists with different dimensions
    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'document_chunks' 
        AND column_name = 'embedding'
    ) THEN
        -- Check if it's already 768-dim
        DECLARE
            current_dims INTEGER;
        BEGIN
            SELECT atttypmod INTO current_dims 
            FROM pg_attribute 
            WHERE attrelid = 'document_chunks'::regclass 
            AND attname = 'embedding';
            
            -- If not 768, drop and recreate
            IF current_dims IS NOT NULL AND current_dims != 768 THEN
                ALTER TABLE document_chunks DROP COLUMN embedding CASCADE;
                ALTER TABLE document_chunks ADD COLUMN embedding vector(768);
            END IF;
        END;
    ELSE
        -- Column doesn't exist, add it
        ALTER TABLE document_chunks ADD COLUMN embedding vector(768);
    END IF;
END $$;

-- Recreate indexes for chunks
CREATE INDEX IF NOT EXISTS idx_document_chunks_embedding 
    ON document_chunks 
    USING ivfflat (embedding vector_cosine_ops)
    WITH (lists = 100);

CREATE INDEX IF NOT EXISTS idx_document_chunks_document_id 
    ON document_chunks(document_id);

-- Full-text search index for hybrid search
CREATE INDEX IF NOT EXISTS idx_document_chunks_content_fts 
    ON document_chunks 
    USING gin(to_tsvector('english', content));

-- ================================================
-- 3. Add helpful metadata columns (optional)
-- ================================================

-- Add provider information to documents (to know which embedding model was used)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'documents' 
        AND column_name = 'embedding_provider'
    ) THEN
        ALTER TABLE documents ADD COLUMN embedding_provider VARCHAR(100);
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'documents' 
        AND column_name = 'embedding_dimensions'
    ) THEN
        ALTER TABLE documents ADD COLUMN embedding_dimensions INTEGER;
    END IF;
END $$;

-- Add comments
COMMENT ON TABLE document_chunks IS 'Stores chunked text segments with embeddings for granular semantic search';
COMMENT ON COLUMN document_chunks.chunk_index IS 'Zero-based index of chunk within parent document';
COMMENT ON COLUMN document_chunks.embedding IS 'Vector embedding for semantic search (768 dims for sentence-transformers, 300 for spacy)';
COMMENT ON COLUMN document_chunks.metadata IS 'Strategy used, token counts, sentence counts, etc.';
COMMENT ON COLUMN documents.embedding_provider IS 'Embedding provider used (e.g., sentence-transformers, spacy, openai)';
COMMENT ON COLUMN documents.embedding_dimensions IS 'Actual dimensions of the embedding vector';

-- ================================================
-- 4. Create helper functions for chunk search
-- ================================================

-- Function to search chunks by similarity
CREATE OR REPLACE FUNCTION search_chunks_by_similarity(
    query_embedding vector(768),
    limit_count INTEGER DEFAULT 10,
    similarity_threshold FLOAT DEFAULT 0.7
)
RETURNS TABLE (
    chunk_id INTEGER,
    document_id UUID,
    chunk_index INTEGER,
    content TEXT,
    similarity FLOAT,
    metadata JSONB
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        dc.id,
        dc.document_id,
        dc.chunk_index,
        dc.content,
        1 - (dc.embedding <=> query_embedding) as similarity,
        dc.metadata
    FROM document_chunks dc
    WHERE dc.embedding IS NOT NULL
        AND 1 - (dc.embedding <=> query_embedding) >= similarity_threshold
    ORDER BY dc.embedding <=> query_embedding
    LIMIT limit_count;
END;
$$ LANGUAGE plpgsql;

-- Function to get all chunks for a document
CREATE OR REPLACE FUNCTION get_document_chunks(doc_id UUID)
RETURNS TABLE (
    chunk_id INTEGER,
    chunk_index INTEGER,
    content TEXT,
    start_offset INTEGER,
    end_offset INTEGER,
    has_embedding BOOLEAN,
    metadata JSONB
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        dc.id,
        dc.chunk_index,
        dc.content,
        dc.start_offset,
        dc.end_offset,
        (dc.embedding IS NOT NULL) as has_embedding,
        dc.metadata
    FROM document_chunks dc
    WHERE dc.document_id = doc_id
    ORDER BY dc.chunk_index;
END;
$$ LANGUAGE plpgsql;

-- ================================================
-- 5. Add indexes for performance
-- ================================================

-- Index on embedding_provider for filtering by provider
CREATE INDEX IF NOT EXISTS idx_documents_embedding_provider 
    ON documents(embedding_provider) 
    WHERE embedding_provider IS NOT NULL;

-- Index on embedding_dimensions for filtering by dimensions
CREATE INDEX IF NOT EXISTS idx_documents_embedding_dimensions 
    ON documents(embedding_dimensions) 
    WHERE embedding_dimensions IS NOT NULL;

-- ================================================
-- 6. Verification queries
-- ================================================

-- Check current vector dimensions
DO $$
DECLARE
    doc_dims INTEGER;
    chunk_dims INTEGER;
BEGIN
    -- Get dimensions for documents
    SELECT atttypmod INTO doc_dims
    FROM pg_attribute 
    WHERE attrelid = 'documents'::regclass 
    AND attname = 'embedding';
    
    -- Get dimensions for chunks
    SELECT atttypmod INTO chunk_dims
    FROM pg_attribute 
    WHERE attrelid = 'document_chunks'::regclass 
    AND attname = 'embedding';
    
    RAISE NOTICE 'Documents embedding dimensions: %', doc_dims;
    RAISE NOTICE 'Chunks embedding dimensions: %', chunk_dims;
END $$;

-- Summary
SELECT 
    'Schema update complete!' as status,
    'Documents and chunks now support 768-dim vectors' as note,
    'Mean-of-chunks architecture ready' as phase3_status;
