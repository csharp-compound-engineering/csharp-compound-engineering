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

