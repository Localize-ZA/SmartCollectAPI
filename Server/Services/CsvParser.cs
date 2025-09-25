using System.Globalization;
using System.Text.Json.Nodes;
using CsvHelper;

namespace SmartCollectAPI.Services;

public class CsvParser : ICsvParser
{
    public async Task<JsonNode?> ParseAsync(Stream s, CancellationToken ct = default)
    {
        using var reader = new StreamReader(s);
        using var csv = new CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture);
        var records = new JsonArray();
        await foreach (var record in csv.GetRecordsAsync<dynamic>(ct))
        {
            var obj = new JsonObject();
            foreach (var kvp in (IDictionary<string, object>)record)
            {
                obj[kvp.Key] = kvp.Value?.ToString();
            }
            records.Add(obj);
        }
        return records;
    }
}
