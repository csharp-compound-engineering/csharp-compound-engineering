# Phase 080: Tool Parameter Validation Framework

> **Status**: NOT_STARTED
> **Effort Estimate**: 8-10 hours
> **Category**: MCP Tools
> **Prerequisites**: Phase 025 (Tool Registration System), Phase 027 (Standardized Error Response Format)

---

## Spec References

This phase implements centralized parameter validation for MCP tools as defined in:

- **spec/mcp-server/tools.md** - Parameter definitions for all 9 tools (types, required/optional, ranges)
- **spec/mcp-server.md** - Error handling patterns (lines 230-247)
- **spec/mcp-server/tools.md** - Error codes including `VALIDATION_ERROR` and `SCHEMA_VALIDATION_FAILED`

---

## Objectives

1. Create a centralized parameter validation framework for consistent validation across all tools
2. Implement type validation for all parameter types (string, integer, float, boolean, arrays, enums)
3. Handle required vs optional parameter distinction with proper null checking
4. Implement range validation for numeric parameters (`top_k`, `limit`, relevance thresholds)
5. Implement path validation and sanitization to prevent path traversal attacks
6. Build validation error aggregation for multi-field validation failures
7. Create reusable validation attributes for declarative parameter validation
8. Integrate validation framework with the error response system from Phase 027

---

## Acceptance Criteria

### Validation Framework Core

- [ ] `IParameterValidator` interface defines validation contract
- [ ] `ParameterValidationResult` captures validation success/failure with error details
- [ ] `ValidationContext` provides context for validation (parameter name, tool name)
- [ ] Validation errors aggregate into single response when multiple failures occur
- [ ] Framework integrates with `IErrorResponseFactory` from Phase 027

### Type Validation

- [ ] String validation (non-empty, max length, pattern matching)
- [ ] Integer validation (min/max bounds, positive-only options)
- [ ] Float validation (min/max bounds, precision handling)
- [ ] Boolean validation (strict true/false, no string coercion)
- [ ] Array validation (element type validation, min/max count, no duplicates option)
- [ ] Enum validation (allowed values, case-insensitive option)

### Required vs Optional Handling

- [ ] Required parameters validated as non-null and non-empty
- [ ] Optional parameters skip validation when null/missing
- [ ] Default values applied before validation for optional parameters
- [ ] Clear error messages distinguish required from optional parameter failures

### Range Validation

- [ ] `max_sources` validated: 1-10 range (default: 3)
- [ ] `limit` validated: 1-100 range (default: 10)
- [ ] `min_relevance_score` validated: 0.0-1.0 range (default: 0.5-0.7 depending on tool)
- [ ] `embedding_dimensions` validated: 128-4096 range (if configurable)
- [ ] Range violations produce clear error messages with allowed bounds

### Path Validation and Sanitization

- [ ] Path traversal prevention (`../` sequences blocked)
- [ ] Absolute path detection and rejection where relative paths expected
- [ ] Path normalization (consistent separators, no double slashes)
- [ ] Allowed path patterns enforced per tool:
  - [ ] `index_document`: relative paths within `./csharp-compounding-docs/`
  - [ ] `update_promotion_level`: relative paths within `./csharp-compounding-docs/`
  - [ ] `activate_project`: absolute paths to config files allowed
- [ ] Invalid path characters detected and rejected

### Validation Attributes

- [ ] `[Required]` attribute marks required parameters
- [ ] `[Range(min, max)]` attribute for numeric bounds
- [ ] `[StringLength(min, max)]` attribute for string length
- [ ] `[AllowedValues(...)]` attribute for enum-like validation
- [ ] `[RelativePath]` attribute for relative path parameters
- [ ] `[AbsolutePath]` attribute for absolute path parameters
- [ ] `[NoPathTraversal]` attribute blocks `../` sequences
- [ ] `[ArrayLength(min, max)]` attribute for array bounds
- [ ] Custom attributes can be combined on single parameter

### Error Aggregation

- [ ] Multiple validation errors collected before returning
- [ ] Error response includes all failed validations, not just first
- [ ] Field-specific error messages mapped to parameter names
- [ ] Total error count included in response
- [ ] Validation errors use `VALIDATION_ERROR` code from Phase 027

---

## Implementation Notes

### Validation Result Model

