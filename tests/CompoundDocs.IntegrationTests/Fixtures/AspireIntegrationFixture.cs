using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace CompoundDocs.IntegrationTests.Fixtures;

/// <summary>
/// Provides an Aspire-based test fixture for integration testing with PostgreSQL and Ollama.
/// Implements xUnit's IAsyncLifetime for proper async setup and teardown.
/// </summary>
public sealed class AspireIntegrationFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    /// <summary>
    /// Gets the running Aspire distributed application.
    /// </summary>
    public DistributedApplication App => _app
        ?? throw new InvalidOperationException("App not initialized. Call InitializeAsync first.");

    /// <summary>
    /// Gets the connection string to the PostgreSQL database.
    /// </summary>
    public string PostgresConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the Ollama endpoint URL.
    /// </summary>
    public string OllamaEndpoint { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the MCP client for E2E testing.
    /// </summary>
    public McpClient? McpClient { get; private set; }

    /// <summary>
    /// Gets the logger factory for tests.
    /// </summary>
    public ILoggerFactory LoggerFactory { get; }

    public AspireIntegrationFixture()
    {
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
    }

    /// <summary>
    /// Initializes the Aspire application host and waits for resources to be ready.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Create the test host from AppHost project
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.CompoundDocs_AppHost>();

        // Configure logging for debugging
        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddFilter("Aspire.", LogLevel.Warning);
        });

        // Build and start
        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        // Wait for resources with timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        // Wait for PostgreSQL to be healthy
        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("postgres", cts.Token);

        PostgresConnectionString = await _app
            .GetConnectionStringAsync("compounddocs", cts.Token)
            ?? throw new InvalidOperationException("PostgreSQL connection string not available");

        // Wait for Ollama
        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("ollama", cts.Token);

        // Get Ollama endpoint
        OllamaEndpoint = await GetOllamaEndpointAsync(cts.Token);

        // Initialize MCP Client for E2E testing
        await InitializeMcpClientAsync(cts.Token);
    }

    private async Task<string> GetOllamaEndpointAsync(CancellationToken cancellationToken)
    {
        // For Ollama, we use the default endpoint with the allocated port
        // The CommunityToolkit.Aspire.Hosting.Ollama configures the default endpoint
        var connectionString = await App.GetConnectionStringAsync("ollama", cancellationToken);

        if (!string.IsNullOrEmpty(connectionString))
        {
            return connectionString;
        }

        // Fallback: construct from environment or default
        return "http://localhost:11434";
    }

    private async Task InitializeMcpClientAsync(CancellationToken cancellationToken)
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "CompoundDocs",
            Command = "dotnet",
            Arguments = ["run", "--project", GetMcpServerProjectPath(), "--no-build"],
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["POSTGRES_CONNECTION"] = PostgresConnectionString,
                ["OLLAMA_ENDPOINT"] = OllamaEndpoint
            }
        });

        McpClient = await McpClient.CreateAsync(
            transport,
            clientOptions: new McpClientOptions
            {
                ClientInfo = new() { Name = "IntegrationTests", Version = "1.0.0" }
            },
            cancellationToken: cancellationToken);
    }

    private static string GetMcpServerProjectPath()
    {
        // Navigate from test project to MCP server project
        var baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(
            baseDir, "..", "..", "..", "..",
            "src", "CompoundDocs.McpServer", "CompoundDocs.McpServer.csproj"));
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
        if (McpClient != null)
        {
            await McpClient.DisposeAsync();
        }

        if (_app != null)
        {
            await _app.DisposeAsync();
        }

        LoggerFactory.Dispose();
    }
}
