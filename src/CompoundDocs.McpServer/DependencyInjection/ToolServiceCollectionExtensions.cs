using CompoundDocs.McpServer.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Server;

namespace CompoundDocs.McpServer.DependencyInjection;

/// <summary>
/// Extension methods for registering MCP tools with the service collection.
/// </summary>
public static class ToolServiceCollectionExtensions
{
    /// <summary>
    /// Adds all MCP tools to the service collection and registers them with the MCP server.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This registers the following tools:
    /// <list type="bullet">
    ///   <item><description>activate_project - Activate a project for the session</description></item>
    ///   <item><description>index_document - Manually index a document</description></item>
    ///   <item><description>semantic_search - Vector similarity search</description></item>
    ///   <item><description>rag_query - RAG-based Q&amp;A with source attribution</description></item>
    ///   <item><description>list_doc_types - List available document types</description></item>
    ///   <item><description>delete_documents - Delete documents from the index</description></item>
    ///   <item><description>update_promotion_level - Update document visibility</description></item>
    ///   <item><description>search_external_docs - Search external documentation</description></item>
    ///   <item><description>rag_query_external - RAG against external docs</description></item>
    ///   <item><description>get_health - Get system health status</description></item>
    ///   <item><description>get_metrics - Get operational metrics</description></item>
    ///   <item><description>get_status - Get server status</description></item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddMcpTools(this IServiceCollection services)
    {
        // Register tool classes for dependency injection
        services.TryAddScoped<ActivateProjectTool>();
        services.TryAddScoped<IndexDocumentTool>();
        services.TryAddScoped<SemanticSearchTool>();
        services.TryAddScoped<RagQueryTool>();
        services.TryAddScoped<ListDocTypesTool>();
        services.TryAddScoped<DeleteDocumentsTool>();
        services.TryAddScoped<UpdatePromotionLevelTool>();
        services.TryAddScoped<SearchExternalDocsTool>();
        services.TryAddScoped<RagQueryExternalTool>();
        services.TryAddScoped<DiagnosticsTool>();

        return services;
    }

    /// <summary>
    /// Registers all MCP tools with the MCP server builder.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IMcpServerBuilder WithAllTools(this IMcpServerBuilder builder)
    {
        // Register all tool types with the MCP server
        builder.WithTools<ActivateProjectTool>();
        builder.WithTools<IndexDocumentTool>();
        builder.WithTools<SemanticSearchTool>();
        builder.WithTools<RagQueryTool>();
        builder.WithTools<ListDocTypesTool>();
        builder.WithTools<DeleteDocumentsTool>();
        builder.WithTools<UpdatePromotionLevelTool>();
        builder.WithTools<SearchExternalDocsTool>();
        builder.WithTools<RagQueryExternalTool>();
        builder.WithTools<DiagnosticsTool>();

        return builder;
    }

    /// <summary>
    /// Registers a subset of MCP tools based on capabilities.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <param name="includeProjectTools">Include project activation tools.</param>
    /// <param name="includeSearchTools">Include search and RAG tools.</param>
    /// <param name="includeManagementTools">Include document management tools.</param>
    /// <param name="includeExternalTools">Include external documentation tools.</param>
    /// <param name="includeDiagnosticsTools">Include diagnostics and monitoring tools.</param>
    /// <returns>The builder for chaining.</returns>
    public static IMcpServerBuilder WithTools(
        this IMcpServerBuilder builder,
        bool includeProjectTools = true,
        bool includeSearchTools = true,
        bool includeManagementTools = true,
        bool includeExternalTools = true,
        bool includeDiagnosticsTools = true)
    {
        if (includeProjectTools)
        {
            builder.WithTools<ActivateProjectTool>();
        }

        if (includeSearchTools)
        {
            builder.WithTools<SemanticSearchTool>();
            builder.WithTools<RagQueryTool>();
        }

        if (includeManagementTools)
        {
            builder.WithTools<IndexDocumentTool>();
            builder.WithTools<ListDocTypesTool>();
            builder.WithTools<DeleteDocumentsTool>();
            builder.WithTools<UpdatePromotionLevelTool>();
        }

        if (includeExternalTools)
        {
            builder.WithTools<SearchExternalDocsTool>();
            builder.WithTools<RagQueryExternalTool>();
        }

        if (includeDiagnosticsTools)
        {
            builder.WithTools<DiagnosticsTool>();
        }

        return builder;
    }
}
