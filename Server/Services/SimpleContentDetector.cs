using System.Text;

namespace SmartCollectAPI.Services;

public class SimpleContentDetector : IContentDetector
{
    public async Task<string> DetectMimeAsync(Stream stream, string? hint = null, CancellationToken ct = default)
    {
        // Prefer explicit hint, but only if it's not the generic octet-stream
        if (!string.IsNullOrWhiteSpace(hint) && hint != "application/octet-stream")
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

        // Get full sample for better detection
        var sample = Encoding.UTF8.GetString(buffer, 0, read);
        
        // Check for markdown indicators
        if (sample.Contains("# ") || sample.Contains("## ") || sample.Contains("### ") || 
            sample.Contains("```") || sample.Contains("---") || sample.Contains("* ") ||
            sample.Contains("- ") || sample.Contains("[") && sample.Contains("]("))
        {
            return "text/markdown";
        }

        // naive CSV heuristic: commas and newlines
        var commaCount = sample.Count(c => c == ',');
        var newlineCount = sample.Count(c => c == '\n');
        if (commaCount > 0 && newlineCount > 0) return "text/csv";

        // Check if it's readable text
        if (IsReadableText(sample))
        {
            return "text/plain";
        }

        return "application/octet-stream";
    }

    private static bool IsReadableText(string sample)
    {
        // Simple heuristic: if most characters are printable ASCII or common UTF-8, treat as text
        var printableCount = 0;
        var totalCount = Math.Min(sample.Length, 512); // Check first 512 chars

        for (int i = 0; i < totalCount; i++)
        {
            var c = sample[i];
            if (char.IsControl(c))
            {
                // Allow common whitespace controls
                if (c == '\n' || c == '\r' || c == '\t')
                    printableCount++;
            }
            else if (c >= 32 && c <= 126) // Printable ASCII
            {
                printableCount++;
            }
            else if (c > 126) // Potential UTF-8
            {
                printableCount++;
            }
        }

        return totalCount > 0 && (double)printableCount / totalCount > 0.7; // 70% printable
    }
}
