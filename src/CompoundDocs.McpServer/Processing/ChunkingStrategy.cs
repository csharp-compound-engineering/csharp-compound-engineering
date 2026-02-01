namespace CompoundDocs.McpServer.Processing;

/// <summary>
/// Configuration options for the chunking strategy.
/// </summary>
public sealed class ChunkingOptions
{
    /// <summary>
    /// The default maximum size for a chunk in characters.
    /// </summary>
    public const int DefaultChunkSize = 1000;

    /// <summary>
    /// The default overlap between chunks in characters.
    /// </summary>
    public const int DefaultOverlap = 200;

    /// <summary>
    /// Maximum size for a chunk in characters.
    /// Default is 1000 characters.
    /// </summary>
    public int ChunkSize { get; init; } = DefaultChunkSize;

    /// <summary>
    /// Overlap between consecutive chunks in characters.
    /// Default is 200 characters.
    /// </summary>
    public int Overlap { get; init; } = DefaultOverlap;

    /// <summary>
    /// Whether to respect paragraph boundaries when chunking.
    /// Default is true.
    /// </summary>
    public bool RespectParagraphBoundaries { get; init; } = true;

    /// <summary>
    /// Minimum chunk size in characters.
    /// Chunks smaller than this may be merged with adjacent chunks.
    /// Default is 100 characters.
    /// </summary>
    public int MinChunkSize { get; init; } = 100;

    /// <summary>
    /// Validates the options and throws if invalid.
    /// </summary>
    public void Validate()
    {
        if (ChunkSize <= 0)
        {
            throw new ArgumentException("ChunkSize must be greater than 0", nameof(ChunkSize));
        }

        if (Overlap < 0)
        {
            throw new ArgumentException("Overlap cannot be negative", nameof(Overlap));
        }

        if (Overlap >= ChunkSize)
        {
            throw new ArgumentException("Overlap must be less than ChunkSize", nameof(Overlap));
        }

        if (MinChunkSize < 0)
        {
            throw new ArgumentException("MinChunkSize cannot be negative", nameof(MinChunkSize));
        }
    }
}

/// <summary>
/// Implements semantic chunking with configurable size and overlap.
/// Respects paragraph boundaries when possible.
/// </summary>
public sealed class ChunkingStrategy
{
    private readonly ChunkingOptions _options;

    /// <summary>
    /// Creates a new ChunkingStrategy with default options.
    /// </summary>
    public ChunkingStrategy() : this(new ChunkingOptions())
    {
    }

    /// <summary>
    /// Creates a new ChunkingStrategy with the specified options.
    /// </summary>
    /// <param name="options">The chunking options.</param>
    public ChunkingStrategy(ChunkingOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    /// <summary>
    /// Gets the configured chunk size.
    /// </summary>
    public int ChunkSize => _options.ChunkSize;

    /// <summary>
    /// Gets the configured overlap size.
    /// </summary>
    public int Overlap => _options.Overlap;

    /// <summary>
    /// Chunks the given content into smaller pieces.
    /// </summary>
    /// <param name="content">The content to chunk.</param>
    /// <param name="documentId">Optional parent document identifier for reference.</param>
    /// <returns>A list of content chunks with metadata.</returns>
    public IReadOnlyList<ContentChunk> Chunk(string content, string? documentId = null)
    {
        if (string.IsNullOrEmpty(content))
        {
            return [];
        }

        // If content is small enough, return as single chunk
        if (content.Length <= _options.ChunkSize)
        {
            return
            [
                new ContentChunk
                {
                    Index = 0,
                    Content = content,
                    StartOffset = 0,
                    EndOffset = content.Length,
                    ParentDocumentId = documentId
                }
            ];
        }

        var chunks = new List<ContentChunk>();

        if (_options.RespectParagraphBoundaries)
        {
            chunks.AddRange(ChunkByParagraphs(content, documentId));
        }
        else
        {
            chunks.AddRange(ChunkBySize(content, documentId));
        }

        // Merge small chunks if needed
        return MergeSmallChunks(chunks);
    }

    /// <summary>
    /// Chunks content by paragraph boundaries.
    /// </summary>
    private IReadOnlyList<ContentChunk> ChunkByParagraphs(string content, string? documentId)
    {
        var chunks = new List<ContentChunk>();

        // Split content into paragraphs (double newline separated)
        var paragraphs = SplitIntoParagraphs(content);

        var currentChunkContent = new System.Text.StringBuilder();
        var currentStartOffset = 0;
        var chunkIndex = 0;
        var currentOffset = 0;

        foreach (var paragraph in paragraphs)
        {
            // Check if adding this paragraph would exceed chunk size
            if (currentChunkContent.Length > 0 &&
                currentChunkContent.Length + paragraph.Length + 2 > _options.ChunkSize) // +2 for paragraph separator
            {
                // Save current chunk
                var chunkContent = currentChunkContent.ToString().Trim();
                if (chunkContent.Length > 0)
                {
                    chunks.Add(new ContentChunk
                    {
                        Index = chunkIndex++,
                        Content = chunkContent,
                        StartOffset = currentStartOffset,
                        EndOffset = currentOffset,
                        ParentDocumentId = documentId
                    });
                }

                // Start new chunk with overlap
                currentChunkContent.Clear();
                if (_options.Overlap > 0 && chunkContent.Length > 0)
                {
                    var overlapContent = GetOverlapContent(chunkContent);
                    currentChunkContent.Append(overlapContent);
                    currentStartOffset = currentOffset - overlapContent.Length;
                }
                else
                {
                    currentStartOffset = currentOffset;
                }
            }

            // Add paragraph to current chunk
            if (currentChunkContent.Length > 0)
            {
                currentChunkContent.Append("\n\n");
            }
            currentChunkContent.Append(paragraph);
            currentOffset += paragraph.Length + 2; // Account for paragraph separator
        }

        // Don't forget the last chunk
        if (currentChunkContent.Length > 0)
        {
            var finalContent = currentChunkContent.ToString().Trim();
            if (finalContent.Length > 0)
            {
                chunks.Add(new ContentChunk
                {
                    Index = chunkIndex,
                    Content = finalContent,
                    StartOffset = currentStartOffset,
                    EndOffset = content.Length,
                    ParentDocumentId = documentId
                });
            }
        }

        return chunks;
    }

    /// <summary>
    /// Chunks content by fixed size with overlap.
    /// </summary>
    private IReadOnlyList<ContentChunk> ChunkBySize(string content, string? documentId)
    {
        var chunks = new List<ContentChunk>();
        var chunkIndex = 0;
        var offset = 0;

        while (offset < content.Length)
        {
            var endOffset = Math.Min(offset + _options.ChunkSize, content.Length);
            var chunkContent = content[offset..endOffset];

            chunks.Add(new ContentChunk
            {
                Index = chunkIndex++,
                Content = chunkContent,
                StartOffset = offset,
                EndOffset = endOffset,
                ParentDocumentId = documentId
            });

            // Move forward by chunk size minus overlap
            offset += _options.ChunkSize - _options.Overlap;

            // Ensure we don't get stuck in an infinite loop
            if (offset <= chunks[^1].StartOffset)
            {
                offset = chunks[^1].EndOffset;
            }
        }

        return chunks;
    }

    /// <summary>
    /// Splits content into paragraphs based on double newlines.
    /// </summary>
    private static IReadOnlyList<string> SplitIntoParagraphs(string content)
    {
        var paragraphs = new List<string>();
        var separators = new[] { "\n\n", "\r\n\r\n" };

        var parts = content.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                paragraphs.Add(trimmed);
            }
        }

