using CompoundDocs.Common.Logging;
using CompoundDocs.McpServer;
using CompoundDocs.McpServer.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

// Configure early Serilog for startup errors (stderr for MCP protocol compliance)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting CSharp Compound Docs MCP Server");

    var serverOptions = new CompoundDocsServerOptions();

    var host = Host.CreateDefaultBuilder(args)
        .ConfigureServices((context, services) =>
        {
            services.Configure<CompoundDocsServerOptions>(options =>
            {
                options.ServerName = serverOptions.ServerName;
            });

            services
                .AddCompoundDocsMcpServer(serverOptions)
                .ConfigureServer()
                .RegisterTools();
        })
        .UseMcpServerLogging()
        .Build();

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
