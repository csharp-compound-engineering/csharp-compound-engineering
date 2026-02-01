using System.Text.Json.Serialization;

namespace CompoundDocs.McpServer.Skills.Capture;

/// <summary>
/// Result model for capture operations.
/// Contains information about the captured document or any errors.
/// </summary>
public sealed class CaptureResult
{
    /// <summary>
    /// Whether the capture operation succeeded.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    /// <summary>
    /// The relative file path where the document was created.
    /// Relative to the project root.
    /// </summary>
    [JsonPropertyName("file_path")]
    public string? FilePath { get; init; }

    /// <summary>
    /// The unique document identifier assigned after indexing.
    /// May be null if indexing was skipped or deferred.
    /// </summary>
    [JsonPropertyName("document_id")]
    public string? DocumentId { get; init; }

    /// <summary>
    /// The doc type of the created document.
    /// </summary>
    [JsonPropertyName("doc_type")]
    public string? DocType { get; init; }

    /// <summary>
    /// The title of the created document.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// Whether the document was indexed into the vector store.
    /// </summary>
    [JsonPropertyName("indexed")]
    public bool Indexed { get; init; }

    /// <summary>
    /// Number of chunks created during indexing.
    /// </summary>
    [JsonPropertyName("chunk_count")]
    public int ChunkCount { get; init; }

    /// <summary>
    /// List of errors if the operation failed.
    /// </summary>
    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Errors { get; init; }

    /// <summary>
    /// List of warnings (non-fatal issues).
    /// </summary>
    [JsonPropertyName("warnings")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Warnings { get; init; }

    /// <summary>
    /// The timestamp when the document was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>
    /// Creates a successful capture result.
    /// </summary>
    public static CaptureResult Succeeded(
        string filePath,
        string docType,
        string title,
        string? documentId = null,
        bool indexed = false,
        int chunkCount = 0,
        IReadOnlyList<string>? warnings = null)
    {
        return new CaptureResult
        {
            Success = true,
            FilePath = filePath,
            DocType = docType,
            Title = title,
            DocumentId = documentId,
            Indexed = indexed,
            ChunkCount = chunkCount,
            Warnings = warnings?.Count > 0 ? warnings : null,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Creates a failed capture result.
    /// </summary>
    public static CaptureResult Failed(
        IReadOnlyList<string> errors,
        IReadOnlyList<string>? warnings = null)
    {
        return new CaptureResult
        {
            Success = false,
            Errors = errors,
            Warnings = warnings?.Count > 0 ? warnings : null
        };
    }

    /// <summary>
    /// Creates a failed capture result with a single error.
    /// </summary>
    public static CaptureResult Failed(string error)
    {
        return new CaptureResult
        {
            Success = false,
            Errors = [error]
        };
    }
}
