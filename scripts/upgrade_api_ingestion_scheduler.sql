-- API Ingestion Scheduler Upgrade Script
-- Adds columns and indexes for background scheduling, pagination, and enhanced features
-- Run this script after Phase 1 implementation

-- =============================================================================
-- PHASE 1: SCHEDULER & TRACKING
-- =============================================================================

-- Add scheduler tracking columns
ALTER TABLE api_sources ADD COLUMN IF NOT EXISTS next_run_at TIMESTAMPTZ;
ALTER TABLE api_sources ADD COLUMN IF NOT EXISTS consecutive_failures INTEGER DEFAULT 0;
ALTER TABLE api_sources ADD COLUMN IF NOT EXISTS last_successful_run_at TIMESTAMPTZ;
ALTER TABLE api_sources ADD COLUMN IF NOT EXISTS total_runs_count INTEGER DEFAULT 0;

-- Add pagination configuration
ALTER TABLE api_sources ADD COLUMN IF NOT EXISTS pagination_type VARCHAR(50) DEFAULT 'None';
ALTER TABLE api_sources ADD COLUMN IF NOT EXISTS pagination_config JSONB;

-- Add rate limiting configuration
ALTER TABLE api_sources ADD COLUMN IF NOT EXISTS rate_limit_per_minute INTEGER DEFAULT 60;
ALTER TABLE api_sources ADD COLUMN IF NOT EXISTS rate_limit_per_hour INTEGER;

-- Add incremental sync support
ALTER TABLE api_sources ADD COLUMN IF NOT EXISTS last_synced_at TIMESTAMPTZ;
ALTER TABLE api_sources ADD COLUMN IF NOT EXISTS last_synced_record_id VARCHAR(255);
ALTER TABLE api_sources ADD COLUMN IF NOT EXISTS incremental_sync_enabled BOOLEAN DEFAULT false;
ALTER TABLE api_sources ADD COLUMN IF NOT EXISTS timestamp_field VARCHAR(100);

-- Add API type for multi-protocol support
ALTER TABLE api_sources ADD COLUMN IF NOT EXISTS api_type VARCHAR(50) DEFAULT 'REST';

-- Add GraphQL support
ALTER TABLE api_sources ADD COLUMN IF NOT EXISTS graphql_query TEXT;
ALTER TABLE api_sources ADD COLUMN IF NOT EXISTS graphql_variables JSONB;

-- Add webhook support columns (for Phase 2)
ALTER TABLE api_sources ADD COLUMN IF NOT EXISTS webhook_secret_encrypted TEXT;
ALTER TABLE api_sources ADD COLUMN IF NOT EXISTS webhook_signature_header VARCHAR(100);
ALTER TABLE api_sources ADD COLUMN IF NOT EXISTS webhook_signature_method VARCHAR(50);

-- =============================================================================
-- INDEXES FOR PERFORMANCE
-- =============================================================================

-- Index for scheduler to find jobs that need to run
CREATE INDEX IF NOT EXISTS idx_api_sources_next_run 
ON api_sources(next_run_at) 
WHERE enabled = true;

-- Index for monitoring failed sources
CREATE INDEX IF NOT EXISTS idx_api_sources_failures 
ON api_sources(consecutive_failures, enabled) 
WHERE consecutive_failures > 0;

-- Index for API type filtering
CREATE INDEX IF NOT EXISTS idx_api_sources_api_type 
ON api_sources(api_type, enabled);

-- Index for incremental sync queries
CREATE INDEX IF NOT EXISTS idx_api_sources_last_synced 
ON api_sources(last_synced_at) 
WHERE incremental_sync_enabled = true;

-- =============================================================================
-- PHASE 2: WEBHOOK PAYLOADS TABLE
-- =============================================================================

-- Create webhook_payloads table for receiving webhook data
CREATE TABLE IF NOT EXISTS webhook_payloads (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    source_id UUID NOT NULL REFERENCES api_sources(id) ON DELETE CASCADE,
    
    -- Payload data
    raw_payload TEXT NOT NULL,
    signature VARCHAR(500),
    headers JSONB,
    
    -- Processing status
    received_at TIMESTAMPTZ DEFAULT NOW(),
    processed_at TIMESTAMPTZ,
    status VARCHAR(50) DEFAULT 'pending',
    
    -- Error tracking
    error_message TEXT,
    retry_count INTEGER DEFAULT 0,
    
    -- Metadata
    source_ip VARCHAR(45),
    user_agent VARCHAR(500),
    
    CONSTRAINT chk_webhook_status CHECK (status IN ('pending', 'processing', 'processed', 'failed'))
);

