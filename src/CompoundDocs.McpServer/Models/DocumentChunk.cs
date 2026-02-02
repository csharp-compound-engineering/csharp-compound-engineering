namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Represents a chunk of a large document for semantic search.
/// </summary>
public sealed class DocumentChunk
{
    /// <summary>
    /// Unique identifier for the chunk (GUID string).
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Reference to the parent document's ID.
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Markdown header path representing the chunk's location in the document structure.
    /// </summary>
    public string HeaderPath { get; set; } = string.Empty;

    /// <summary>
    /// Starting line number of this chunk in the source document (1-indexed).
    /// </summary>
    public int StartLine { get; set; }

    /// <summary>
    /// Ending line number of this chunk in the source document (1-indexed, inclusive).
    /// </summary>
    public int EndLine { get; set; }

    /// <summary>
    /// Content of this chunk for retrieval.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Creates a new chunk from a parent document.
    /// </summary>
    public static DocumentChunk CreateFromParent(
        CompoundDocument parent,
        string headerPath,
        string content,
        int startLine,
        int endLine)
    {
        return new DocumentChunk
        {
            DocumentId = parent.Id,
            HeaderPath = headerPath,
            Content = content,
            StartLine = startLine,
            EndLine = endLine
        };
    }

    /// <summary>
    /// Gets the line count of this chunk.
    /// </summary>
    public int LineCount => EndLine - StartLine + 1;
}