```csharp
namespace CompoundDocs.McpServer.Validation;

/// <summary>
/// Result of parameter validation with error details.
/// </summary>
public sealed record ParameterValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<ValidationError> Errors { get; init; } = [];

    public static ParameterValidationResult Success() => new() { IsValid = true };

    public static ParameterValidationResult Failure(params ValidationError[] errors)
        => new() { IsValid = false, Errors = errors };

    public static ParameterValidationResult Failure(IEnumerable<ValidationError> errors)
        => new() { IsValid = false, Errors = errors.ToList() };
}

/// <summary>
/// Individual validation error for a parameter.
/// </summary>
public sealed record ValidationError(
    string ParameterName,
    string ErrorMessage,
    object? AttemptedValue = null);
```

### Validator Interface

```csharp
namespace CompoundDocs.McpServer.Validation;

/// <summary>
/// Validates a single parameter value.
/// </summary>
public interface IParameterValidator
{
    /// <summary>
    /// Validates the parameter value.
    /// </summary>
    ParameterValidationResult Validate(
        string parameterName,
        object? value,
        ValidationContext context);
}

/// <summary>
/// Context for parameter validation.
/// </summary>
public sealed record ValidationContext(
    string ToolName,
    Type ParameterType,
    bool IsRequired,
    object? DefaultValue = null);
```

### Validation Attributes

```csharp
namespace CompoundDocs.McpServer.Validation.Attributes;

/// <summary>
/// Marks a parameter as required (non-null and non-empty).
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class RequiredAttribute : ValidationAttribute
{
    public override ParameterValidationResult Validate(
        string parameterName, object? value, ValidationContext context)
    {
        if (value is null)
            return ParameterValidationResult.Failure(
                new ValidationError(parameterName, $"{parameterName} is required"));

        if (value is string s && string.IsNullOrWhiteSpace(s))
            return ParameterValidationResult.Failure(
                new ValidationError(parameterName, $"{parameterName} cannot be empty"));

        return ParameterValidationResult.Success();
    }
}

/// <summary>
/// Validates numeric parameters are within specified range.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class RangeAttribute : ValidationAttribute
{
    public double Minimum { get; }
    public double Maximum { get; }

    public RangeAttribute(double minimum, double maximum)
    {
        Minimum = minimum;
        Maximum = maximum;
    }

    public override ParameterValidationResult Validate(
        string parameterName, object? value, ValidationContext context)
    {
        if (value is null) return ParameterValidationResult.Success(); // Optional handling

        var numericValue = Convert.ToDouble(value);
        if (numericValue < Minimum || numericValue > Maximum)
        {
            return ParameterValidationResult.Failure(
                new ValidationError(
                    parameterName,
                    $"{parameterName} must be between {Minimum} and {Maximum}, got {numericValue}",
                    value));
        }

        return ParameterValidationResult.Success();
    }
}

/// <summary>
/// Validates string length is within bounds.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class StringLengthAttribute : ValidationAttribute
{
    public int MinimumLength { get; }
    public int MaximumLength { get; }

    public StringLengthAttribute(int minimumLength = 0, int maximumLength = int.MaxValue)
    {
        MinimumLength = minimumLength;
        MaximumLength = maximumLength;
    }

    public override ParameterValidationResult Validate(
        string parameterName, object? value, ValidationContext context)
    {
        if (value is not string s) return ParameterValidationResult.Success();

        if (s.Length < MinimumLength)
            return ParameterValidationResult.Failure(
                new ValidationError(parameterName,
                    $"{parameterName} must be at least {MinimumLength} characters"));

        if (s.Length > MaximumLength)
            return ParameterValidationResult.Failure(
                new ValidationError(parameterName,
                    $"{parameterName} must not exceed {MaximumLength} characters"));

        return ParameterValidationResult.Success();
    }
}

/// <summary>
/// Validates parameter value is one of allowed values (enum-like).
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class AllowedValuesAttribute : ValidationAttribute
{
    public IReadOnlyList<string> AllowedValues { get; }
    public bool CaseInsensitive { get; set; } = true;

    public AllowedValuesAttribute(params string[] allowedValues)
    {
        AllowedValues = allowedValues;
    }

    public override ParameterValidationResult Validate(
        string parameterName, object? value, ValidationContext context)
    {
        if (value is not string s) return ParameterValidationResult.Success();

        var comparer = CaseInsensitive
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        if (!AllowedValues.Contains(s, comparer))
        {
            return ParameterValidationResult.Failure(
                new ValidationError(
                    parameterName,
                    $"{parameterName} must be one of: {string.Join(", ", AllowedValues)}. Got: {s}",
                    value));
        }

        return ParameterValidationResult.Success();
    }
}
```

