using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartCollectAPI.Models;

[Table("api_sources")]
public class ApiSource
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(255)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    // API Configuration
    [Required]
    [MaxLength(50)]
    [Column("api_type")]
    public string ApiType { get; set; } = "REST"; // REST, GraphQL, SOAP

    [Required]
    [Column("endpoint_url")]
    public string EndpointUrl { get; set; } = string.Empty;

    [MaxLength(10)]
    [Column("http_method")]
    public string HttpMethod { get; set; } = "GET";

    // Authentication
    [MaxLength(50)]
    [Column("auth_type")]
    public string? AuthType { get; set; } // None, Basic, Bearer, OAuth2, ApiKey

    [Column("auth_config_encrypted")]
    public string? AuthConfigEncrypted { get; set; }

    // Headers & Body
    [Column("custom_headers", TypeName = "jsonb")]
    public string? CustomHeaders { get; set; } // Stored as JSON string

    [Column("request_body")]
    public string? RequestBody { get; set; }

    [Column("query_params", TypeName = "jsonb")]
    public string? QueryParams { get; set; } // Stored as JSON string

    // Data Transformation
    [MaxLength(500)]
    [Column("response_path")]
    public string? ResponsePath { get; set; } // JSONPath expression

    [Column("field_mappings", TypeName = "jsonb")]
    public string? FieldMappings { get; set; } // Stored as JSON string

    // Pagination
    [MaxLength(50)]
    [Column("pagination_type")]
    public string? PaginationType { get; set; } // None, Offset, Cursor, Page

    [Column("pagination_config", TypeName = "jsonb")]
    public string? PaginationConfig { get; set; } // Stored as JSON string

    // Scheduling
    [MaxLength(100)]
    [Column("schedule_cron")]
    public string? ScheduleCron { get; set; }

    [Column("enabled")]
    public bool Enabled { get; set; } = true;

    // Tracking
    [Column("last_run_at")]
    public DateTime? LastRunAt { get; set; }

    [Column("next_run_at")]
    public DateTime? NextRunAt { get; set; }

    [MaxLength(50)]
    [Column("last_status")]
    public string? LastStatus { get; set; } // success, failed, partial, running

    [Column("consecutive_failures")]
    public int ConsecutiveFailures { get; set; } = 0;

    // Metadata
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(255)]
    [Column("created_by")]
    public string? CreatedBy { get; set; }

    // Navigation property
    public ICollection<ApiIngestionLog> IngestionLogs { get; set; } = new List<ApiIngestionLog>();
}
