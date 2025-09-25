using System.Text;

namespace SmartCollectAPI.Services;

public class SimpleContentDetector : IContentDetector
{
    public async Task<string> DetectMimeAsync(Stream stream, string? hint = null, CancellationToken ct = default)
    {
        // Prefer explicit hint
        if (!string.IsNullOrWhiteSpace(hint))
        {
            return hint!;
        }

        // Read up to first 4KB for sniffing
        var max = (int)Math.Min(4096, stream.Length - stream.Position);
        var buffer = new byte[max];
        var read = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
        if (stream.CanSeek) stream.Position -= read;

        // Magic bytes checks
        if (read >= 4)
        {
            // PDF: %PDF
            if (buffer[0] == 0x25 && buffer[1] == 0x50 && buffer[2] == 0x44 && buffer[3] == 0x46) return "application/pdf";
        }

        var textPrefix = Encoding.UTF8.GetString(buffer, 0, Math.Min(read, 32)).TrimStart();
        if (textPrefix.StartsWith("{")) return "application/json";
        if (textPrefix.StartsWith("<")) return "application/xml"; // could be XML

        // naive CSV heuristic: commas and newlines
        var sample = Encoding.UTF8.GetString(buffer, 0, read);
        var commaCount = sample.Count(c => c == ',');
        var newlineCount = sample.Count(c => c == '\n');
        if (commaCount > 0 && newlineCount > 0) return "text/csv";

        return "application/octet-stream";
    }
}
