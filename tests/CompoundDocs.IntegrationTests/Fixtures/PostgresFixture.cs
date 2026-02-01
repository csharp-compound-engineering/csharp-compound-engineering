using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CompoundDocs.McpServer.Data;

namespace CompoundDocs.IntegrationTests.Fixtures;

/// <summary>
/// Provides a PostgreSQL database fixture for integration testing using Aspire.
/// Implements xUnit's IAsyncLifetime for proper async setup and teardown.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private DistributedApplication? _app;
    private bool _disposed;
    private bool _isAvailable;
    private string? _connectionString;

    /// <summary>
    /// Gets the connection string to the PostgreSQL database.
    /// </summary>
    public string ConnectionString => _connectionString
        ?? throw new InvalidOperationException("Aspire app not initialized. Call InitializeAsync first.");

    /// <summary>
    /// Gets whether the Aspire application is available and running.
    /// </summary>
    public bool IsAvailable => _isAvailable;

    /// <summary>
    /// Gets the logger factory for tests.
    /// </summary>
    public ILoggerFactory LoggerFactory { get; }

    public PostgresFixture()
    {
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
    }

    /// <summary>
    /// Initializes the Aspire application with PostgreSQL and creates the database schema.
    /// Gracefully handles cases where Docker is not available.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            // Create the test host from AppHost project
            var appHost = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.CompoundDocs_AppHost>();

            // Configure logging
            appHost.Services.AddLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Debug);
                logging.AddFilter("Aspire.", LogLevel.Warning);
            });

            // Build and start
            _app = await appHost.BuildAsync();
            await _app.StartAsync();

            // Wait for PostgreSQL to be healthy with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

            await _app.ResourceNotifications
                .WaitForResourceHealthyAsync("postgres", cts.Token);

            _connectionString = await _app
                .GetConnectionStringAsync("compounddocs", cts.Token)
                ?? throw new InvalidOperationException("PostgreSQL connection string not available");

            _isAvailable = true;

            // Create schema and apply migrations
            await using var context = CreateDbContext();
            await context.Database.EnsureCreatedAsync();
        }
        catch (Exception ex) when (
            ex.Message.Contains("Docker") ||
            ex.InnerException?.Message?.Contains("Docker") == true ||
            ex.Message.Contains("container") ||
            ex.Message.Contains("Cannot connect"))
        {
            _isAvailable = false;
            // Docker is not available - tests will be skipped
        }
    }

    /// <summary>
    /// Creates a new TenantDbContext connected to the test database.
    /// </summary>
    /// <returns>A new TenantDbContext instance.</returns>
    public TenantDbContext CreateDbContext()
    {
        if (_connectionString == null)
        {
            throw new InvalidOperationException("Aspire app not initialized. Cannot create DbContext.");
        }

        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(_connectionString)
            .UseLoggerFactory(LoggerFactory)
            .EnableSensitiveDataLogging()
            .Options;

        return new TenantDbContext(options);
    }

    /// <summary>
    /// Creates a logger of the specified type.
    /// </summary>
    /// <typeparam name="T">The type for the logger category.</typeparam>
    /// <returns>A logger instance.</returns>
    public ILogger<T> CreateLogger<T>()
    {
        return LoggerFactory.CreateLogger<T>();
    }

    /// <summary>
    /// Cleans up the Aspire application after all tests have run.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (!_disposed)
        {
            if (_app != null)
            {
                await _app.DisposeAsync();
            }
            LoggerFactory.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Collection definition for PostgreSQL integration tests.
/// All tests in this collection share the same PostgreSQL fixture via Aspire.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "PostgreSQL Integration Tests";
}
