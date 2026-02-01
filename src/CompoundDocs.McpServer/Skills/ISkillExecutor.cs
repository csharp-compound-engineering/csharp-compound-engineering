namespace CompoundDocs.McpServer.Skills;

/// <summary>
/// Interface for skill execution.
/// </summary>
public interface ISkillExecutor
{
    /// <summary>
    /// Executes a skill with the given parameters.
    /// </summary>
    /// <param name="skillName">The skill name (with or without /cdocs: prefix).</param>
    /// <param name="parameters">Parameters to pass to the skill.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The skill execution result.</returns>
    Task<SkillExecutionResult> ExecuteAsync(
        string skillName,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a skill definition by name.
    /// </summary>
    /// <param name="skillName">The skill name (with or without /cdocs: prefix).</param>
    /// <returns>The skill definition, or null if not found.</returns>
    Task<SkillDefinition?> GetSkillByNameAsync(string skillName);

    /// <summary>
    /// Gets all available skills.
    /// </summary>
    /// <returns>List of all loaded skill definitions.</returns>
    Task<IReadOnlyList<SkillDefinition>> GetAllSkillsAsync();

    /// <summary>
    /// Finds skills matching a trigger phrase.
    /// </summary>
    /// <param name="triggerPhrase">The phrase to match against skill triggers.</param>
    /// <returns>List of matching skill definitions.</returns>
    Task<IReadOnlyList<SkillDefinition>> FindSkillsByTriggerAsync(string triggerPhrase);

    /// <summary>
    /// Validates parameters for a skill without executing it.
    /// </summary>
    /// <param name="skillName">The skill name.</param>
    /// <param name="parameters">Parameters to validate.</param>
    /// <returns>Validation result with any errors.</returns>
    Task<SkillValidationResult> ValidateParametersAsync(
        string skillName,
        Dictionary<string, object?> parameters);
}

/// <summary>
/// Result of a skill execution.
/// </summary>
public sealed class SkillExecutionResult
{
    /// <summary>
    /// Whether the skill executed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The skill that was executed.
    /// </summary>
    public SkillDefinition? Skill { get; init; }

    /// <summary>
    /// The generated output content (e.g., markdown document).
    /// </summary>
    public string? OutputContent { get; init; }

    /// <summary>
    /// The file path where output was written, if applicable.
    /// </summary>
    public string? OutputFilePath { get; init; }

    /// <summary>
    /// Results from individual tool calls.
    /// </summary>
    public List<ToolCallResult> ToolResults { get; init; } = [];

    /// <summary>
    /// Error message if execution failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Error code for programmatic error handling.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static SkillExecutionResult Ok(
        SkillDefinition skill,
        string? outputContent = null,
        string? outputFilePath = null,
        List<ToolCallResult>? toolResults = null)
    {
        return new SkillExecutionResult
        {
            Success = true,
            Skill = skill,
            OutputContent = outputContent,
            OutputFilePath = outputFilePath,
            ToolResults = toolResults ?? []
        };
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static SkillExecutionResult Fail(string error, string? errorCode = null)
    {
        return new SkillExecutionResult
        {
            Success = false,
            Error = error,
            ErrorCode = errorCode
        };
    }

    /// <summary>
    /// Creates a failed result for a skill not found error.
    /// </summary>
    public static SkillExecutionResult SkillNotFound(string skillName)
    {
        return new SkillExecutionResult
        {
            Success = false,
            Error = $"Skill not found: {skillName}",
            ErrorCode = "SKILL_NOT_FOUND"
        };
    }

    /// <summary>
    /// Creates a failed result for validation errors.
    /// </summary>
    public static SkillExecutionResult ValidationFailed(IEnumerable<string> errors)
    {
        return new SkillExecutionResult
        {
            Success = false,
            Error = string.Join("; ", errors),
            ErrorCode = "VALIDATION_FAILED"
        };
    }
}

/// <summary>
/// Result of a single tool call within a skill execution.
/// </summary>
public sealed class ToolCallResult
{
    /// <summary>
    /// The tool that was called.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Whether the tool call succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The result data from the tool.
    /// </summary>
    public object? Result { get; init; }

    /// <summary>
    /// Error message if the tool call failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// The variable name this result was stored in (if any).
    /// </summary>
    public string? ResultVariable { get; init; }
}

/// <summary>
/// Result of parameter validation.
/// </summary>
public sealed class SkillValidationResult
{
    /// <summary>
    /// Whether validation passed.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// List of validation errors.
    /// </summary>
    public List<string> Errors { get; init; } = [];

    /// <summary>
    /// List of validation warnings (non-blocking).
    /// </summary>
    public List<string> Warnings { get; init; } = [];

    /// <summary>
    /// Creates a valid result.
    /// </summary>
    public static SkillValidationResult Valid() => new() { IsValid = true };

    /// <summary>
    /// Creates an invalid result with errors.
    /// </summary>
    public static SkillValidationResult Invalid(params string[] errors) => new()
    {
        IsValid = false,
        Errors = [.. errors]
    };

    /// <summary>
    /// Creates an invalid result with errors.
    /// </summary>
    public static SkillValidationResult Invalid(IEnumerable<string> errors) => new()
    {
        IsValid = false,
        Errors = errors.ToList()
    };
}
