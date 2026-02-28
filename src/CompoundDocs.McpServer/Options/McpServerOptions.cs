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

    /// <summary>
    /// Port for HTTP transport.
    /// </summary>
    public int Port { get; set; } = 8080;
}

/// <summary>
/// Configuration options for API key authentication.
/// </summary>
public sealed class ApiKeyAuthenticationOptions
{
    /// <summary>
    /// Comma-separated list of valid API keys. Bound from the "Authentication" configuration section.
    /// </summary>
    public string ApiKeys { get; set; } = string.Empty;

    /// <summary>
    /// Header name to check for the API key.
    /// </summary>
    public string HeaderName { get; set; } = "X-API-Key";

    /// <summary>
    /// Whether API key authentication is enabled. Disable for local development.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Parses the comma-separated API keys into a HashSet for O(1) lookup.
    /// </summary>
    public HashSet<string> GetValidApiKeys()
    {
        if (string.IsNullOrWhiteSpace(ApiKeys))
            return new HashSet<string>(StringComparer.Ordinal);

        return new HashSet<string>(
            ApiKeys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.Ordinal);
    }
}
