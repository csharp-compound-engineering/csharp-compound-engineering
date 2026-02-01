using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace CompoundDocs.McpServer.Skills;

/// <summary>
/// Represents a skill definition loaded from YAML.
/// </summary>
public sealed class SkillDefinition
{
    /// <summary>
    /// Unique skill identifier with /cdocs: prefix (e.g., /cdocs:capture-problem).
    /// </summary>
    [YamlMember(Alias = "name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of what the skill does.
    /// </summary>
    [YamlMember(Alias = "description")]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Semantic version of the skill.
    /// </summary>
    [YamlMember(Alias = "version")]
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Phrases that trigger this skill.
    /// </summary>
    [YamlMember(Alias = "triggers")]
    [JsonPropertyName("triggers")]
    public List<string> Triggers { get; set; } = [];

    /// <summary>
    /// Parameters accepted by the skill.
    /// </summary>
    [YamlMember(Alias = "parameters")]
    [JsonPropertyName("parameters")]
    public List<SkillParameter> Parameters { get; set; } = [];

    /// <summary>
    /// MCP tool calls to execute.
    /// </summary>
    [YamlMember(Alias = "tool_calls")]
    [JsonPropertyName("tool_calls")]
    public List<SkillToolCall> ToolCalls { get; set; } = [];

    /// <summary>
    /// Output configuration for the skill.
    /// </summary>
    [YamlMember(Alias = "output")]
    [JsonPropertyName("output")]
    public SkillOutput? Output { get; set; }

    /// <summary>
    /// Additional metadata for the skill.
    /// </summary>
    [YamlMember(Alias = "metadata")]
    [JsonPropertyName("metadata")]
    public SkillMetadata? Metadata { get; set; }

    /// <summary>
    /// Gets the short name without the /cdocs: prefix.
    /// </summary>
    [YamlIgnore]
    [JsonIgnore]
    public string ShortName => Name.StartsWith("/cdocs:", StringComparison.OrdinalIgnoreCase)
        ? Name[7..]
        : Name;
}

/// <summary>
/// Represents a parameter definition for a skill.
/// </summary>
public sealed class SkillParameter
{
    /// <summary>
    /// Parameter name (used in templates as {{name}}).
    /// </summary>
    [YamlMember(Alias = "name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Parameter data type.
    /// </summary>
    [YamlMember(Alias = "type")]
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    /// <summary>
    /// Human-readable parameter description.
    /// </summary>
    [YamlMember(Alias = "description")]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether the parameter is required.
    /// </summary>
    [YamlMember(Alias = "required")]
    [JsonPropertyName("required")]
    public bool Required { get; set; }

    /// <summary>
    /// Default value if not provided.
    /// </summary>
    [YamlMember(Alias = "default")]
    [JsonPropertyName("default")]
    public object? Default { get; set; }

    /// <summary>
    /// Validation rules for the parameter.
    /// </summary>
    [YamlMember(Alias = "validation")]
    [JsonPropertyName("validation")]
    public ParameterValidation? Validation { get; set; }
}

/// <summary>
/// Represents validation rules for a skill parameter.
/// </summary>
public sealed class ParameterValidation
{
    /// <summary>
    /// Regex pattern for validation.
    /// </summary>
    [YamlMember(Alias = "pattern")]
    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    /// <summary>
    /// Minimum length for string values.
    /// </summary>
    [YamlMember(Alias = "min_length")]
    [JsonPropertyName("min_length")]
    public int? MinLength { get; set; }

    /// <summary>
    /// Maximum length for string values.
    /// </summary>
    [YamlMember(Alias = "max_length")]
    [JsonPropertyName("max_length")]
    public int? MaxLength { get; set; }

    /// <summary>
    /// Minimum value for numeric types.
    /// </summary>
    [YamlMember(Alias = "min")]
    [JsonPropertyName("min")]
    public double? Min { get; set; }

    /// <summary>
    /// Maximum value for numeric types.
    /// </summary>
    [YamlMember(Alias = "max")]
    [JsonPropertyName("max")]
    public double? Max { get; set; }

    /// <summary>
    /// Allowed values (enum constraint).
    /// </summary>
    [YamlMember(Alias = "enum")]
    [JsonPropertyName("enum")]
    public List<string>? AllowedValues { get; set; }
}

/// <summary>
/// Represents an MCP tool call within a skill.
/// </summary>
public sealed class SkillToolCall
{
    /// <summary>
    /// Name of the MCP tool to call.
    /// </summary>
    [YamlMember(Alias = "tool")]
    [JsonPropertyName("tool")]
    public string Tool { get; set; } = string.Empty;

    /// <summary>
    /// Arguments to pass to the tool (supports {{parameter}} templates).
    /// </summary>
    [YamlMember(Alias = "arguments")]
    [JsonPropertyName("arguments")]
    public Dictionary<string, object?> Arguments { get; set; } = [];

    /// <summary>
    /// Optional condition for executing this tool call.
    /// </summary>
    [YamlMember(Alias = "condition")]
    [JsonPropertyName("condition")]
    public string? Condition { get; set; }

    /// <summary>
    /// Variable name to store the result for use in subsequent calls.
    /// </summary>
    [YamlMember(Alias = "result_variable")]
    [JsonPropertyName("result_variable")]
    public string? ResultVariable { get; set; }
}

/// <summary>
/// Represents output configuration for a skill.
/// </summary>
public sealed class SkillOutput
{
    /// <summary>
    /// Output format (json, markdown, text).
    /// </summary>
    [YamlMember(Alias = "format")]
    [JsonPropertyName("format")]
    public string Format { get; set; } = "json";

    /// <summary>
    /// Template for formatting output (supports {{variable}} placeholders).
    /// </summary>
    [YamlMember(Alias = "template")]
    [JsonPropertyName("template")]
    public string? Template { get; set; }

    /// <summary>
    /// Whether to include raw tool results.
    /// </summary>
    [YamlMember(Alias = "include_tool_results")]
    [JsonPropertyName("include_tool_results")]
    public bool IncludeToolResults { get; set; } = true;
}

/// <summary>
/// Represents metadata for a skill.
/// </summary>
public sealed class SkillMetadata
{
    /// <summary>
    /// Author of the skill.
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
    /// Category of the skill.
    /// </summary>
    [YamlMember(Alias = "category")]
    [JsonPropertyName("category")]
    public string? Category { get; set; }
}