### Path Validation Attributes

```csharp
namespace CompoundDocs.McpServer.Validation.Attributes;

/// <summary>
/// Validates path does not contain traversal sequences.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class NoPathTraversalAttribute : ValidationAttribute
{
    private static readonly string[] TraversalPatterns = ["../", "..\\", "..", "%2e%2e"];

    public override ParameterValidationResult Validate(
        string parameterName, object? value, ValidationContext context)
    {
        if (value is not string path) return ParameterValidationResult.Success();

        var normalizedPath = path.ToLowerInvariant();
        foreach (var pattern in TraversalPatterns)
        {
            if (normalizedPath.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return ParameterValidationResult.Failure(
                    new ValidationError(
                        parameterName,
                        $"{parameterName} contains invalid path traversal sequence",
                        value));
            }
        }

        return ParameterValidationResult.Success();
    }
}

/// <summary>
/// Validates path is relative (not absolute).
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class RelativePathAttribute : ValidationAttribute
{
    public string? BasePath { get; set; }

    public override ParameterValidationResult Validate(
        string parameterName, object? value, ValidationContext context)
    {
        if (value is not string path) return ParameterValidationResult.Success();

        if (Path.IsPathRooted(path))
        {
            return ParameterValidationResult.Failure(
                new ValidationError(
                    parameterName,
                    $"{parameterName} must be a relative path, got absolute path",
                    value));
        }

        return ParameterValidationResult.Success();
    }
}

/// <summary>
/// Validates path is absolute (rooted).
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class AbsolutePathAttribute : ValidationAttribute
{
    public override ParameterValidationResult Validate(
        string parameterName, object? value, ValidationContext context)
    {
        if (value is not string path) return ParameterValidationResult.Success();

        if (!Path.IsPathRooted(path))
        {
            return ParameterValidationResult.Failure(
                new ValidationError(
                    parameterName,
                    $"{parameterName} must be an absolute path",
                    value));
        }

        return ParameterValidationResult.Success();
    }
}
```

### Array Validation Attribute

```csharp
namespace CompoundDocs.McpServer.Validation.Attributes;

/// <summary>
/// Validates array parameters for length and element constraints.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class ArrayLengthAttribute : ValidationAttribute
{
    public int MinLength { get; }
    public int MaxLength { get; }
    public bool NoDuplicates { get; set; } = false;

    public ArrayLengthAttribute(int minLength = 0, int maxLength = int.MaxValue)
    {
        MinLength = minLength;
        MaxLength = maxLength;
    }

    public override ParameterValidationResult Validate(
        string parameterName, object? value, ValidationContext context)
    {
        if (value is not IEnumerable enumerable) return ParameterValidationResult.Success();

        var errors = new List<ValidationError>();
        var items = enumerable.Cast<object>().ToList();

        if (items.Count < MinLength)
        {
            errors.Add(new ValidationError(
                parameterName,
                $"{parameterName} must contain at least {MinLength} items, got {items.Count}"));
        }

        if (items.Count > MaxLength)
        {
            errors.Add(new ValidationError(
                parameterName,
                $"{parameterName} must not exceed {MaxLength} items, got {items.Count}"));
        }

        if (NoDuplicates && items.Distinct().Count() != items.Count)
        {
            errors.Add(new ValidationError(
                parameterName,
                $"{parameterName} contains duplicate values"));
        }

        return errors.Count > 0
            ? ParameterValidationResult.Failure(errors)
            : ParameterValidationResult.Success();
    }
}
```

### Composite Validator

