# Phase 027: Standardized Error Response Format

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: MCP Server Core
> **Prerequisites**: Phase 026 (Tool Request Handler)

---

## Spec References

This phase implements the standardized error response format defined in:

- **spec/mcp-server/tools.md** - [Error Handling](#error-handling) section (lines 409-448)
- **spec/mcp-server/tools.md** - Standard error codes table (lines 426-436)
- **spec/mcp-server/tools.md** - Error response behaviors (lines 438-446)

---

## Objectives

1. Create the standardized `ErrorResponse` model structure
2. Define the `ErrorCode` enumeration with all standard error codes
3. Implement error message formatting utilities
4. Build exception-to-error-response mapping infrastructure
5. Implement validation error aggregation for multi-field validation failures
6. Ensure all error responses are client-friendly and actionable

---

## Acceptance Criteria

- [ ] `ErrorResponse` model exists with required properties:
  - [ ] `Error` (bool) - always `true` for error responses
  - [ ] `Code` (string) - machine-readable error code
  - [ ] `Message` (string) - human-readable error description
  - [ ] `Details` (object) - optional additional context
- [ ] `ErrorCode` enum/constants defined for all standard error codes:
  - [ ] `PROJECT_NOT_ACTIVATED`
  - [ ] `EXTERNAL_DOCS_NOT_CONFIGURED`
  - [ ] `EXTERNAL_DOCS_NOT_PROMOTABLE`
  - [ ] `DOCUMENT_NOT_FOUND`
  - [ ] `INVALID_DOC_TYPE`
  - [ ] `SCHEMA_VALIDATION_FAILED`
  - [ ] `EMBEDDING_SERVICE_ERROR`
  - [ ] `DATABASE_ERROR`
  - [ ] `FILE_SYSTEM_ERROR`
- [ ] `IErrorResponseFactory` interface and implementation created
- [ ] Exception mapping infrastructure handles:
  - [ ] Domain-specific exceptions mapped to appropriate error codes
  - [ ] Validation exceptions aggregated into single response
  - [ ] Unknown exceptions wrapped with generic error code
- [ ] Validation error aggregation supports multiple field errors
- [ ] Error messages include actionable guidance where appropriate
- [ ] Unit tests cover all error code scenarios

---

## Implementation Notes

### ErrorResponse Model

Create the response model per spec/mcp-server/tools.md format:

```csharp
namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Standardized error response for all MCP tool operations.
/// </summary>
public sealed record ErrorResponse
{
    /// <summary>
    /// Always true for error responses.
    /// </summary>
    public bool Error { get; init; } = true;

    /// <summary>
    /// Machine-readable error code (e.g., "PROJECT_NOT_ACTIVATED").
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Human-readable error description with actionable guidance.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Optional additional context (validation errors, affected resources, etc.).
    /// </summary>
    public object? Details { get; init; }
}
```

### ErrorCode Constants

Define error codes as constants for type safety and discoverability:

```csharp
namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Standard error codes for MCP tool operations.
/// </summary>
public static class ErrorCodes
{
    /// <summary>No project is currently activated.</summary>
    public const string ProjectNotActivated = "PROJECT_NOT_ACTIVATED";

    /// <summary>external_docs not configured in project config.</summary>
    public const string ExternalDocsNotConfigured = "EXTERNAL_DOCS_NOT_CONFIGURED";

    /// <summary>Cannot promote external documentation.</summary>
    public const string ExternalDocsNotPromotable = "EXTERNAL_DOCS_NOT_PROMOTABLE";

    /// <summary>Requested document does not exist.</summary>
    public const string DocumentNotFound = "DOCUMENT_NOT_FOUND";

    /// <summary>Unknown or invalid doc-type specified.</summary>
    public const string InvalidDocType = "INVALID_DOC_TYPE";

    /// <summary>Document frontmatter doesn't match schema.</summary>
    public const string SchemaValidationFailed = "SCHEMA_VALIDATION_FAILED";

    /// <summary>Ollama embedding generation failed.</summary>
    public const string EmbeddingServiceError = "EMBEDDING_SERVICE_ERROR";

    /// <summary>PostgreSQL operation failed.</summary>
    public const string DatabaseError = "DATABASE_ERROR";

    /// <summary>File read/write operation failed.</summary>
    public const string FileSystemError = "FILE_SYSTEM_ERROR";

    /// <summary>Parameter validation failed.</summary>
    public const string ValidationError = "VALIDATION_ERROR";

    /// <summary>An unexpected internal error occurred.</summary>
    public const string InternalError = "INTERNAL_ERROR";
}
```

### Error Response Factory

Create a factory for consistent error response generation:

```csharp
namespace CompoundDocs.McpServer.Services;

public interface IErrorResponseFactory
{
    /// <summary>
    /// Create an error response from an error code and message.
    /// </summary>
    ErrorResponse Create(string code, string message, object? details = null);

    /// <summary>
    /// Create an error response from an exception.
    /// </summary>
    ErrorResponse FromException(Exception exception);

    /// <summary>
    /// Create a validation error response with multiple field errors.
    /// </summary>
    ErrorResponse FromValidationErrors(IDictionary<string, string[]> fieldErrors);
}
```

### Exception Mapping

Create domain exceptions that map to specific error codes:

```csharp
namespace CompoundDocs.McpServer.Exceptions;

/// <summary>
/// Base exception for domain-specific errors with error code mapping.
/// </summary>
public abstract class CompoundDocsException : Exception
{
    public abstract string ErrorCode { get; }
    public virtual object? Details { get; }

    protected CompoundDocsException(string message) : base(message) { }
    protected CompoundDocsException(string message, Exception inner) : base(message, inner) { }
}

public class ProjectNotActivatedException : CompoundDocsException
{
    public override string ErrorCode => ErrorCodes.ProjectNotActivated;

    public ProjectNotActivatedException()
        : base("No project is currently activated. Call activate_project first.") { }
}

public class DocumentNotFoundException : CompoundDocsException
{
    public override string ErrorCode => ErrorCodes.DocumentNotFound;
    public string DocumentPath { get; }
    public override object? Details => new { Path = DocumentPath };

    public DocumentNotFoundException(string documentPath)
        : base($"Document not found: {documentPath}")
    {
        DocumentPath = documentPath;
    }
}

public class ExternalDocsNotConfiguredException : CompoundDocsException
{
    public override string ErrorCode => ErrorCodes.ExternalDocsNotConfigured;

    public ExternalDocsNotConfiguredException()
        : base("External docs not configured. Add 'external_docs' to project config.json.") { }
}

public class ExternalDocsNotPromotableException : CompoundDocsException
{
    public override string ErrorCode => ErrorCodes.ExternalDocsNotPromotable;

    public ExternalDocsNotPromotableException()
        : base("External documentation cannot be promoted. Only compounding docs support promotion levels.") { }
}

public class InvalidDocTypeException : CompoundDocsException
{
    public override string ErrorCode => ErrorCodes.InvalidDocType;
    public string DocType { get; }
    public IReadOnlyList<string> ValidDocTypes { get; }
    public override object? Details => new { DocType, ValidDocTypes };

    public InvalidDocTypeException(string docType, IEnumerable<string> validDocTypes)
        : base($"Invalid doc-type: {docType}")
    {
        DocType = docType;
        ValidDocTypes = validDocTypes.ToList();
    }
}

public class SchemaValidationException : CompoundDocsException
{
    public override string ErrorCode => ErrorCodes.SchemaValidationFailed;
    public IReadOnlyList<string> ValidationErrors { get; }
    public override object? Details => new { Errors = ValidationErrors };

    public SchemaValidationException(IEnumerable<string> errors)
        : base("Document frontmatter validation failed")
    {
        ValidationErrors = errors.ToList();
    }
}

public class EmbeddingServiceException : CompoundDocsException
{
    public override string ErrorCode => ErrorCodes.EmbeddingServiceError;

    public EmbeddingServiceException(string message)
        : base($"Embedding service error: {message}") { }

    public EmbeddingServiceException(string message, Exception inner)
        : base($"Embedding service error: {message}", inner) { }
}

public class DatabaseException : CompoundDocsException
{
    public override string ErrorCode => ErrorCodes.DatabaseError;

    public DatabaseException(string message)
        : base($"Database error: {message}") { }

    public DatabaseException(string message, Exception inner)
        : base($"Database error: {message}", inner) { }
}

public class FileSystemException : CompoundDocsException
{
    public override string ErrorCode => ErrorCodes.FileSystemError;
    public string? FilePath { get; }
    public override object? Details => FilePath != null ? new { Path = FilePath } : null;

    public FileSystemException(string message, string? filePath = null)
        : base($"File system error: {message}")
    {
        FilePath = filePath;
    }

    public FileSystemException(string message, Exception inner, string? filePath = null)
        : base($"File system error: {message}", inner)
    {
        FilePath = filePath;
    }
}
```

### Validation Error Aggregation

Support aggregating multiple validation errors:

```csharp
namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Details for validation errors with multiple field-level issues.
/// </summary>
public sealed record ValidationErrorDetails
{
    /// <summary>
    /// Dictionary of field names to their validation error messages.
    /// </summary>
    public required IDictionary<string, string[]> FieldErrors { get; init; }

    /// <summary>
    /// Total count of validation errors across all fields.
    /// </summary>
    public int TotalErrors => FieldErrors.Values.Sum(e => e.Length);
}
```

### Error Response Factory Implementation

```csharp
namespace CompoundDocs.McpServer.Services;

public sealed class ErrorResponseFactory : IErrorResponseFactory
{
    private readonly ILogger<ErrorResponseFactory> _logger;

    public ErrorResponseFactory(ILogger<ErrorResponseFactory> logger)
    {
        _logger = logger;
    }

    public ErrorResponse Create(string code, string message, object? details = null)
    {
        return new ErrorResponse
        {
            Code = code,
            Message = message,
            Details = details
        };
    }

    public ErrorResponse FromException(Exception exception)
    {
        return exception switch
        {
            CompoundDocsException cde => Create(cde.ErrorCode, cde.Message, cde.Details),
            OperationCanceledException => Create(
                ErrorCodes.InternalError,
                "Operation was cancelled"),
            _ => HandleUnknownException(exception)
        };
    }

    public ErrorResponse FromValidationErrors(IDictionary<string, string[]> fieldErrors)
    {
        var details = new ValidationErrorDetails { FieldErrors = fieldErrors };
        var errorCount = details.TotalErrors;
        var message = errorCount == 1
            ? "Validation failed: 1 error"
            : $"Validation failed: {errorCount} errors";

        return Create(ErrorCodes.ValidationError, message, details);
    }

    private ErrorResponse HandleUnknownException(Exception exception)
    {
        // Log the full exception for debugging
        _logger.LogError(exception, "Unhandled exception in tool execution");

        // Return sanitized response (don't leak internal details)
        return Create(
            ErrorCodes.InternalError,
            "An unexpected error occurred. Check server logs for details.");
    }
}
```

### Error Message Templates

Provide helper methods for consistent, actionable error messages:

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Standard error message templates with actionable guidance.
/// </summary>
public static class ErrorMessages
{
    public static string ProjectNotActivated()
        => "No project is currently activated. Call the activate_project tool with your project's config.json path.";

    public static string ExternalDocsNotConfigured()
        => "External documentation search requires 'external_docs' to be configured in your project's config.json. " +
           "Add an 'external_docs' property with the path to your documentation folder.";

    public static string DocumentNotFound(string path)
        => $"Document not found at path: {path}. Verify the file exists and the path is relative to the compounding docs folder.";

    public static string InvalidDocType(string docType, IEnumerable<string> validTypes)
        => $"Invalid doc-type '{docType}'. Valid doc-types are: {string.Join(", ", validTypes)}.";

    public static string SchemaValidationFailed(int errorCount)
        => $"Document frontmatter validation failed with {errorCount} error(s). See details for specific issues.";

    public static string EmbeddingServiceUnavailable()
        => "Embedding service (Ollama) is temporarily unavailable. The operation will be retried automatically. " +
           "If this persists, check that Ollama is running and accessible.";

    public static string DatabaseUnavailable()
        => "Database connection failed. The operation will be retried automatically. " +
           "If this persists, check PostgreSQL connectivity.";
}
```

### DI Registration

Register error handling services:

```csharp
// In service registration
services.AddSingleton<IErrorResponseFactory, ErrorResponseFactory>();
```

---

## File Structure

```
src/CompoundDocs.McpServer/
├── Models/
│   ├── ErrorResponse.cs
│   ├── ErrorCodes.cs
│   └── ValidationErrorDetails.cs
├── Exceptions/
│   ├── CompoundDocsException.cs
│   ├── ProjectNotActivatedException.cs
│   ├── DocumentNotFoundException.cs
│   ├── ExternalDocsNotConfiguredException.cs
│   ├── ExternalDocsNotPromotableException.cs
│   ├── InvalidDocTypeException.cs
│   ├── SchemaValidationException.cs
│   ├── EmbeddingServiceException.cs
│   ├── DatabaseException.cs
│   └── FileSystemException.cs
└── Services/
    ├── IErrorResponseFactory.cs
    ├── ErrorResponseFactory.cs
    └── ErrorMessages.cs

tests/CompoundDocs.McpServer.Tests/
└── Services/
    └── ErrorResponseFactoryTests.cs
```

---

## Dependencies

### Depends On
- Phase 026: Tool Request Handler (provides execution context for error handling)

### Blocks
- Phase 028+ (all tool implementations will use standardized error responses)
- Any phase implementing tool execution

---

## Verification Steps

After completing this phase, verify:

1. **Error response format**: Create error responses and verify JSON structure matches spec:
   ```json
   {
     "error": true,
     "code": "PROJECT_NOT_ACTIVATED",
     "message": "No project is currently activated. Call activate_project first.",
     "details": {}
   }
   ```

2. **Exception mapping**: Throw each domain exception and verify correct error code mapping

3. **Validation aggregation**: Create multiple field errors and verify aggregation:
   ```json
   {
     "error": true,
     "code": "VALIDATION_ERROR",
     "message": "Validation failed: 3 errors",
     "details": {
       "fieldErrors": {
         "query": ["Query is required"],
         "doc_types": ["Invalid doc-type: foo", "Invalid doc-type: bar"]
       },
       "totalErrors": 3
     }
   }
   ```

4. **Unknown exception handling**: Verify unknown exceptions produce sanitized responses without leaking internal details

5. **Unit tests pass**: All error response factory tests pass

---

## Notes

- Error codes use SCREAMING_SNAKE_CASE to match typical API conventions
- Error messages should be actionable - tell the user what to do, not just what went wrong
- The `Details` property is optional and type varies by error type
- Unknown exceptions should never expose stack traces or internal paths to clients
- Consider future extensibility for error localization if needed
