using System.Diagnostics;

namespace CompoundDocs.McpServer.Processing;

/// <summary>
/// Result of an indexing operation containing status and performance metrics.
/// </summary>
public sealed class IndexResult
{
    /// <summary>
    /// The unique identifier for the indexed document.
    /// </summary>
    public string DocumentId { get; init; } = string.Empty;

    /// <summary>
    /// The file path of the indexed document.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// The number of chunks created from the document.
    /// </summary>
    public int ChunkCount { get; init; }

    /// <summary>
    /// Whether the indexing operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Errors that occurred during indexing.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Warnings generated during indexing.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Total processing time in milliseconds.
    /// </summary>
    public long ProcessingTimeMs { get; init; }

    /// <summary>
    /// Time spent generating embeddings in milliseconds.
    /// </summary>
    public long EmbeddingTimeMs { get; init; }

    /// <summary>
    /// The document type that was detected or specified.
    /// </summary>
    public string? DocType { get; init; }

    /// <summary>
    /// The document title that was extracted.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Creates a successful index result.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="filePath">The file path.</param>
    /// <param name="chunkCount">Number of chunks created.</param>
    /// <param name="processingTimeMs">Processing time in milliseconds.</param>
    /// <param name="embeddingTimeMs">Embedding generation time in milliseconds.</param>
    /// <param name="warnings">Any warnings generated.</param>
    /// <param name="docType">The document type.</param>
    /// <param name="title">The document title.</param>
    /// <returns>A successful IndexResult.</returns>
    public static IndexResult Success(
        string documentId,
        string filePath,
        int chunkCount,
        long processingTimeMs,
        long embeddingTimeMs,
        IReadOnlyList<string>? warnings = null,
        string? docType = null,
        string? title = null)
    {
        return new IndexResult
        {
            DocumentId = documentId,
            FilePath = filePath,
            ChunkCount = chunkCount,
            IsSuccess = true,
            Errors = [],
            Warnings = warnings ?? [],
            ProcessingTimeMs = processingTimeMs,
            EmbeddingTimeMs = embeddingTimeMs,
            DocType = docType,
            Title = title
        };
    }

    /// <summary>
    /// Creates a failed index result.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <param name="errors">The errors that occurred.</param>
    /// <param name="processingTimeMs">Processing time in milliseconds.</param>
    /// <param name="warnings">Any warnings generated.</param>
    /// <returns>A failed IndexResult.</returns>
    public static IndexResult Failure(
        string filePath,
        IReadOnlyList<string> errors,
        long processingTimeMs = 0,
        IReadOnlyList<string>? warnings = null)
    {
        return new IndexResult
        {
            DocumentId = string.Empty,
            FilePath = filePath,
            ChunkCount = 0,
            IsSuccess = false,
            Errors = errors,
            Warnings = warnings ?? [],
            ProcessingTimeMs = processingTimeMs,
            EmbeddingTimeMs = 0
        };
    }

    /// <summary>
    /// Creates a failed index result with a single error.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <param name="error">The error message.</param>
    /// <param name="processingTimeMs">Processing time in milliseconds.</param>
    /// <returns>A failed IndexResult.</returns>
    public static IndexResult Failure(string filePath, string error, long processingTimeMs = 0)
    {
        return Failure(filePath, [error], processingTimeMs);
    }

    /// <summary>
    /// Returns a string representation of the index result.
    /// </summary>
    public override string ToString()
    {
        if (IsSuccess)
        {
            return $"IndexResult[Success]: {FilePath} - {ChunkCount} chunks in {ProcessingTimeMs}ms";
        }
        return $"IndexResult[Failed]: {FilePath} - {string.Join("; ", Errors)}";
    }
}

/// <summary>
/// Builder for creating IndexResult instances with timing measurements.
/// </summary>
public sealed class IndexResultBuilder
{
    private readonly Stopwatch _totalStopwatch;
    private readonly Stopwatch _embeddingStopwatch;
    private string _documentId = string.Empty;
    private string _filePath = string.Empty;
    private int _chunkCount;
    private bool _isSuccess = true;
    private readonly List<string> _errors = [];
    private readonly List<string> _warnings = [];
    private string? _docType;
    private string? _title;

    /// <summary>
    /// Creates a new IndexResultBuilder and starts timing.
    /// </summary>
    public IndexResultBuilder()
    {
        _totalStopwatch = Stopwatch.StartNew();
        _embeddingStopwatch = new Stopwatch();
    }

    /// <summary>
    /// Sets the document ID.
    /// </summary>
    public IndexResultBuilder WithDocumentId(string documentId)
    {
        _documentId = documentId;
        return this;
    }

    /// <summary>
    /// Sets the file path.
    /// </summary>
    public IndexResultBuilder WithFilePath(string filePath)
    {
        _filePath = filePath;
        return this;
    }

    /// <summary>
    /// Sets the chunk count.
    /// </summary>
    public IndexResultBuilder WithChunkCount(int chunkCount)
    {
        _chunkCount = chunkCount;
        return this;
    }

    /// <summary>
    /// Sets the document type.
    /// </summary>
    public IndexResultBuilder WithDocType(string? docType)
    {
        _docType = docType;
        return this;
    }

    /// <summary>
    /// Sets the document title.
    /// </summary>
    public IndexResultBuilder WithTitle(string? title)
    {
        _title = title;
        return this;
    }

    /// <summary>
    /// Adds an error and marks the result as failed.
    /// </summary>
    public IndexResultBuilder WithError(string error)
    {
        _errors.Add(error);
        _isSuccess = false;
        return this;
    }

    /// <summary>
    /// Adds multiple errors and marks the result as failed.
    /// </summary>
    public IndexResultBuilder WithErrors(IEnumerable<string> errors)
    {
        _errors.AddRange(errors);
        if (_errors.Count > 0)
        {
            _isSuccess = false;
        }
        return this;
    }

    /// <summary>
    /// Adds a warning.
    /// </summary>
    public IndexResultBuilder WithWarning(string warning)
    {
        _warnings.Add(warning);
        return this;
    }

    /// <summary>
    /// Adds multiple warnings.
    /// </summary>
    public IndexResultBuilder WithWarnings(IEnumerable<string> warnings)
    {
        _warnings.AddRange(warnings);
        return this;
    }

    /// <summary>
    /// Starts timing embedding generation.
    /// </summary>
    public void StartEmbeddingTimer()
    {
        _embeddingStopwatch.Start();
    }

    /// <summary>
    /// Stops timing embedding generation.
    /// </summary>
    public void StopEmbeddingTimer()
    {
        _embeddingStopwatch.Stop();
    }

    /// <summary>
    /// Builds the final IndexResult.
    /// </summary>
    public IndexResult Build()
    {
        _totalStopwatch.Stop();

        return new IndexResult
        {
            DocumentId = _documentId,
            FilePath = _filePath,
            ChunkCount = _chunkCount,
            IsSuccess = _isSuccess,
            Errors = _errors,
            Warnings = _warnings,
            ProcessingTimeMs = _totalStopwatch.ElapsedMilliseconds,
            EmbeddingTimeMs = _embeddingStopwatch.ElapsedMilliseconds,
            DocType = _docType,
            Title = _title
        };
    }
}