```csharp
namespace CompoundDocs.McpServer.Validation;

/// <summary>
/// Validates all parameters for a tool invocation.
/// </summary>
public interface IToolParameterValidator
{
    /// <summary>
    /// Validates all parameters and returns aggregated result.
    /// </summary>
    ParameterValidationResult ValidateAll(
        string toolName,
        IDictionary<string, object?> parameters,
        IReadOnlyDictionary<string, ParameterSchema> schemas);
}

public sealed class ToolParameterValidator : IToolParameterValidator
{
    private readonly ILogger<ToolParameterValidator> _logger;

    public ToolParameterValidator(ILogger<ToolParameterValidator> logger)
    {
        _logger = logger;
    }

    public ParameterValidationResult ValidateAll(
        string toolName,
        IDictionary<string, object?> parameters,
        IReadOnlyDictionary<string, ParameterSchema> schemas)
    {
        var allErrors = new List<ValidationError>();

        foreach (var (name, schema) in schemas)
        {
            parameters.TryGetValue(name, out var value);
            var context = new ValidationContext(toolName, schema.Type, schema.IsRequired, schema.DefaultValue);

            // Apply default if value is null and default exists
            if (value is null && schema.DefaultValue is not null)
            {
                parameters[name] = schema.DefaultValue;
                value = schema.DefaultValue;
            }

            // Validate each attribute
            foreach (var validator in schema.Validators)
            {
                var result = validator.Validate(name, value, context);
                if (!result.IsValid)
                {
                    allErrors.AddRange(result.Errors);
                }
            }
        }

        if (allErrors.Count > 0)
        {
            _logger.LogWarning("Validation failed for tool {ToolName}: {ErrorCount} errors",
                toolName, allErrors.Count);
            return ParameterValidationResult.Failure(allErrors);
        }

        return ParameterValidationResult.Success();
    }
}
```

### Tool-Specific Parameter Schemas

Define validation rules per tool based on spec:

```csharp
namespace CompoundDocs.McpServer.Validation;

/// <summary>
/// Parameter validation schemas for all MCP tools.
/// </summary>
public static class ToolParameterSchemas
{
    public static IReadOnlyDictionary<string, ParameterSchema> RagQuery => new Dictionary<string, ParameterSchema>
    {
        ["query"] = new(typeof(string), IsRequired: true, Validators: [new RequiredAttribute(), new StringLengthAttribute(1, 10000)]),
        ["doc_types"] = new(typeof(string[]), IsRequired: false, Validators: [new ArrayLengthAttribute(0, 20)]),
        ["max_sources"] = new(typeof(int), IsRequired: false, DefaultValue: 3, Validators: [new RangeAttribute(1, 10)]),
        ["min_relevance_score"] = new(typeof(float), IsRequired: false, DefaultValue: 0.7f, Validators: [new RangeAttribute(0.0, 1.0)]),
        ["min_promotion_level"] = new(typeof(string), IsRequired: false, DefaultValue: "standard",
            Validators: [new AllowedValuesAttribute("standard", "important", "critical")]),
        ["include_critical"] = new(typeof(bool), IsRequired: false, DefaultValue: true),
    };

    public static IReadOnlyDictionary<string, ParameterSchema> SemanticSearch => new Dictionary<string, ParameterSchema>
    {
        ["query"] = new(typeof(string), IsRequired: true, Validators: [new RequiredAttribute(), new StringLengthAttribute(1, 10000)]),
        ["doc_types"] = new(typeof(string[]), IsRequired: false, Validators: [new ArrayLengthAttribute(0, 20)]),
        ["limit"] = new(typeof(int), IsRequired: false, DefaultValue: 10, Validators: [new RangeAttribute(1, 100)]),
        ["min_relevance_score"] = new(typeof(float), IsRequired: false, DefaultValue: 0.5f, Validators: [new RangeAttribute(0.0, 1.0)]),
        ["promotion_levels"] = new(typeof(string[]), IsRequired: false,
            Validators: [new ArrayLengthAttribute(0, 3), new ArrayElementsAllowedAttribute("standard", "important", "critical")]),
    };

    public static IReadOnlyDictionary<string, ParameterSchema> IndexDocument => new Dictionary<string, ParameterSchema>
    {
        ["path"] = new(typeof(string), IsRequired: true,
            Validators: [new RequiredAttribute(), new RelativePathAttribute(), new NoPathTraversalAttribute()]),
    };

    public static IReadOnlyDictionary<string, ParameterSchema> DeleteDocuments => new Dictionary<string, ParameterSchema>
    {
        ["project_name"] = new(typeof(string), IsRequired: true, Validators: [new RequiredAttribute()]),
        ["branch_name"] = new(typeof(string), IsRequired: false),
        ["path_hash"] = new(typeof(string), IsRequired: false),
        ["dry_run"] = new(typeof(bool), IsRequired: false, DefaultValue: false),
    };

    public static IReadOnlyDictionary<string, ParameterSchema> UpdatePromotionLevel => new Dictionary<string, ParameterSchema>
    {
        ["document_path"] = new(typeof(string), IsRequired: true,
            Validators: [new RequiredAttribute(), new RelativePathAttribute(), new NoPathTraversalAttribute()]),
        ["promotion_level"] = new(typeof(string), IsRequired: true,
            Validators: [new RequiredAttribute(), new AllowedValuesAttribute("standard", "important", "critical")]),
    };

    public static IReadOnlyDictionary<string, ParameterSchema> ActivateProject => new Dictionary<string, ParameterSchema>
    {
        ["config_path"] = new(typeof(string), IsRequired: true,
            Validators: [new RequiredAttribute(), new AbsolutePathAttribute(), new NoPathTraversalAttribute()]),
        ["branch_name"] = new(typeof(string), IsRequired: true, Validators: [new RequiredAttribute()]),
    };

    public static IReadOnlyDictionary<string, ParameterSchema> SearchExternalDocs => new Dictionary<string, ParameterSchema>
    {
        ["query"] = new(typeof(string), IsRequired: true, Validators: [new RequiredAttribute(), new StringLengthAttribute(1, 10000)]),
        ["limit"] = new(typeof(int), IsRequired: false, DefaultValue: 10, Validators: [new RangeAttribute(1, 100)]),
        ["min_relevance_score"] = new(typeof(float), IsRequired: false, DefaultValue: 0.7f, Validators: [new RangeAttribute(0.0, 1.0)]),
    };

    public static IReadOnlyDictionary<string, ParameterSchema> RagQueryExternal => new Dictionary<string, ParameterSchema>
    {
        ["query"] = new(typeof(string), IsRequired: true, Validators: [new RequiredAttribute(), new StringLengthAttribute(1, 10000)]),
        ["max_sources"] = new(typeof(int), IsRequired: false, DefaultValue: 3, Validators: [new RangeAttribute(1, 10)]),
        ["min_relevance_score"] = new(typeof(float), IsRequired: false, DefaultValue: 0.7f, Validators: [new RangeAttribute(0.0, 1.0)]),
    };
}

/// <summary>
/// Schema definition for a tool parameter.
/// </summary>
public sealed record ParameterSchema(
    Type Type,
    bool IsRequired,
    IReadOnlyList<ValidationAttribute>? Validators = null,
    object? DefaultValue = null)
{
    public IReadOnlyList<ValidationAttribute> Validators { get; } = Validators ?? [];
}
```

