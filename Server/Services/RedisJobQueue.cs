using System.Text.Json;
using StackExchange.Redis;
using SmartCollectAPI.Models;

namespace SmartCollectAPI.Services;

public class RedisJobQueue(IConnectionMultiplexer mux, ILogger<RedisJobQueue> logger) : IJobQueue
{
    private readonly IConnectionMultiplexer _mux = mux;
    private readonly ILogger<RedisJobQueue> _logger = logger;
    private const string StreamName = "ingest-stream";
    private const string DLQStreamName = "ingest-dlq-stream";

    public async Task EnqueueAsync(JobEnvelope job, CancellationToken ct = default)
    {
        var db = _mux.GetDatabase();
        var payload = JsonSerializer.Serialize(job);
        var values = new NameValueEntry[]
        {
            new("job", payload),
            new("sha256", job.Sha256),
            new("mime", job.MimeType),
            new("retry_count", "0"),
            new("enqueued_at", DateTimeOffset.UtcNow.ToString("O"))
        };
        
        try
        {
            // XADD ingest-stream * job {json} sha256 {...} mime {...} retry_count 0 enqueued_at {...}
            var messageId = await db.StreamAddAsync(StreamName, values);
            _logger.LogInformation("Enqueued job {JobId} with message ID {MessageId}", job.JobId, messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue job {JobId}", job.JobId);
            throw;
        }
    }

    public async Task<bool> MoveToDeadLetterQueueAsync(string streamName, string groupName, string messageId, string reason, CancellationToken ct = default)
    {
        try
        {
            var db = _mux.GetDatabase();
            
            // Read the original message
            var pendingMessages = await db.StreamReadGroupAsync(streamName, groupName, "consumer-dlq", messageId, count: 1);
            if (pendingMessages.Length == 0)
            {
                _logger.LogWarning("Message {MessageId} not found for DLQ move", messageId);
                return false;
            }

            var originalMessage = pendingMessages[0];
            
            // Create DLQ entry with additional metadata
            var dlqValues = originalMessage.Values.ToList();
            dlqValues.Add(new NameValueEntry("dlq_reason", reason));
            dlqValues.Add(new NameValueEntry("dlq_timestamp", DateTimeOffset.UtcNow.ToString("O")));
            dlqValues.Add(new NameValueEntry("original_stream", streamName));
            dlqValues.Add(new NameValueEntry("original_message_id", messageId));

            await db.StreamAddAsync(DLQStreamName, dlqValues.ToArray());
            
            // Acknowledge the original message to remove it from pending
            await db.StreamAcknowledgeAsync(streamName, groupName, messageId);
            
            _logger.LogWarning("Moved message {MessageId} to DLQ due to: {Reason}", messageId, reason);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move message {MessageId} to DLQ", messageId);
            return false;
        }
    }
}
