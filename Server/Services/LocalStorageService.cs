using Microsoft.Extensions.Options;

namespace SmartCollectAPI.Services;

public class LocalStorageOptions
{
    public string LocalPath { get; set; } = "uploads";
}

public class LocalStorageService(IOptions<LocalStorageOptions> options) : IStorageService
{
    private readonly string _root = options.Value.LocalPath;

    public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_root);
        var safeName = Path.GetFileName(fileName);
        var now = DateTime.UtcNow;
        var jobDir = Path.Combine(_root, now.ToString("yyyy"), now.ToString("MM"), now.ToString("dd"));
        Directory.CreateDirectory(jobDir);
        var uniqueName = $"{Path.GetFileNameWithoutExtension(safeName)}_{Guid.NewGuid():N}{Path.GetExtension(safeName)}";
        var fullPath = Path.Combine(jobDir, uniqueName);
        await using var fs = File.Create(fullPath);
        await content.CopyToAsync(fs, ct);
        return fullPath;
    }
}
