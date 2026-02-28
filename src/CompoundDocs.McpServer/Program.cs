using Amazon.Lambda.AspNetCoreServer;
using CompoundDocs.Bedrock.DependencyInjection;
using CompoundDocs.Common.Configuration;
using CompoundDocs.Common.DependencyInjection;
using CompoundDocs.Graph.DependencyInjection;
using CompoundDocs.GraphRag.DependencyInjection;
using CompoundDocs.McpServer;
using CompoundDocs.McpServer.DependencyInjection;
using CompoundDocs.McpServer.Health;
using CompoundDocs.McpServer.Options;
using CompoundDocs.Vector.DependencyInjection;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Information).AddConsole());
var logger = loggerFactory.CreateLogger("CompoundDocs.McpServer");

try
{
    logger.LogInformation("Starting CSharp Compound Docs MCP Server");

    var isLambda = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME") is not null;

    var serverOptions = new CompoundDocsServerOptions();
    var builder = WebApplication.CreateBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();

    builder.Services.Configure<CompoundDocsServerOptions>(options =>
    {
        options.ServerName = serverOptions.ServerName;
    });

    // Lambda hosting: uses API Gateway HTTP API (v2) as the event source.
    // Auth is handled by ApiKeyAuthenticationHandler on the Function URL / API Gateway level.
    // The same X-API-Key header validation works in both ECS and Lambda modes.
    if (isLambda)
    {
        builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
    }

    builder.Services.AddApiKeyAuthentication(builder.Configuration);

    var config = builder.Configuration;
    builder.Services.AddCompoundDocsCloudConfig(config);
    builder.Services.AddCompoundDocsCommon();
    builder.Services.AddBedrockServices(config);
    builder.Services.AddNeptuneGraph(config);
    builder.Services.AddOpenSearchVector(config);
    builder.Services.AddGraphRag();

    builder.Services
        .AddCompoundDocsMcpServer(serverOptions)
        .ConfigureServer(stateless: isLambda)
        .RegisterTools();

    builder.Services.AddObservability();

    if (!isLambda)
    {
        builder.Services.AddHealthChecks()
            .AddCheck<NeptuneHealthCheck>("neptune")
            .AddCheck<OpenSearchHealthCheck>("opensearch")
            .AddCheck<BedrockHealthCheck>("bedrock");
    }

    var app = builder.Build();

    app.UseAuthentication();
    app.UseAuthorization();

    if (!isLambda)
    {
        app.MapHealthChecks("/health").AllowAnonymous();
    }

    app.MapMcp().RequireAuthorization();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    logger.LogCritical(ex, "MCP Server terminated unexpectedly");
    return 1;
}

return 0;

// Make the implicit Program class accessible for WebApplicationFactory<Program> in integration tests
public partial class Program;
