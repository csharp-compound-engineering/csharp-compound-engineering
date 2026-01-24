namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Represents a compound document stored in the vector and graph databases.
/// </summary>
public sealed class CompoundDocument
{
    /// <summary>
    /// Unique identifier for the document (GUID string).
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Document title extracted from frontmatter or first heading.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Full content of the document for retrieval.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Document type identifier — emergent, discovered by LLM during entity extraction.
    /// </summary>
    public string? DocType { get; set; }

    /// <summary>
    /// Relative path from the repository root to the document.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Last modified timestamp of the source file.
    /// </summary>
    public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// JSON-serialized array of document links for graph traversal.
    /// </summary>
    public string? Links { get; set; }
}
