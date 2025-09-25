using System.Text.Json;
using StackExchange.Redis;
using SmartCollectAPI.Models;
using SmartCollectAPI.Services;
using System.Text;

namespace SmartCollectAPI.Services;

/// <summary>
/// Service that listens for mock data on Redis streams/channels and processes it through the document pipeline
/// </summary>
public class RedisDataConsumer : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IJobQueue _jobQueue;
    private readonly ILogger<RedisDataConsumer> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private const string MockDataStreamName = "mock-data-stream";
    private const string ConsumerGroupName = "smart-collect-processors";
    private const string ConsumerName = "data-processor-1";

    public RedisDataConsumer(
        IConnectionMultiplexer redis,
        IJobQueue jobQueue,
        ILogger<RedisDataConsumer> logger,
        IServiceScopeFactory scopeFactory)
    {
        _redis = redis;
        _jobQueue = jobQueue;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Redis Data Consumer started, listening for mock data on stream: {StreamName}", MockDataStreamName);
        
        await EnsureConsumerGroupExists();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRedisMessages(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Redis data consumer loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task EnsureConsumerGroupExists()
    {
        try
        {
            var db = _redis.GetDatabase();
            
            // Try to create the consumer group, ignore if it already exists
            await db.StreamCreateConsumerGroupAsync(MockDataStreamName, ConsumerGroupName, "0-0", createStream: true);
            _logger.LogInformation("Consumer group {GroupName} created/ensured for stream {StreamName}", ConsumerGroupName, MockDataStreamName);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Consumer group already exists, this is expected
            _logger.LogDebug("Consumer group {GroupName} already exists for stream {StreamName}", ConsumerGroupName, MockDataStreamName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure consumer group exists");
            throw;
        }
    }

    private async Task ProcessRedisMessages(CancellationToken stoppingToken)
    {
        var db = _redis.GetDatabase();

        // Read new messages from the stream
        var streamEntries = await db.StreamReadGroupAsync(
            MockDataStreamName,
            ConsumerGroupName,
            ConsumerName,
            ">",
            count: 10,
            noAck: false);

        if (streamEntries.Length == 0)
        {
            // No new messages, wait a bit
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            return;
        }

        foreach (var entry in streamEntries)
        {
            try
            {
                await ProcessSingleMessage(entry, stoppingToken);
                
                // Acknowledge the message after successful processing
                await db.StreamAcknowledgeAsync(MockDataStreamName, ConsumerGroupName, entry.Id);
                _logger.LogDebug("Acknowledged message {MessageId} from Redis stream", entry.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process Redis message {MessageId}", entry.Id);
                // Message will remain unacknowledged and can be retried
            }
        }
    }

    private async Task ProcessSingleMessage(StreamEntry entry, CancellationToken stoppingToken)
    {
        var messageId = entry.Id.ToString();
        var values = entry.Values.ToDictionary(kv => kv.Name.ToString(), kv => kv.Value.ToString());

        _logger.LogInformation("Processing Redis message {MessageId} with {FieldCount} fields", messageId, values.Count);

        // Extract data from Redis message - flexible format support
        var content = ExtractContent(values);
        var metadata = ExtractMetadata(values);
        var contentType = DetermineContentType(values, content);

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("No content found in Redis message {MessageId}, skipping", messageId);
            return;
        }

        // Create a temporary file-like structure to feed into the existing pipeline
        var mockFileName = values.TryGetValue("filename", out var fname) ? fname : $"redis-data-{messageId}.txt";
        var sourceUri = $"redis://{MockDataStreamName}/{messageId}";

        // Convert content to bytes for the pipeline
        var contentBytes = Encoding.UTF8.GetBytes(content);
        
        // Create job envelope similar to file upload process
        var jobId = Guid.NewGuid();
        var sha256 = ComputeSha256(contentBytes);

        using var scope = _scopeFactory.CreateScope();
        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();

        // Store the content temporarily (similar to file upload)
        var tempPath = await storageService.StoreFileAsync(
            new MemoryStream(contentBytes), 
            mockFileName, 
            stoppingToken);

        var jobEnvelope = new JobEnvelope
        {
            JobId = jobId,
            FilePath = tempPath,
            MimeType = contentType,
            Sha256 = sha256,
            Metadata = new Dictionary<string, object>
            {
                ["source"] = "redis",
                ["stream"] = MockDataStreamName,
                ["message_id"] = messageId,
                ["original_metadata"] = metadata,
                ["processed_at"] = DateTimeOffset.UtcNow
            }
        };

        // Enqueue for processing through the existing pipeline
        await _jobQueue.EnqueueAsync(jobEnvelope, stoppingToken);
        
        _logger.LogInformation("Successfully enqueued Redis data from message {MessageId} as job {JobId}", 
            messageId, jobId);
    }

    private string ExtractContent(Dictionary<string, string> values)
    {
        // Try various common field names for content
        var contentFields = new[] { "content", "data", "text", "body", "message", "payload" };
        
        foreach (var field in contentFields)
        {
            if (values.TryGetValue(field, out var content) && !string.IsNullOrWhiteSpace(content))
            {
                return content;
            }
        }

        // If no standard content field, check if it's JSON and extract from there
        if (values.TryGetValue("json", out var jsonData))
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonData);
                if (doc.RootElement.TryGetProperty("content", out var contentProp))
                    return contentProp.GetString() ?? string.Empty;
                if (doc.RootElement.TryGetProperty("text", out var textProp))
                    return textProp.GetString() ?? string.Empty;
                if (doc.RootElement.TryGetProperty("data", out var dataProp))
                    return dataProp.GetString() ?? string.Empty;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse JSON content from Redis message");
            }
        }

        return string.Empty;
    }

    private Dictionary<string, object> ExtractMetadata(Dictionary<string, string> values)
    {
        var metadata = new Dictionary<string, object>();

        foreach (var (key, value) in values)
        {
            // Skip content fields, include everything else as metadata
            if (!new[] { "content", "data", "text", "body", "message", "payload" }.Contains(key.ToLower()))
            {
                metadata[key] = value;
            }
        }

        return metadata;
    }

    private string DetermineContentType(Dictionary<string, string> values, string content)
    {
        // Check if content type is explicitly provided
        if (values.TryGetValue("content_type", out var explicitType) || 
            values.TryGetValue("mime_type", out explicitType) ||
            values.TryGetValue("type", out explicitType))
        {
            return explicitType;
        }

        // Check filename extension
        if (values.TryGetValue("filename", out var filename))
        {
            var ext = Path.GetExtension(filename).ToLowerInvariant();
            return ext switch
            {
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".csv" => "text/csv",
                ".md" => "text/markdown",
                ".txt" => "text/plain",
                ".html" => "text/html",
                _ => "text/plain"
            };
        }

        // Try to detect from content structure
        if (content.TrimStart().StartsWith("{") || content.TrimStart().StartsWith("["))
        {
            return "application/json";
        }
        
        if (content.TrimStart().StartsWith("<"))
        {
            return "application/xml";
        }

        if (content.Contains("# ") || content.Contains("## ") || content.Contains("```"))
        {
            return "text/markdown";
        }

        // Default to plain text
        return "text/plain";
    }

    private static string ComputeSha256(byte[] data)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}