namespace CompoundDocs.McpServer.DocTypes;

/// <summary>
/// Registry for document type definitions and their schemas.
/// Provides access to both built-in and custom doc-types.
/// </summary>
public interface IDocTypeRegistry
{
    /// <summary>
    /// Gets a document type definition by its identifier.
    /// </summary>
    /// <param name="docTypeId">The document type identifier (e.g., "problem", "insight").</param>
    /// <returns>The document type definition, or null if not found.</returns>
    DocTypeDefinition? GetDocType(string docTypeId);

    /// <summary>
    /// Gets the JSON schema for a document type.
    /// </summary>
    /// <param name="docTypeId">The document type identifier.</param>
    /// <returns>The JSON schema string, or null if not found.</returns>
    string? GetSchema(string docTypeId);

    /// <summary>
    /// Validates frontmatter against the schema for a document type.
    /// </summary>
    /// <param name="docTypeId">The document type identifier.</param>
    /// <param name="frontmatter">The frontmatter dictionary to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result with any errors or warnings.</returns>
    Task<DocTypeValidationResult> ValidateAsync(
        string docTypeId,
        IDictionary<string, object?> frontmatter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all registered document types.
    /// </summary>
    /// <returns>A read-only collection of all document type definitions.</returns>
    IReadOnlyList<DocTypeDefinition> GetAllDocTypes();

    /// <summary>
    /// Registers a custom document type.
    /// </summary>
    /// <param name="definition">The document type definition to register.</param>
    /// <param name="schema">Optional JSON schema for the document type.</param>
    /// <exception cref="ArgumentException">If a document type with the same ID already exists.</exception>
    void RegisterDocType(DocTypeDefinition definition, string? schema = null);

    /// <summary>
    /// Checks if a document type is registered.
    /// </summary>
    /// <param name="docTypeId">The document type identifier.</param>
    /// <returns>True if the document type is registered.</returns>
    bool IsRegistered(string docTypeId);
}
