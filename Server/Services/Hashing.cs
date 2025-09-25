using System.Security.Cryptography;

namespace SmartCollectAPI.Services;

public static class Hashing
{
    public static async Task<string> ComputeSha256Async(Stream stream, bool resetPosition = true, CancellationToken ct = default)
    {
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, ct);
        if (resetPosition && stream.CanSeek)
        {
            stream.Position = 0;
        }
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
