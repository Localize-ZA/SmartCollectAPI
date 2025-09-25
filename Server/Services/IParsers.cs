using System.Text.Json.Nodes;

namespace SmartCollectAPI.Services;

public interface IJsonParser { Task<JsonNode?> ParseAsync(Stream s, CancellationToken ct = default); }
public interface IXmlParser { Task<JsonNode?> ParseAsync(Stream s, CancellationToken ct = default); }
public interface ICsvParser { Task<JsonNode?> ParseAsync(Stream s, CancellationToken ct = default); }
