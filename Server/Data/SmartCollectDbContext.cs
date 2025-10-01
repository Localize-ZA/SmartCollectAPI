using Microsoft.EntityFrameworkCore;
using SmartCollectAPI.Models;
using Pgvector.EntityFrameworkCore;

namespace SmartCollectAPI.Data;

public class SmartCollectDbContext(DbContextOptions<SmartCollectDbContext> options) : DbContext(options)
{
    public DbSet<StagingDocument> StagingDocuments { get; set; } = null!;
    public DbSet<Document> Documents { get; set; } = null!;
    public DbSet<DocumentChunk> DocumentChunks { get; set; } = null!;
    public DbSet<ApiSource> ApiSources { get; set; } = null!;
    public DbSet<ApiIngestionLog> ApiIngestionLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure staging_documents table
        modelBuilder.Entity<StagingDocument>(entity =>
        {
            entity.ToTable("staging_documents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.JobId).HasColumnName("job_id").IsRequired();
            entity.Property(e => e.SourceUri).HasColumnName("source_uri").IsRequired();
            entity.Property(e => e.Mime).HasColumnName("mime");
            entity.Property(e => e.Sha256).HasColumnName("sha256");
            entity.Property(e => e.RawMetadata).HasColumnName("raw_metadata").HasColumnType("jsonb");
            entity.Property(e => e.Normalized).HasColumnName("normalized").HasColumnType("jsonb");
            entity.Property(e => e.Status).HasColumnName("status").HasDefaultValue("pending");
            entity.Property(e => e.Attempts).HasColumnName("attempts").HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

            // Indexes
            entity.HasIndex(e => e.Status).HasDatabaseName("idx_staging_documents_status");
            entity.HasIndex(e => e.JobId).HasDatabaseName("idx_staging_documents_job_id");
            entity.HasIndex(e => e.Sha256).HasDatabaseName("idx_staging_documents_sha256");
        });

        // Configure documents table
        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("documents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.SourceUri).HasColumnName("source_uri").IsRequired();
            entity.Property(e => e.Mime).HasColumnName("mime");
            entity.Property(e => e.Sha256).HasColumnName("sha256");
            entity.Property(e => e.Canonical).HasColumnName("canonical").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.Embedding).HasColumnName("embedding");
            entity.Property(e => e.EmbeddingProvider).HasColumnName("embedding_provider");
            entity.Property(e => e.EmbeddingDimensions).HasColumnName("embedding_dimensions");

            // Unique constraint on sha256
            entity.HasIndex(e => e.Sha256).IsUnique().HasDatabaseName("idx_documents_sha256");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_documents_created_at");
            entity.HasIndex(e => e.EmbeddingProvider).HasDatabaseName("idx_documents_embedding_provider");
        });

        // Configure document_chunks table
        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.ToTable("document_chunks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.DocumentId).HasColumnName("document_id").IsRequired();
            entity.Property(e => e.ChunkIndex).HasColumnName("chunk_index").IsRequired();
            entity.Property(e => e.Content).HasColumnName("content").IsRequired();
            entity.Property(e => e.StartOffset).HasColumnName("start_offset").IsRequired();
            entity.Property(e => e.EndOffset).HasColumnName("end_offset").IsRequired();
            entity.Property(e => e.Embedding).HasColumnName("embedding");
            entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb").HasDefaultValue("{}");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            // Foreign key
            entity.HasOne(e => e.Document)
                .WithMany()
                .HasForeignKey(e => e.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(e => new { e.DocumentId, e.ChunkIndex })
                .IsUnique()
                .HasDatabaseName("unique_document_chunk");
            entity.HasIndex(e => e.DocumentId).HasDatabaseName("idx_document_chunks_document_id");
        });

        // Configure api_sources table
        modelBuilder.Entity<ApiSource>(entity =>
        {
            entity.ToTable("api_sources");
            entity.HasKey(e => e.Id);

            // Indexes
            entity.HasIndex(e => e.Enabled).HasDatabaseName("idx_api_sources_enabled");
            entity.HasIndex(e => e.NextRunAt).HasDatabaseName("idx_api_sources_next_run");
            entity.HasIndex(e => e.ApiType).HasDatabaseName("idx_api_sources_type");
            entity.HasIndex(e => e.LastStatus).HasDatabaseName("idx_api_sources_last_status");
            entity.HasIndex(e => e.Name).IsUnique().HasDatabaseName("ux_api_sources_name");
            entity.HasIndex(e => e.EndpointUrl).HasDatabaseName("idx_api_sources_endpoint_url");
            entity.HasIndex(e => e.HasApiKey).HasDatabaseName("idx_api_sources_has_api_key");
        });

        // Configure api_ingestion_logs table
        modelBuilder.Entity<ApiIngestionLog>(entity =>
        {
            entity.ToTable("api_ingestion_logs");
            entity.HasKey(e => e.Id);

            // Foreign key
            entity.HasOne(e => e.Source)
                .WithMany(s => s.IngestionLogs)
                .HasForeignKey(e => e.SourceId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(e => e.SourceId).HasDatabaseName("idx_ingestion_logs_source_id");
            entity.HasIndex(e => e.StartedAt).HasDatabaseName("idx_ingestion_logs_started_at");
            entity.HasIndex(e => e.Status).HasDatabaseName("idx_ingestion_logs_status");
            entity.HasIndex(e => new { e.SourceId, e.StartedAt }).HasDatabaseName("idx_ingestion_logs_source_started");
        });

        // Configure pgvector
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("uuid-ossp");
    }
}
