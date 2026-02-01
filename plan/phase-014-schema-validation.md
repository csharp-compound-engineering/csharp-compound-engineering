# Phase 014: Schema Validation Library Integration

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Infrastructure Setup
> **Prerequisites**: Phase 001 (Solution & Project Structure)

---

## Spec References

This phase implements schema validation capabilities defined in:

- **spec/configuration.md** - [Schema Validation Libraries](../spec/configuration.md#schema-validation-libraries) (lines 372-419)
- **spec/configuration/schema-files.md** - [Doc-Type Schema Files](../spec/configuration/schema-files.md) - JSON Schema Draft 2020-12 format
- **research/json-schema-validation-library-research.md** - Library comparison and implementation patterns
- **SPEC.md** - [Technology Stack](../SPEC.md#technology-stack) (line 309) - JsonSchema.Net + Yaml2JsonNode

---

## Objectives

1. Add JsonSchema.Net and Yaml2JsonNode NuGet packages to the solution
2. Create a schema validation service that supports both YAML and JSON schema files
3. Implement schema loading with caching to avoid repeated parsing
4. Provide detailed, user-friendly validation error reporting
5. Support custom validation messages for common error scenarios
6. Enable validation of YAML frontmatter against doc-type schemas

---

## Acceptance Criteria

### Package Integration
- [ ] `JsonSchema.Net` package (v8.0.5+) added to `Directory.Packages.props`
- [ ] `Yaml2JsonNode` package (v2.4.0+) added to `Directory.Packages.props`
- [ ] Packages referenced in `CompoundDocs.Common` project

### Schema Loading
- [ ] `ISchemaLoader` interface defined with methods:
  - [ ] `LoadSchema(string schemaPath)` - Load schema from file path
  - [ ] `LoadSchemaFromString(string content, SchemaFormat format)` - Parse inline schema
- [ ] Schema format detection based on file extension (`.yaml`, `.yml`, `.json`)
- [ ] YAML schemas converted to JsonNode via Yaml2JsonNode before parsing
- [ ] JSON schemas loaded directly via `JsonSchema.FromText()`

### Schema Caching
- [ ] `ISchemaCache` interface for schema caching
- [ ] Memory-based cache implementation with configurable expiration
- [ ] Cache key based on schema file path and last modified timestamp
- [ ] Cache invalidation when schema file changes
- [ ] Thread-safe cache access for concurrent validation requests

### Validation Service
- [ ] `ISchemaValidator` interface with methods:
  - [ ] `ValidateAsync(JsonNode data, string schemaPath)` - Validate against schema file
  - [ ] `ValidateAsync(JsonNode data, JsonSchema schema)` - Validate against loaded schema
- [ ] `SchemaValidationResult` record containing:
  - [ ] `IsValid` - Boolean validation status
  - [ ] `Errors` - Collection of `SchemaValidationError` records
- [ ] `SchemaValidationError` record containing:
  - [ ] `Path` - Instance location (JSON pointer to error)
  - [ ] `Keyword` - Failed schema keyword (e.g., "required", "type", "pattern")
  - [ ] `Message` - Human-readable error description
  - [ ] `SchemaLocation` - Schema path where constraint is defined

### Error Reporting
- [ ] Validation uses `OutputFormat.List` for flat error collection
- [ ] `Draft.Draft202012` specified in `EvaluationOptions`
- [ ] Custom error message formatting for common keywords:
  - [ ] `required` - "Missing required field: {fieldName}"
  - [ ] `type` - "Expected {expectedType}, got {actualType} at {path}"
  - [ ] `pattern` - "Value '{value}' does not match pattern '{pattern}' at {path}"
  - [ ] `enum` - "Value '{value}' must be one of: {allowedValues} at {path}"
  - [ ] `minimum`/`maximum` - "Value {value} is {below|above} {limit} at {path}"
- [ ] Error messages suitable for display in MCP tool responses

### Built-in Schema Support
- [ ] Method to load embedded schemas from assembly resources
- [ ] Schema resolution for built-in doc-types (problem, insight, codebase, tool, style)
- [ ] Support for relative schema references within schema files

---

## Implementation Notes

### NuGet Package Configuration

Add to `Directory.Packages.props`:

```xml
<ItemGroup>
  <!-- Schema Validation -->
  <PackageVersion Include="JsonSchema.Net" Version="8.0.5" />
  <PackageVersion Include="Yaml2JsonNode" Version="2.4.0" />
</ItemGroup>
```

Reference in `CompoundDocs.Common.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="JsonSchema.Net" />
  <PackageReference Include="Yaml2JsonNode" />
</ItemGroup>
```

### Schema Loader Implementation

```csharp
using System.Text.Json.Nodes;
using Json.Schema;
using Yaml2JsonNode;

public interface ISchemaLoader
{
    Task<JsonSchema> LoadSchemaAsync(string schemaPath, CancellationToken ct = default);
    JsonSchema LoadSchemaFromString(string content, SchemaFormat format);
}

public enum SchemaFormat
{
    Json,
    Yaml
}

public class SchemaLoader : ISchemaLoader
{
    public async Task<JsonSchema> LoadSchemaAsync(string schemaPath, CancellationToken ct = default)
    {
        var content = await File.ReadAllTextAsync(schemaPath, ct);
        var format = GetFormatFromExtension(schemaPath);
        return LoadSchemaFromString(content, format);
    }

    public JsonSchema LoadSchemaFromString(string content, SchemaFormat format)
    {
        if (format == SchemaFormat.Yaml)
        {
            // Convert YAML to JSON using Yaml2JsonNode
            var yamlStream = new YamlStream();
            yamlStream.Load(new StringReader(content));
            var jsonNode = yamlStream.Documents[0].RootNode.ToJsonNode();
            return JsonSchema.FromText(jsonNode!.ToJsonString());
        }

        return JsonSchema.FromText(content);
    }

    private static SchemaFormat GetFormatFromExtension(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".yaml" or ".yml" => SchemaFormat.Yaml,
            ".json" => SchemaFormat.Json,
            _ => throw new ArgumentException($"Unsupported schema file extension: {extension}")
        };
    }
}
```

### Schema Cache Implementation

```csharp
using System.Collections.Concurrent;
using Json.Schema;

public interface ISchemaCache
{
    Task<JsonSchema?> GetOrAddAsync(
        string schemaPath,
        Func<Task<JsonSchema>> factory,
        CancellationToken ct = default);
    void Invalidate(string schemaPath);
    void Clear();
}

public class SchemaCache : ISchemaCache
{
    private readonly ConcurrentDictionary<string, CachedSchema> _cache = new();
    private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(30);

    public async Task<JsonSchema?> GetOrAddAsync(
        string schemaPath,
        Func<Task<JsonSchema>> factory,
        CancellationToken ct = default)
    {
        var absolutePath = Path.GetFullPath(schemaPath);
        var fileInfo = new FileInfo(absolutePath);

        if (!fileInfo.Exists)
            return null;

        var cacheKey = absolutePath;
        var lastModified = fileInfo.LastWriteTimeUtc;

        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            // Check if file was modified or cache expired
            if (cached.LastModified >= lastModified &&
                cached.CachedAt.Add(_defaultExpiration) > DateTime.UtcNow)
            {
                return cached.Schema;
            }
        }

        var schema = await factory();
        _cache[cacheKey] = new CachedSchema(schema, lastModified, DateTime.UtcNow);
        return schema;
    }

    public void Invalidate(string schemaPath)
    {
        var absolutePath = Path.GetFullPath(schemaPath);
        _cache.TryRemove(absolutePath, out _);
    }

    public void Clear() => _cache.Clear();

    private record CachedSchema(JsonSchema Schema, DateTime LastModified, DateTime CachedAt);
}
```

### Validation Service Implementation

```csharp
using System.Text.Json.Nodes;
using Json.Schema;

public interface ISchemaValidator
{
    Task<SchemaValidationResult> ValidateAsync(
        JsonNode data,
        string schemaPath,
        CancellationToken ct = default);

    SchemaValidationResult Validate(JsonNode data, JsonSchema schema);
}

public record SchemaValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<SchemaValidationError> Errors { get; init; } = [];

    public static SchemaValidationResult Success() => new() { IsValid = true };
}

public record SchemaValidationError
{
    public required string Path { get; init; }
    public required string Keyword { get; init; }
    public required string Message { get; init; }
    public string? SchemaLocation { get; init; }
}

public class SchemaValidator : ISchemaValidator
{
    private readonly ISchemaLoader _schemaLoader;
    private readonly ISchemaCache _schemaCache;
    private readonly IValidationErrorFormatter _errorFormatter;

    public SchemaValidator(
        ISchemaLoader schemaLoader,
        ISchemaCache schemaCache,
        IValidationErrorFormatter errorFormatter)
    {
        _schemaLoader = schemaLoader;
        _schemaCache = schemaCache;
        _errorFormatter = errorFormatter;
    }

    public async Task<SchemaValidationResult> ValidateAsync(
        JsonNode data,
        string schemaPath,
        CancellationToken ct = default)
    {
        var schema = await _schemaCache.GetOrAddAsync(
            schemaPath,
            () => _schemaLoader.LoadSchemaAsync(schemaPath, ct),
            ct);

        if (schema is null)
        {
            return new SchemaValidationResult
            {
                IsValid = false,
                Errors = [new SchemaValidationError
                {
                    Path = "",
                    Keyword = "schema",
                    Message = $"Schema file not found: {schemaPath}"
                }]
            };
        }

        return Validate(data, schema);
    }

    public SchemaValidationResult Validate(JsonNode data, JsonSchema schema)
    {
        var options = new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
            ValidateAs = Draft.Draft202012
        };

        var result = schema.Evaluate(data, options);

        if (result.IsValid)
            return SchemaValidationResult.Success();

        var errors = _errorFormatter.FormatErrors(result);
        return new SchemaValidationResult
        {
            IsValid = false,
            Errors = errors
        };
    }
}
```

### Custom Error Formatter

```csharp
using Json.Schema;

public interface IValidationErrorFormatter
{
    IReadOnlyList<SchemaValidationError> FormatErrors(EvaluationResults results);
}

public class ValidationErrorFormatter : IValidationErrorFormatter
{
    public IReadOnlyList<SchemaValidationError> FormatErrors(EvaluationResults results)
    {
        var errors = new List<SchemaValidationError>();

        if (results.Details is null)
            return errors;

        foreach (var detail in results.Details.Where(d => !d.IsValid && d.Errors is not null))
        {
            foreach (var error in detail.Errors!)
            {
                var formattedMessage = FormatMessage(
                    error.Key,
                    error.Value,
                    detail.InstanceLocation?.ToString() ?? "");

                errors.Add(new SchemaValidationError
                {
                    Path = detail.InstanceLocation?.ToString() ?? "",
                    Keyword = error.Key,
                    Message = formattedMessage,
                    SchemaLocation = detail.SchemaLocation?.ToString()
                });
            }
        }

        return errors;
    }

    private static string FormatMessage(string keyword, string defaultMessage, string path)
    {
        // Provide user-friendly messages for common validation keywords
        return keyword switch
        {
            "required" => FormatRequiredMessage(defaultMessage),
            "type" => FormatTypeMessage(defaultMessage, path),
            "pattern" => FormatPatternMessage(defaultMessage, path),
            "enum" => FormatEnumMessage(defaultMessage, path),
            "minimum" => FormatMinimumMessage(defaultMessage, path),
            "maximum" => FormatMaximumMessage(defaultMessage, path),
            "minLength" => FormatMinLengthMessage(defaultMessage, path),
            "maxLength" => FormatMaxLengthMessage(defaultMessage, path),
            "format" => FormatFormatMessage(defaultMessage, path),
            _ => defaultMessage
        };
    }

    private static string FormatRequiredMessage(string defaultMessage)
    {
        // Extract field names from "Required properties [\"foo\", \"bar\"] were not present"
        var match = System.Text.RegularExpressions.Regex.Match(
            defaultMessage,
            @"Required properties \[(.+)\] were not present");

        if (match.Success)
        {
            var fields = match.Groups[1].Value.Replace("\"", "");
            return $"Missing required field(s): {fields}";
        }

        return defaultMessage;
    }

    private static string FormatTypeMessage(string defaultMessage, string path)
    {
        var location = string.IsNullOrEmpty(path) ? "root" : path;
        return $"{defaultMessage} at '{location}'";
    }

    private static string FormatPatternMessage(string defaultMessage, string path)
    {
        var location = string.IsNullOrEmpty(path) ? "root" : path;
        return $"Value at '{location}' does not match required pattern. {defaultMessage}";
    }

    private static string FormatEnumMessage(string defaultMessage, string path)
    {
        var location = string.IsNullOrEmpty(path) ? "root" : path;
        return $"Invalid value at '{location}'. {defaultMessage}";
    }

    private static string FormatMinimumMessage(string defaultMessage, string path)
    {
        var location = string.IsNullOrEmpty(path) ? "root" : path;
        return $"Value at '{location}' is below minimum. {defaultMessage}";
    }

    private static string FormatMaximumMessage(string defaultMessage, string path)
    {
        var location = string.IsNullOrEmpty(path) ? "root" : path;
        return $"Value at '{location}' exceeds maximum. {defaultMessage}";
    }

    private static string FormatMinLengthMessage(string defaultMessage, string path)
    {
        var location = string.IsNullOrEmpty(path) ? "root" : path;
        return $"Value at '{location}' is too short. {defaultMessage}";
    }

    private static string FormatMaxLengthMessage(string defaultMessage, string path)
    {
        var location = string.IsNullOrEmpty(path) ? "root" : path;
        return $"Value at '{location}' is too long. {defaultMessage}";
    }

    private static string FormatFormatMessage(string defaultMessage, string path)
    {
        var location = string.IsNullOrEmpty(path) ? "root" : path;
        return $"Invalid format at '{location}'. {defaultMessage}";
    }
}
```

### Dependency Injection Registration

```csharp
using Microsoft.Extensions.DependencyInjection;

public static class SchemaValidationServiceExtensions
{
    public static IServiceCollection AddSchemaValidation(this IServiceCollection services)
    {
        services.AddSingleton<ISchemaCache, SchemaCache>();
        services.AddSingleton<ISchemaLoader, SchemaLoader>();
        services.AddSingleton<IValidationErrorFormatter, ValidationErrorFormatter>();
        services.AddSingleton<ISchemaValidator, SchemaValidator>();

        return services;
    }
}
```

### Frontmatter Validation Integration

Example usage for validating YAML frontmatter extracted from markdown documents:

```csharp
public class FrontmatterValidator
{
    private readonly ISchemaValidator _schemaValidator;
    private readonly ISchemaLoader _schemaLoader;

    public FrontmatterValidator(ISchemaValidator schemaValidator, ISchemaLoader schemaLoader)
    {
        _schemaValidator = schemaValidator;
        _schemaLoader = schemaLoader;
    }

    public async Task<SchemaValidationResult> ValidateFrontmatterAsync(
        string yamlFrontmatter,
        string schemaPath,
        CancellationToken ct = default)
    {
        // Parse YAML frontmatter to JsonNode
        var yamlStream = new YamlStream();
        yamlStream.Load(new StringReader(yamlFrontmatter));
        var jsonNode = yamlStream.Documents[0].RootNode.ToJsonNode();

        if (jsonNode is null)
        {
            return new SchemaValidationResult
            {
                IsValid = false,
                Errors = [new SchemaValidationError
                {
                    Path = "",
                    Keyword = "parse",
                    Message = "Failed to parse YAML frontmatter"
                }]
            };
        }

        return await _schemaValidator.ValidateAsync(jsonNode, schemaPath, ct);
    }
}
```

---

## File Structure

After implementation, the following files should exist in `CompoundDocs.Common`:

```
src/CompoundDocs.Common/
├── Schema/
│   ├── ISchemaLoader.cs
│   ├── SchemaLoader.cs
│   ├── ISchemaCache.cs
│   ├── SchemaCache.cs
│   ├── ISchemaValidator.cs
│   ├── SchemaValidator.cs
│   ├── SchemaValidationResult.cs
│   ├── SchemaValidationError.cs
│   ├── IValidationErrorFormatter.cs
│   ├── ValidationErrorFormatter.cs
│   └── SchemaValidationServiceExtensions.cs
└── ...
```

---

## Dependencies

### Depends On
- **Phase 001**: Solution & Project Structure (solution file, Directory.Packages.props)

### Blocks
- Custom doc-type validation (requires schema validation infrastructure)
- Project configuration validation (uses schema validation for config.json)
- Built-in doc-type frontmatter validation (problem, insight, codebase, tool, style)

---

## Verification Steps

After completing this phase, verify:

1. **Package Installation**
   ```bash
   dotnet restore
   # Verify JsonSchema.Net and Yaml2JsonNode are restored
   ```

2. **Schema Loading**
   - Load a YAML schema file and verify it parses without errors
   - Load a JSON schema file and verify it parses without errors
   - Attempt to load a non-existent file and verify graceful error handling

3. **Validation**
   - Validate valid frontmatter against schema - expect `IsValid = true`
   - Validate invalid frontmatter (missing required field) - expect appropriate error
   - Validate invalid frontmatter (wrong type) - expect appropriate error
   - Validate invalid frontmatter (pattern mismatch) - expect appropriate error

4. **Caching**
   - Load same schema twice - verify second load uses cache
   - Modify schema file - verify cache invalidates and reloads

5. **Error Messages**
   - Verify error messages are human-readable and actionable
   - Verify error paths correctly identify the problematic field

---

## Testing Notes

Create unit tests in `tests/CompoundDocs.Tests/Schema/`:

```
tests/CompoundDocs.Tests/Schema/
├── SchemaLoaderTests.cs
├── SchemaCacheTests.cs
├── SchemaValidatorTests.cs
├── ValidationErrorFormatterTests.cs
└── TestSchemas/
    ├── valid-schema.yaml
    ├── valid-schema.json
    └── test-data/
        ├── valid-frontmatter.yaml
        └── invalid-frontmatter.yaml
```

Test scenarios:
- YAML schema loading
- JSON schema loading
- Invalid schema format handling
- Cache hit/miss behavior
- Cache invalidation on file change
- All supported validation keywords
- Custom error message formatting
- Thread-safe concurrent validation

---

## Notes

- **Licensing**: JsonSchema.Net has a licensing change effective February 2026 requiring a maintenance fee for commercial use. Monitor this and budget accordingly.
- **Performance**: Schema caching is critical for performance since schema parsing is expensive. The cache uses file modification time to invalidate stale entries.
- **Draft 2020-12**: All schemas should declare `$schema: "https://json-schema.org/draft/2020-12/schema"` for consistency.
- **Yaml2JsonNode vs YamlDotNet**: The spec specifically calls for Yaml2JsonNode (from json-everything ecosystem) rather than YamlDotNet.System.Text.Json for better integration with JsonSchema.Net.
