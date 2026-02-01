using System.Text.Json.Serialization;

namespace CompoundDocs.McpServer.Skills.Capture;

/// <summary>
/// Request model for capture operations.
/// Contains all information needed to create a new compound document.
/// </summary>
public sealed class CaptureRequest
{
    /// <summary>
    /// The document type to create.
    /// Must be one of the supported types: problem, insight, codebase, tool, style.
    /// </summary>
    [JsonPropertyName("doc_type")]
    public required string DocType { get; init; }

    /// <summary>
    /// The title of the document.
    /// Used in frontmatter and as the primary identifier.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// The main content of the document.
    /// Should follow the template structure for the doc type.
    /// </summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    /// <summary>
    /// Optional additional metadata for the frontmatter.
    /// Keys are field names, values are field values.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; init; }

    /// <summary>
    /// Optional explicit file path for the document.
    /// If not provided, a path will be generated from the title and doc type.
    /// Path should be relative to the .csharp-compounding-docs directory.
    /// </summary>
    [JsonPropertyName("file_path")]
    public string? FilePath { get; init; }

    /// <summary>
    /// Optional tags for the document.
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string>? Tags { get; init; }

    /// <summary>
    /// Optional promotion level for the document.
    /// Defaults to "standard" if not specified.
    /// </summary>
    [JsonPropertyName("promotion_level")]
    public string? PromotionLevel { get; init; }

    /// <summary>
    /// Whether to use the template for the doc type.
    /// If true and content is empty, the template will be used.
    /// If true and content is provided, template sections will be validated.
    /// </summary>
    [JsonPropertyName("use_template")]
    public bool UseTemplate { get; init; } = true;

    /// <summary>
    /// Whether to overwrite an existing file at the same path.
    /// Defaults to false for safety.
    /// </summary>
    [JsonPropertyName("overwrite")]
    public bool Overwrite { get; init; } = false;
}
