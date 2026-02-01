using System.Text.Json;
using Microsoft.Extensions.Logging;
using NJsonSchema;

namespace CompoundDocs.McpServer.DocTypes;

/// <summary>
/// Validates frontmatter against document type schemas using NJsonSchema.
/// </summary>
public sealed class DocTypeValidator
{
    private readonly ILogger<DocTypeValidator> _logger;
    private readonly Dictionary<string, JsonSchema> _schemaCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _cacheLock = new();

    /// <summary>
    /// Creates a new instance of DocTypeValidator.
    /// </summary>
    public DocTypeValidator(ILogger<DocTypeValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates frontmatter against a JSON schema.
    /// </summary>
    /// <param name="docTypeId">The document type identifier for error reporting.</param>
    /// <param name="schemaJson">The JSON schema to validate against.</param>
    /// <param name="frontmatter">The frontmatter dictionary to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    public async Task<DocTypeValidationResult> ValidateAsync(
        string docTypeId,
        string schemaJson,
        IDictionary<string, object?> frontmatter,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(docTypeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaJson);
        ArgumentNullException.ThrowIfNull(frontmatter);

        try
        {
            var schema = await GetOrParseSchemaAsync(docTypeId, schemaJson, cancellationToken);
            var json = JsonSerializer.Serialize(frontmatter);
            var errors = schema.Validate(json);

            if (errors.Count == 0)
            {
                _logger.LogDebug("Frontmatter validation passed for doc-type '{DocTypeId}'", docTypeId);
                return DocTypeValidationResult.Success(docTypeId);
            }

            var validationErrors = errors.Select(e => new ValidationError
            {
                PropertyPath = e.Path ?? "root",
                Message = e.Kind.ToString(),
                ErrorType = MapErrorKind(e.Kind),
                Expected = e.Schema?.ToString(),
                Actual = e.Property
            }).ToList();

            _logger.LogDebug(
                "Frontmatter validation failed for doc-type '{DocTypeId}' with {ErrorCount} errors",
                docTypeId, validationErrors.Count);

            return DocTypeValidationResult.Failure(docTypeId, validationErrors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating frontmatter for doc-type '{DocTypeId}'", docTypeId);

            return DocTypeValidationResult.Failure(docTypeId, [new ValidationError
            {
                PropertyPath = "root",
                Message = $"Schema validation error: {ex.Message}",
                ErrorType = ValidationErrorType.Schema
            }]);
        }
    }

    /// <summary>
    /// Validates that required fields are present in the frontmatter.
    /// </summary>
    /// <param name="docTypeId">The document type identifier.</param>
    /// <param name="frontmatter">The frontmatter dictionary.</param>
    /// <param name="requiredFields">The list of required field names.</param>
    /// <returns>A list of validation errors for missing required fields.</returns>
    public IReadOnlyList<ValidationError> ValidateRequiredFields(
        string docTypeId,
        IDictionary<string, object?> frontmatter,
        IReadOnlyList<string> requiredFields)
    {
        ArgumentNullException.ThrowIfNull(frontmatter);
        ArgumentNullException.ThrowIfNull(requiredFields);

        var errors = new List<ValidationError>();

        foreach (var field in requiredFields)
        {
            if (!frontmatter.TryGetValue(field, out var value) || value is null)
            {
                errors.Add(new ValidationError
                {
                    PropertyPath = field,
                    Message = $"Required field '{field}' is missing.",
                    ErrorType = ValidationErrorType.RequiredField
                });
            }
            else if (value is string str && string.IsNullOrWhiteSpace(str))
            {
                errors.Add(new ValidationError
                {
                    PropertyPath = field,
                    Message = $"Required field '{field}' cannot be empty.",
                    ErrorType = ValidationErrorType.RequiredField
                });
            }
        }

        return errors;
    }

    /// <summary>
    /// Clears the schema cache.
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _schemaCache.Clear();
        }
        _logger.LogDebug("Schema cache cleared");
    }

    private async Task<JsonSchema> GetOrParseSchemaAsync(
        string docTypeId,
        string schemaJson,
        CancellationToken cancellationToken)
    {
        lock (_cacheLock)
        {
            if (_schemaCache.TryGetValue(docTypeId, out var cached))
            {
                return cached;
            }
        }

        var schema = await JsonSchema.FromJsonAsync(schemaJson, cancellationToken);

        lock (_cacheLock)
        {
            _schemaCache[docTypeId] = schema;
        }

        return schema;
    }

    private static ValidationErrorType MapErrorKind(NJsonSchema.Validation.ValidationErrorKind kind)
    {
        return kind switch
        {
            NJsonSchema.Validation.ValidationErrorKind.PropertyRequired => ValidationErrorType.RequiredField,
            NJsonSchema.Validation.ValidationErrorKind.StringExpected
                or NJsonSchema.Validation.ValidationErrorKind.BooleanExpected
                or NJsonSchema.Validation.ValidationErrorKind.IntegerExpected
                or NJsonSchema.Validation.ValidationErrorKind.NumberExpected
                or NJsonSchema.Validation.ValidationErrorKind.ArrayExpected
                or NJsonSchema.Validation.ValidationErrorKind.ObjectExpected => ValidationErrorType.InvalidType,
            NJsonSchema.Validation.ValidationErrorKind.NotInEnumeration => ValidationErrorType.InvalidEnum,
            NJsonSchema.Validation.ValidationErrorKind.PatternMismatch => ValidationErrorType.PatternMismatch,
            NJsonSchema.Validation.ValidationErrorKind.NumberTooBig
                or NJsonSchema.Validation.ValidationErrorKind.NumberTooSmall
                or NJsonSchema.Validation.ValidationErrorKind.StringTooShort
                or NJsonSchema.Validation.ValidationErrorKind.StringTooLong
                or NJsonSchema.Validation.ValidationErrorKind.TooManyItems
                or NJsonSchema.Validation.ValidationErrorKind.TooFewItems => ValidationErrorType.OutOfRange,
            _ => ValidationErrorType.Schema
        };
    }
}
