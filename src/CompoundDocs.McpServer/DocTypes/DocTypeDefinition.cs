using CompoundDocs.McpServer.Models;

namespace CompoundDocs.McpServer.DocTypes;

/// <summary>
/// Defines a document type with its metadata and schema information.
/// </summary>
public sealed class DocTypeDefinition
{
    /// <summary>
    /// The unique identifier for the document type (e.g., "problem", "insight").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The display name for the document type.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// A description of what this document type is used for.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The JSON schema for validating frontmatter (may be null for custom types without schema).
    /// </summary>
    public string? Schema { get; init; }

    /// <summary>
    /// Trigger phrases that suggest this document type should be used.
    /// Used for automatic doc-type detection.
    /// </summary>
    public IReadOnlyList<string> TriggerPhrases { get; init; } = [];

    /// <summary>
    /// The default promotion level for documents of this type.
    /// </summary>
    public string PromotionLevel { get; init; } = PromotionLevels.Standard;

    /// <summary>
    /// Whether this is a built-in document type.
    /// </summary>
    public bool IsBuiltIn { get; init; }

    /// <summary>
    /// The required fields for this document type's frontmatter.
    /// </summary>
    public IReadOnlyList<string> RequiredFields { get; init; } = [];

    /// <summary>
    /// The optional fields for this document type's frontmatter.
    /// </summary>
    public IReadOnlyList<string> OptionalFields { get; init; } = [];

    /// <summary>
    /// Creates a built-in document type definition.
    /// </summary>
    public static DocTypeDefinition CreateBuiltIn(
        string id,
        string name,
        string description,
        string? schema = null,
        IReadOnlyList<string>? triggerPhrases = null,
        IReadOnlyList<string>? requiredFields = null,
        IReadOnlyList<string>? optionalFields = null)
    {
        return new DocTypeDefinition
        {
            Id = id,
            Name = name,
            Description = description,
            Schema = schema,
            TriggerPhrases = triggerPhrases ?? [],
            IsBuiltIn = true,
            RequiredFields = requiredFields ?? [],
            OptionalFields = optionalFields ?? []
        };
    }

    /// <summary>
    /// Creates a custom document type definition.
    /// </summary>
    public static DocTypeDefinition CreateCustom(
        string id,
        string name,
        string description,
        string? schema = null,
        IReadOnlyList<string>? triggerPhrases = null,
        string? promotionLevel = null,
        IReadOnlyList<string>? requiredFields = null,
        IReadOnlyList<string>? optionalFields = null)
    {
        return new DocTypeDefinition
        {
            Id = id,
            Name = name,
            Description = description,
            Schema = schema,
            TriggerPhrases = triggerPhrases ?? [],
            PromotionLevel = promotionLevel ?? PromotionLevels.Standard,
            IsBuiltIn = false,
            RequiredFields = requiredFields ?? [],
            OptionalFields = optionalFields ?? []
        };
    }
}
