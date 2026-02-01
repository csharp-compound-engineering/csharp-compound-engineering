using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompoundDocs.McpServer.Agents;

/// <summary>
/// Implementation of IAgentRegistry that loads agents from a directory.
/// </summary>
public sealed class AgentRegistry : IAgentRegistry, IDisposable
{
    private readonly AgentLoader _loader;
    private readonly ILogger<AgentRegistry> _logger;
    private readonly string _agentsDirectory;
    private bool _disposed;

    /// <summary>
    /// Creates a new instance of AgentRegistry.
    /// </summary>
    public AgentRegistry(
        AgentLoader loader,
        IOptions<AgentRegistryOptions> options,
        ILogger<AgentRegistry> logger)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var opts = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _agentsDirectory = opts.AgentsDirectory ?? AgentLoader.DefaultAgentsDirectory;
    }

    /// <inheritdoc />
    public AgentDefinition? GetAgent(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return _loader.GetAgent(name);
    }

    /// <inheritdoc />
    public IReadOnlyList<AgentDefinition> GetAllAgents()
    {
        return _loader.GetAllAgents();
    }

    /// <inheritdoc />
    public IReadOnlyList<AgentDefinition> GetAgentsByCategory(string category)
    {
        if (string.IsNullOrEmpty(category))
        {
            return [];
        }

        return _loader.GetAllAgents()
            .Where(a => a.Metadata?.Category != null &&
                        a.Metadata.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<AgentDefinition> GetAgentsBySkill(string skillName)
    {
        if (string.IsNullOrEmpty(skillName))
        {
            return [];
        }

        return _loader.GetAllAgents()
            .Where(a => a.Skills.Any(s =>
                s.Name.Contains(skillName, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<AgentDefinition> GetAgentsByTool(string toolName)
    {
        if (string.IsNullOrEmpty(toolName))
        {
            return [];
        }

        return _loader.GetAllAgents()
            .Where(a => a.McpTools.Any(t =>
                t.Name.Contains(toolName, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <inheritdoc />
    public bool AgentExists(string name)
    {
        return GetAgent(name) != null;
    }

    /// <inheritdoc />
    public int AgentCount => _loader.AgentCount;

    /// <inheritdoc />
    public bool IsInitialized => _loader.IsInitialized;

    /// <inheritdoc />
    public async Task<int> ReloadAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reloading agents from {Directory}", _agentsDirectory);
        return await _loader.ReloadAsync(_agentsDirectory, cancellationToken);
    }

    /// <summary>
    /// Initializes the registry by loading all agents.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_loader.IsInitialized)
        {
            _logger.LogDebug("Agent registry already initialized");
            return;
        }

        _logger.LogInformation("Initializing agent registry from {Directory}", _agentsDirectory);
        var count = await _loader.LoadAgentsAsync(_agentsDirectory, cancellationToken);
        _logger.LogInformation("Agent registry initialized with {Count} agents", count);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _loader.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Options for the agent registry.
/// </summary>
public sealed class AgentRegistryOptions
{
    /// <summary>
    /// Path to the agents directory.
    /// </summary>
    public string? AgentsDirectory { get; set; }
}