### Integration with Error Response Factory

```csharp
namespace CompoundDocs.McpServer.Validation;

/// <summary>
/// Extension methods to convert validation results to error responses.
/// </summary>
public static class ValidationResultExtensions
{
    public static ErrorResponse ToErrorResponse(
        this ParameterValidationResult result,
        IErrorResponseFactory factory)
    {
        if (result.IsValid)
            throw new InvalidOperationException("Cannot convert successful validation to error response");

        var fieldErrors = result.Errors
            .GroupBy(e => e.ParameterName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        return factory.FromValidationErrors(fieldErrors);
    }
}
```

### Tool Integration Example

```csharp
[McpServerToolType]
public class RagTools
{
    private readonly IToolParameterValidator _validator;
    private readonly IErrorResponseFactory _errorFactory;

    [McpServerTool(Name = "rag_query")]
    public async Task<string> RagQuery(
        string query,
        string[]? docTypes = null,
        int maxSources = 3,
        float minRelevanceScore = 0.7f,
        string minPromotionLevel = "standard",
        bool includeCritical = true,
        CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["query"] = query,
            ["doc_types"] = docTypes,
            ["max_sources"] = maxSources,
            ["min_relevance_score"] = minRelevanceScore,
            ["min_promotion_level"] = minPromotionLevel,
            ["include_critical"] = includeCritical
        };

        var validationResult = _validator.ValidateAll(
            "rag_query",
            parameters,
            ToolParameterSchemas.RagQuery);

        if (!validationResult.IsValid)
        {
            return JsonSerializer.Serialize(validationResult.ToErrorResponse(_errorFactory));
        }

        // Continue with validated parameters...
    }
}
```

---

## File Structure

