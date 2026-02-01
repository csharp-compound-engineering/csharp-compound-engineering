using CompoundDocs.Common.Logging;
using CompoundDocs.McpServer;
using CompoundDocs.McpServer.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

// Parse command-line arguments for connection options
var postgresHost = GetArgValue(args, "--postgres-host")
    ?? Environment.GetEnvironmentVariable("POSTGRES_HOST")
    ?? "127.0.0.1";
var postgresPort = int.TryParse(
    GetArgValue(args, "--postgres-port") ?? Environment.GetEnvironmentVariable("POSTGRES_PORT"),
    out var pp) ? pp : 5433;
// Ollama supports either --ollama-host/--ollama-port, OLLAMA_HOST/OLLAMA_PORT env vars,
// or a full endpoint URL via OLLAMA_ENDPOINT (e.g. "http://localhost:11434").
var ollamaEndpoint = GetArgValue(args, "--ollama-endpoint")
    ?? Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT");

string ollamaHost;
int ollamaPort;

if (ollamaEndpoint is not null && Uri.TryCreate(ollamaEndpoint, UriKind.Absolute, out var endpointUri))
{
    ollamaHost = endpointUri.Host;
    ollamaPort = endpointUri.Port;
}
else
{
    ollamaHost = GetArgValue(args, "--ollama-host")
        ?? Environment.GetEnvironmentVariable("OLLAMA_HOST")
        ?? "127.0.0.1";
    ollamaPort = int.TryParse(
        GetArgValue(args, "--ollama-port") ?? Environment.GetEnvironmentVariable("OLLAMA_PORT"),
        out var op) ? op : 11435;
}

// Configure early Serilog for startup errors (stderr for MCP protocol compliance)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting CSharp Compound Docs MCP Server");
    Log.Information("PostgreSQL: {Host}:{Port}", postgresHost, postgresPort);
    Log.Information("Ollama: {Host}:{Port}", ollamaHost, ollamaPort);

    // Build options for use during configuration
    var serverOptions = new CompoundDocsServerOptions
    {
        Postgres = new PostgresConnectionOptions
        {
            Host = postgresHost,
            Port = postgresPort
        },
        Ollama = new OllamaConnectionOptions
        {
            Host = ollamaHost,
            Port = ollamaPort
        }
    };

    var host = Host.CreateDefaultBuilder(args)
        .ConfigureServices((context, services) =>
        {
            // Configure MCP server options
            services.Configure<CompoundDocsServerOptions>(options =>
            {
                options.Postgres.Host = postgresHost;
                options.Postgres.Port = postgresPort;
                options.Ollama.Host = ollamaHost;
                options.Ollama.Port = ollamaPort;
            });

            // Configure MCP server
            services
                .AddCompoundDocsMcpServer(serverOptions)
                .ConfigureServer()
                .RegisterTools();
        })
        .UseMcpServerLogging()
        .Build();

    // Handle graceful shutdown
    var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
    lifetime.ApplicationStopping.Register(() =>
    {
        Log.Information("MCP Server shutting down gracefully");
    });

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "MCP Server terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;

/// <summary>
/// Gets a command-line argument value.
/// </summary>
static string? GetArgValue(string[] args, string argName)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals(argName, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }
    return null;
}
