using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace SmartCollectAPI.Services;

public class XmlParser : IXmlParser
{
    public Task<JsonNode?> ParseAsync(Stream s, CancellationToken ct = default)
    {
        var xdoc = XDocument.Load(s);
        return Task.FromResult<JsonNode?>(ToJson(xdoc.Root));
    }

    private static JsonNode ToJson(XElement? element)
    {
        if (element is null) return new JsonObject();
        var obj = new JsonObject
        {
            ["name"] = element.Name.LocalName,
            ["attributes"] = new JsonObject(element.Attributes().ToDictionary(a => a.Name.LocalName, a => (JsonNode?)a.Value)),
            ["value"] = element.HasElements ? null : element.Value
        };
        if (element.HasElements)
        {
            var children = new JsonArray();
            foreach (var child in element.Elements())
            {
                children.Add(ToJson(child));
            }
            obj["children"] = children;
        }
        return obj;
    }
}
