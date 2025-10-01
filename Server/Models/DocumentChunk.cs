using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace SmartCollectAPI.Models;

[Table("document_chunks")]
public class DocumentChunk
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("document_id")]
    [Required]
    public Guid DocumentId { get; set; }

    [Column("chunk_index")]
    [Required]
    public int ChunkIndex { get; set; }

    [Column("content")]
    [Required]
    public string Content { get; set; } = string.Empty;

    [Column("start_offset")]
    [Required]
    public int StartOffset { get; set; }

    [Column("end_offset")]
    [Required]
    public int EndOffset { get; set; }

    [Column("embedding")]
    public Vector? Embedding { get; set; }

    [Column("metadata", TypeName = "jsonb")]
    public string Metadata { get; set; } = "{}";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey("DocumentId")]
    public Document? Document { get; set; }
}
