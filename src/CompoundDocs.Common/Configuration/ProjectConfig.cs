namespace CompoundDocs.Common.Configuration;

/// <summary>
/// Project-specific configuration stored in .csharp-compounding-docs/config.json
/// </summary>
public sealed class ProjectConfig
{
    /// <summary>
    /// Project name (derived from directory name if not specified)
    /// </summary>
    public string? ProjectName { get; set; }

    /// <summary>
    /// RAG query configuration
    /// </summary>
    public RagSettings Rag { get; set; } = new();

    /// <summary>
    /// Link resolution configuration
    /// </summary>
    public LinkResolutionSettings LinkResolution { get; set; } = new();

    /// <summary>
    /// File watcher configuration
    /// </summary>
    public FileWatcherSettings FileWatcher { get; set; } = new();

    /// <summary>
    /// External documentation sources (legacy - prefer ExternalDocsConfig)
    /// </summary>
    public List<ExternalDocSource> ExternalDocs { get; set; } = [];

    /// <summary>
    /// External documentation configuration (Phase 134)
    /// </summary>
    public ExternalDocsConfig ExternalDocsSettings { get; set; } = new();

    /// <summary>
    /// Custom doc-type definitions
    /// </summary>
    public List<CustomDocType> CustomDocTypes { get; set; } = [];
}

/// <summary>
/// Dedicated RAG (Retrieval-Augmented Generation) configuration class (Phase 135).
/// Can be used separately or integrated into ProjectConfig.
/// </summary>
public sealed class RagConfig
{
    /// <summary>
    /// Size of text chunks for embedding (default 1000 characters)
    /// </summary>
    public int ChunkSize { get; set; } = 1000;

    /// <summary>
    /// Overlap between chunks in characters (default 200)
    /// </summary>
    public int ChunkOverlap { get; set; } = 200;

    /// <summary>
    /// Maximum number of results to return (default 10)
    /// </summary>
    public int MaxResults { get; set; } = 10;

    /// <summary>
    /// Similarity threshold for relevance (0.0-1.0, default 0.7)
    /// </summary>
    public float SimilarityThreshold { get; set; } = 0.7f;

    /// <summary>
    /// Maximum depth for following document links (default 2)
    /// </summary>
    public int LinkDepth { get; set; } = 2;
}

/// <summary>
/// RAG (Retrieval-Augmented Generation) configuration (Phase 135).
/// </summary>
public sealed class RagSettings
{
    /// <summary>
    /// Size of text chunks for embedding (default 1000 characters)
    /// </summary>
    public int ChunkSize { get; set; } = 1000;

    /// <summary>
    /// Overlap between chunks in characters (default 200)
    /// </summary>
    public int ChunkOverlap { get; set; } = 200;

    /// <summary>
    /// Maximum number of results to return (default 10)
    /// </summary>
    public int MaxResults { get; set; } = 10;

    /// <summary>
    /// Similarity threshold for relevance (0.0-1.0, default 0.7)
    /// </summary>
    public float SimilarityThreshold { get; set; } = 0.7f;

    /// <summary>
    /// Maximum depth for following document links (default 2)
    /// </summary>
    public int LinkDepth { get; set; } = 2;

    /// <summary>
    /// [Deprecated] Use SimilarityThreshold instead
    /// </summary>
    [Obsolete("Use SimilarityThreshold instead")]
    public float RelevanceThreshold
    {
        get => SimilarityThreshold;
        set => SimilarityThreshold = value;
    }

    /// <summary>
    /// [Deprecated] Use LinkDepth instead
    /// </summary>
    [Obsolete("Use LinkDepth instead")]
    public int MaxLinkedDocs { get; set; } = 5;
}

public sealed class LinkResolutionSettings
{
    /// <summary>
    /// Maximum depth for following document links
    /// </summary>
    public int MaxDepth { get; set; } = 2;

    /// <summary>
    /// Maximum number of linked documents to include
    /// </summary>
    public int MaxLinkedDocs { get; set; } = 5;
}

public sealed class FileWatcherSettings
{
    /// <summary>
    /// Debounce interval in milliseconds
    /// </summary>
    public int DebounceMs { get; set; } = 500;

    /// <summary>
    /// File patterns to watch (glob patterns)
    /// </summary>
    public List<string> IncludePatterns { get; set; } = ["**/*.md"];

    /// <summary>
    /// File patterns to exclude (glob patterns)
    /// </summary>
    public List<string> ExcludePatterns { get; set; } = ["**/node_modules/**", "**/.git/**"];
}

public sealed class ExternalDocSource
{
    /// <summary>
    /// Unique identifier for this doc source
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Display name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Path to external documentation (can be relative or absolute)
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Whether this source is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Namespace prefix for documents from this source
    /// </summary>
    public string? NamespacePrefix { get; set; }
}

/// <summary>
/// Configuration for external documentation sources (Phase 134).
/// </summary>
public sealed class ExternalDocsConfig
{
    /// <summary>
    /// List of external doc sources (URLs, paths, etc.)
    /// </summary>
    public List<ExternalDocSource> Sources { get; set; } = [];

    /// <summary>
    /// Sync frequency in hours (0 = no automatic sync)
    /// </summary>
    public int SyncFrequencyHours { get; set; } = 24;

    /// <summary>
    /// Whether to automatically sync external docs on startup
    /// </summary>
    public bool SyncOnStartup { get; set; } = true;

    /// <summary>
    /// Default namespace prefix for external docs without explicit prefix
    /// </summary>
    public string DefaultNamespacePrefix { get; set; } = "external";
}

public sealed class CustomDocType
{
    /// <summary>
    /// Unique identifier (used in doc_type frontmatter field)
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Display name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Description of what this doc-type captures
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// JSON schema for frontmatter validation
    /// </summary>
    public string? Schema { get; set; }

    /// <summary>
    /// Trigger phrases for auto-invoke
    /// </summary>
    public List<string> TriggerPhrases { get; set; } = [];
}
