namespace CompoundDocs.McpServer.Skills.Capture;

/// <summary>
/// Interface for handling capture skill operations.
/// Defines the contract for capturing and creating documentation artifacts.
/// </summary>
public interface ICaptureSkillHandler
{
    /// <summary>
    /// Handles a capture request to create a new document.
    /// </summary>
    /// <param name="request">The capture request containing document details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the capture operation.</returns>
    Task<CaptureResult> HandleCaptureAsync(
        CaptureRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the content structure for a given document type.
    /// </summary>
    /// <param name="docType">The document type to validate against.</param>
    /// <param name="content">The content to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with any errors or warnings.</returns>
    Task<ContentValidationResult> ValidateContentAsync(
        string docType,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the template for a specific document type.
    /// </summary>
    /// <param name="docType">The document type.</param>
    /// <returns>The markdown template for the document type, or null if not found.</returns>
    string? GetTemplate(string docType);

    /// <summary>
    /// Gets the available document types for capture operations.
    /// </summary>
    /// <returns>List of supported document types.</returns>
    IReadOnlyList<string> GetSupportedDocTypes();
}

/// <summary>
/// Result of content validation.
/// </summary>
public sealed class ContentValidationResult
{
    /// <summary>
    /// Whether the content is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Validation errors that prevent document creation.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Validation warnings that don't prevent creation but may indicate issues.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ContentValidationResult Success(IReadOnlyList<string>? warnings = null)
    {
        return new ContentValidationResult
        {
            IsValid = true,
            Warnings = warnings ?? []
        };
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static ContentValidationResult Failure(
        IReadOnlyList<string> errors,
        IReadOnlyList<string>? warnings = null)
    {
        return new ContentValidationResult
        {
            IsValid = false,
            Errors = errors,
            Warnings = warnings ?? []
        };
    }
}
