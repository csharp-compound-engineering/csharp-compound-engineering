using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace CompoundDocs.McpServer.Agents;

/// <summary>
/// Represents a research agent definition loaded from YAML.
/// </summary>
public sealed class AgentDefinition
{
    /// <summary>
    /// Unique agent identifier (e.g., "best-practices-researcher").
    /// </summary>
    [YamlMember(Alias = "name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of what the agent does.
    /// </summary>
    [YamlMember(Alias = "description")]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Semantic version of the agent definition.
    /// </summary>
    [YamlMember(Alias = "version")]
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Detailed purpose and use cases for the agent.
    /// </summary>
    [YamlMember(Alias = "purpose")]
    [JsonPropertyName("purpose")]
    public string Purpose { get; set; } = string.Empty;

    /// <summary>
    /// MCP tools the agent can use.
    /// </summary>
    [YamlMember(Alias = "mcp_tools")]
    [JsonPropertyName("mcp_tools")]
    public List<AgentMcpTool> McpTools { get; set; } = [];

    /// <summary>
    /// Skills and capabilities of the agent.
    /// </summary>
    [YamlMember(Alias = "skills")]
    [JsonPropertyName("skills")]
    public List<AgentSkill> Skills { get; set; } = [];

    /// <summary>
    /// System and task prompts for the agent.
    /// </summary>
    [YamlMember(Alias = "prompts")]
    [JsonPropertyName("prompts")]
    public AgentPrompts? Prompts { get; set; }

    /// <summary>
    /// Agent-specific configuration options.
    /// </summary>
    [YamlMember(Alias = "configuration")]
    [JsonPropertyName("configuration")]
    public AgentConfiguration? Configuration { get; set; }

    /// <summary>
    /// Additional metadata for the agent.
    /// </summary>
    [YamlMember(Alias = "metadata")]
    [JsonPropertyName("metadata")]
    public AgentMetadata? Metadata { get; set; }
}

/// <summary>
/// Represents an MCP tool that an agent can use.
/// </summary>
public sealed class AgentMcpTool
{
    /// <summary>
    /// Name of the MCP tool.
    /// </summary>
    [YamlMember(Alias = "name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// How the agent uses this tool.
    /// </summary>
    [YamlMember(Alias = "description")]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// MCP server or provider name (e.g., "context7", "cdocs", "git").
    /// </summary>
    [YamlMember(Alias = "provider")]
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "cdocs";

    /// <summary>
    /// Whether this tool is required for the agent to function.
    /// </summary>
    [YamlMember(Alias = "required")]
    [JsonPropertyName("required")]
    public bool Required { get; set; } = true;

    /// <summary>
    /// Default or suggested parameter values.
    /// </summary>
    [YamlMember(Alias = "parameters")]
    [JsonPropertyName("parameters")]
    public Dictionary<string, object?> Parameters { get; set; } = [];
}

/// <summary>
/// Represents a skill or capability of an agent.
/// </summary>
public sealed class AgentSkill
{
    /// <summary>
    /// Skill name.
    /// </summary>
    [YamlMember(Alias = "name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// What this skill enables the agent to do.
    /// </summary>
    [YamlMember(Alias = "description")]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Example use cases for this skill.
    /// </summary>
    [YamlMember(Alias = "examples")]
    [JsonPropertyName("examples")]
    public List<string> Examples { get; set; } = [];
}

/// <summary>
/// Represents prompts for an agent.
/// </summary>
public sealed class AgentPrompts
{
    /// <summary>
    /// System prompt defining the agent's role and behavior.
    /// </summary>
    [YamlMember(Alias = "system")]
    [JsonPropertyName("system")]
    public string? System { get; set; }

    /// <summary>
    /// Templates for common task types.
    /// </summary>
    [YamlMember(Alias = "task_templates")]
    [JsonPropertyName("task_templates")]
    public Dictionary<string, string> TaskTemplates { get; set; } = [];
}

/// <summary>
/// Represents agent-specific configuration options.
/// </summary>
public sealed class AgentConfiguration
{
    /// <summary>
    /// Maximum number of tool call iterations.
    /// </summary>
    [YamlMember(Alias = "max_iterations")]
    [JsonPropertyName("max_iterations")]
    public int MaxIterations { get; set; } = 10;

    /// <summary>
    /// Timeout for agent operations in seconds.
    /// </summary>
    [YamlMember(Alias = "timeout_seconds")]
    [JsonPropertyName("timeout_seconds")]
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Whether to cache research results.
    /// </summary>
    [YamlMember(Alias = "cache_results")]
    [JsonPropertyName("cache_results")]
    public bool CacheResults { get; set; } = true;
}

/// <summary>
/// Represents metadata for an agent.
/// </summary>
public sealed class AgentMetadata
{
    /// <summary>
    /// Author of the agent.
    /// </summary>
    [YamlMember(Alias = "author")]
    [JsonPropertyName("author")]
    public string? Author { get; set; }

    /// <summary>
    /// Tags for categorization.
    /// </summary>
    [YamlMember(Alias = "tags")]
    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Category of the agent.
    /// </summary>
    [YamlMember(Alias = "category")]
    [JsonPropertyName("category")]
    public string? Category { get; set; }
}
