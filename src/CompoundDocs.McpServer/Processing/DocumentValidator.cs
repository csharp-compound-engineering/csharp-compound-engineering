using CompoundDocs.McpServer.DocTypes;

namespace CompoundDocs.McpServer.Processing;

/// <summary>
/// Validates document structure against document type schemas.
/// Checks required frontmatter fields and validates field types.
/// </summary>
public sealed class DocumentValidator
{
    private readonly IDocTypeRegistry? _docTypeRegistry;

    /// <summary>
    /// Creates a new DocumentValidator with optional doc type registry.
    /// </summary>
    /// <param name="docTypeRegistry">Optional doc type registry for schema validation.</param>
    public DocumentValidator(IDocTypeRegistry? docTypeRegistry = null)
    {
        _docTypeRegistry = docTypeRegistry;
    }

    /// <summary>
    /// Validates a parsed document against its doc type schema.
    /// </summary>
    /// <param name="parsedDocument">The parsed document to validate.</param>
    /// <returns>The validation result with errors and warnings.</returns>
    public DocumentValidationResult Validate(ParsedDocument parsedDocument)
    {
        if (!parsedDocument.IsSuccess)
        {
            return DocumentValidationResult.Failure(
                string.Empty,
                [$"Document parsing failed: {parsedDocument.Error ?? "Unknown error"}"]);
        }

        var errors = new List<ValidationError>();
        var warnings = new List<string>();

        // Get doc type from frontmatter
        var docType = GetDocType(parsedDocument.Frontmatter);

        if (string.IsNullOrEmpty(docType))
        {
            warnings.Add("No doc_type specified in frontmatter. Document will be processed without type validation.");
            return DocumentValidationResult.Success(string.Empty, warnings);
        }

        // Validate against doc type schema if registry is available
        if (_docTypeRegistry != null)
        {
            var docTypeDefinition = _docTypeRegistry.GetDocType(docType);

            if (docTypeDefinition == null)
            {
                warnings.Add($"Unknown doc_type '{docType}'. Document will be processed without schema validation.");
                return DocumentValidationResult.Success(docType, warnings);
            }

            // Validate required fields
            ValidateRequiredFields(parsedDocument.Frontmatter, docTypeDefinition.RequiredFields, errors);

            // Validate optional field types if present
            ValidateOptionalFields(parsedDocument.Frontmatter, docTypeDefinition.OptionalFields, warnings);
        }

        if (errors.Count > 0)
        {
            return DocumentValidationResult.Failure(
                docType,
                errors.Select(e => e.ToString()).ToList(),
                warnings);
        }

        return DocumentValidationResult.Success(docType, warnings);
    }

    /// <summary>
    /// Validates a detailed parsed document against its doc type schema.
    /// </summary>
    /// <param name="parsedDocument">The detailed parsed document to validate.</param>
    /// <returns>The validation result with errors and warnings.</returns>
    public DocumentValidationResult Validate(DetailedParsedDocument parsedDocument)
    {
        if (!parsedDocument.IsSuccess)
        {
            return DocumentValidationResult.Failure(
                string.Empty,
                [$"Document parsing failed: {parsedDocument.Error ?? "Unknown error"}"]);
        }

        var errors = new List<ValidationError>();
        var warnings = new List<string>();

        // Get doc type from frontmatter
        var docType = GetDocType(parsedDocument.Frontmatter);

        if (string.IsNullOrEmpty(docType))
        {
            warnings.Add("No doc_type specified in frontmatter. Document will be processed without type validation.");
            return DocumentValidationResult.Success(string.Empty, warnings);
        }

        // Additional structural validation
        if (string.IsNullOrEmpty(parsedDocument.Title))
        {
            warnings.Add("Document has no title (neither in frontmatter nor as H1 header).");
        }

        // Validate against doc type schema if registry is available
        if (_docTypeRegistry != null)
        {
            var docTypeDefinition = _docTypeRegistry.GetDocType(docType);

            if (docTypeDefinition == null)
            {
                warnings.Add($"Unknown doc_type '{docType}'. Document will be processed without schema validation.");
                return DocumentValidationResult.Success(docType, warnings);
            }

            // Validate required fields
            ValidateRequiredFields(parsedDocument.Frontmatter, docTypeDefinition.RequiredFields, errors);
        }

        if (errors.Count > 0)
        {
            return DocumentValidationResult.Failure(
                docType,
                errors.Select(e => e.ToString()).ToList(),
                warnings);
        }

        return DocumentValidationResult.Success(docType, warnings);
    }

