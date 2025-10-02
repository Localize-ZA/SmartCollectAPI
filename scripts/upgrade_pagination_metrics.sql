-- Phase 1 Day 5: Add Pagination Metrics to api_ingestion_logs
-- Date: 2024
-- Description: Add columns for tracking pagination metrics in ingestion logs

DO $$
DECLARE
    columns_added INTEGER := 0;
BEGIN
    RAISE NOTICE '🚀 Starting Phase 1 Day 5: Pagination Metrics Migration...';
    
    -- Add pagination_time_ms column
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'api_ingestion_logs' 
        AND column_name = 'pagination_time_ms'
    ) THEN
        ALTER TABLE api_ingestion_logs ADD COLUMN pagination_time_ms BIGINT DEFAULT 0;
        columns_added := columns_added + 1;
        RAISE NOTICE '  ✅ Added column: pagination_time_ms (BIGINT)';
    ELSE
        RAISE NOTICE '  ⏭️  Column pagination_time_ms already exists';
    END IF;

    -- Add max_pages_reached column
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'api_ingestion_logs' 
        AND column_name = 'max_pages_reached'
    ) THEN
        ALTER TABLE api_ingestion_logs ADD COLUMN max_pages_reached BOOLEAN DEFAULT false;
        columns_added := columns_added + 1;
        RAISE NOTICE '  ✅ Added column: max_pages_reached (BOOLEAN)';
    ELSE
        RAISE NOTICE '  ⏭️  Column max_pages_reached already exists';
    END IF;

    -- Add pagination_metrics column for detailed per-page metrics
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'api_ingestion_logs' 
        AND column_name = 'pagination_metrics'
    ) THEN
        ALTER TABLE api_ingestion_logs ADD COLUMN pagination_metrics JSONB;
        columns_added := columns_added + 1;
        RAISE NOTICE '  ✅ Added column: pagination_metrics (JSONB)';
    ELSE
        RAISE NOTICE '  ⏭️  Column pagination_metrics already exists';
    END IF;

    -- Add index on pages_processed for query optimization
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE tablename = 'api_ingestion_logs' 
        AND indexname = 'idx_api_ingestion_logs_pages_processed'
    ) THEN
        CREATE INDEX idx_api_ingestion_logs_pages_processed 
        ON api_ingestion_logs(pages_processed) 
        WHERE pages_processed > 1;
        RAISE NOTICE '  ✅ Created index: idx_api_ingestion_logs_pages_processed';
    ELSE
        RAISE NOTICE '  ⏭️  Index idx_api_ingestion_logs_pages_processed already exists';
    END IF;

    -- Add index on max_pages_reached for monitoring
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE tablename = 'api_ingestion_logs' 
        AND indexname = 'idx_api_ingestion_logs_max_pages_reached'
    ) THEN
        CREATE INDEX idx_api_ingestion_logs_max_pages_reached 
        ON api_ingestion_logs(max_pages_reached) 
        WHERE max_pages_reached = true;
        RAISE NOTICE '  ✅ Created index: idx_api_ingestion_logs_max_pages_reached';
    ELSE
        RAISE NOTICE '  ⏭️  Index idx_api_ingestion_logs_max_pages_reached already exists';
    END IF;

    RAISE NOTICE '';
    RAISE NOTICE '========================================';
    RAISE NOTICE '✅ Phase 1 Day 5 Migration Complete!';
    RAISE NOTICE '========================================';
    RAISE NOTICE 'New columns added: %', columns_added;
    RAISE NOTICE 'Total columns in api_ingestion_logs: %', (
        SELECT COUNT(*) FROM information_schema.columns 
        WHERE table_name = 'api_ingestion_logs'
    );
    RAISE NOTICE '';
    RAISE NOTICE '📊 Pagination tracking is now enabled!';
    RAISE NOTICE '';
END $$;

-- Verification query
SELECT 
    'api_ingestion_logs' as table_name,
    column_name,
    data_type,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_name = 'api_ingestion_logs'
AND column_name IN ('pages_processed', 'total_pages', 'pagination_time_ms', 'max_pages_reached', 'pagination_metrics')
ORDER BY column_name;
