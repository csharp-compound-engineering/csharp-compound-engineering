namespace CompoundDocs.McpServer.Options;

/// <summary>
/// Configuration options for the Compound Docs MCP server.
/// </summary>
public sealed class CompoundDocsServerOptions
{
    /// <summary>
    /// Configuration section name for binding.
    /// </summary>
    public const string SectionName = "McpServer";

    /// <summary>
    /// Server name reported to MCP clients.
    /// </summary>
    public string ServerName { get; set; } = "csharp-compounding-docs";

    /// <summary>
    /// Server description reported to MCP clients.
    /// </summary>
    public string ServerDescription { get; set; } = "CSharp Compound Docs MCP Server - Knowledge oracle with GraphRAG";
}
