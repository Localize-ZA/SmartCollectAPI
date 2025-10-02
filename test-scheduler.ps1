# Test Script: API Ingestion Scheduler
# Tests the background scheduler with a simple API source

Write-Host "🧪 API Ingestion Scheduler Test" -ForegroundColor Cyan
Write-Host "================================`n" -ForegroundColor Cyan

$API_BASE = "http://localhost:5082"

# Step 1: Create a test API source with 1-minute cron schedule
Write-Host "Step 1: Creating test API source with 1-minute cron schedule..." -ForegroundColor Yellow

$testSource = @{
    name = "Test Scheduler - JSONPlaceholder"
    description = "Test source for scheduler verification (runs every minute)"
    apiType = "REST"
    endpointUrl = "https://jsonplaceholder.typicode.com/posts"
    httpMethod = "GET"
    authType = "None"
    responsePath = "$"
    fieldMappings = @{
        title = "title"
        content = "body"
    } | ConvertTo-Json
    scheduleCron = "*/1 * * * *"  # Every minute
    enabled = $true
    paginationType = "None"
} | ConvertTo-Json

try {
    $createResponse = Invoke-RestMethod -Uri "$API_BASE/api/sources" `
        -Method Post `
        -Body $testSource `
        -ContentType "application/json"
    
    $sourceId = $createResponse.id
    Write-Host "✅ API source created successfully!" -ForegroundColor Green
    Write-Host "   Source ID: $sourceId" -ForegroundColor Gray
    Write-Host "   Name: $($createResponse.name)" -ForegroundColor Gray
    Write-Host "   Cron: $($createResponse.scheduleCron)" -ForegroundColor Gray
    Write-Host "   Next Run: $($createResponse.nextRunAt)" -ForegroundColor Gray
} catch {
    Write-Host "❌ Failed to create API source: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Step 2: Trigger manual ingestion to verify it works
Write-Host "Step 2: Triggering manual ingestion to verify setup..." -ForegroundColor Yellow

try {
    $triggerResponse = Invoke-RestMethod -Uri "$API_BASE/api/sources/$sourceId/trigger" `
        -Method Post `
        -ContentType "application/json"
    
    if ($triggerResponse.success) {
        Write-Host "✅ Manual ingestion successful!" -ForegroundColor Green
        Write-Host "   Records Fetched: $($triggerResponse.recordsFetched)" -ForegroundColor Gray
        Write-Host "   Documents Created: $($triggerResponse.documentsCreated)" -ForegroundColor Gray
        Write-Host "   Execution Time: $($triggerResponse.executionTimeMs)ms" -ForegroundColor Gray
    } else {
        Write-Host "⚠️  Manual ingestion failed: $($triggerResponse.errorMessage)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ Failed to trigger ingestion: $_" -ForegroundColor Red
}

Write-Host ""

# Step 3: Monitor for automatic scheduler executions
Write-Host "Step 3: Monitoring for automatic scheduler executions..." -ForegroundColor Yellow
Write-Host "         (Will check every 20 seconds for 3 minutes)" -ForegroundColor Gray
Write-Host ""

$startTime = Get-Date
$checksRemaining = 9  # 3 minutes / 20 seconds = 9 checks
$executionsSeen = @{}

while ($checksRemaining -gt 0) {
    $elapsed = ((Get-Date) - $startTime).TotalSeconds
    Write-Host "⏱️  Check $((9 - $checksRemaining + 1))/9 (Elapsed: $([math]::Round($elapsed))s)" -ForegroundColor Cyan
    
    try {
        # Get the source details
        $source = Invoke-RestMethod -Uri "$API_BASE/api/sources/$sourceId" `
            -Method Get
        
        # Get ingestion logs
        $logs = Invoke-RestMethod -Uri "$API_BASE/api/sources/$sourceId/logs?limit=10" `
            -Method Get
        
        Write-Host "   Last Run: $($source.lastRunAt)" -ForegroundColor Gray
        Write-Host "   Next Run: $($source.nextRunAt)" -ForegroundColor Gray
        Write-Host "   Total Runs: $($source.totalRunsCount)" -ForegroundColor Gray
        Write-Host "   Consecutive Failures: $($source.consecutiveFailures)" -ForegroundColor Gray
        Write-Host "   Log Entries: $($logs.Count)" -ForegroundColor Gray
        
        # Check for new executions
        foreach ($log in $logs) {
            $logId = $log.id
            if (-not $executionsSeen.ContainsKey($logId)) {
                $executionsSeen[$logId] = $true
                $status = if ($log.status -eq "success") { "✅" } else { "❌" }
                Write-Host ""
                Write-Host "   $status NEW EXECUTION DETECTED:" -ForegroundColor Green
                Write-Host "      Started: $($log.startedAt)" -ForegroundColor Gray
                Write-Host "      Status: $($log.status)" -ForegroundColor Gray
                Write-Host "      Records: $($log.recordsFetched)" -ForegroundColor Gray
                Write-Host "      Documents: $($log.documentsCreated)" -ForegroundColor Gray
                Write-Host "      Time: $($log.executionTimeMs)ms" -ForegroundColor Gray
            }
        }
        
        Write-Host ""
        
    } catch {
        Write-Host "   ⚠️  Error checking status: $_" -ForegroundColor Yellow
    }
    
    if ($checksRemaining -gt 1) {
        Start-Sleep -Seconds 20
    }
    $checksRemaining--
}

Write-Host ""
Write-Host "📊 Scheduler Test Summary" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan
Write-Host "Source ID: $sourceId" -ForegroundColor Gray
Write-Host "Executions Detected: $($executionsSeen.Count)" -ForegroundColor Gray
Write-Host "Duration Monitored: 3 minutes" -ForegroundColor Gray
Write-Host ""

# Final check
try {
    $finalSource = Invoke-RestMethod -Uri "$API_BASE/api/sources/$sourceId" -Method Get
    $finalLogs = Invoke-RestMethod -Uri "$API_BASE/api/sources/$sourceId/logs?limit=5" -Method Get
    
    Write-Host "Final State:" -ForegroundColor Yellow
    Write-Host "  Total Runs: $($finalSource.totalRunsCount)" -ForegroundColor Gray
    Write-Host "  Last Successful: $($finalSource.lastSuccessfulRunAt)" -ForegroundColor Gray
    Write-Host "  Enabled: $($finalSource.enabled)" -ForegroundColor Gray
    Write-Host ""
    
    if ($finalSource.totalRunsCount -gt 1) {
        Write-Host "✅ SUCCESS: Scheduler is working!" -ForegroundColor Green
        Write-Host "   The scheduler automatically executed the job $($finalSource.totalRunsCount) times." -ForegroundColor Green
    } elseif ($finalSource.totalRunsCount -eq 1) {
        Write-Host "⚠️  WARNING: Only manual execution detected" -ForegroundColor Yellow
        Write-Host "   Scheduler may need more time, or check API logs" -ForegroundColor Yellow
    } else {
        Write-Host "❌ FAILED: No executions detected" -ForegroundColor Red
        Write-Host "   Check API logs and ensure scheduler service is running" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ Failed to get final status: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "🧹 Cleanup" -ForegroundColor Cyan
Write-Host "=========" -ForegroundColor Cyan
$cleanup = Read-Host "Delete test API source? (y/N)"

if ($cleanup -eq "y") {
    try {
        Invoke-RestMethod -Uri "$API_BASE/api/sources/$sourceId" -Method Delete | Out-Null
        Write-Host "✅ Test source deleted" -ForegroundColor Green
    } catch {
        Write-Host "⚠️  Failed to delete: $_" -ForegroundColor Yellow
        Write-Host "   You can manually delete it via: DELETE $API_BASE/api/sources/$sourceId" -ForegroundColor Gray
    }
} else {
    Write-Host "ℹ️  Test source kept. Delete manually if needed:" -ForegroundColor Cyan
    Write-Host "   DELETE $API_BASE/api/sources/$sourceId" -ForegroundColor Gray
}

Write-Host ""
Write-Host "✅ Scheduler test complete!" -ForegroundColor Green
