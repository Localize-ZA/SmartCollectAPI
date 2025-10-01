-- API Ingestion Service - Database Schema
-- Phase 1: Foundation Tables

-- Table 1: API Sources Configuration
CREATE TABLE IF NOT EXISTS api_sources (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    
    -- API Configuration
    api_type VARCHAR(50) NOT NULL, -- 'REST', 'GraphQL', 'SOAP'
    endpoint_url TEXT NOT NULL,
    http_method VARCHAR(10) DEFAULT 'GET', -- GET, POST, PUT, DELETE, PATCH
    
    -- Authentication
    auth_type VARCHAR(50), -- 'None', 'Basic', 'Bearer', 'OAuth2', 'ApiKey'
    auth_config_encrypted TEXT, -- Encrypted JSON with credentials (DataProtection)
    -- API Key (structured, AES-256-GCM)
    auth_location VARCHAR(20), -- 'header' | 'query'
    header_name VARCHAR(100),
    query_param VARCHAR(100),
    has_api_key BOOLEAN DEFAULT false,
    key_version INTEGER,
    api_key_ciphertext BYTEA,
    api_key_iv BYTEA,
    api_key_tag BYTEA,
    
    -- Headers & Body
    custom_headers JSONB, -- {"Authorization": "Bearer ...", "Content-Type": "application/json"}
    request_body TEXT, -- For POST/PUT requests
    query_params JSONB, -- {"page": "1", "limit": "100", "sort": "desc"}
    
    -- Data Transformation
    response_path VARCHAR(500), -- JSONPath: "$.data.items[*]" or "$.results"
    field_mappings JSONB, -- {"title": "$.headline", "content": "$.body", "author": "$.metadata.author"}
    
    -- Pagination
    pagination_type VARCHAR(50), -- 'None', 'Offset', 'Cursor', 'Page', 'LinkHeader'
    pagination_config JSONB, -- {"limit": 100, "page_param": "page", "size_param": "pageSize"}
    
    -- Scheduling
    schedule_cron VARCHAR(100), -- "0 */6 * * *" (every 6 hours), "0 0 * * *" (daily at midnight)
    enabled BOOLEAN DEFAULT true,
    
    -- Tracking
    last_run_at TIMESTAMPTZ,
    next_run_at TIMESTAMPTZ,
    last_status VARCHAR(50), -- 'success', 'failed', 'partial', 'running'
    consecutive_failures INTEGER DEFAULT 0,
    
    -- Metadata
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    created_by VARCHAR(255),
    last_used_at TIMESTAMPTZ,
    
    -- Constraints
    CONSTRAINT chk_api_type CHECK (api_type IN ('REST', 'GraphQL', 'SOAP')),
    CONSTRAINT chk_http_method CHECK (http_method IN ('GET', 'POST', 'PUT', 'DELETE', 'PATCH', 'HEAD', 'OPTIONS')),
    CONSTRAINT chk_auth_type CHECK (auth_type IN ('None', 'Basic', 'Bearer', 'OAuth2', 'ApiKey')),
    CONSTRAINT chk_last_status CHECK (last_status IN ('success', 'failed', 'partial', 'running'))
);

-- Table 2: API Ingestion Execution Logs
CREATE TABLE IF NOT EXISTS api_ingestion_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    source_id UUID NOT NULL REFERENCES api_sources(id) ON DELETE CASCADE,
    
    -- Execution Timing
    started_at TIMESTAMPTZ DEFAULT NOW(),
    completed_at TIMESTAMPTZ,
    status VARCHAR(50) NOT NULL DEFAULT 'running', -- 'running', 'success', 'failed', 'partial'
    
    -- Statistics
    records_fetched INTEGER DEFAULT 0,
    documents_created INTEGER DEFAULT 0,
    documents_failed INTEGER DEFAULT 0,
    errors_count INTEGER DEFAULT 0,
    
    -- Error Information
    error_message TEXT,
    error_details JSONB, -- Structured error data
    stack_trace TEXT,
    
    -- Performance Metrics
    execution_time_ms INTEGER,
    http_status_code INTEGER,
    response_size_bytes BIGINT,
    
    -- Pagination Tracking
    pages_processed INTEGER DEFAULT 0,
    total_pages INTEGER,
    
    -- Additional Context
    metadata JSONB, -- Custom metadata (rate limit info, API version, etc.)
    
    CONSTRAINT chk_status CHECK (status IN ('running', 'success', 'failed', 'partial'))
);

