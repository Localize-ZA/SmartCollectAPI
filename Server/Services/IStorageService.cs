namespace SmartCollectAPI.Services;

public interface IStorageService
{
    Task<string> SaveAsync(Stream content, string fileName, CancellationToken ct = default);
}
