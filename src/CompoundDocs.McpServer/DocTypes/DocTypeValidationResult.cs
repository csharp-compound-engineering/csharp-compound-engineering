namespace CompoundDocs.McpServer.DocTypes;

/// <summary>
/// Represents the result of validating frontmatter against a document type schema.
/// </summary>
public sealed class DocTypeValidationResult
{
    /// <summary>
    /// Whether the validation passed.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// The document type that was validated against.
    /// </summary>
    public string DocTypeId { get; init; } = string.Empty;

    /// <summary>
    /// Validation errors found during validation.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; init; } = [];

    /// <summary>
    /// Validation warnings (non-fatal issues).
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static DocTypeValidationResult Success(string docTypeId, IReadOnlyList<string>? warnings = null)
    {
        return new DocTypeValidationResult
        {
            IsValid = true,
            DocTypeId = docTypeId,
            Warnings = warnings ?? []
        };
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static DocTypeValidationResult Failure(
        string docTypeId,
        IReadOnlyList<ValidationError> errors,
        IReadOnlyList<string>? warnings = null)
    {
        return new DocTypeValidationResult
        {
            IsValid = false,
            DocTypeId = docTypeId,
            Errors = errors,
            Warnings = warnings ?? []
        };
    }

    /// <summary>
    /// Creates a result for when the document type is not found.
    /// </summary>
    public static DocTypeValidationResult DocTypeNotFound(string docTypeId)
    {
        return new DocTypeValidationResult
        {
            IsValid = false,
            DocTypeId = docTypeId,
            Errors = [new ValidationError
            {
                PropertyPath = "doc_type",
                Message = $"Document type '{docTypeId}' is not registered.",
                ErrorType = ValidationErrorType.InvalidDocType
            }]
        };
    }
}

/// <summary>
/// Represents a validation error.
/// </summary>
public sealed class ValidationError
{
    /// <summary>
    /// The JSON path to the property that failed validation.
    /// </summary>
    public required string PropertyPath { get; init; }

    /// <summary>
    /// The error message describing the validation failure.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The type of validation error.
    /// </summary>
    public ValidationErrorType ErrorType { get; init; } = ValidationErrorType.Schema;

    /// <summary>
    /// The expected value or format (if applicable).
    /// </summary>
    public string? Expected { get; init; }

    /// <summary>
    /// The actual value that was provided (if applicable).
    /// </summary>
    public string? Actual { get; init; }

    /// <inheritdoc/>
    public override string ToString() => $"{PropertyPath}: {Message}";
}

/// <summary>
/// Types of validation errors.
/// </summary>
public enum ValidationErrorType
{
    /// <summary>
    /// A required field is missing.
    /// </summary>
    RequiredField,

    /// <summary>
    /// A field has an invalid type.
    /// </summary>
    InvalidType,

    /// <summary>
    /// A field value failed schema validation.
    /// </summary>
    Schema,

    /// <summary>
    /// The document type is not registered.
    /// </summary>
    InvalidDocType,

    /// <summary>
    /// A field value is not in the allowed set.
    /// </summary>
    InvalidEnum,

    /// <summary>
    /// A field value failed pattern validation.
    /// </summary>
    PatternMismatch,

    /// <summary>
    /// A field value is outside the allowed range.
    /// </summary>
    OutOfRange
}
