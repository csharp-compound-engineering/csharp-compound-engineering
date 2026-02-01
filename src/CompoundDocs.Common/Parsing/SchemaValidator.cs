using NJsonSchema;
using System.Text.Json;

namespace CompoundDocs.Common.Parsing;

/// <summary>
/// Validates JSON/YAML data against JSON Schema using NJsonSchema.
/// </summary>
public sealed class SchemaValidator
{
    private readonly Dictionary<string, JsonSchema> _schemaCache = new();

    /// <summary>
    /// Validates data against a JSON schema.
    /// </summary>
    public async Task<ValidationResult> ValidateAsync(
        object data,
        string schemaJson,
        CancellationToken ct = default)
    {
        var schema = await GetOrParseSchemaAsync(schemaJson, ct);
        var json = JsonSerializer.Serialize(data);
        var errors = schema.Validate(json);

        return new ValidationResult(
            errors.Count == 0,
            errors.Select(e => new ValidationError(e.Path ?? "", e.Kind.ToString(), e.ToString())).ToList());
    }

    /// <summary>
    /// Validates a JSON string against a schema.
    /// </summary>
    public async Task<ValidationResult> ValidateJsonAsync(
        string json,
        string schemaJson,
        CancellationToken ct = default)
    {
        var schema = await GetOrParseSchemaAsync(schemaJson, ct);
        var errors = schema.Validate(json);

        return new ValidationResult(
            errors.Count == 0,
            errors.Select(e => new ValidationError(e.Path ?? "", e.Kind.ToString(), e.ToString())).ToList());
    }

    /// <summary>
    /// Loads a schema from a file.
    /// </summary>
    public async Task<JsonSchema> LoadSchemaAsync(string filePath, CancellationToken ct = default)
    {
        var schemaJson = await File.ReadAllTextAsync(filePath, ct);
        return await JsonSchema.FromJsonAsync(schemaJson, ct);
    }

    private async Task<JsonSchema> GetOrParseSchemaAsync(string schemaJson, CancellationToken ct)
    {
        var hash = schemaJson.GetHashCode().ToString();

        if (_schemaCache.TryGetValue(hash, out var cached))
        {
            return cached;
        }

        var schema = await JsonSchema.FromJsonAsync(schemaJson, ct);
        _schemaCache[hash] = schema;
        return schema;
    }
}

public sealed record ValidationResult(
    bool IsValid,
    IReadOnlyList<ValidationError> Errors);

public sealed record ValidationError(
    string Path,
    string Kind,
    string Message);
