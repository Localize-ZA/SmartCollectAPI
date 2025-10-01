using System.Text.Json.Nodes;

namespace SmartCollectAPI.Services;

public class CsvParser : ICsvParser
{
    public async Task<JsonNode?> ParseAsync(Stream s, CancellationToken ct = default)
    {
        using var reader = new StreamReader(s);
        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine == null) return null;
        var headers = headerLine.Split(',');
        var rows = new JsonArray();
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            var cols = line.Split(',');
            var obj = new JsonObject();
            for (int i = 0; i < headers.Length && i < cols.Length; i++)
            {
                obj[headers[i]] = cols[i];
            }
            rows.Add(obj);
            if (ct.IsCancellationRequested) break;
        }
        return rows;
    }
}
