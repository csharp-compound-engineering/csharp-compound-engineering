using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CompoundDocs.McpServer.Processing;

/// <summary>
/// Parses YAML frontmatter from markdown documents.
/// Handles common YAML types including strings, numbers, booleans, lists, and nested objects.
/// </summary>
public sealed class FrontmatterParser
{
    private readonly IDeserializer _deserializer;

    /// <summary>
    /// Creates a new instance of FrontmatterParser.
    /// </summary>
    public FrontmatterParser()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Parses YAML frontmatter from a markdown document.
    /// </summary>
    /// <param name="markdown">The full markdown content including frontmatter.</param>
    /// <returns>The parse result with frontmatter dictionary and body.</returns>
    public FrontmatterParseResult Parse(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return FrontmatterParseResult.NoFrontmatter(markdown);
        }

        if (!markdown.StartsWith("---"))
        {
            return FrontmatterParseResult.NoFrontmatter(markdown);
        }

        var endIndex = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (endIndex == -1)
        {
            return FrontmatterParseResult.NoFrontmatter(markdown);
        }

        var yamlContent = markdown.Substring(4, endIndex - 4);
        var bodyStartIndex = endIndex + 4;

        // Skip any leading newlines after frontmatter
        while (bodyStartIndex < markdown.Length &&
               (markdown[bodyStartIndex] == '\n' || markdown[bodyStartIndex] == '\r'))
        {
            bodyStartIndex++;
        }

        var body = bodyStartIndex < markdown.Length ? markdown[bodyStartIndex..] : string.Empty;

        try
        {
            var frontmatter = _deserializer.Deserialize<Dictionary<string, object?>>(yamlContent);

            if (frontmatter == null)
            {
                return FrontmatterParseResult.NoFrontmatter(markdown);
            }

            // Convert YamlDotNet types to standard .NET types
            var normalizedFrontmatter = NormalizeFrontmatter(frontmatter);

            return FrontmatterParseResult.Success(normalizedFrontmatter, body);
        }
        catch (Exception ex)
        {
            return FrontmatterParseResult.ParseError(markdown, ex.Message);
        }
    }

    /// <summary>
    /// Parses YAML frontmatter and validates required fields based on document type.
    /// </summary>
    /// <param name="markdown">The full markdown content including frontmatter.</param>
    /// <param name="requiredFields">List of required field names.</param>
    /// <returns>The parse result with validation errors if any required fields are missing.</returns>
    public FrontmatterParseResult ParseAndValidate(string markdown, IReadOnlyList<string> requiredFields)
    {
        var result = Parse(markdown);

        if (!result.HasFrontmatter || result.Frontmatter == null)
        {
            if (requiredFields.Count > 0)
            {
                var errors = requiredFields.Select(f => $"Required field '{f}' is missing").ToList();
                return FrontmatterParseResult.ValidationError(markdown, errors);
            }
            return result;
        }

        var validationErrors = new List<string>();
        foreach (var field in requiredFields)
        {
            if (!result.Frontmatter.ContainsKey(field) || result.Frontmatter[field] == null)
            {
                validationErrors.Add($"Required field '{field}' is missing or null");
            }
        }

        if (validationErrors.Count > 0)
        {
            return FrontmatterParseResult.ValidationError(
                result.Body,
                validationErrors,
                result.Frontmatter);
        }

        return result;
    }

    /// <summary>
    /// Gets a typed value from the frontmatter.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="frontmatter">The frontmatter dictionary.</param>
    /// <param name="key">The key to look up.</param>
    /// <param name="defaultValue">Default value if not found or wrong type.</param>
    /// <returns>The value or default.</returns>
    public static T GetValue<T>(Dictionary<string, object?>? frontmatter, string key, T defaultValue)
    {
        if (frontmatter == null || !frontmatter.TryGetValue(key, out var value) || value == null)
        {
            return defaultValue;
        }

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Gets a string list from the frontmatter.
    /// </summary>
    /// <param name="frontmatter">The frontmatter dictionary.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The list or empty list if not found.</returns>
    public static IReadOnlyList<string> GetStringList(Dictionary<string, object?>? frontmatter, string key)
    {
        if (frontmatter == null || !frontmatter.TryGetValue(key, out var value))
        {
            return [];
        }

        return value switch
        {
            List<object?> list => list.Where(i => i != null).Select(i => i!.ToString()!).ToList(),
            IEnumerable<object> enumerable => enumerable.Where(i => i != null).Select(i => i.ToString()!).ToList(),
            string str => [str],
            _ => []
        };
    }

    /// <summary>
    /// Normalizes YamlDotNet types to standard .NET types.
    /// </summary>
    private static Dictionary<string, object?> NormalizeFrontmatter(Dictionary<string, object?> frontmatter)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in frontmatter)
        {
            result[kvp.Key] = NormalizeValue(kvp.Value);
        }

        return result;
    }

    /// <summary>
    /// Normalizes a single value to standard .NET types.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Defensive type-checking branches; YamlDotNet deserializes all scalars as strings so int/long/float/double/bool/IEnumerable/default branches are unreachable through YAML parsing")]
    private static object? NormalizeValue(object? value)
    {
        return value switch
        {
            null => null,
            string s => s,
            int i => i,
            long l => l,
            float f => f,
            double d => d,
            bool b => b,
            Dictionary<object, object?> dict => NormalizeNestedDictionary(dict),
            List<object?> list => list.Select(NormalizeValue).ToList(),
            IEnumerable<object> enumerable => enumerable.Select(NormalizeValue).ToList(),
            _ => value.ToString()
        };
    }

    /// <summary>
    /// Normalizes a nested dictionary.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Null key branch requires Key.ToString() returning null; unreachable in practice")]
    private static Dictionary<string, object?> NormalizeNestedDictionary(Dictionary<object, object?> dict)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in dict)
        {
            var key = kvp.Key.ToString() ?? string.Empty;
            result[key] = NormalizeValue(kvp.Value);
        }

        return result;
    }
}

