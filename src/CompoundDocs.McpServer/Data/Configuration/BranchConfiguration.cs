using CompoundDocs.McpServer.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CompoundDocs.McpServer.Data.Configuration;

/// <summary>
/// EF Core configuration for the Branch entity.
/// Maps to tenant_management.branches table.
/// </summary>
public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        // Table configuration
        builder.ToTable("branches", "tenant_management");

        // Primary key
        builder.HasKey(b => b.Id);

        // Properties
        builder.Property(b => b.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(b => b.RepoPathId)
            .HasColumnName("repo_path_id")
            .IsRequired();

        builder.Property(b => b.BranchName)
            .HasColumnName("branch_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(b => b.IsDefault)
            .HasColumnName("is_default")
            .HasDefaultValue(false);

        builder.Property(b => b.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(b => b.LastAccessedAt)
            .HasColumnName("last_accessed_at")
            .HasDefaultValueSql("NOW()");

        // Unique constraint on (repo_path_id, branch_name)
        builder.HasIndex(b => new { b.RepoPathId, b.BranchName })
            .IsUnique()
            .HasDatabaseName("uq_branches_repo_branch");

        // Index on repo_path_id for FK lookups
        builder.HasIndex(b => b.RepoPathId)
            .HasDatabaseName("idx_branches_repo_path_id");

        // Index on last_accessed_at for cleanup queries
        builder.HasIndex(b => b.LastAccessedAt)
            .HasDatabaseName("idx_branches_last_accessed");

        // Filtered index on is_default=true
        builder.HasIndex(b => b.IsDefault)
            .HasDatabaseName("idx_branches_is_default")
            .HasFilter("is_default = TRUE");

        // Relationship to RepoPath (cascade delete handled by FK)
        builder.HasOne(b => b.RepoPath)
            .WithMany(r => r.Branches)
            .HasForeignKey(b => b.RepoPathId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
