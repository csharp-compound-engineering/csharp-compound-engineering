using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Skills;

/// <summary>
/// Executes skills by processing templates and invoking tool calls.
/// </summary>
public sealed partial class SkillExecutor : ISkillExecutor
{
    private readonly SkillLoader _skillLoader;
    private readonly ILogger<SkillExecutor> _logger;
    private readonly string _skillsDirectory;

    /// <summary>
    /// Regex pattern for template parameter substitution.
    /// Matches {{parameter_name}} patterns.
    /// </summary>
    [GeneratedRegex(@"\{\{([a-zA-Z_][a-zA-Z0-9_]*)\}\}", RegexOptions.Compiled)]
    private static partial Regex TemplateParameterRegex();

    /// <summary>
    /// Regex pattern for conditional blocks.
    /// Matches {{#if parameter}}...{{/if}} patterns.
    /// </summary>
    [GeneratedRegex(@"\{\{#if\s+([a-zA-Z_][a-zA-Z0-9_]*)\}\}(.*?)\{\{/if\}\}", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex ConditionalBlockRegex();

    /// <summary>
    /// Regex pattern for each loops.
    /// Matches {{#each array}}...{{/each}} patterns.
    /// </summary>
    [GeneratedRegex(@"\{\{#each\s+([a-zA-Z_][a-zA-Z0-9_]*)\}\}(.*?)\{\{/each\}\}", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex EachBlockRegex();

    /// <summary>
    /// Creates a new instance of SkillExecutor.
    /// </summary>
    public SkillExecutor(
        SkillLoader skillLoader,
        ILogger<SkillExecutor> logger,
        string? skillsDirectory = null)
    {
        _skillLoader = skillLoader ?? throw new ArgumentNullException(nameof(skillLoader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _skillsDirectory = skillsDirectory ?? SkillLoader.DefaultSkillsDirectory;
    }

    /// <inheritdoc />
    public async Task<SkillExecutionResult> ExecuteAsync(
        string skillName,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        // Ensure skills are loaded
        await EnsureInitializedAsync(cancellationToken);

        var skill = _skillLoader.GetSkill(skillName);
        if (skill == null)
        {
            _logger.LogWarning("Skill not found: {SkillName}", skillName);
            return SkillExecutionResult.SkillNotFound(skillName);
        }

        _logger.LogInformation("Executing skill: {SkillName}", skill.Name);

        // Validate parameters
        var validation = ValidateParameters(skill, parameters);
        if (!validation.IsValid)
        {
            _logger.LogWarning(
                "Parameter validation failed for skill {SkillName}: {Errors}",
                skill.Name,
                string.Join(", ", validation.Errors));
            return SkillExecutionResult.ValidationFailed(validation.Errors);
        }

        // Apply default values for missing optional parameters
        var effectiveParameters = ApplyDefaults(skill, parameters);

        // Add built-in parameters
        AddBuiltInParameters(effectiveParameters);

        try
        {
            // Generate output content from template
            string? outputContent = null;
            if (skill.Output?.Template != null)
            {
                outputContent = ProcessTemplate(skill.Output.Template, effectiveParameters);
            }

            // Execute tool calls
            var toolResults = new List<ToolCallResult>();
            foreach (var toolCall in skill.ToolCalls)
            {
                // Check condition if specified
                if (!string.IsNullOrEmpty(toolCall.Condition) &&
                    !EvaluateCondition(toolCall.Condition, effectiveParameters))
                {
                    _logger.LogDebug(
                        "Skipping tool call {Tool} due to condition: {Condition}",
                        toolCall.Tool,
                        toolCall.Condition);
                    continue;
                }

                // Substitute parameters in arguments
                var processedArguments = ProcessArguments(toolCall.Arguments, effectiveParameters);

                // Tool calls are prepared but not executed here.
                // The MCP server framework invokes the actual tools based on the prepared call info.
                // This executor provides the skill orchestration layer.
                var toolResult = new ToolCallResult
                {
                    ToolName = toolCall.Tool,
                    Success = true, // Indicates successful preparation; actual execution happens at MCP layer
                    Result = processedArguments,
                    ResultVariable = toolCall.ResultVariable
                };

                toolResults.Add(toolResult);

                // Store result for subsequent calls
                if (!string.IsNullOrEmpty(toolCall.ResultVariable))
                {
                    effectiveParameters[toolCall.ResultVariable] = toolResult.Result;
                }

                _logger.LogDebug(
                    "Tool call prepared: {Tool} with arguments: {Arguments}",
                    toolCall.Tool,
                    processedArguments);
            }

            _logger.LogInformation(
                "Skill {SkillName} executed successfully with {ToolCount} tool calls",
                skill.Name,
                toolResults.Count);

            return SkillExecutionResult.Ok(
                skill,
                outputContent,
                outputFilePath: null,
                toolResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing skill {SkillName}", skill.Name);
            return SkillExecutionResult.Fail(
                $"Skill execution failed: {ex.Message}",
                "EXECUTION_ERROR");
        }
    }

    /// <inheritdoc />
    public async Task<SkillDefinition?> GetSkillByNameAsync(string skillName)
    {
        await EnsureInitializedAsync(CancellationToken.None);
        return _skillLoader.GetSkill(skillName);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SkillDefinition>> GetAllSkillsAsync()
    {
        await EnsureInitializedAsync(CancellationToken.None);
        return _skillLoader.GetAllSkills();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SkillDefinition>> FindSkillsByTriggerAsync(string triggerPhrase)
    {
        await EnsureInitializedAsync(CancellationToken.None);
        return _skillLoader.FindByTrigger(triggerPhrase);
    }

    /// <inheritdoc />
    public async Task<SkillValidationResult> ValidateParametersAsync(
        string skillName,
        Dictionary<string, object?> parameters)
    {
        await EnsureInitializedAsync(CancellationToken.None);

        var skill = _skillLoader.GetSkill(skillName);
        if (skill == null)
        {
            return SkillValidationResult.Invalid($"Skill not found: {skillName}");
        }

        return ValidateParameters(skill, parameters);
    }

    /// <summary>
    /// Ensures the skill loader has been initialized.
    /// </summary>
    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (!_skillLoader.IsInitialized)
        {
            await _skillLoader.LoadSkillsAsync(_skillsDirectory, cancellationToken);
        }
    }

    /// <summary>
    /// Validates parameters against skill definition.
    /// </summary>
    private static SkillValidationResult ValidateParameters(
        SkillDefinition skill,
        Dictionary<string, object?> parameters)
    {
        var errors = new List<string>();

        foreach (var param in skill.Parameters)
        {
            var hasValue = parameters.TryGetValue(param.Name, out var value) &&
                          value != null &&
                          (value is not string strValue || !string.IsNullOrEmpty(strValue));

            // Check required parameters
            if (param.Required && !hasValue)
            {
                errors.Add($"Required parameter '{param.Name}' is missing");
                continue;
            }

            if (!hasValue)
            {
                continue;
            }

            // Validate string constraints
            if (param.Type == "string" && value is string stringValue && param.Validation != null)
            {
                if (param.Validation.MinLength.HasValue && stringValue.Length < param.Validation.MinLength.Value)
                {
                    errors.Add($"Parameter '{param.Name}' must be at least {param.Validation.MinLength} characters");
                }

                if (param.Validation.MaxLength.HasValue && stringValue.Length > param.Validation.MaxLength.Value)
                {
                    errors.Add($"Parameter '{param.Name}' must be at most {param.Validation.MaxLength} characters");
                }

                if (param.Validation.Pattern != null)
                {
                    var regex = new Regex(param.Validation.Pattern);
                    if (!regex.IsMatch(stringValue))
                    {
                        errors.Add($"Parameter '{param.Name}' does not match required pattern");
                    }
                }

                if (param.Validation.AllowedValues != null &&
                    !param.Validation.AllowedValues.Contains(stringValue, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"Parameter '{param.Name}' must be one of: {string.Join(", ", param.Validation.AllowedValues)}");
                }
            }

            // Validate numeric constraints
            if ((param.Type == "integer" || param.Type == "number") && param.Validation != null)
            {
                if (double.TryParse(value?.ToString(), out var numValue))
                {
                    if (param.Validation.Min.HasValue && numValue < param.Validation.Min.Value)
                    {
                        errors.Add($"Parameter '{param.Name}' must be at least {param.Validation.Min}");
                    }

                    if (param.Validation.Max.HasValue && numValue > param.Validation.Max.Value)
                    {
                        errors.Add($"Parameter '{param.Name}' must be at most {param.Validation.Max}");
                    }
                }
            }
        }

        return errors.Count > 0
            ? SkillValidationResult.Invalid(errors)
            : SkillValidationResult.Valid();
    }

    /// <summary>
    /// Applies default values for missing optional parameters.
    /// </summary>
    private static Dictionary<string, object?> ApplyDefaults(
        SkillDefinition skill,
        Dictionary<string, object?> parameters)
    {
        var result = new Dictionary<string, object?>(parameters, StringComparer.OrdinalIgnoreCase);

        foreach (var param in skill.Parameters)
        {
            if (!result.ContainsKey(param.Name) && param.Default != null)
            {
                result[param.Name] = param.Default;
            }
        }

        return result;
    }

    /// <summary>
    /// Adds built-in parameters like timestamp.
    /// </summary>
    private static void AddBuiltInParameters(Dictionary<string, object?> parameters)
    {
        parameters["timestamp"] = DateTimeOffset.UtcNow.ToString("o");
        parameters["date"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        parameters["generated_file_path"] = $"docs/{Guid.NewGuid():N}.md";
    }

    /// <summary>
    /// Processes a template string with parameter substitution.
    /// </summary>
    private string ProcessTemplate(string template, Dictionary<string, object?> parameters)
    {
        var result = template;

        // Process {{#each}} blocks first
        result = EachBlockRegex().Replace(result, match =>
        {
            var arrayName = match.Groups[1].Value;
            var blockContent = match.Groups[2].Value;

            if (!parameters.TryGetValue(arrayName, out var arrayValue) || arrayValue == null)
            {
                return string.Empty;
            }

            var items = arrayValue as IEnumerable<object?>;
            if (items == null && arrayValue is string strArray)
            {
                // Handle single string as array of one
                items = [strArray];
            }

            if (items == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            var index = 0;
            foreach (var item in items)
            {
                var itemResult = blockContent
                    .Replace("{{this}}", item?.ToString() ?? string.Empty)
                    .Replace("{{@index}}", index.ToString());
                sb.Append(itemResult);
                index++;
            }

            return sb.ToString();
        });

        // Process {{#if}} blocks
        result = ConditionalBlockRegex().Replace(result, match =>
        {
            var paramName = match.Groups[1].Value;
            var blockContent = match.Groups[2].Value;

            if (parameters.TryGetValue(paramName, out var value) && HasValue(value))
            {
                return ProcessTemplate(blockContent, parameters);
            }

            return string.Empty;
        });

        // Process simple {{parameter}} substitutions
        result = TemplateParameterRegex().Replace(result, match =>
        {
            var paramName = match.Groups[1].Value;
            if (parameters.TryGetValue(paramName, out var value))
            {
                return FormatValue(value);
            }
            return match.Value; // Keep original if not found
        });

        return result;
    }

    /// <summary>
    /// Processes tool call arguments with parameter substitution.
    /// </summary>
    private Dictionary<string, object?> ProcessArguments(
        Dictionary<string, object?> arguments,
        Dictionary<string, object?> parameters)
    {
        var result = new Dictionary<string, object?>();

        foreach (var (key, value) in arguments)
        {
            if (value is string stringValue)
            {
                result[key] = ProcessTemplate(stringValue, parameters);
            }
            else
            {
                result[key] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// Evaluates a simple condition expression.
    /// </summary>
    private static bool EvaluateCondition(string condition, Dictionary<string, object?> parameters)
    {
        // Simple implementation: check if parameter exists and has a truthy value
        var trimmedCondition = condition.Trim();

        // Handle negation
        if (trimmedCondition.StartsWith('!'))
        {
            var paramName = trimmedCondition[1..].Trim();
            return !parameters.TryGetValue(paramName, out var value) || !HasValue(value);
        }

        // Handle equality comparison (param == value)
        if (trimmedCondition.Contains("=="))
        {
            var parts = trimmedCondition.Split("==", 2);
            var paramName = parts[0].Trim();
            var expectedValue = parts[1].Trim().Trim('"', '\'');

            if (parameters.TryGetValue(paramName, out var actualValue))
            {
                return string.Equals(actualValue?.ToString(), expectedValue, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        // Simple existence check
        return parameters.TryGetValue(trimmedCondition, out var val) && HasValue(val);
    }

    /// <summary>
    /// Checks if a value is considered "truthy".
    /// </summary>
    private static bool HasValue(object? value)
    {
        return value switch
        {
            null => false,
            string s => !string.IsNullOrEmpty(s),
            bool b => b,
            IEnumerable<object?> e => e.Any(),
            _ => true
        };
    }

    /// <summary>
    /// Formats a value for template output.
    /// </summary>
    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string s => s,
            bool b => b.ToString().ToLowerInvariant(),
            IEnumerable<object?> e => string.Join(", ", e),
            _ => value.ToString() ?? string.Empty
        };
    }
}
