using CompoundDocs.McpServer.Data;
using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CompoundDocs.McpServer.DependencyInjection;

/// <summary>
/// Extension methods for registering data services with the dependency injection container.
/// </summary>
public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// Adds data services including TenantDbContext and all repositories.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This registers:
    /// - NpgsqlDataSource as Singleton (connection pooling)
    /// - TenantDbContext as Scoped (per-request)
    /// - IRepoPathRepository as Scoped
    /// - IBranchRepository as Scoped
    /// - IDocumentRepository as Scoped
    ///
    /// The connection string is read from CompoundDocsServerOptions.
    /// </remarks>
    public static IServiceCollection AddDataServices(this IServiceCollection services)
    {
        // Register NpgsqlDataSource as singleton for connection pooling
        services.TryAddSingleton<NpgsqlDataSource>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<CompoundDocsServerOptions>>();
            var postgresOptions = options.Value.Postgres;

            var connectionString = BuildConnectionString(postgresOptions);

            var builder = new NpgsqlDataSourceBuilder(connectionString);
            builder.UseVector(); // Enable pgvector support

            return builder.Build();
        });

        // Register TenantDbContext with Npgsql
        services.AddDbContext<TenantDbContext>((sp, options) =>
        {
            var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
            options.UseNpgsql(dataSource);
        });

        // Register repositories
        services.TryAddScoped<IRepoPathRepository, RepoPathRepository>();
        services.TryAddScoped<IBranchRepository, BranchRepository>();
        services.TryAddScoped<IDocumentRepository, DocumentRepository>();

        return services;
    }

    /// <summary>
    /// Adds data services with a custom connection string.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDataServices(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // Register NpgsqlDataSource as singleton for connection pooling
        services.TryAddSingleton<NpgsqlDataSource>(_ =>
        {
            var builder = new NpgsqlDataSourceBuilder(connectionString);
            builder.UseVector(); // Enable pgvector support
            return builder.Build();
        });

        // Register TenantDbContext with Npgsql
        services.AddDbContext<TenantDbContext>((sp, options) =>
        {
            var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
            options.UseNpgsql(dataSource);
        });

        // Register repositories
        services.TryAddScoped<IRepoPathRepository, RepoPathRepository>();
        services.TryAddScoped<IBranchRepository, BranchRepository>();
        services.TryAddScoped<IDocumentRepository, DocumentRepository>();

        return services;
    }

    /// <summary>
    /// Adds only the relational data services (TenantDbContext, RepoPath/Branch repositories).
    /// Use this when document repository is configured separately.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRelationalDataServices(this IServiceCollection services)
    {
        // Register NpgsqlDataSource as singleton for connection pooling
        services.TryAddSingleton<NpgsqlDataSource>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<CompoundDocsServerOptions>>();
            var postgresOptions = options.Value.Postgres;

            var connectionString = BuildConnectionString(postgresOptions);

            var builder = new NpgsqlDataSourceBuilder(connectionString);
            builder.UseVector(); // Enable pgvector support

            return builder.Build();
        });

        // Register TenantDbContext with Npgsql
        services.AddDbContext<TenantDbContext>((sp, options) =>
        {
            var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
            options.UseNpgsql(dataSource);
        });

        // Register only relational repositories
        services.TryAddScoped<IRepoPathRepository, RepoPathRepository>();
        services.TryAddScoped<IBranchRepository, BranchRepository>();

        return services;
    }

    /// <summary>
    /// Adds only the document repository for vector operations.
    /// Requires VectorStoreFactory to be registered separately.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDocumentRepository(this IServiceCollection services)
    {
        services.TryAddScoped<IDocumentRepository, DocumentRepository>();
        return services;
    }

    /// <summary>
    /// Builds a PostgreSQL connection string from options.
    /// </summary>
    private static string BuildConnectionString(PostgresConnectionOptions options)
    {
        return $"Host={options.Host};" +
               $"Port={options.Port};" +
               $"Database={options.Database};" +
               $"Username={options.Username};" +
               $"Password={options.Password}";
    }
}
