using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using SmartCollectAPI.Models;

namespace SmartCollectAPI.Services;

public class IngestWorker : BackgroundService
{
    private readonly ILogger<IngestWorker> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IContentDetector _detector;
    private readonly IJsonParser _jsonParser;
    private readonly IXmlParser _xmlParser;
    private readonly ICsvParser _csvParser;

    private const string StreamName = "ingest-stream";
    private readonly string _uploadsRoot;
    private readonly string _processedRoot;

    public IngestWorker(
        ILogger<IngestWorker> logger,
        IConnectionMultiplexer redis,
        IContentDetector detector,
        IJsonParser jsonParser,
        IXmlParser xmlParser,
        ICsvParser csvParser)
    {
        _logger = logger;
        _redis = redis;
        _detector = detector;
        _jsonParser = jsonParser;
        _xmlParser = xmlParser;
        _csvParser = csvParser;

        // Assume LocalStorageService default path 'uploads' when local. For now derive processed beside it.
        _uploadsRoot = Path.Combine(AppContext.BaseDirectory, "uploads");
        _processedRoot = Path.Combine(AppContext.BaseDirectory, "processed");
        Directory.CreateDirectory(_processedRoot);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var db = _redis.GetDatabase();
        var group = "worker-group";
        var consumer = Environment.MachineName;

        // Create consumer group if missing
        try { await db.StreamCreateConsumerGroupAsync(StreamName, group, "$", true); }
        catch { /* ignore if exists */ }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var entries = await db.StreamReadGroupAsync(StreamName, group, consumer, ">", count: 1, noAck: false);
                if (entries is null || entries.Length == 0)
                {
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }

                foreach (var e in entries)
                {
                    try
                    {
                        var fields = e.Values.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
                        if (!fields.TryGetValue("job", out var jobJson))
                        {
                            _logger.LogWarning("Entry {Id} has no job field", e.Id);
                            await db.StreamAcknowledgeAsync(StreamName, group, e.Id);
                            continue;
                        }
                        var job = JsonSerializer.Deserialize<JobEnvelope>(jobJson);
                        if (job is null)
                        {
                            _logger.LogWarning("Entry {Id} job payload invalid", e.Id);
                            await db.StreamAcknowledgeAsync(StreamName, group, e.Id);
                            continue;
                        }

                        var localPath = job.SourceUri;
                        var absPath = Path.IsPathRooted(localPath) ? localPath : Path.Combine(AppContext.BaseDirectory, localPath);
                        if (!File.Exists(absPath))
                        {
                            _logger.LogError("Source file missing: {Path}", absPath);
                            await db.StreamAcknowledgeAsync(StreamName, group, e.Id);
                            continue;
                        }

                        await using var fs = File.OpenRead(absPath);
                        var mime = await _detector.DetectMimeAsync(fs, job.MimeType, stoppingToken);
                        fs.Position = 0;

                        System.Text.Json.Nodes.JsonNode? parsed = null;
                        if (mime == "application/json")
                            parsed = await _jsonParser.ParseAsync(fs, stoppingToken);
                        else if (mime == "text/xml" || mime == "application/xml")
                            parsed = await _xmlParser.ParseAsync(fs, stoppingToken);
                        else if (mime == "text/csv")
                            parsed = await _csvParser.ParseAsync(fs, stoppingToken);

                        var canonical = new Models.CanonicalDocument
                        {
                            Id = job.JobId,
                            SourceUri = job.SourceUri,
                            IngestTs = DateTimeOffset.UtcNow,
                            Mime = mime,
                            Structured = parsed is not null,
                            StructuredPayload = parsed
                        };

                        var now = DateTime.UtcNow;
                        var outDir = Path.Combine(_processedRoot, now.ToString("yyyy"), now.ToString("MM"), now.ToString("dd"));
                        Directory.CreateDirectory(outDir);
                        var outFile = Path.Combine(outDir, job.JobId + ".json");
                        await File.WriteAllTextAsync(outFile, JsonSerializer.Serialize(canonical, new JsonSerializerOptions { WriteIndented = true }), stoppingToken);

                        await db.StreamAcknowledgeAsync(StreamName, group, e.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed processing entry {Id}", e.Id);
                        // Don't ack; it can be retried or moved later.
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker loop error");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }
}
