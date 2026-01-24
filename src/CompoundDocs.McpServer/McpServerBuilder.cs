using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using CompoundDocs.McpServer.DependencyInjection;
using CompoundDocs.McpServer.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;
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
    /// Configures the MCP server with HTTP transport.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Calls MCP SDK extensions (AddMcpServer, WithAllTools, WithHttpTransport)")]
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
        .WithAllTools()
        .WithHttpTransport();

        return this;
    }

    /// <summary>
    /// Registers the RagQueryTool dependencies.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Calls MCP SDK extension (AddMcpTools)")]
    public McpServerBuilder RegisterTools()
    {
        _services.AddMcpTools();
        return this;
    }

    [ExcludeFromCodeCoverage(Justification = "Assembly.GetName().Version always returns non-null; the null fallback branch cannot be triggered")]
    internal static string GetAssemblyVersion()
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
