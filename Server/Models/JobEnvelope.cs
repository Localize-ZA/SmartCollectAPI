using System.Text.Json.Serialization;

namespace SmartCollectAPI.Models;

public record JobEnvelope(
    [property: JsonPropertyName("job_id")] Guid JobId,
    [property: JsonPropertyName("source_uri")] string SourceUri,
    [property: JsonPropertyName("mime_type")] string MimeType,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("received_at")] DateTimeOffset ReceivedAt,
    [property: JsonPropertyName("origin")] string Origin,
    [property: JsonPropertyName("notify_email")] string? NotifyEmail
);