/// <summary>
/// Result of parsing YAML frontmatter.
/// </summary>
public sealed class FrontmatterParseResult
{
    /// <summary>
    /// Whether the document has frontmatter.
    /// </summary>
    public bool HasFrontmatter { get; init; }

    /// <summary>
    /// The parsed frontmatter dictionary.
    /// </summary>
    public Dictionary<string, object?>? Frontmatter { get; init; }

    /// <summary>
    /// The document body without frontmatter.
    /// </summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// Whether parsing was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Parsing errors if any occurred.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Creates a successful result with frontmatter.
    /// </summary>
    public static FrontmatterParseResult Success(Dictionary<string, object?> frontmatter, string body)
    {
        return new FrontmatterParseResult
        {
            HasFrontmatter = true,
            Frontmatter = frontmatter,
            Body = body,
            IsSuccess = true
        };
    }

    /// <summary>
    /// Creates a result indicating no frontmatter was found.
    /// </summary>
    public static FrontmatterParseResult NoFrontmatter(string content)
    {
        return new FrontmatterParseResult
        {
            HasFrontmatter = false,
            Frontmatter = null,
            Body = content,
            IsSuccess = true
        };
    }

    /// <summary>
    /// Creates a result indicating a parse error.
    /// </summary>
    public static FrontmatterParseResult ParseError(string content, string error)
    {
        return new FrontmatterParseResult
        {
            HasFrontmatter = false,
            Frontmatter = null,
            Body = content,
            IsSuccess = false,
            Errors = [error]
        };
    }

    /// <summary>
    /// Creates a result indicating validation errors.
    /// </summary>
    public static FrontmatterParseResult ValidationError(
        string body,
        IReadOnlyList<string> errors,
        Dictionary<string, object?>? frontmatter = null)
    {
        return new FrontmatterParseResult
        {
            HasFrontmatter = frontmatter != null,
            Frontmatter = frontmatter,
            Body = body,
            IsSuccess = false,
            Errors = errors
        };
    }
}