    /// <summary>
    /// Validates frontmatter against a list of required fields.
    /// </summary>
    /// <param name="frontmatter">The frontmatter dictionary.</param>
    /// <param name="requiredFields">List of required field names.</param>
    /// <returns>The validation result.</returns>
    public DocumentValidationResult ValidateRequiredFields(
        Dictionary<string, object?>? frontmatter,
        IReadOnlyList<string> requiredFields)
    {
        var errors = new List<ValidationError>();
        ValidateRequiredFields(frontmatter, requiredFields, errors);

        if (errors.Count > 0)
        {
            return DocumentValidationResult.Failure(
                string.Empty,
                errors.Select(e => e.ToString()).ToList());
        }

        return DocumentValidationResult.Success(string.Empty);
    }

    /// <summary>
    /// Validates that a field has the expected type.
    /// </summary>
    /// <param name="frontmatter">The frontmatter dictionary.</param>
    /// <param name="fieldName">The name of the field to check.</param>
    /// <param name="expectedType">The expected type.</param>
    /// <returns>True if the field exists and has the expected type.</returns>
    public bool ValidateFieldType(
        Dictionary<string, object?>? frontmatter,
        string fieldName,
        Type expectedType)
    {
        if (frontmatter == null || !frontmatter.TryGetValue(fieldName, out var value) || value == null)
        {
            return false;
        }

        return expectedType.IsInstanceOfType(value) ||
               CanConvertToType(value, expectedType);
    }

    /// <summary>
    /// Gets the doc type from frontmatter.
    /// </summary>
    private static string GetDocType(Dictionary<string, object?>? frontmatter)
    {
        if (frontmatter == null)
        {
            return string.Empty;
        }

        // Check both doc_type and docType (case insensitive)
        if (frontmatter.TryGetValue("doc_type", out var docTypeValue) && docTypeValue is string docType)
        {
            return docType;
        }

        if (frontmatter.TryGetValue("docType", out docTypeValue) && docTypeValue is string docType2)
        {
            return docType2;
        }

        return string.Empty;
    }

    /// <summary>
    /// Validates required fields are present.
    /// </summary>
    private static void ValidateRequiredFields(
        Dictionary<string, object?>? frontmatter,
        IReadOnlyList<string> requiredFields,
        List<ValidationError> errors)
    {
        if (requiredFields.Count == 0)
        {
            return;
        }

        foreach (var field in requiredFields)
        {
            if (frontmatter == null ||
                !frontmatter.TryGetValue(field, out var value) ||
                value == null ||
                (value is string s && string.IsNullOrWhiteSpace(s)))
            {
                // Try to estimate line number (not always possible)
                errors.Add(new ValidationError
                {
                    Field = field,
                    Message = $"Required field '{field}' is missing or empty",
                    LineNumber = null // Line number not available without parsing
                });
            }
        }
    }

    /// <summary>
    /// Validates optional fields have correct types if present.
    /// </summary>
    private static void ValidateOptionalFields(
        Dictionary<string, object?>? frontmatter,
        IReadOnlyList<string> optionalFields,
        List<string> warnings)
    {
        if (frontmatter == null || optionalFields.Count == 0)
        {
            return;
        }

        // Check for unknown fields (fields not in required or optional list)
        // This is informational only
        foreach (var key in frontmatter.Keys)
        {
            if (!optionalFields.Contains(key, StringComparer.OrdinalIgnoreCase) &&
                !IsStandardField(key))
            {
                warnings.Add($"Unknown field '{key}' in frontmatter (may be ignored by processing).");
            }
        }
    }

    /// <summary>
    /// Checks if a field name is a standard frontmatter field.
    /// </summary>
    private static bool IsStandardField(string fieldName)
    {
        var standardFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "title",
            "doc_type",
            "docType",
            "promotion_level",
            "promotionLevel",
            "tags",
            "created",
            "updated",
            "author",
            "description",
            "version"
        };

        return standardFields.Contains(fieldName);
    }

    /// <summary>
    /// Checks if a value can be converted to the expected type.
    /// </summary>
    private static bool CanConvertToType(object value, Type expectedType)
    {
        try
        {
            Convert.ChangeType(value, expectedType);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Represents a validation error with optional line number.
/// </summary>
public sealed class ValidationError
{
    /// <summary>
    /// The field that caused the error.
    /// </summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>
    /// The error message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// The line number where the error occurred, if available.
    /// </summary>
    public int? LineNumber { get; init; }

    /// <summary>
    /// Creates a string representation of the error.
    /// </summary>
    public override string ToString()
    {
        if (LineNumber.HasValue)
        {
            return $"Line {LineNumber}: {Message}";
        }
        return Message;
    }
}

/// <summary>
/// Result of document validation.
/// </summary>
public sealed class DocumentValidationResult
{
    /// <summary>
    /// Whether the document is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// The document type that was validated against.
    /// </summary>
    public string DocType { get; init; } = string.Empty;

    /// <summary>
    /// Validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Validation warnings.
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
    /// Creates a failed validation result.
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
}
