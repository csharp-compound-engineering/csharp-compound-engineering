using CompoundDocs.McpServer;
using CompoundDocs.McpServer.DependencyInjection;
using CompoundDocs.McpServer.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

// Configure early Serilog for startup errors
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting CSharp Compound Docs MCP Server");

    var serverOptions = new CompoundDocsServerOptions();
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, configuration) => configuration
        .MinimumLevel.Information()
        .WriteTo.Console());

    builder.Services.Configure<CompoundDocsServerOptions>(options =>
    {
        options.ServerName = serverOptions.ServerName;
    });

    builder.Services.AddApiKeyAuthentication(builder.Configuration);

    builder.Services
        .AddCompoundDocsMcpServer(serverOptions)
        .ConfigureServer()
        .RegisterTools();

    builder.Services.AddHealthChecks();

    var app = builder.Build();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/health").AllowAnonymous();
    app.MapMcp().RequireAuthorization();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "MCP Server terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;

// Make the implicit Program class accessible for WebApplicationFactory<Program> in integration tests
public partial class Program;
