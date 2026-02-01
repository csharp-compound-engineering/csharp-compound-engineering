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
    public string ServerDescription { get; set; } = "CSharp Compound Docs MCP Server - Semantic documentation management";

    /// <summary>
    /// PostgreSQL connection options.
    /// </summary>
    public PostgresConnectionOptions Postgres { get; set; } = new();

    /// <summary>
    /// Ollama connection options.
    /// </summary>
    public OllamaConnectionOptions Ollama { get; set; } = new();
}

/// <summary>
/// PostgreSQL connection configuration.
/// </summary>
public sealed class PostgresConnectionOptions
{
    /// <summary>
    /// PostgreSQL server host.
    /// Default: localhost, can be overridden by POSTGRES_HOST environment variable.
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// PostgreSQL server port.
    /// Default: 5433, can be overridden by POSTGRES_PORT environment variable.
    /// </summary>
    public int Port { get; set; } = 5433;

    /// <summary>
    /// PostgreSQL database name.
    /// </summary>
    public string Database { get; set; } = "compounding_docs";

    /// <summary>
    /// PostgreSQL username.
    /// </summary>
    public string Username { get; set; } = "compounding";

    /// <summary>
    /// PostgreSQL password.
    /// </summary>
    public string Password { get; set; } = "compounding";

    /// <summary>
    /// Builds a connection string from the configured options.
    /// </summary>
    public string GetConnectionString() =>
        $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password}";
}

/// <summary>
/// Ollama connection configuration.
/// </summary>
public sealed class OllamaConnectionOptions
{
    /// <summary>
    /// Ollama server host.
    /// Default: localhost, can be overridden by OLLAMA_HOST environment variable.
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// Ollama server port.
    /// Default: 11435, can be overridden by OLLAMA_PORT environment variable.
    /// </summary>
    public int Port { get; set; } = 11435;

    /// <summary>
    /// Model to use for text generation.
    /// </summary>
    public string GenerationModel { get; set; } = "mistral";

    /// <summary>
    /// Embedding model (fixed, not configurable).
    /// </summary>
    public static string EmbeddingModel => "mxbai-embed-large";

    /// <summary>
    /// Embedding dimensions for mxbai-embed-large.
    /// </summary>
    public static int EmbeddingDimensions => 1024;

    /// <summary>
    /// Gets the Ollama API endpoint URI.
    /// </summary>
    public Uri GetEndpoint() => new($"http://{Host}:{Port}");
}
