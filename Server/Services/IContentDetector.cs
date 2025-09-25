namespace SmartCollectAPI.Services;

public interface IContentDetector
{
    Task<string> DetectMimeAsync(Stream stream, string? hint = null, CancellationToken ct = default);
}