        return paragraphs;
    }

    /// <summary>
    /// Gets overlap content from the end of a chunk.
    /// </summary>
    private string GetOverlapContent(string content)
    {
        if (content.Length <= _options.Overlap)
        {
            return content;
        }

        // Try to break at a word boundary
        var startIndex = content.Length - _options.Overlap;

        // Look for a space near the start index
        var spaceIndex = content.IndexOf(' ', startIndex);
        if (spaceIndex != -1 && spaceIndex < content.Length - 10)
        {
            return content[(spaceIndex + 1)..];
        }

        return content[startIndex..];
    }

    /// <summary>
    /// Merges chunks that are smaller than the minimum size.
    /// </summary>
    private IReadOnlyList<ContentChunk> MergeSmallChunks(List<ContentChunk> chunks)
    {
        if (chunks.Count <= 1 || _options.MinChunkSize <= 0)
        {
            return chunks;
        }

        var result = new List<ContentChunk>();

        for (var i = 0; i < chunks.Count; i++)
        {
            var current = chunks[i];

            // If current chunk is small and there's a next chunk, merge them
            if (current.Content.Length < _options.MinChunkSize && i + 1 < chunks.Count)
            {
                var next = chunks[i + 1];
                var mergedContent = current.Content + "\n\n" + next.Content;

                // Replace next chunk with merged content
                chunks[i + 1] = new ContentChunk
                {
                    Index = result.Count,
                    Content = mergedContent,
                    StartOffset = current.StartOffset,
                    EndOffset = next.EndOffset,
                    ParentDocumentId = current.ParentDocumentId
                };
                continue;
            }

            result.Add(new ContentChunk
            {
                Index = result.Count,
                Content = current.Content,
                StartOffset = current.StartOffset,
                EndOffset = current.EndOffset,
                ParentDocumentId = current.ParentDocumentId
            });
        }

        return result;
    }

    /// <summary>
    /// Determines if content should be chunked based on size.
    /// </summary>
    /// <param name="content">The content to check.</param>
    /// <returns>True if the content exceeds the chunk size threshold.</returns>
    public bool ShouldChunk(string content)
    {
        return !string.IsNullOrEmpty(content) && content.Length > _options.ChunkSize;
    }

    /// <summary>
    /// Gets the estimated number of chunks for the given content.
    /// </summary>
    /// <param name="content">The content to estimate.</param>
    /// <returns>The estimated number of chunks.</returns>
    public int EstimateChunkCount(string content)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= _options.ChunkSize)
        {
            return 1;
        }

        var effectiveChunkSize = _options.ChunkSize - _options.Overlap;
        return (int)Math.Ceiling((double)content.Length / effectiveChunkSize);
    }
}

/// <summary>
/// Represents a chunk of content with metadata.
/// </summary>
public sealed class ContentChunk
{
    /// <summary>
    /// The index of this chunk within the parent document.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// The content of this chunk.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// The start offset of this chunk in the original content.
    /// </summary>
    public int StartOffset { get; init; }

    /// <summary>
    /// The end offset of this chunk in the original content.
    /// </summary>
    public int EndOffset { get; init; }

    /// <summary>
    /// The identifier of the parent document.
    /// </summary>
    public string? ParentDocumentId { get; init; }

    /// <summary>
    /// The length of this chunk's content.
    /// </summary>
    public int Length => Content.Length;

    /// <summary>
    /// Creates a string representation of this chunk.
    /// </summary>
    public override string ToString() =>
        $"Chunk[{Index}] ({StartOffset}-{EndOffset}, {Length} chars)";
}
