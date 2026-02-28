using NJsonSchema;

namespace CompoundDocs.Common.Parsing;

/// <summary>
/// Validates JSON/YAML data against JSON Schema.
/// </summary>
public interface ISchemaValidator
{
    /// <summary>
    /// Validates data against a JSON schema.
    /// </summary>
    Task<ValidationResult> ValidateAsync(object data, string schemaJson, CancellationToken ct = default);

    /// <summary>
    /// Validates a JSON string against a schema.
    /// </summary>
    Task<ValidationResult> ValidateJsonAsync(string json, string schemaJson, CancellationToken ct = default);

    /// <summary>
    /// Loads a schema from a file.
    /// </summary>
    Task<JsonSchema> LoadSchemaAsync(string filePath, CancellationToken ct = default);
}
