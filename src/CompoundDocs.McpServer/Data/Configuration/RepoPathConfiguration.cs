using CompoundDocs.McpServer.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CompoundDocs.McpServer.Data.Configuration;

/// <summary>
/// EF Core configuration for the RepoPath entity.
/// Maps to tenant_management.repo_paths table.
/// </summary>
public sealed class RepoPathConfiguration : IEntityTypeConfiguration<RepoPath>
{
    public void Configure(EntityTypeBuilder<RepoPath> builder)
    {
        // Table configuration
        builder.ToTable("repo_paths", "tenant_management");

        // Primary key
        builder.HasKey(r => r.Id);

        // Properties
        builder.Property(r => r.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.ProjectName)
            .HasColumnName("project_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(r => r.AbsolutePath)
            .HasColumnName("absolute_path")
            .IsRequired();

        builder.Property(r => r.PathHash)
            .HasColumnName("path_hash")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(r => r.LastAccessedAt)
            .HasColumnName("last_accessed_at")
            .HasDefaultValueSql("NOW()");

        // Unique constraint on path_hash
        builder.HasIndex(r => r.PathHash)
            .IsUnique()
            .HasDatabaseName("uq_repo_paths_path_hash");

        // Index on project_name for filtering
        builder.HasIndex(r => r.ProjectName)
            .HasDatabaseName("idx_repo_paths_project_name");

        // Index on last_accessed_at for cleanup queries
        builder.HasIndex(r => r.LastAccessedAt)
            .HasDatabaseName("idx_repo_paths_last_accessed");

        // Relationship to branches (configured on Branch side)
        builder.HasMany(r => r.Branches)
            .WithOne(b => b.RepoPath)
            .HasForeignKey(b => b.RepoPathId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
