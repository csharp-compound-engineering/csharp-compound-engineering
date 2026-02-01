namespace CompoundDocs.McpServer.Agents;

/// <summary>
/// Interface for the agent registry that manages research agent definitions.
/// </summary>
public interface IAgentRegistry
{
    /// <summary>
    /// Gets an agent by name.
    /// </summary>
    /// <param name="name">The agent name (e.g., "best-practices-researcher").</param>
    /// <returns>The agent definition, or null if not found.</returns>
    AgentDefinition? GetAgent(string name);

    /// <summary>
    /// Gets all registered agents.
    /// </summary>
    /// <returns>A read-only list of all agent definitions.</returns>
    IReadOnlyList<AgentDefinition> GetAllAgents();

    /// <summary>
    /// Gets agents by category.
    /// </summary>
    /// <param name="category">The category to filter by (e.g., "research", "analysis").</param>
    /// <returns>A read-only list of agents in the specified category.</returns>
    IReadOnlyList<AgentDefinition> GetAgentsByCategory(string category);

    /// <summary>
    /// Gets agents that have a specific skill.
    /// </summary>
    /// <param name="skillName">The skill name to search for.</param>
    /// <returns>A read-only list of agents with the specified skill.</returns>
    IReadOnlyList<AgentDefinition> GetAgentsBySkill(string skillName);

    /// <summary>
    /// Gets agents that use a specific MCP tool.
    /// </summary>
    /// <param name="toolName">The MCP tool name to search for.</param>
    /// <returns>A read-only list of agents that use the specified tool.</returns>
    IReadOnlyList<AgentDefinition> GetAgentsByTool(string toolName);

    /// <summary>
    /// Checks if an agent exists.
    /// </summary>
    /// <param name="name">The agent name.</param>
    /// <returns>True if the agent exists, false otherwise.</returns>
    bool AgentExists(string name);

    /// <summary>
    /// Gets the number of registered agents.
    /// </summary>
    int AgentCount { get; }

    /// <summary>
    /// Indicates whether the registry has been initialized.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Reloads all agents from the agents directory.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of agents loaded.</returns>
    Task<int> ReloadAsync(CancellationToken cancellationToken = default);
}