-- Indexes for webhook_payloads
CREATE INDEX IF NOT EXISTS idx_webhook_payloads_status 
ON webhook_payloads(status, received_at);

CREATE INDEX IF NOT EXISTS idx_webhook_payloads_source 
ON webhook_payloads(source_id, received_at DESC);

CREATE INDEX IF NOT EXISTS idx_webhook_payloads_processing 
ON webhook_payloads(status, retry_count) 
WHERE status = 'pending' OR status = 'failed';

-- =============================================================================
-- UPDATE EXISTING RECORDS
-- =============================================================================

-- Set default values for existing records
UPDATE api_sources 
SET 
    consecutive_failures = COALESCE(consecutive_failures, 0),
    total_runs_count = COALESCE(total_runs_count, 0),
    pagination_type = COALESCE(pagination_type, 'None'),
    rate_limit_per_minute = COALESCE(rate_limit_per_minute, 60),
    incremental_sync_enabled = COALESCE(incremental_sync_enabled, false),
    api_type = COALESCE(api_type, 'REST')
WHERE 
    consecutive_failures IS NULL 
    OR total_runs_count IS NULL
    OR pagination_type IS NULL
    OR rate_limit_per_minute IS NULL
    OR incremental_sync_enabled IS NULL
    OR api_type IS NULL;

-- Calculate next_run_at for enabled sources with cron schedules
-- Note: This is a placeholder - actual cron calculation happens in C# code
UPDATE api_sources 
SET next_run_at = NOW() + INTERVAL '1 hour'
WHERE 
    enabled = true 
    AND schedule_cron IS NOT NULL 
    AND next_run_at IS NULL;

-- =============================================================================
-- CONSTRAINTS & VALIDATION
-- =============================================================================

-- Add check constraint for pagination type
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint 
        WHERE conname = 'chk_pagination_type'
    ) THEN
        ALTER TABLE api_sources 
        ADD CONSTRAINT chk_pagination_type 
        CHECK (pagination_type IN ('None', 'Offset', 'Page', 'Cursor', 'LinkHeader'));
    END IF;
END $$;

-- Add check constraint for API type
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint 
        WHERE conname = 'chk_api_type'
    ) THEN
        ALTER TABLE api_sources 
        ADD CONSTRAINT chk_api_type 
        CHECK (api_type IN ('REST', 'GraphQL', 'SOAP', 'Webhook'));
    END IF;
END $$;

-- =============================================================================
-- VERIFICATION QUERIES
-- =============================================================================

-- Verify all columns exist
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_name = 'api_sources' 
    AND column_name IN (
        'next_run_at', 'consecutive_failures', 'last_successful_run_at', 
        'total_runs_count', 'pagination_type', 'pagination_config',
        'rate_limit_per_minute', 'last_synced_at', 'incremental_sync_enabled',
        'api_type', 'graphql_query', 'webhook_secret_encrypted'
    )
ORDER BY column_name;

-- Verify indexes exist
SELECT 
    indexname, 
    indexdef 
FROM pg_indexes 
WHERE tablename = 'api_sources' 
    AND indexname LIKE 'idx_api_sources_%'
ORDER BY indexname;

-- Show webhook_payloads table structure
SELECT 
    column_name, 
    data_type, 
    is_nullable 
FROM information_schema.columns 
WHERE table_name = 'webhook_payloads' 
ORDER BY ordinal_position;

-- =============================================================================
-- COMPLETION MESSAGE
-- =============================================================================

DO $$ 
BEGIN 
    RAISE NOTICE '✅ API Ingestion Scheduler upgrade completed successfully!';
    RAISE NOTICE 'New columns added: 17';
    RAISE NOTICE 'New indexes created: 7';
    RAISE NOTICE 'New tables created: 1 (webhook_payloads)';
    RAISE NOTICE 'Ready for Phase 1 deployment!';
END $$;
