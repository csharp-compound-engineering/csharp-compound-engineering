using System.Collections.Concurrent;
using System.Reflection;
using CompoundDocs.McpServer.Models;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.DocTypes;

/// <summary>
/// Registry for document type definitions and their schemas.
/// Provides built-in types and supports custom doc-type registration.
/// </summary>
public sealed class DocTypeRegistry : IDocTypeRegistry
{
    private readonly ILogger<DocTypeRegistry> _logger;
    private readonly DocTypeValidator _validator;
    private readonly ConcurrentDictionary<string, DocTypeDefinition> _docTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _schemas = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a new instance of DocTypeRegistry.
    /// </summary>
    public DocTypeRegistry(ILogger<DocTypeRegistry> logger, DocTypeValidator validator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));

        RegisterBuiltInDocTypes();
    }

    /// <inheritdoc/>
    public DocTypeDefinition? GetDocType(string docTypeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(docTypeId);

        _docTypes.TryGetValue(docTypeId, out var definition);
        return definition;
    }

    /// <inheritdoc/>
    public string? GetSchema(string docTypeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(docTypeId);

        _schemas.TryGetValue(docTypeId, out var schema);
        return schema;
    }

    /// <inheritdoc/>
    public async Task<DocTypeValidationResult> ValidateAsync(
        string docTypeId,
        IDictionary<string, object?> frontmatter,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(docTypeId);
        ArgumentNullException.ThrowIfNull(frontmatter);

        if (!_docTypes.TryGetValue(docTypeId, out var definition))
        {
            _logger.LogWarning("Attempted to validate unknown doc-type '{DocTypeId}'", docTypeId);
            return DocTypeValidationResult.DocTypeNotFound(docTypeId);
        }

        // First validate required fields
        var requiredFieldErrors = _validator.ValidateRequiredFields(
            docTypeId, frontmatter, definition.RequiredFields);

        if (requiredFieldErrors.Count > 0)
        {
            return DocTypeValidationResult.Failure(docTypeId, requiredFieldErrors);
        }

        // Then validate against schema if available
        if (_schemas.TryGetValue(docTypeId, out var schema))
        {
            return await _validator.ValidateAsync(docTypeId, schema, frontmatter, cancellationToken);
        }

        // No schema, just return success
        _logger.LogDebug("No schema found for doc-type '{DocTypeId}', skipping schema validation", docTypeId);
        return DocTypeValidationResult.Success(docTypeId);
    }

    /// <inheritdoc/>
    public IReadOnlyList<DocTypeDefinition> GetAllDocTypes()
    {
        return _docTypes.Values.ToList();
    }

    /// <inheritdoc/>
    public void RegisterDocType(DocTypeDefinition definition, string? schema = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Id);

        if (!_docTypes.TryAdd(definition.Id, definition))
        {
            throw new ArgumentException(
                $"Document type '{definition.Id}' is already registered.",
                nameof(definition));
        }

        if (!string.IsNullOrWhiteSpace(schema))
        {
            _schemas[definition.Id] = schema;
        }

        _logger.LogInformation(
            "Registered doc-type '{DocTypeId}' (built-in: {IsBuiltIn})",
            definition.Id, definition.IsBuiltIn);
    }

    /// <inheritdoc/>
    public bool IsRegistered(string docTypeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(docTypeId);
        return _docTypes.ContainsKey(docTypeId);
    }

    private void RegisterBuiltInDocTypes()
    {
        // Load schemas from embedded resources or files
        var problemSchema = LoadSchema("problem");
        var insightSchema = LoadSchema("insight");
        var codebaseSchema = LoadSchema("codebase");
        var toolSchema = LoadSchema("tool");
        var styleSchema = LoadSchema("style");

        // Register Problem doc-type
        RegisterDocType(DocTypeDefinition.CreateBuiltIn(
            id: DocumentTypes.Problem,
            name: "Problem Statement",
            description: "Problem statements and issue descriptions for tracking challenges.",
            schema: problemSchema,
            triggerPhrases: ["problem", "issue", "bug", "error", "failure", "challenge"],
            requiredFields: ["doc_type", "title", "tags"],
            optionalFields: ["root_cause", "solution_status", "severity", "promotion_level", "links", "date"]
        ), problemSchema);

        // Register Insight doc-type
        RegisterDocType(DocTypeDefinition.CreateBuiltIn(
            id: DocumentTypes.Insight,
            name: "Insight",
            description: "Insights and lessons learned from project experiences.",
            schema: insightSchema,
            triggerPhrases: ["insight", "lesson", "learning", "discovery", "realization"],
            requiredFields: ["doc_type", "title", "tags"],
            optionalFields: ["discovery_context", "applications", "promotion_level", "links", "date", "category"]
        ), insightSchema);

        // Register Codebase doc-type
        RegisterDocType(DocTypeDefinition.CreateBuiltIn(
            id: DocumentTypes.Codebase,
            name: "Codebase Documentation",
            description: "Documentation specific to the codebase structure and implementation.",
            schema: codebaseSchema,
            triggerPhrases: ["codebase", "code", "implementation", "module", "component", "architecture"],
            requiredFields: ["doc_type", "title", "component"],
            optionalFields: ["dependencies", "patterns", "promotion_level", "links", "tags", "module"]
        ), codebaseSchema);

        // Register Tool doc-type
        RegisterDocType(DocTypeDefinition.CreateBuiltIn(
            id: DocumentTypes.Tool,
            name: "Tool Documentation",
            description: "Documentation for tools, utilities, and scripts used in the project.",
            schema: toolSchema,
            triggerPhrases: ["tool", "utility", "script", "cli", "command"],
            requiredFields: ["doc_type", "title", "tool_name"],
            optionalFields: ["version", "setup_requirements", "promotion_level", "links", "tags", "usage"]
        ), toolSchema);

        // Register Style doc-type
        RegisterDocType(DocTypeDefinition.CreateBuiltIn(
            id: DocumentTypes.Style,
            name: "Style Guide",
            description: "Style guides and coding standards for the project.",
            schema: styleSchema,
            triggerPhrases: ["style", "convention", "standard", "guideline", "best practice", "coding standard"],
            requiredFields: ["doc_type", "title", "category"],
            optionalFields: ["exceptions", "applies_to", "promotion_level", "links", "tags", "language"]
        ), styleSchema);

        // Register other built-in doc-types without specific schemas
        RegisterDocType(DocTypeDefinition.CreateBuiltIn(
            id: DocumentTypes.Spec,
            name: "Specification",
            description: "Specification documents defining system behavior, requirements, and constraints.",
            triggerPhrases: ["spec", "specification", "requirement", "design"],
            requiredFields: ["doc_type", "title"],
            optionalFields: ["version", "status", "links", "tags"]
        ));

        RegisterDocType(DocTypeDefinition.CreateBuiltIn(
            id: DocumentTypes.Adr,
            name: "Architecture Decision Record",
            description: "Architecture Decision Records documenting significant architectural choices.",
            triggerPhrases: ["adr", "architecture decision", "decision record"],
            requiredFields: ["doc_type", "title", "status"],
            optionalFields: ["date", "context", "decision", "consequences", "tags"]
        ));

        RegisterDocType(DocTypeDefinition.CreateBuiltIn(
            id: DocumentTypes.Research,
            name: "Research",
            description: "Research documents with background information, analysis, and findings.",
            triggerPhrases: ["research", "analysis", "investigation", "study"],
            requiredFields: ["doc_type", "title"],
            optionalFields: ["date", "author", "links", "tags"]
        ));

        RegisterDocType(DocTypeDefinition.CreateBuiltIn(
            id: DocumentTypes.Doc,
            name: "Documentation",
            description: "General documentation for guides, tutorials, and reference material.",
            triggerPhrases: ["doc", "documentation", "guide", "tutorial", "reference"],
            requiredFields: ["doc_type", "title"],
            optionalFields: ["category", "links", "tags"]
        ));

        _logger.LogInformation("Registered {Count} built-in doc-types", _docTypes.Count);
    }

    private string? LoadSchema(string docTypeId)
    {
        try
        {
            // Try to load from embedded resource first
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"CompoundDocs.McpServer.Schemas.{docTypeId}.schema.json";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var schema = reader.ReadToEnd();
                _logger.LogDebug("Loaded schema for '{DocTypeId}' from embedded resource", docTypeId);
                return schema;
            }

            // Try to load from file system
            var schemaPath = Path.Combine(
                AppContext.BaseDirectory,
                "schemas",
                $"{docTypeId}.schema.json");

            if (File.Exists(schemaPath))
            {
                var schema = File.ReadAllText(schemaPath);
                _logger.LogDebug("Loaded schema for '{DocTypeId}' from file: {Path}", docTypeId, schemaPath);
                return schema;
            }

            // Try alternate location (development)
            var altPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "..",
                "..",
                "..",
                "schemas",
                $"{docTypeId}.schema.json");

            if (File.Exists(altPath))
            {
                var schema = File.ReadAllText(altPath);
                _logger.LogDebug("Loaded schema for '{DocTypeId}' from alt path: {Path}", docTypeId, altPath);
                return schema;
            }

            _logger.LogWarning("Schema file not found for doc-type '{DocTypeId}'", docTypeId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading schema for doc-type '{DocTypeId}'", docTypeId);
            return null;
        }
    }
}
