using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartCollectAPI.Models;

[Table("api_ingestion_logs")]
public class ApiIngestionLog
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("source_id")]
    public Guid SourceId { get; set; }

    // Execution Timing
    [Column("started_at")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = "running"; // running, success, failed, partial

    // Statistics
    [Column("records_fetched")]
    public int RecordsFetched { get; set; } = 0;

    [Column("documents_created")]
    public int DocumentsCreated { get; set; } = 0;

    [Column("documents_failed")]
    public int DocumentsFailed { get; set; } = 0;

    [Column("errors_count")]
    public int ErrorsCount { get; set; } = 0;

    // Error Information
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("error_details", TypeName = "jsonb")]
    public string? ErrorDetails { get; set; } // Stored as JSON string

    [Column("stack_trace")]
    public string? StackTrace { get; set; }

    // Performance Metrics
    [Column("execution_time_ms")]
    public int? ExecutionTimeMs { get; set; }

    [Column("http_status_code")]
    public int? HttpStatusCode { get; set; }

    [Column("response_size_bytes")]
    public long? ResponseSizeBytes { get; set; }

    // Pagination Tracking
    [Column("pages_processed")]
    public int PagesProcessed { get; set; } = 0;

    [Column("total_pages")]
    public int? TotalPages { get; set; }

    // Additional Context
    [Column("metadata", TypeName = "jsonb")]
    public string? Metadata { get; set; } // Stored as JSON string

    // Navigation property
    [ForeignKey("SourceId")]
    public ApiSource? Source { get; set; }
}
