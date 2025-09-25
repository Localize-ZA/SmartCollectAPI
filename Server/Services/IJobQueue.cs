using SmartCollectAPI.Models;

namespace SmartCollectAPI.Services;

public interface IJobQueue
{
    Task EnqueueAsync(JobEnvelope job, CancellationToken ct = default);
}
