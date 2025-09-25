using System.Text.Json;
using StackExchange.Redis;
using SmartCollectAPI.Models;

namespace SmartCollectAPI.Services;

public class RedisJobQueue(IConnectionMultiplexer mux) : IJobQueue
{
    private readonly IConnectionMultiplexer _mux = mux;
    private const string StreamName = "ingest-stream";

    public async Task EnqueueAsync(JobEnvelope job, CancellationToken ct = default)
    {
        var db = _mux.GetDatabase();
        var payload = JsonSerializer.Serialize(job);
        var values = new NameValueEntry[]
        {
            new("job", payload),
            new("sha256", job.Sha256),
            new("mime", job.MimeType)
        };
        // XADD ingest-stream * job {json} sha256 {...} mime {...}
        await db.StreamAddAsync(StreamName, values);
    }
}
