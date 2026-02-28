using CompoundDocs.McpServer.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CompoundDocs.McpServer.DependencyInjection;

/// <summary>
/// Extension methods for registering MCP tools with the service collection.
/// </summary>
public static class ToolServiceCollectionExtensions
{
    /// <summary>
    /// Adds the RagQueryTool to the service collection.
    /// </summary>
    public static IServiceCollection AddMcpTools(this IServiceCollection services)
    {
        services.TryAddScoped<RagQueryTool>();
        return services;
    }
}