-- Indexes for Performance
CREATE INDEX IF NOT EXISTS idx_api_sources_enabled ON api_sources(enabled) WHERE enabled = true;
CREATE INDEX IF NOT EXISTS idx_api_sources_next_run ON api_sources(next_run_at) WHERE enabled = true;
CREATE INDEX IF NOT EXISTS idx_api_sources_type ON api_sources(api_type);
CREATE INDEX IF NOT EXISTS idx_api_sources_last_status ON api_sources(last_status);
CREATE UNIQUE INDEX IF NOT EXISTS ux_api_sources_name ON api_sources(name);
CREATE INDEX IF NOT EXISTS idx_api_sources_endpoint_url ON api_sources(endpoint_url);
CREATE INDEX IF NOT EXISTS idx_api_sources_has_api_key ON api_sources(has_api_key);

CREATE INDEX IF NOT EXISTS idx_ingestion_logs_source_id ON api_ingestion_logs(source_id);
CREATE INDEX IF NOT EXISTS idx_ingestion_logs_started_at ON api_ingestion_logs(started_at DESC);
CREATE INDEX IF NOT EXISTS idx_ingestion_logs_status ON api_ingestion_logs(status);
CREATE INDEX IF NOT EXISTS idx_ingestion_logs_source_started ON api_ingestion_logs(source_id, started_at DESC);

-- Function to automatically update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Trigger for api_sources
DROP TRIGGER IF EXISTS update_api_sources_updated_at ON api_sources;
CREATE TRIGGER update_api_sources_updated_at
    BEFORE UPDATE ON api_sources
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Comments for documentation
COMMENT ON TABLE api_sources IS 'Configuration for external API data sources to ingest';
COMMENT ON TABLE api_ingestion_logs IS 'Execution history and logs for API ingestion jobs';

COMMENT ON COLUMN api_sources.response_path IS 'JSONPath expression to extract data array from API response';
COMMENT ON COLUMN api_sources.field_mappings IS 'Map API response fields to document properties using JSONPath';
COMMENT ON COLUMN api_sources.auth_config_encrypted IS 'Encrypted credentials stored using ASP.NET Data Protection API (legacy/general)';
COMMENT ON COLUMN api_sources.auth_location IS 'Where to apply API key (header or query)';
COMMENT ON COLUMN api_sources.header_name IS 'Header name for API key when auth_location=header';
COMMENT ON COLUMN api_sources.query_param IS 'Query string parameter name when auth_location=query';
COMMENT ON COLUMN api_sources.has_api_key IS 'Flag indicating if an API key is set';
COMMENT ON COLUMN api_sources.key_version IS 'Master key version used for AES-GCM encryption';
COMMENT ON COLUMN api_sources.api_key_ciphertext IS 'AES-GCM ciphertext bytes of the API key';
COMMENT ON COLUMN api_sources.api_key_iv IS 'AES-GCM IV (nonce) bytes';
COMMENT ON COLUMN api_sources.api_key_tag IS 'AES-GCM authentication tag bytes';
COMMENT ON COLUMN api_sources.last_used_at IS 'Timestamp when this source was last used for authentication';
COMMENT ON COLUMN api_sources.schedule_cron IS 'Cron expression for scheduling (e.g., "0 */6 * * *" for every 6 hours)';

-- Sample data for testing (optional - remove in production)
-- INSERT INTO api_sources (name, description, api_type, endpoint_url, http_method, auth_type, response_path, schedule_cron, enabled)
-- VALUES 
-- ('JSONPlaceholder Posts', 'Test REST API for posts', 'REST', 'https://jsonplaceholder.typicode.com/posts', 'GET', 'None', '$[*]', '0 */1 * * *', false),
-- ('GitHub API', 'GitHub public events', 'REST', 'https://api.github.com/events', 'GET', 'None', '$[*]', '0 */2 * * *', false);
