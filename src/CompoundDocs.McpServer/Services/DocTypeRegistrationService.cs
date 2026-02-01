using System.Collections.Concurrent;
using System.Text.Json;
using CompoundDocs.Common.Configuration;
using CompoundDocs.Common.Parsing;
using CompoundDocs.McpServer.DocTypes;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Service for registering custom doc-types at runtime.
/// Loads doc-types from project configuration and provides thread-safe registration/unregistration.
/// </summary>
public interface IDocTypeRegistrationService
{
    /// <summary>
    /// Loads and registers custom doc-types from the project configuration.
    /// </summary>
    /// <param name="config">The project configuration containing custom doc-type definitions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation with the count of registered doc-types.</returns>
    Task<int> LoadFromConfigAsync(ProjectConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a custom doc-type at runtime.
    /// </summary>
    /// <param name="customDocType">The custom doc-type definition from configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if registration was successful, false if validation failed.</returns>
    Task<bool> RegisterAsync(CustomDocType customDocType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unregisters a custom doc-type at runtime.
    /// Built-in doc-types cannot be unregistered.
    /// </summary>
    /// <param name="docTypeId">The identifier of the doc-type to unregister.</param>
    /// <returns>True if unregistration was successful.</returns>
    bool Unregister(string docTypeId);

    /// <summary>
    /// Gets all registered custom (non-built-in) doc-types.
    /// </summary>
    /// <returns>A read-only collection of custom doc-type identifiers.</returns>
    IReadOnlyList<string> GetCustomDocTypeIds();

    /// <summary>
    /// Validates a custom doc-type definition including its JSON schema.
    /// </summary>
    /// <param name="customDocType">The custom doc-type to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with any errors.</returns>
    Task<DocTypeRegistrationResult> ValidateAsync(CustomDocType customDocType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the validation cache.
    /// </summary>
    void ClearCache();
}

/// <summary>
/// Result of a doc-type registration validation.
/// </summary>
public sealed class DocTypeRegistrationResult
{
    /// <summary>
    /// Whether the validation was successful.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Validation errors, if any.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static DocTypeRegistrationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    public static DocTypeRegistrationResult Failure(IReadOnlyList<string> errors) =>
        new() { IsValid = false, Errors = errors };

    /// <summary>
    /// Creates a failed validation result with a single error.
    /// </summary>
    /// <param name="error">The validation error.</param>
    public static DocTypeRegistrationResult Failure(string error) =>
        new() { IsValid = false, Errors = [error] };
}

/// <summary>
/// Implementation of IDocTypeRegistrationService providing runtime doc-type registration.
/// </summary>
public sealed class DocTypeRegistrationService : IDocTypeRegistrationService
{
    private readonly IDocTypeRegistry _docTypeRegistry;
    private readonly SchemaValidator _schemaValidator;
    private readonly ILogger<DocTypeRegistrationService> _logger;

    /// <summary>
    /// Cache of validated schemas to avoid re-validation.
    /// Key: schema hash, Value: validation timestamp.
    /// </summary>
    private readonly ConcurrentDictionary<string, DateTimeOffset> _schemaValidationCache = new();

    /// <summary>
    /// Set of custom doc-type IDs registered through this service.
    /// </summary>
    private readonly ConcurrentDictionary<string, bool> _customDocTypeIds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Lock for thread-safe registration operations.
    /// </summary>
    private readonly SemaphoreSlim _registrationLock = new(1, 1);

    /// <summary>
    /// JSON schema for validating custom doc-type definitions.
    /// </summary>
    private static readonly string CustomDocTypeMetaSchema = JsonSerializer.Serialize(new
    {
        type = "object",
        properties = new
        {
            id = new { type = "string", minLength = 1, pattern = "^[a-z][a-z0-9_-]*$" },
            name = new { type = "string", minLength = 1 },
            description = new { type = "string" },
            schema = new { type = "string" },
            triggerPhrases = new { type = "array", items = new { type = "string" } }
        },
        required = new[] { "id", "name" }
    });

    /// <summary>
    /// Creates a new instance of DocTypeRegistrationService.
    /// </summary>
    /// <param name="docTypeRegistry">The doc-type registry to register doc-types in.</param>
    /// <param name="schemaValidator">The schema validator for validating JSON schemas.</param>
    /// <param name="logger">Logger instance.</param>
    public DocTypeRegistrationService(
        IDocTypeRegistry docTypeRegistry,
        SchemaValidator schemaValidator,
        ILogger<DocTypeRegistrationService> logger)
    {
        _docTypeRegistry = docTypeRegistry ?? throw new ArgumentNullException(nameof(docTypeRegistry));
        _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<int> LoadFromConfigAsync(ProjectConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config.CustomDocTypes.Count == 0)
        {
            _logger.LogDebug("No custom doc-types defined in project configuration");
            return 0;
        }

        _logger.LogInformation("Loading {Count} custom doc-types from project configuration",
            config.CustomDocTypes.Count);

        var registeredCount = 0;

        foreach (var customDocType in config.CustomDocTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var success = await RegisterAsync(customDocType, cancellationToken);
                if (success)
                {
                    registeredCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to register custom doc-type '{DocTypeId}'", customDocType.Id);
            }
        }

        _logger.LogInformation("Successfully registered {Count} of {Total} custom doc-types",
            registeredCount, config.CustomDocTypes.Count);

        return registeredCount;
    }

    /// <inheritdoc />
    public async Task<bool> RegisterAsync(CustomDocType customDocType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customDocType);
        ArgumentException.ThrowIfNullOrWhiteSpace(customDocType.Id);

        await _registrationLock.WaitAsync(cancellationToken);
        try
        {
            // Validate the custom doc-type definition
            var validationResult = await ValidateAsync(customDocType, cancellationToken);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning(
                    "Custom doc-type '{DocTypeId}' failed validation: {Errors}",
                    customDocType.Id,
                    string.Join("; ", validationResult.Errors));
                return false;
            }

            // Check if already registered
            if (_docTypeRegistry.IsRegistered(customDocType.Id))
            {
                var existing = _docTypeRegistry.GetDocType(customDocType.Id);
                if (existing?.IsBuiltIn == true)
                {
                    _logger.LogWarning(
                        "Cannot override built-in doc-type '{DocTypeId}'",
                        customDocType.Id);
                    return false;
                }

                _logger.LogDebug(
                    "Custom doc-type '{DocTypeId}' already registered, skipping",
                    customDocType.Id);
                return true;
            }

            // Create and register the doc-type definition
            var definition = DocTypeDefinition.CreateCustom(
                id: customDocType.Id,
                name: customDocType.Name,
                description: customDocType.Description ?? $"Custom doc-type: {customDocType.Name}",
                schema: customDocType.Schema,
                triggerPhrases: customDocType.TriggerPhrases);

            _docTypeRegistry.RegisterDocType(definition, customDocType.Schema);
            _customDocTypeIds[customDocType.Id] = true;

            _logger.LogInformation(
                "Registered custom doc-type '{DocTypeId}' ({Name})",
                customDocType.Id,
                customDocType.Name);

            return true;
        }
        finally
        {
            _registrationLock.Release();
        }
    }

    /// <inheritdoc />
    public bool Unregister(string docTypeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(docTypeId);

        // Check if it's a built-in doc-type
        var docType = _docTypeRegistry.GetDocType(docTypeId);
        if (docType?.IsBuiltIn == true)
        {
            _logger.LogWarning("Cannot unregister built-in doc-type '{DocTypeId}'", docTypeId);
            return false;
        }

        // Check if it's a custom doc-type registered through this service
        if (!_customDocTypeIds.TryRemove(docTypeId, out _))
        {
            _logger.LogDebug("Doc-type '{DocTypeId}' was not registered through this service", docTypeId);
            return false;
        }

        _logger.LogInformation("Unregistered custom doc-type '{DocTypeId}'", docTypeId);

        // Note: The IDocTypeRegistry interface doesn't have an Unregister method,
        // so we only track removal in our local set. The registry maintains the type
        // but we track that it's been "unregistered" for our purposes.
        return true;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetCustomDocTypeIds()
    {
        return _customDocTypeIds.Keys.ToList();
    }

    /// <inheritdoc />
    public async Task<DocTypeRegistrationResult> ValidateAsync(
        CustomDocType customDocType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customDocType);

        var errors = new List<string>();

        // Validate required fields
        if (string.IsNullOrWhiteSpace(customDocType.Id))
        {
            errors.Add("Doc-type ID is required");
        }
        else if (!IsValidDocTypeId(customDocType.Id))
        {
            errors.Add("Doc-type ID must start with a letter and contain only lowercase letters, numbers, underscores, and hyphens");
        }

        if (string.IsNullOrWhiteSpace(customDocType.Name))
        {
            errors.Add("Doc-type name is required");
        }

        // Validate schema if provided
        if (!string.IsNullOrWhiteSpace(customDocType.Schema))
        {
            var schemaValidation = await ValidateSchemaAsync(customDocType.Schema, cancellationToken);
            if (!schemaValidation.IsValid)
            {
                errors.AddRange(schemaValidation.Errors.Select(e => $"Schema: {e}"));
            }
        }

        // Validate trigger phrases
        if (customDocType.TriggerPhrases.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("Trigger phrases cannot contain empty strings");
        }

        return errors.Count > 0
            ? DocTypeRegistrationResult.Failure(errors)
            : DocTypeRegistrationResult.Success();
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        _schemaValidationCache.Clear();
        _logger.LogDebug("Schema validation cache cleared");
    }

    /// <summary>
    /// Validates that a JSON schema is well-formed.
    /// </summary>
    private async Task<DocTypeRegistrationResult> ValidateSchemaAsync(
        string schema,
        CancellationToken cancellationToken)
    {
        // Check cache first
        var schemaHash = ComputeSchemaHash(schema);
        if (_schemaValidationCache.ContainsKey(schemaHash))
        {
            return DocTypeRegistrationResult.Success();
        }

        try
        {
            // Parse the JSON to ensure it's valid JSON
            using var document = JsonDocument.Parse(schema);

            // Basic schema structure validation
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return DocTypeRegistrationResult.Failure("Schema must be a JSON object");
            }

            // Check for 'type' property (required for JSON Schema)
            if (!document.RootElement.TryGetProperty("type", out _))
            {
                return DocTypeRegistrationResult.Failure("Schema must have a 'type' property");
            }

            // Use the schema validator to test against a minimal valid object
            var testData = new Dictionary<string, object?> { ["doc_type"] = "test" };
            await _schemaValidator.ValidateAsync(testData, schema, cancellationToken);

            // Cache successful validation
            _schemaValidationCache[schemaHash] = DateTimeOffset.UtcNow;

            return DocTypeRegistrationResult.Success();
        }
        catch (JsonException ex)
        {
            return DocTypeRegistrationResult.Failure($"Invalid JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            return DocTypeRegistrationResult.Failure($"Schema validation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that a doc-type ID follows the required format.
    /// </summary>
    private static bool IsValidDocTypeId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 50)
            return false;

        // Must start with a letter
        if (!char.IsLetter(id[0]) || !char.IsLower(id[0]))
            return false;

        // Must contain only lowercase letters, numbers, underscores, and hyphens
        return id.All(c => char.IsLower(c) || char.IsDigit(c) || c == '_' || c == '-');
    }

    /// <summary>
    /// Computes a hash for caching schema validation results.
    /// </summary>
    private static string ComputeSchemaHash(string schema)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(schema);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
