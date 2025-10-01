using System.Text.Json.Nodes;

namespace SmartCollectAPI.Models;

public class CanonicalDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SourceUri { get; set; } = string.Empty;
    public DateTimeOffset IngestTs { get; set; } = DateTimeOffset.UtcNow;
    public string Mime { get; set; } = "application/octet-stream";
    public bool Structured { get; set; }
    public JsonNode? StructuredPayload { get; set; }
    public string? ExtractedText { get; set; }
    public JsonArray? Entities { get; set; }
    public JsonArray? Tables { get; set; }
    public JsonArray? Sections { get; set; }
    public float[]? Embedding { get; set; }
    public int EmbeddingDim { get; set; } = 300;
    public string ProcessingStatus { get; set; } = "processed";
    public JsonNode? ProcessingErrors { get; set; }
    public string SchemaVersion { get; set; } = "v1";
}
