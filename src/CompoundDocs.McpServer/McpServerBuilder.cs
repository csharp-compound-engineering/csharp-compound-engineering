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

    /// <summary>
    /// Creates a new MCP server builder.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="options">The server options.</param>
    public McpServerBuilder(IServiceCollection services, CompoundDocsServerOptions options)
    {
        _services = services;
        _options = options;
    }

    /// <summary>
    /// Configures the MCP server with all tools and settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
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
        .WithAllTools(); // Register all MCP tools

        // Register hooks (session start/end, document lifecycle)
        _services.AddHooks();

        return this;
    }

    /// <summary>
    /// Registers all MCP tools with the server.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public McpServerBuilder RegisterTools()
    {
        // Register tool dependencies
        _services.AddMcpTools();

        // Register all tools with the MCP server
        // Note: Tools are registered with the MCP server in ConfigureServer()
        // This method sets up the dependency injection for tool classes

        // The 9 MCP tools available:
        // 1. activate_project - Activate a project for the session
        // 2. index_document - Manually index a document
        // 3. semantic_search - Vector similarity search
        // 4. rag_query - RAG-based Q&A with source attribution
        // 5. list_doc_types - List available document types
        // 6. delete_documents - Delete documents from the index
        // 7. update_promotion_level - Update document visibility
        // 8. search_external_docs - Search external documentation
        // 9. rag_query_external - RAG against external docs

        return this;
    }

    /// <summary>
    /// Gets the assembly version for MCP server info.
    /// </summary>
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
    /// <summary>
    /// Adds and configures the MCP server.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The server options.</param>
    /// <returns>The MCP server builder for further configuration.</returns>
    public static McpServerBuilder AddCompoundDocsMcpServer(
        this IServiceCollection services,
        CompoundDocsServerOptions options)
    {
        return new McpServerBuilder(services, options);
    }

    /// <summary>
    /// Adds and configures the MCP server using options from configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsAccessor">The options accessor.</param>
    /// <returns>The MCP server builder for further configuration.</returns>
    public static McpServerBuilder AddCompoundDocsMcpServer(
        this IServiceCollection services,
        IOptions<CompoundDocsServerOptions> optionsAccessor)
    {
        return new McpServerBuilder(services, optionsAccessor.Value);
    }
}
