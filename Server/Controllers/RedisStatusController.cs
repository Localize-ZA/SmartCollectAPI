using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace SmartCollectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RedisStatusController : ControllerBase
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisStatusController> _logger;

    public RedisStatusController(IConnectionMultiplexer redis, ILogger<RedisStatusController> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    [HttpGet("stream-info")]
    public async Task<IActionResult> GetStreamInfo()
    {
        try
        {
            var db = _redis.GetDatabase();
            
            // Check ingest-stream info
            var streamInfo = await db.StreamInfoAsync("ingest-stream");
            var pendingInfo = await db.StreamPendingAsync("ingest-stream", "worker-group");
            
            return Ok(new
            {
                StreamName = "ingest-stream",
                Length = streamInfo.Length,
                Groups = streamInfo.ConsumerGroupCount,
                LastGeneratedId = streamInfo.LastGeneratedId.ToString(),
                PendingMessages = pendingInfo.PendingMessageCount,
                ConsumerNames = pendingInfo.Consumers?.Select(c => c.Name.ToString()).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Redis stream info");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("recent-messages")]
    public async Task<IActionResult> GetRecentMessages()
    {
        try
        {
            var db = _redis.GetDatabase();
            
            // Get last 10 messages from ingest-stream
            var messages = await db.StreamRangeAsync("ingest-stream", "-", "+", 10, Order.Descending);
            
            var messageList = messages.Select(m => new
            {
                Id = m.Id.ToString(),
                Values = m.Values.ToDictionary(v => v.Name.ToString(), v => v.Value.ToString())
            }).ToList();
            
            return Ok(new
            {
                StreamName = "ingest-stream",
                MessageCount = messageList.Count,
                Messages = messageList
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent messages");
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}