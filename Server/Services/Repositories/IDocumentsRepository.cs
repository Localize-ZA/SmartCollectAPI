using System.Text.Json.Nodes;

namespace SmartCollectAPI.Services.Repositories;

public interface IDocumentsRepository
{
    Task<Guid> UpsertAsync(string sourceUri, string? mime, string? sha256, JsonNode canonical, CancellationToken ct);
}
