using System.Text.Json.Nodes;

namespace SmartCollectAPI.Services.Repositories;

public interface IStagingRepository
{
    Task<Guid> InsertAsync(string jobId, string sourceUri, string? mime, string? sha256, JsonNode? rawMetadata, string status, CancellationToken ct);
    Task<int> UpdateStatusAsync(string jobId, string status, CancellationToken ct);
}
