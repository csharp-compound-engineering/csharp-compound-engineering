using CompoundDocs.Common.Parsing;
using CompoundDocs.McpServer.Models;

namespace CompoundDocs.McpServer.Services.DocumentProcessing;

/// <summary>
/// Result of processing a markdown document.
/// Contains extracted metadata, content, embeddings, and any validation errors.
/// </summary>
public sealed class ProcessedDocument
{
    /// <summary>
    /// The relative file path of the source document.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// Document title extracted from frontmatter or first heading.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Document type from frontmatter (e.g., "problem", "insight", "codebase", "tool", "style").
    /// </summary>
    public string DocType { get; init; } = string.Empty;

    /// <summary>
    /// Promotion level from frontmatter or default "standard".
    /// </summary>
    public string PromotionLevel { get; init; } = PromotionLevels.Standard;

    /// <summary>
    /// The document body content (without frontmatter).
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Vector embedding for the document content.
    /// Null if embedding generation failed.
    /// </summary>
    public ReadOnlyMemory<float>? Embedding { get; init; }

    /// <summary>
    /// Document chunks if the document exceeds the chunking threshold.
    /// Empty list if document is not chunked.
    /// </summary>
    public IReadOnlyList<ProcessedChunk> Chunks { get; init; } = [];

    /// <summary>
    /// Links extracted from the document to other documents.
    /// </summary>
    public IReadOnlyList<LinkInfo> Links { get; init; } = [];

    /// <summary>
    /// Raw frontmatter dictionary from the document.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Frontmatter { get; init; }

    /// <summary>
    /// Whether the document was chunked due to size.
    /// </summary>
    public bool IsChunked => Chunks.Count > 0;

    /// <summary>
    /// Error message if processing failed. Null if successful.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Whether the document was processed successfully.
    /// </summary>
    public bool IsSuccess => Error == null;

    /// <summary>
    /// Validation result for doc-type schema compliance.
    /// </summary>
    public DocumentValidationResult? ValidationResult { get; init; }

    /// <summary>
    /// The tenant key for this document.
    /// </summary>
    public string TenantKey { get; init; } = string.Empty;

    /// <summary>
    /// Creates a successful processed document.
    /// </summary>
    public static ProcessedDocument Success(
        string filePath,
        string title,
        string docType,
        string promotionLevel,
        string content,
        ReadOnlyMemory<float>? embedding,
        IReadOnlyList<ProcessedChunk> chunks,
        IReadOnlyList<LinkInfo> links,
        IReadOnlyDictionary<string, object?>? frontmatter,
        DocumentValidationResult? validationResult,
        string tenantKey)
    {
        return new ProcessedDocument
        {
            FilePath = filePath,
            Title = title,
            DocType = docType,
            PromotionLevel = promotionLevel,
            Content = content,
            Embedding = embedding,
            Chunks = chunks,
            Links = links,
            Frontmatter = frontmatter,
            ValidationResult = validationResult,
            TenantKey = tenantKey
        };
    }

    /// <summary>
    /// Creates a failed processed document with an error message.
    /// </summary>
    public static ProcessedDocument Failure(string filePath, string tenantKey, string error)
    {
        return new ProcessedDocument
        {
            FilePath = filePath,
            TenantKey = tenantKey,
            Error = error
        };
    }
}

/// <summary>
/// A processed chunk of a document.
/// </summary>
public sealed class ProcessedChunk
{
    /// <summary>
    /// Index of this chunk within the document.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Markdown header path for this chunk (e.g., "## Section > ### Subsection").
    /// </summary>
    public string HeaderPath { get; init; } = string.Empty;

    /// <summary>
    /// Starting line number (0-indexed).
    /// </summary>
    public int StartLine { get; init; }

    /// <summary>
    /// Ending line number (0-indexed, inclusive).
    /// </summary>
    public int EndLine { get; init; }

    /// <summary>
    /// Content of this chunk.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Vector embedding for this chunk.
    /// Null if embedding generation failed.
    /// </summary>
    public ReadOnlyMemory<float>? Embedding { get; init; }

    /// <summary>
    /// Number of lines in this chunk.
    /// </summary>
    public int LineCount => EndLine - StartLine + 1;
}
