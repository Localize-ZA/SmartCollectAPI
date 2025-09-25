using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmartCollectAPI.Services;

public class JsonParser : IJsonParser
{
    public async Task<JsonNode?> ParseAsync(Stream s, CancellationToken ct = default)
    {
        var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct);
        return JsonNode.Parse(doc.RootElement.GetRawText());
    }
}
