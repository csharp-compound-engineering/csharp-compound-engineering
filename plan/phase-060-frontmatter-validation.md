# Phase 060: Frontmatter Schema Validation

> **Status**: NOT_STARTED
> **Effort Estimate**: 6-8 hours
> **Category**: Document Processing
> **Prerequisites**: Phase 059 (Frontmatter Extraction), Phase 014 (Schema Validation Library Integration)

---

## Spec References

This phase implements frontmatter validation against doc-type schemas as defined in:

- **spec/doc-types.md** - [Common Fields](../spec/doc-types.md#common-fields) and validation requirements
- **spec/doc-types/built-in-types.md** - [Built-in Doc-Types](../spec/doc-types/built-in-types.md) - Complete schemas for problem, insight, codebase, tool, style
- **spec/doc-types/custom-types.md** - [Custom Types](../spec/doc-types/custom-types.md#validation-rules) - Field types, enum validation, validation errors
- **spec/configuration/schema-files.md** - [Schema File Specifications](../spec/configuration/schema-files.md) - JSON Schema Draft 2020-12 format
- **research/json-schema-validation-libraries-research.md** - JsonSchema.Net implementation patterns

---

## Objectives

1. Create a frontmatter validation service that validates extracted YAML frontmatter against doc-type schemas
2. Implement schema loading for both built-in and custom doc-types
3. Validate all common required fields (doc_type, title, date, summary, significance)
4. Validate doc-type-specific required and optional fields
5. Provide detailed, user-friendly validation error reporting
6. Support validation of custom doc-type schemas

---

## Acceptance Criteria

### Schema Loading for Doc-Type Validation
- [ ] `IDocTypeSchemaResolver` interface defined with methods:
  - [ ] `ResolveSchemaAsync(string docType)` - Get schema for doc-type by name
  - [ ] `GetBuiltInSchemaAsync(string docType)` - Get embedded built-in schema
  - [ ] `GetCustomSchemaAsync(string docType, string projectRoot)` - Get custom schema from project
- [ ] Built-in schemas loaded from embedded assembly resources
- [ ] Custom schemas loaded from `./csharp-compounding-docs/schemas/{doc-type}.schema.yaml`
- [ ] Schema format auto-detection based on file extension (`.yaml`, `.yml`, `.json`)
- [ ] Graceful fallback when custom schema not found (use built-in if available)

### Required Field Validation
- [ ] Common required fields validated for all doc-types:
  - [ ] `doc_type` - string, must match a valid doc-type identifier
  - [ ] `title` - string, non-empty
  - [ ] `date` - string, matches YYYY-MM-DD pattern
  - [ ] `summary` - string, non-empty (one-line summary for search results)
  - [ ] `significance` - enum: `architectural`, `behavioral`, `performance`, `correctness`, `convention`, `integration`
- [ ] Doc-type-specific required fields validated per schema
- [ ] Missing required fields reported with clear error messages
- [ ] Field presence check distinguishes between missing and null values

### Enum Value Validation
- [ ] Enum fields validated against allowed values from schema
- [ ] Case-sensitive matching by default (configurable)
- [ ] Invalid enum values reported with list of allowed options
- [ ] Support for enum fields in both common and doc-type-specific fields
- [ ] Validation of nested enum fields in complex structures

### Type Checking
- [ ] String type validation - value must be string or convertible
- [ ] Array type validation - value must be array/list of strings
- [ ] Boolean type validation - value must be true/false
- [ ] Date pattern validation - string must match `^\d{4}-\d{2}-\d{2}$`
- [ ] Type mismatch errors include expected type and actual type

### Validation Error Reporting
- [ ] `FrontmatterValidationResult` record containing:
  - [ ] `IsValid` - Boolean validation status
  - [ ] `DocType` - Identified doc-type (null if invalid/missing)
  - [ ] `Errors` - Collection of `FrontmatterValidationError` records
  - [ ] `Warnings` - Collection of non-fatal issues (e.g., deprecated fields)
- [ ] `FrontmatterValidationError` record containing:
  - [ ] `FieldPath` - Path to the problematic field (e.g., `symptoms[0]`, `problem_type`)
  - [ ] `ErrorType` - Category: `MissingRequired`, `InvalidType`, `InvalidEnum`, `PatternMismatch`, `UnknownField`
  - [ ] `Message` - Human-readable error description
  - [ ] `ExpectedValue` - What was expected (for type/enum errors)
  - [ ] `ActualValue` - What was provided (for debugging)
- [ ] Error messages suitable for display in MCP tool responses
- [ ] Aggregated errors (all issues reported, not just first failure)

### Custom Doc-Type Schema Validation
- [ ] Custom schemas validated against meta-schema before use
- [ ] Custom schema structure requirements enforced:
  - [ ] `name` - Required, must match doc-type identifier
  - [ ] `description` - Required, human-readable description
  - [ ] `required_fields` - Optional array of field definitions
  - [ ] `optional_fields` - Optional array of field definitions
- [ ] Field definition structure validated:
  - [ ] `name` - Required, field identifier
  - [ ] `type` - Required, one of: `string`, `enum`, `array`, `boolean`
  - [ ] `values` - Required for enum type, array of allowed values
  - [ ] `description` - Optional, field documentation

---

## Implementation Notes

### Interface Definitions

Create interfaces in `CompoundDocs.Common/Validation/`:

```csharp
// IDocTypeSchemaResolver.cs
public interface IDocTypeSchemaResolver
{
    Task<JsonSchema?> ResolveSchemaAsync(string docType, string? projectRoot = null, CancellationToken ct = default);
    Task<JsonSchema?> GetBuiltInSchemaAsync(string docType, CancellationToken ct = default);
    Task<JsonSchema?> GetCustomSchemaAsync(string docType, string projectRoot, CancellationToken ct = default);
    IReadOnlyList<string> GetBuiltInDocTypes();
}

// IFrontmatterValidator.cs
public interface IFrontmatterValidator
{
    Task<FrontmatterValidationResult> ValidateAsync(
        Dictionary<string, object> frontmatter,
        string? projectRoot = null,
        CancellationToken ct = default);

    Task<FrontmatterValidationResult> ValidateAsync(
        Dictionary<string, object> frontmatter,
        string docType,
        string? projectRoot = null,
        CancellationToken ct = default);
}
```

### Validation Result Types

```csharp
// FrontmatterValidationResult.cs
public record FrontmatterValidationResult
{
    public bool IsValid { get; init; }
    public string? DocType { get; init; }
    public IReadOnlyList<FrontmatterValidationError> Errors { get; init; } = [];
    public IReadOnlyList<FrontmatterValidationWarning> Warnings { get; init; } = [];

    public static FrontmatterValidationResult Success(string docType) =>
        new() { IsValid = true, DocType = docType };

    public static FrontmatterValidationResult Failure(IEnumerable<FrontmatterValidationError> errors) =>
        new() { IsValid = false, Errors = errors.ToList() };
}

// FrontmatterValidationError.cs
public record FrontmatterValidationError
{
    public required string FieldPath { get; init; }
    public required FrontmatterErrorType ErrorType { get; init; }
    public required string Message { get; init; }
    public string? ExpectedValue { get; init; }
    public string? ActualValue { get; init; }
}

public enum FrontmatterErrorType
{
    MissingRequired,
    InvalidType,
    InvalidEnum,
    PatternMismatch,
    UnknownDocType,
    SchemaNotFound,
    ParseError
}

// FrontmatterValidationWarning.cs
public record FrontmatterValidationWarning
{
    public required string FieldPath { get; init; }
    public required string Message { get; init; }
}
```

### Doc-Type Schema Resolver

```csharp
using System.Reflection;
using System.Text.Json.Nodes;
using Json.Schema;

public class DocTypeSchemaResolver : IDocTypeSchemaResolver
{
    private static readonly string[] BuiltInDocTypes = ["problem", "insight", "codebase", "tool", "style"];

    private readonly ISchemaLoader _schemaLoader;
    private readonly ISchemaCache _schemaCache;

    public DocTypeSchemaResolver(ISchemaLoader schemaLoader, ISchemaCache schemaCache)
    {
        _schemaLoader = schemaLoader;
        _schemaCache = schemaCache;
    }

    public async Task<JsonSchema?> ResolveSchemaAsync(
        string docType,
        string? projectRoot = null,
        CancellationToken ct = default)
    {
        // Try custom schema first if project root provided
        if (!string.IsNullOrEmpty(projectRoot))
        {
            var customSchema = await GetCustomSchemaAsync(docType, projectRoot, ct);
            if (customSchema is not null)
                return customSchema;
        }

        // Fall back to built-in schema
        return await GetBuiltInSchemaAsync(docType, ct);
    }

    public async Task<JsonSchema?> GetBuiltInSchemaAsync(string docType, CancellationToken ct = default)
    {
        if (!BuiltInDocTypes.Contains(docType, StringComparer.OrdinalIgnoreCase))
            return null;

        var resourceName = $"CompoundDocs.Common.Schemas.{docType}.schema.yaml";
        var assembly = Assembly.GetExecutingAssembly();

        await using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return null;

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(ct);

        return _schemaLoader.LoadSchemaFromString(content, SchemaFormat.Yaml);
    }

    public async Task<JsonSchema?> GetCustomSchemaAsync(
        string docType,
        string projectRoot,
        CancellationToken ct = default)
    {
        var schemaDir = Path.Combine(projectRoot, "csharp-compounding-docs", "schemas");

        // Try YAML first, then JSON
        var yamlPath = Path.Combine(schemaDir, $"{docType}.schema.yaml");
        var ymlPath = Path.Combine(schemaDir, $"{docType}.schema.yml");
        var jsonPath = Path.Combine(schemaDir, $"{docType}.schema.json");

        string? schemaPath = null;
        if (File.Exists(yamlPath)) schemaPath = yamlPath;
        else if (File.Exists(ymlPath)) schemaPath = ymlPath;
        else if (File.Exists(jsonPath)) schemaPath = jsonPath;

        if (schemaPath is null)
            return null;

        return await _schemaCache.GetOrAddAsync(
            schemaPath,
            () => _schemaLoader.LoadSchemaAsync(schemaPath, ct),
            ct);
    }

    public IReadOnlyList<string> GetBuiltInDocTypes() => BuiltInDocTypes;
}
```

### Frontmatter Validator Implementation

```csharp
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Json.Schema;
using Yaml2JsonNode;

public partial class FrontmatterValidator : IFrontmatterValidator
{
    private static readonly string[] CommonRequiredFields = ["doc_type", "title", "date", "summary", "significance"];
    private static readonly string[] SignificanceValues = ["architectural", "behavioral", "performance", "correctness", "convention", "integration"];

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex DatePatternRegex();

    private readonly IDocTypeSchemaResolver _schemaResolver;
    private readonly ISchemaValidator _schemaValidator;

    public FrontmatterValidator(
        IDocTypeSchemaResolver schemaResolver,
        ISchemaValidator schemaValidator)
    {
        _schemaResolver = schemaResolver;
        _schemaValidator = schemaValidator;
    }

    public async Task<FrontmatterValidationResult> ValidateAsync(
        Dictionary<string, object> frontmatter,
        string? projectRoot = null,
        CancellationToken ct = default)
    {
        var errors = new List<FrontmatterValidationError>();
        var warnings = new List<FrontmatterValidationWarning>();

        // Step 1: Validate common required fields
        ValidateCommonRequiredFields(frontmatter, errors);

        if (errors.Count > 0)
        {
            // Can't proceed without doc_type
            if (errors.Any(e => e.FieldPath == "doc_type"))
            {
                return FrontmatterValidationResult.Failure(errors);
            }
        }

        // Step 2: Extract doc_type and validate against schema
        var docType = frontmatter["doc_type"]?.ToString();
        if (string.IsNullOrEmpty(docType))
        {
            errors.Add(new FrontmatterValidationError
            {
                FieldPath = "doc_type",
                ErrorType = FrontmatterErrorType.InvalidType,
                Message = "doc_type must be a non-empty string",
                ExpectedValue = "string",
                ActualValue = frontmatter["doc_type"]?.GetType().Name ?? "null"
            });
            return FrontmatterValidationResult.Failure(errors);
        }

        return await ValidateAsync(frontmatter, docType, projectRoot, ct);
    }

    public async Task<FrontmatterValidationResult> ValidateAsync(
        Dictionary<string, object> frontmatter,
        string docType,
        string? projectRoot = null,
        CancellationToken ct = default)
    {
        var errors = new List<FrontmatterValidationError>();
        var warnings = new List<FrontmatterValidationWarning>();

        // Step 1: Validate common required fields
        ValidateCommonRequiredFields(frontmatter, errors);

        // Step 2: Resolve schema for doc-type
        var schema = await _schemaResolver.ResolveSchemaAsync(docType, projectRoot, ct);
        if (schema is null)
        {
            errors.Add(new FrontmatterValidationError
            {
                FieldPath = "doc_type",
                ErrorType = FrontmatterErrorType.UnknownDocType,
                Message = $"Unknown doc-type: '{docType}'. No schema found.",
                ExpectedValue = string.Join(", ", _schemaResolver.GetBuiltInDocTypes()),
                ActualValue = docType
            });
            return new FrontmatterValidationResult
            {
                IsValid = false,
                DocType = docType,
                Errors = errors,
                Warnings = warnings
            };
        }

        // Step 3: Convert frontmatter to JsonNode and validate against schema
        var jsonNode = ConvertToJsonNode(frontmatter);
        var schemaResult = _schemaValidator.Validate(jsonNode, schema);

        if (!schemaResult.IsValid)
        {
            foreach (var error in schemaResult.Errors)
            {
                errors.Add(new FrontmatterValidationError
                {
                    FieldPath = error.Path.TrimStart('/').Replace("/", "."),
                    ErrorType = MapKeywordToErrorType(error.Keyword),
                    Message = error.Message,
                    SchemaLocation = error.SchemaLocation
                });
            }
        }

        return new FrontmatterValidationResult
        {
            IsValid = errors.Count == 0,
            DocType = docType,
            Errors = errors,
            Warnings = warnings
        };
    }

    private void ValidateCommonRequiredFields(
        Dictionary<string, object> frontmatter,
        List<FrontmatterValidationError> errors)
    {
        // Check doc_type
        if (!frontmatter.ContainsKey("doc_type") || frontmatter["doc_type"] is null)
        {
            errors.Add(new FrontmatterValidationError
            {
                FieldPath = "doc_type",
                ErrorType = FrontmatterErrorType.MissingRequired,
                Message = "Missing required field: doc_type"
            });
        }

        // Check title
        if (!frontmatter.ContainsKey("title") || string.IsNullOrWhiteSpace(frontmatter["title"]?.ToString()))
        {
            errors.Add(new FrontmatterValidationError
            {
                FieldPath = "title",
                ErrorType = FrontmatterErrorType.MissingRequired,
                Message = "Missing required field: title"
            });
        }

        // Check date with pattern validation
        if (!frontmatter.ContainsKey("date") || frontmatter["date"] is null)
        {
            errors.Add(new FrontmatterValidationError
            {
                FieldPath = "date",
                ErrorType = FrontmatterErrorType.MissingRequired,
                Message = "Missing required field: date"
            });
        }
        else
        {
            var dateValue = frontmatter["date"]?.ToString() ?? "";
            if (!DatePatternRegex().IsMatch(dateValue))
            {
                errors.Add(new FrontmatterValidationError
                {
                    FieldPath = "date",
                    ErrorType = FrontmatterErrorType.PatternMismatch,
                    Message = "date must be in YYYY-MM-DD format",
                    ExpectedValue = "YYYY-MM-DD",
                    ActualValue = dateValue
                });
            }
        }

        // Check summary
        if (!frontmatter.ContainsKey("summary") || string.IsNullOrWhiteSpace(frontmatter["summary"]?.ToString()))
        {
            errors.Add(new FrontmatterValidationError
            {
                FieldPath = "summary",
                ErrorType = FrontmatterErrorType.MissingRequired,
                Message = "Missing required field: summary"
            });
        }

        // Check significance enum
        if (!frontmatter.ContainsKey("significance") || frontmatter["significance"] is null)
        {
            errors.Add(new FrontmatterValidationError
            {
                FieldPath = "significance",
                ErrorType = FrontmatterErrorType.MissingRequired,
                Message = "Missing required field: significance"
            });
        }
        else
        {
            var significanceValue = frontmatter["significance"]?.ToString() ?? "";
            if (!SignificanceValues.Contains(significanceValue, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add(new FrontmatterValidationError
                {
                    FieldPath = "significance",
                    ErrorType = FrontmatterErrorType.InvalidEnum,
                    Message = $"Invalid significance value: '{significanceValue}'",
                    ExpectedValue = string.Join(", ", SignificanceValues),
                    ActualValue = significanceValue
                });
            }
        }
    }

    private static JsonNode ConvertToJsonNode(Dictionary<string, object> frontmatter)
    {
        var jsonObject = new JsonObject();

        foreach (var (key, value) in frontmatter)
        {
            jsonObject[key] = ConvertValue(value);
        }

        return jsonObject;
    }

    private static JsonNode? ConvertValue(object? value)
    {
        return value switch
        {
            null => null,
            string s => JsonValue.Create(s),
            bool b => JsonValue.Create(b),
            int i => JsonValue.Create(i),
            long l => JsonValue.Create(l),
            double d => JsonValue.Create(d),
            IEnumerable<object> list => new JsonArray(list.Select(ConvertValue).ToArray()),
            IDictionary<string, object> dict => ConvertToJsonNode(
                dict.ToDictionary(kv => kv.Key, kv => kv.Value)),
            _ => JsonValue.Create(value.ToString())
        };
    }

    private static FrontmatterErrorType MapKeywordToErrorType(string keyword)
    {
        return keyword switch
        {
            "required" => FrontmatterErrorType.MissingRequired,
            "type" => FrontmatterErrorType.InvalidType,
            "enum" => FrontmatterErrorType.InvalidEnum,
            "pattern" => FrontmatterErrorType.PatternMismatch,
            _ => FrontmatterErrorType.InvalidType
        };
    }
}
```

### Built-in Schema Embedding

Embed built-in schemas as assembly resources. Create JSON Schema files for each built-in doc-type in `CompoundDocs.Common/Schemas/`:

```yaml
# problem.schema.yaml
$schema: "https://json-schema.org/draft/2020-12/schema"
title: "Problem Document Schema"
type: object
required:
  - doc_type
  - title
  - date
  - summary
  - significance
  - problem_type
  - symptoms
  - root_cause
  - solution

properties:
  doc_type:
    type: string
    const: "problem"

  title:
    type: string
    minLength: 1

  date:
    type: string
    pattern: '^\d{4}-\d{2}-\d{2}$'

  summary:
    type: string
    minLength: 1

  significance:
    type: string
    enum: [architectural, behavioral, performance, correctness, convention, integration]

  problem_type:
    type: string
    enum: [bug, configuration, integration, performance, security, data]

  symptoms:
    type: array
    items:
      type: string
    minItems: 1

  root_cause:
    type: string
    minLength: 1

  solution:
    type: string
    minLength: 1

  component:
    type: string

  severity:
    type: string
    enum: [critical, high, medium, low]

  prevention:
    type: string

  tags:
    type: array
    items:
      type: string

  related_docs:
    type: array
    items:
      type: string

  supersedes:
    type: string

  promotion_level:
    type: string
    enum: [standard, important, critical]
    default: standard
```

Update `.csproj` to embed schemas:

```xml
<ItemGroup>
  <EmbeddedResource Include="Schemas\*.schema.yaml" />
  <EmbeddedResource Include="Schemas\*.schema.json" />
</ItemGroup>
```

### Dependency Injection Registration

```csharp
using Microsoft.Extensions.DependencyInjection;

public static class FrontmatterValidationServiceExtensions
{
    public static IServiceCollection AddFrontmatterValidation(this IServiceCollection services)
    {
        // Assumes AddSchemaValidation() from Phase 014 is already registered
        services.AddSingleton<IDocTypeSchemaResolver, DocTypeSchemaResolver>();
        services.AddSingleton<IFrontmatterValidator, FrontmatterValidator>();

        return services;
    }
}
```

---

## File Structure

After implementation, the following files should exist:

```
src/CompoundDocs.Common/
├── Schemas/
│   ├── problem.schema.yaml
│   ├── insight.schema.yaml
│   ├── codebase.schema.yaml
│   ├── tool.schema.yaml
│   └── style.schema.yaml
├── Validation/
│   ├── Abstractions/
│   │   ├── IDocTypeSchemaResolver.cs
│   │   └── IFrontmatterValidator.cs
│   ├── DocTypeSchemaResolver.cs
│   ├── FrontmatterValidator.cs
│   ├── FrontmatterValidationResult.cs
│   ├── FrontmatterValidationError.cs
│   ├── FrontmatterValidationWarning.cs
│   ├── FrontmatterErrorType.cs
│   └── FrontmatterValidationServiceExtensions.cs
tests/CompoundDocs.Tests/
└── Validation/
    ├── DocTypeSchemaResolverTests.cs
    ├── FrontmatterValidatorTests.cs
    ├── CommonFieldsValidationTests.cs
    ├── EnumValidationTests.cs
    ├── TypeValidationTests.cs
    └── TestData/
        ├── valid-problem-frontmatter.yaml
        ├── valid-insight-frontmatter.yaml
        ├── valid-codebase-frontmatter.yaml
        ├── valid-tool-frontmatter.yaml
        ├── valid-style-frontmatter.yaml
        ├── invalid-missing-required.yaml
        ├── invalid-enum-value.yaml
        ├── invalid-date-format.yaml
        └── custom-doctype.schema.yaml
```

---

## Dependencies

### Depends On
- **Phase 059**: Frontmatter Extraction (provides extracted frontmatter dictionary)
- **Phase 014**: Schema Validation Library Integration (provides ISchemaLoader, ISchemaCache, ISchemaValidator)
- **Phase 001**: Solution & Project Structure (solution file, project structure)

### Blocks
- Document indexing service (requires validated frontmatter)
- Capture skill validation (validates frontmatter before writing)
- MCP tool document creation (validates documents on create/update)
- RAG query result enhancement (validated doc-type metadata)

---

## Verification Steps

After completing this phase, verify:

1. **Schema Loading**
   - Load built-in schema for `problem` doc-type
   - Load built-in schema for all 5 doc-types
   - Attempt to load non-existent built-in doc-type (expect null)
   - Load custom schema from project directory
   - Custom schema takes precedence over built-in

2. **Common Field Validation**
   - Valid frontmatter with all common fields - expect success
   - Missing `doc_type` - expect `MissingRequired` error
   - Missing `title` - expect `MissingRequired` error
   - Invalid date format `2025-1-24` - expect `PatternMismatch` error
   - Invalid significance value - expect `InvalidEnum` error

3. **Enum Validation**
   - Valid `problem_type: bug` - expect success
   - Invalid `problem_type: unknown` - expect `InvalidEnum` error
   - Valid `severity: high` - expect success
   - Case sensitivity: `BUG` vs `bug` behavior documented

4. **Type Validation**
   - String field with string value - expect success
   - String field with number value - expect appropriate handling
   - Array field with array value - expect success
   - Array field with string value - expect `InvalidType` error
   - Boolean field with boolean value - expect success

5. **Error Aggregation**
   - Multiple errors in same document - all errors reported
   - Error messages are human-readable
   - Field paths correctly identify problematic fields

6. **Custom Doc-Type Validation**
   - Create custom schema in project directory
   - Validate document against custom schema
   - Custom schema with enum fields validates correctly

---

## Testing Notes

Create unit tests in `tests/CompoundDocs.Tests/Validation/`:

### Test Scenarios

```csharp
// DocTypeSchemaResolverTests.cs
[Fact] public async Task GetBuiltInSchemaAsync_Problem_ReturnsSchema()
[Fact] public async Task GetBuiltInSchemaAsync_UnknownType_ReturnsNull()
[Fact] public async Task GetCustomSchemaAsync_ExistingSchema_ReturnsSchema()
[Fact] public async Task GetCustomSchemaAsync_NonExistent_ReturnsNull()
[Fact] public async Task ResolveSchemaAsync_CustomTakesPrecedence()
[Theory, MemberData(nameof(BuiltInDocTypes))] public async Task AllBuiltInSchemasLoadSuccessfully(string docType)

// FrontmatterValidatorTests.cs
[Fact] public async Task ValidateAsync_ValidProblemFrontmatter_ReturnsSuccess()
[Fact] public async Task ValidateAsync_MissingDocType_ReturnsError()
[Fact] public async Task ValidateAsync_MissingTitle_ReturnsError()
[Fact] public async Task ValidateAsync_InvalidDateFormat_ReturnsPatternError()
[Fact] public async Task ValidateAsync_InvalidSignificance_ReturnsEnumError()
[Fact] public async Task ValidateAsync_InvalidProblemType_ReturnsEnumError()
[Fact] public async Task ValidateAsync_MissingSymptoms_ReturnsError()
[Fact] public async Task ValidateAsync_EmptySymptoms_ReturnsError()
[Fact] public async Task ValidateAsync_MultipleErrors_ReportsAll()
[Fact] public async Task ValidateAsync_UnknownDocType_ReturnsError()

// TypeValidationTests.cs
[Fact] public async Task StringField_WithString_Passes()
[Fact] public async Task ArrayField_WithArray_Passes()
[Fact] public async Task ArrayField_WithString_Fails()
[Fact] public async Task BooleanField_WithBoolean_Passes()
[Fact] public async Task BooleanField_WithString_Fails()
```

### Test Data Files

Create YAML test data files with various valid and invalid frontmatter:

```yaml
# valid-problem-frontmatter.yaml
doc_type: problem
title: Database connection timeout
date: 2025-01-24
summary: Connection pool exhaustion under load
significance: performance
problem_type: performance
symptoms:
  - Timeouts after 30 seconds
  - Connection refused errors
root_cause: Pool size too small for concurrent requests
solution: Increased pool size from 10 to 50
severity: high
```

```yaml
# invalid-missing-required.yaml
doc_type: problem
title: Missing fields test
# date: missing
# summary: missing
# significance: missing
problem_type: bug
```

---

## Notes

- **Schema Caching**: Leverage Phase 014's `ISchemaCache` for performance - schemas don't need to be reloaded for each validation
- **Error Messages**: All error messages should be actionable - tell the user what's wrong AND how to fix it
- **Common Fields First**: Always validate common required fields before doc-type-specific fields to provide consistent error ordering
- **Custom Schemas**: Custom schemas extend the base validation - they can add fields but cannot remove common required fields
- **Future Enhancement**: Consider adding support for conditional validation (e.g., if `severity: critical`, then `prevention` becomes required)
- **Spec Alignment**: The `significance` field values are defined in spec/doc-types.md and must match exactly
