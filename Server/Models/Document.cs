using System.Text.Json.Nodes;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace SmartCollectAPI.Models;

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SourceUri { get; set; } = string.Empty;
    public string? Mime { get; set; }
    public string? Sha256 { get; set; }
    public JsonNode Canonical { get; set; } = new JsonObject();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    [Column(TypeName = "vector(768)")]
    public Vector? Embedding { get; set; } // pgvector embedding (mean-of-chunks)
    
    public string? EmbeddingProvider { get; set; } // e.g., "sentence-transformers", "spacy"
    public int? EmbeddingDimensions { get; set; } // Actual dimensions (768, 300, etc.)
}
