namespace CompoundDocs.McpServer.Services.DocumentProcessing;

/// <summary>
/// Result of validating a document against its doc-type schema.
/// </summary>
public sealed class DocumentValidationResult
{
    /// <summary>
    /// Whether the document is valid according to its doc-type schema.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// The detected or specified doc-type.
    /// </summary>
    public string DocType { get; init; } = string.Empty;

    /// <summary>
    /// Validation errors that indicate schema violations.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Validation warnings that indicate potential issues but don't prevent processing.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static DocumentValidationResult Success(string docType, IReadOnlyList<string>? warnings = null)
    {
        return new DocumentValidationResult
        {
            IsValid = true,
            DocType = docType,
            Warnings = warnings ?? []
        };
    }

    /// <summary>
    /// Creates a failed validation result with errors.
    /// </summary>
    public static DocumentValidationResult Failure(
        string docType,
        IReadOnlyList<string> errors,
        IReadOnlyList<string>? warnings = null)
    {
        return new DocumentValidationResult
        {
            IsValid = false,
            DocType = docType,
            Errors = errors,
            Warnings = warnings ?? []
        };
    }

    /// <summary>
    /// Creates a validation result indicating no schema was found for the doc-type.
    /// This is not an error - the document is valid but not schema-validated.
    /// </summary>
    public static DocumentValidationResult NoSchema(string docType)
    {
        return new DocumentValidationResult
        {
            IsValid = true,
            DocType = docType,
            Warnings = [$"No schema found for doc-type '{docType}'. Skipping schema validation."]
        };
    }

    /// <summary>
    /// Creates a validation result for documents without a doc-type.
    /// </summary>
    public static DocumentValidationResult NoDocType()
    {
        return new DocumentValidationResult
        {
            IsValid = true,
            DocType = string.Empty,
            Warnings = ["No doc-type specified in frontmatter. Using default document type."]
        };
    }
}
