using System.Reflection;
using CompoundDocs.McpServer.DependencyInjection;
using CompoundDocs.McpServer.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace CompoundDocs.McpServer;

/// <summary>
/// Fluent builder for configuring the MCP server.
/// </summary>
public sealed class McpServerBuilder
{
    private readonly IServiceCollection _services;
    private readonly CompoundDocsServerOptions _options;

    public McpServerBuilder(IServiceCollection services, CompoundDocsServerOptions options)
    {
        _services = services;
        _options = options;
    }

    /// <summary>
    /// Configures the MCP server with the rag_query tool.
    /// </summary>
    public McpServerBuilder ConfigureServer()
    {
        var version = GetAssemblyVersion();

        _services.AddMcpServer(options =>
        {
            options.ServerInfo = new()
            {
                Name = _options.ServerName,
                Version = version
            };
        })
        .WithStdioServerTransport()
        .WithAllTools();

        return this;
    }

    /// <summary>
    /// Registers the RagQueryTool dependencies.
    /// </summary>
    public McpServerBuilder RegisterTools()
    {
        _services.AddMcpTools();
        return this;
    }

    private static string GetAssemblyVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString(3) ?? "1.0.0";
    }
}

/// <summary>
/// Extension methods for MCP server configuration.
/// </summary>
public static class McpServerBuilderExtensions
{
    public static McpServerBuilder AddCompoundDocsMcpServer(
        this IServiceCollection services,
        CompoundDocsServerOptions options)
    {
        return new McpServerBuilder(services, options);
    }

    public static McpServerBuilder AddCompoundDocsMcpServer(
        this IServiceCollection services,
        IOptions<CompoundDocsServerOptions> optionsAccessor)
    {
        return new McpServerBuilder(services, optionsAccessor.Value);
    }
}