```
src/CompoundDocs.McpServer/
├── Validation/
│   ├── Attributes/
│   │   ├── ValidationAttribute.cs        # Base attribute class
│   │   ├── RequiredAttribute.cs
│   │   ├── RangeAttribute.cs
│   │   ├── StringLengthAttribute.cs
│   │   ├── AllowedValuesAttribute.cs
│   │   ├── ArrayLengthAttribute.cs
│   │   ├── ArrayElementsAllowedAttribute.cs
│   │   ├── NoPathTraversalAttribute.cs
│   │   ├── RelativePathAttribute.cs
│   │   └── AbsolutePathAttribute.cs
│   ├── IParameterValidator.cs
│   ├── ParameterValidationResult.cs
│   ├── ValidationContext.cs
│   ├── ValidationError.cs
│   ├── ParameterSchema.cs
│   ├── ToolParameterValidator.cs
│   ├── ToolParameterSchemas.cs
│   └── ValidationResultExtensions.cs

tests/CompoundDocs.McpServer.Tests/
└── Validation/
    ├── Attributes/
    │   ├── RequiredAttributeTests.cs
    │   ├── RangeAttributeTests.cs
    │   ├── StringLengthAttributeTests.cs
    │   ├── AllowedValuesAttributeTests.cs
    │   ├── ArrayLengthAttributeTests.cs
    │   ├── PathValidationAttributeTests.cs
    │   └── NoPathTraversalAttributeTests.cs
    ├── ToolParameterValidatorTests.cs
    └── ToolParameterSchemasTests.cs
```

---

## Dependencies

### Depends On
- Phase 025: Tool Registration System (tool structure and parameter definitions)
- Phase 027: Standardized Error Response Format (`IErrorResponseFactory`, `ErrorCodes`)

### Blocks
- All tool implementation phases (tools will use validation framework)
- Phase 081+: Individual tool implementations

---

## Verification Steps

After completing this phase, verify:

1. **Type validation**: Test each type validator with valid and invalid inputs
   ```csharp
   // String validation
   var result = new RequiredAttribute().Validate("query", null, context);
   Assert.False(result.IsValid);
   Assert.Contains("required", result.Errors[0].ErrorMessage);
   ```

2. **Range validation**: Test numeric bounds
   ```csharp
   // max_sources must be 1-10
   var result = new RangeAttribute(1, 10).Validate("max_sources", 15, context);
   Assert.False(result.IsValid);
   Assert.Contains("between 1 and 10", result.Errors[0].ErrorMessage);
   ```

3. **Path validation**: Test path traversal prevention
   ```csharp
   var result = new NoPathTraversalAttribute().Validate("path", "../../../etc/passwd", context);
   Assert.False(result.IsValid);
   Assert.Contains("traversal", result.Errors[0].ErrorMessage);
   ```

4. **Error aggregation**: Test multiple validation failures aggregate
   ```csharp
   var result = validator.ValidateAll("rag_query", new Dictionary<string, object?>
   {
       ["query"] = "",           // Required + empty
       ["max_sources"] = 100,    // Out of range
       ["min_relevance_score"] = 2.0f  // Out of range
   }, ToolParameterSchemas.RagQuery);

   Assert.False(result.IsValid);
   Assert.True(result.Errors.Count >= 3);
   ```

5. **Error response integration**: Verify validation errors produce correct JSON
   ```json
   {
     "error": true,
     "code": "VALIDATION_ERROR",
     "message": "Validation failed: 3 errors",
     "details": {
       "fieldErrors": {
         "query": ["query is required"],
         "max_sources": ["max_sources must be between 1 and 10, got 100"],
         "min_relevance_score": ["min_relevance_score must be between 0 and 1, got 2"]
       },
       "totalErrors": 3
     }
   }
   ```

6. **All tool schemas defined**: Verify `ToolParameterSchemas` covers all 9 tools

---

## Notes

- Validation attributes follow a similar pattern to ASP.NET Core DataAnnotations but are tool-specific
- The framework validates before tool logic executes, providing fast-fail behavior
- Path sanitization is critical for security - block all traversal attempts
- Float comparisons use tolerance-aware comparison for relevance scores
- Array element validation (e.g., valid doc_types) may require runtime schema lookup
- Consider caching compiled validator chains for performance in hot paths
- Default values are applied during validation, not in tool method signatures, for consistency
