namespace CompoundDocs.Common.Configuration;

/// <summary>
/// Global configuration stored in ~/.claude/.csharp-compounding-docs/
/// </summary>
public sealed class GlobalConfig
{
    /// <summary>
    /// Path to global config directory (default: ~/.claude/.csharp-compounding-docs/)
    /// </summary>
    public string ConfigDirectory { get; set; } = GetDefaultConfigDirectory();

    /// <summary>
    /// PostgreSQL connection settings
    /// </summary>
    public PostgresSettings Postgres { get; set; } = new();

    /// <summary>
    /// Ollama connection settings
    /// </summary>
    public OllamaSettings Ollama { get; set; } = new();

    private static string GetDefaultConfigDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".claude", ".csharp-compounding-docs");
    }
}

public sealed class PostgresSettings
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5433;
    public string Database { get; set; } = "compounding_docs";
    public string Username { get; set; } = "compounding";
    public string Password { get; set; } = "compounding";

    public string GetConnectionString() =>
        $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password}";
}

public sealed class OllamaSettings
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 11435;
    public string GenerationModel { get; set; } = "mistral";

    /// <summary>
    /// Embedding model is fixed and not configurable
    /// </summary>
    public static string EmbeddingModel => "mxbai-embed-large";

    /// <summary>
    /// Embedding dimensions for mxbai-embed-large
    /// </summary>
    public static int EmbeddingDimensions => 1024;

    public Uri GetEndpoint() => new($"http://{Host}:{Port}");
}
