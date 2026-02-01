using CompoundDocs.Common.Parsing;

namespace CompoundDocs.McpServer.Services.DocumentProcessing;

/// <summary>
/// Handles chunking of large documents into smaller pieces for processing.
/// Documents are chunked at H2 (##) and H3 (###) header boundaries.
/// </summary>
public sealed class DocumentChunker
{
    /// <summary>
    /// Default line threshold for chunking documents.
    /// Documents exceeding this line count will be chunked.
    /// </summary>
    public const int DefaultChunkThreshold = 500;

    private readonly MarkdownParser _markdownParser;
    private readonly int _chunkThreshold;

    /// <summary>
    /// Creates a new DocumentChunker with the specified line threshold.
    /// </summary>
    /// <param name="markdownParser">The markdown parser instance.</param>
    /// <param name="chunkThreshold">Line count threshold for chunking. Defaults to 500.</param>
    public DocumentChunker(MarkdownParser markdownParser, int chunkThreshold = DefaultChunkThreshold)
    {
        _markdownParser = markdownParser ?? throw new ArgumentNullException(nameof(markdownParser));
        _chunkThreshold = chunkThreshold > 0 ? chunkThreshold : DefaultChunkThreshold;
    }

    /// <summary>
    /// Determines whether a document should be chunked based on line count.
    /// </summary>
    /// <param name="content">The document content.</param>
    /// <returns>True if the document exceeds the chunk threshold.</returns>
    public bool ShouldChunk(string content)
    {
        if (string.IsNullOrEmpty(content))
            return false;

        var lineCount = content.Split('\n').Length;
        return lineCount > _chunkThreshold;
    }

    /// <summary>
    /// Chunks a document into smaller pieces at H2/H3 header boundaries.
    /// </summary>
    /// <param name="content">The document content.</param>
    /// <returns>List of chunk information.</returns>
    public IReadOnlyList<ChunkInfo> ChunkDocument(string content)
    {
        if (string.IsNullOrEmpty(content))
            return [];

        // Use MarkdownParser's built-in chunking functionality
        return _markdownParser.ChunkByHeaders(content, _chunkThreshold);
    }

    /// <summary>
    /// Chunks a document and converts to ProcessedChunk objects.
    /// Embeddings will be null and need to be populated separately.
    /// </summary>
    /// <param name="content">The document content.</param>
    /// <returns>List of processed chunks without embeddings.</returns>
    public IReadOnlyList<ProcessedChunk> CreateProcessedChunks(string content)
    {
        var chunkInfos = ChunkDocument(content);

        return chunkInfos.Select(c => new ProcessedChunk
        {
            Index = c.Index,
            HeaderPath = c.HeaderPath,
            StartLine = c.StartLine,
            EndLine = c.EndLine,
            Content = c.Content,
            Embedding = null // Will be populated by the processor
        }).ToList();
    }

    /// <summary>
    /// Gets the line count of a document.
    /// </summary>
    /// <param name="content">The document content.</param>
    /// <returns>The number of lines.</returns>
    public static int GetLineCount(string content)
    {
        if (string.IsNullOrEmpty(content))
            return 0;

        return content.Split('\n').Length;
    }

    /// <summary>
    /// Gets the chunk threshold for this chunker.
    /// </summary>
    public int ChunkThreshold => _chunkThreshold;
}
