using System.Text.Json.Nodes;
using Pgvector;

namespace SmartCollectAPI.Models;

public class StagingDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string JobId { get; set; } = string.Empty;
    public string SourceUri { get; set; } = string.Empty;
    public string? Mime { get; set; }
    public string? Sha256 { get; set; }
    public JsonNode? RawMetadata { get; set; }
    public JsonNode? Normalized { get; set; }
    public string Status { get; set; } = "pending"; // pending, processing, failed, done
    public int Attempts { get; set; } = 0;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}