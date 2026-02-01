using CompoundDocs.McpServer.Data.Configuration;
using CompoundDocs.McpServer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CompoundDocs.McpServer.Data;

/// <summary>
/// Entity Framework Core DbContext for the tenant_management schema.
/// Manages repository paths and branches for multi-tenant isolation.
/// </summary>
/// <remarks>
/// This context handles the relational tables in the tenant_management schema,
/// which is managed by Liquibase migrations. The vector collections (documents, chunks)
/// are handled separately by Semantic Kernel's PostgresCollection.
/// </remarks>
public sealed class TenantDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the TenantDbContext.
    /// </summary>
    /// <param name="options">The DbContext options.</param>
    public TenantDbContext(DbContextOptions<TenantDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Repository paths tracked for multi-tenant isolation.
    /// </summary>
    public DbSet<RepoPath> RepoPaths => Set<RepoPath>();

    /// <summary>
    /// Git branches tracked for multi-tenant isolation.
    /// </summary>
    public DbSet<Branch> Branches => Set<Branch>();

    /// <summary>
    /// Configures the entity mappings and relationships.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply entity configurations
        modelBuilder.ApplyConfiguration(new RepoPathConfiguration());
        modelBuilder.ApplyConfiguration(new BranchConfiguration());

        // Set default schema for tenant management tables
        modelBuilder.HasDefaultSchema("tenant_management");
    }
}
