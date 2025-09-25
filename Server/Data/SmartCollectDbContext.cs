using Microsoft.EntityFrameworkCore;
using SmartCollectAPI.Models;
using Pgvector.EntityFrameworkCore;

namespace SmartCollectAPI.Data;

public class SmartCollectDbContext : DbContext
{
    public SmartCollectDbContext(DbContextOptions<SmartCollectDbContext> options) : base(options)
    {
    }

    public DbSet<StagingDocument> StagingDocuments { get; set; } = null!;
    public DbSet<Document> Documents { get; set; } = null!;

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
            entity.Property(e => e.Embedding).HasColumnName("embedding").HasColumnType("vector(1536)");

            // Unique constraint on sha256
            entity.HasIndex(e => e.Sha256).IsUnique().HasDatabaseName("idx_documents_sha256");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_documents_created_at");
        });

        // Configure pgvector
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("uuid-ossp");
    }
}