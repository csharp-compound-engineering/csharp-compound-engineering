using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NJsonSchema;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CompoundDocs.McpServer.Agents;

/// <summary>
/// Loads and parses agent YAML files with schema validation and caching.
/// </summary>
public sealed class AgentLoader : IDisposable
{
    private readonly ILogger<AgentLoader> _logger;
    private readonly IDeserializer _yamlDeserializer;
    private readonly ConcurrentDictionary<string, AgentDefinition> _agentCache = new(StringComparer.OrdinalIgnoreCase);
    private JsonSchema? _schema;
    private bool _isInitialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    /// <summary>
    /// Default agents directory name.
    /// </summary>
    public const string DefaultAgentsDirectory = "agents";

    /// <summary>
    /// Default schema file name.
    /// </summary>
    public const string SchemaFileName = "agent-schema.json";

    /// <summary>
    /// Creates a new instance of AgentLoader.
    /// </summary>
    public AgentLoader(ILogger<AgentLoader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Loads all agents from the specified directory.
    /// </summary>
    /// <param name="agentsDirectory">Path to the agents directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of agents successfully loaded.</returns>
    public async Task<int> LoadAgentsAsync(string agentsDirectory, CancellationToken cancellationToken = default)
    {
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (!Directory.Exists(agentsDirectory))
            {
                _logger.LogWarning("Agents directory not found: {Directory}", agentsDirectory);
                return 0;
            }

            // Load schema for validation
            await LoadSchemaAsync(agentsDirectory, cancellationToken);

            // Clear existing cache
            _agentCache.Clear();

            // Find all YAML agent files
            var agentFiles = Directory.GetFiles(agentsDirectory, "*.yaml", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(agentsDirectory, "*.yml", SearchOption.TopDirectoryOnly))
                .Where(f => !Path.GetFileName(f).Equals(SchemaFileName, StringComparison.OrdinalIgnoreCase)
                         && !Path.GetFileName(f).EndsWith("-schema.json", StringComparison.OrdinalIgnoreCase));

            var loadedCount = 0;
            foreach (var filePath in agentFiles)
            {
                try
                {
                    var agent = await LoadAgentFileAsync(filePath, cancellationToken);
                    if (agent != null)
                    {
                        CacheAgent(agent);
                        loadedCount++;
                        _logger.LogDebug("Loaded agent: {AgentName} from {FilePath}", agent.Name, filePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load agent from {FilePath}", filePath);
                }
            }

            _isInitialized = true;
            _logger.LogInformation("Loaded {Count} agents from {Directory}", loadedCount, agentsDirectory);
            return loadedCount;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Loads a single agent file.
    /// </summary>
    private async Task<AgentDefinition?> LoadAgentFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);

        // Skip YAML frontmatter delimiter if present
        if (content.StartsWith("---"))
        {
            var endIndex = content.IndexOf("\n---", 3, StringComparison.Ordinal);
            if (endIndex > 0)
            {
                // Extract YAML content between delimiters
                content = content.Substring(4, endIndex - 4);
            }
            else
            {
                // Single --- at the start, just skip it
                content = content[4..];
            }
        }

        // Remove yaml-language-server comment line if present
        var lines = content.Split('\n');
        var filteredLines = lines.Where(l => !l.TrimStart().StartsWith("# yaml-language-server"));
        content = string.Join('\n', filteredLines);

        // Validate against schema if available
        if (_schema != null)
        {
            var validationResult = await ValidateYamlAgainstSchemaAsync(content, filePath);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning(
                    "Agent file {FilePath} failed schema validation: {Errors}",
                    filePath,
                    string.Join(", ", validationResult.Errors));
                // Continue loading despite validation errors to be lenient
            }
        }

        var agent = _yamlDeserializer.Deserialize<AgentDefinition>(content);
        if (string.IsNullOrEmpty(agent?.Name))
        {
            _logger.LogWarning("Agent file {FilePath} has no name defined", filePath);
            return null;
        }

        // Validate required fields
        if (string.IsNullOrEmpty(agent.Purpose))
        {
            _logger.LogWarning("Agent {AgentName} in {FilePath} has no purpose defined", agent.Name, filePath);
        }

        if (agent.McpTools.Count == 0)
        {
            _logger.LogWarning("Agent {AgentName} in {FilePath} has no MCP tools defined", agent.Name, filePath);
        }

        if (agent.Skills.Count == 0)
        {
            _logger.LogWarning("Agent {AgentName} in {FilePath} has no skills defined", agent.Name, filePath);
        }

        return agent;
    }

    /// <summary>
    /// Loads the JSON schema for validation.
    /// </summary>
    private async Task LoadSchemaAsync(string agentsDirectory, CancellationToken cancellationToken)
    {
        var schemaPath = Path.Combine(agentsDirectory, SchemaFileName);
        if (!File.Exists(schemaPath))
        {
            _logger.LogDebug("Schema file not found at {Path}, skipping validation", schemaPath);
            return;
        }

        try
        {
            var schemaContent = await File.ReadAllTextAsync(schemaPath, cancellationToken);
            _schema = await JsonSchema.FromJsonAsync(schemaContent, cancellationToken);
            _logger.LogDebug("Loaded agent schema from {Path}", schemaPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load agent schema from {Path}", schemaPath);
        }
    }

    /// <summary>
    /// Validates YAML content against the JSON schema.
    /// </summary>
    private async Task<AgentValidationResult> ValidateYamlAgainstSchemaAsync(string yamlContent, string filePath)
    {
        if (_schema == null)
        {
            return AgentValidationResult.Valid();
        }

        try
        {
            // Convert YAML to JSON for schema validation
            var deserializer = new DeserializerBuilder().Build();
            var yamlObject = deserializer.Deserialize<object>(yamlContent);
            var jsonContent = JsonSerializer.Serialize(yamlObject);

            var errors = _schema.Validate(jsonContent);
            if (errors.Count > 0)
            {
                return AgentValidationResult.Invalid(
                    errors.Select(e => $"{e.Path}: {e.Kind}"));
            }

            return AgentValidationResult.Valid();
        }
        catch (Exception ex)
        {
            return AgentValidationResult.Invalid($"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Caches an agent.
    /// </summary>
    private void CacheAgent(AgentDefinition agent)
    {
        _agentCache[agent.Name] = agent;
    }

    /// <summary>
    /// Gets an agent by name.
    /// </summary>
    /// <param name="name">Agent name.</param>
    /// <returns>The agent definition, or null if not found.</returns>
    public AgentDefinition? GetAgent(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        _agentCache.TryGetValue(name, out var agent);
        return agent;
    }

    /// <summary>
    /// Gets all loaded agents.
    /// </summary>
    public IReadOnlyList<AgentDefinition> GetAllAgents()
    {
        return _agentCache.Values
            .OrderBy(a => a.Name)
            .ToList();
    }

    /// <summary>
    /// Reloads all agents from the directory.
    /// </summary>
    public async Task<int> ReloadAsync(string agentsDirectory, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reloading agents from {Directory}", agentsDirectory);
        return await LoadAgentsAsync(agentsDirectory, cancellationToken);
    }

    /// <summary>
    /// Whether the loader has been initialized.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Number of loaded agents.
    /// </summary>
    public int AgentCount => _agentCache.Count;

    /// <inheritdoc />
    public void Dispose()
    {
        _initLock.Dispose();
    }
}

/// <summary>
/// Result of agent YAML validation.
/// </summary>
public sealed class AgentValidationResult
{
    /// <summary>
    /// Whether the validation passed.
    /// </summary>
    public bool IsValid { get; private init; }

    /// <summary>
    /// Validation errors if any.
    /// </summary>
    public IReadOnlyList<string> Errors { get; private init; } = [];

    /// <summary>
    /// Creates a valid result.
    /// </summary>
    public static AgentValidationResult Valid() => new() { IsValid = true };

    /// <summary>
    /// Creates an invalid result with errors.
    /// </summary>
    public static AgentValidationResult Invalid(IEnumerable<string> errors) =>
        new() { IsValid = false, Errors = errors.ToList() };

    /// <summary>
    /// Creates an invalid result with a single error.
    /// </summary>
    public static AgentValidationResult Invalid(string error) =>
        new() { IsValid = false, Errors = [error] };
}
