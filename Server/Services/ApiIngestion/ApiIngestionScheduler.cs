using Cronos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartCollectAPI.Data;

namespace SmartCollectAPI.Services.ApiIngestion;

/// <summary>
/// Background service that automatically runs API ingestion jobs based on cron schedules.
/// Monitors enabled API sources and triggers ingestion when next_run_at is reached.
/// </summary>
public class ApiIngestionScheduler : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ApiIngestionScheduler> _logger;
    private readonly ApiIngestionSchedulerOptions _options;

    public ApiIngestionScheduler(
        IServiceScopeFactory scopeFactory,
        ILogger<ApiIngestionScheduler> logger,
        IOptions<ApiIngestionSchedulerOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "API Ingestion Scheduler started. Check interval: {Interval} seconds",
            _options.SchedulerIntervalSeconds
        );

        // Wait a bit for the application to fully start
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndRunScheduledJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in API ingestion scheduler loop");
            }

            // Wait before next check
            await Task.Delay(
                TimeSpan.FromSeconds(_options.SchedulerIntervalSeconds),
                stoppingToken
            );
        }

        _logger.LogInformation("API Ingestion Scheduler stopped");
    }

    private async Task CheckAndRunScheduledJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SmartCollectDbContext>();
        var ingestionService = scope.ServiceProvider.GetRequiredService<IApiIngestionService>();

        var now = DateTime.UtcNow;

        // Find sources that are due to run:
        // 1. Enabled = true
        // 2. Has a cron schedule
        // 3. next_run_at is in the past (or null for first run)
        var dueSources = await context.ApiSources
            .Where(s =>
                s.Enabled &&
                s.ScheduleCron != null &&
                (s.NextRunAt == null || s.NextRunAt <= now)
            )
            .OrderBy(s => s.NextRunAt ?? DateTime.MinValue)
            .ToListAsync(cancellationToken);

        if (dueSources.Count == 0)
        {
            _logger.LogDebug("No API sources due for ingestion at {Time}", now);
            return;
        }

        _logger.LogInformation(
            "Found {Count} API source(s) due for ingestion",
            dueSources.Count
        );

        foreach (var source in dueSources)
        {
            // Process each source independently
            try
            {
                await ProcessScheduledSourceAsync(source, ingestionService, context, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to process scheduled ingestion for source {SourceId} - {SourceName}",
                    source.Id,
                    source.Name
                );
            }
        }
    }

    private async Task ProcessScheduledSourceAsync(
        Models.ApiSource source,
        IApiIngestionService ingestionService,
        SmartCollectDbContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Running scheduled ingestion for source: {SourceName} (ID: {SourceId})",
            source.Name,
            source.Id
        );

        var startTime = DateTime.UtcNow;
        bool success = false;

        try
        {
            // Execute the ingestion
            var result = await ingestionService.ExecuteIngestionAsync(source.Id, cancellationToken);

            success = result.Success;

            if (result.Success)
            {
                _logger.LogInformation(
                    "Scheduled ingestion completed successfully for {SourceName}. " +
                    "Records: {RecordsFetched}, Documents: {DocsCreated}, Time: {TimeMs}ms",
                    source.Name,
                    result.RecordsFetched,
                    result.DocumentsCreated,
                    result.ExecutionTimeMs
                );

                // Update success tracking
                source.LastSuccessfulRunAt = DateTime.UtcNow;
                source.ConsecutiveFailures = 0;
            }
            else
            {
                _logger.LogWarning(
                    "Scheduled ingestion failed for {SourceName}: {Error}",
                    source.Name,
                    result.ErrorMessage
                );

                source.ConsecutiveFailures++;
            }

            source.TotalRunsCount++;
        }
        catch (Exception ex)
        {
            success = false;
            source.ConsecutiveFailures++;

            _logger.LogError(
                ex,
                "Exception during scheduled ingestion for {SourceName}",
                source.Name
            );
        }

        // Update LastRunAt
        source.LastRunAt = startTime;

        // Calculate next run time from cron expression
        if (!string.IsNullOrWhiteSpace(source.ScheduleCron))
        {
            try
            {
                var cronExpression = CronExpression.Parse(source.ScheduleCron, CronFormat.IncludeSeconds);
                var nextOccurrence = cronExpression.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc);

                if (nextOccurrence.HasValue)
                {
                    source.NextRunAt = nextOccurrence.Value;

                    _logger.LogInformation(
                        "Next run for {SourceName} scheduled at {NextRun} (in {Minutes} minutes)",
                        source.Name,
                        source.NextRunAt,
                        (source.NextRunAt.Value - DateTime.UtcNow).TotalMinutes
                    );
                }
                else
                {
                    _logger.LogWarning(
                        "Could not calculate next run time for {SourceName}. Cron: {Cron}",
                        source.Name,
                        source.ScheduleCron
                    );
                    source.NextRunAt = DateTime.UtcNow.AddHours(1); // Fallback: 1 hour
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Invalid cron expression for {SourceName}: {Cron}",
                    source.Name,
                    source.ScheduleCron
                );
                source.NextRunAt = DateTime.UtcNow.AddHours(1); // Fallback: 1 hour
            }
        }
        else
        {
            _logger.LogWarning(
                "Source {SourceName} has no cron schedule defined",
                source.Name
            );
        }

        // Check if we should disable the source due to too many failures
        if (_options.DisableOnFailure &&
            source.ConsecutiveFailures >= _options.MaxConsecutiveFailures)
        {
            source.Enabled = false;

            _logger.LogWarning(
                "Disabled source {SourceName} after {Failures} consecutive failures",
                source.Name,
                source.ConsecutiveFailures
            );

            // TODO: Send notification email about disabled source
        }

        // Save changes
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save scheduler updates for source {SourceId}",
                source.Id
            );
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("API Ingestion Scheduler is stopping...");
        await base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Configuration options for the API Ingestion Scheduler
/// </summary>
public class ApiIngestionSchedulerOptions
{
    /// <summary>
    /// How often to check for due jobs (in seconds). Default: 60 seconds.
    /// </summary>
    public int SchedulerIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum number of consecutive failures before disabling a source. Default: 5.
    /// </summary>
    public int MaxConsecutiveFailures { get; set; } = 5;

    /// <summary>
    /// Whether to automatically disable sources after max failures. Default: true.
    /// </summary>
    public bool DisableOnFailure { get; set; } = true;

    /// <summary>
    /// Whether the scheduler is enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
