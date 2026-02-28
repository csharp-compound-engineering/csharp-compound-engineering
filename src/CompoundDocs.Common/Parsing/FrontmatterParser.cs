using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CompoundDocs.Common.Parsing;

/// <summary>
/// Parses YAML frontmatter from markdown documents.
/// Handles common YAML types including strings, numbers, booleans, lists, and nested objects.
/// </summary>
public sealed class FrontmatterParser : IFrontmatterParser
{
    private readonly IDeserializer _deserializer;

    public FrontmatterParser()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Extracts frontmatter from a markdown document.
    /// </summary>
    public FrontmatterResult Parse(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return FrontmatterResult.NoFrontmatter(markdown ?? string.Empty);
        }

        if (!markdown.StartsWith("---"))
        {
            return FrontmatterResult.NoFrontmatter(markdown);
        }

        var endIndex = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (endIndex == -1)
        {
            return FrontmatterResult.NoFrontmatter(markdown);
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
                return FrontmatterResult.NoFrontmatter(body);
            }

            var normalizedFrontmatter = NormalizeFrontmatter(frontmatter);
            return FrontmatterResult.Success(normalizedFrontmatter, body);
        }
        catch (Exception ex)
        {
            return FrontmatterResult.ParseError(markdown, ex.Message);
        }
    }

    /// <summary>
    /// Deserializes frontmatter to a strongly-typed object.
    /// </summary>
    public T? ParseAs<T>(string markdown) where T : class
    {
        if (string.IsNullOrEmpty(markdown) || !markdown.StartsWith("---"))
            return null;

        var endIndex = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (endIndex == -1)
            return null;

        var yamlContent = markdown.Substring(4, endIndex - 4);

        try
        {
            return _deserializer.Deserialize<T>(yamlContent);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses YAML frontmatter and validates required fields.
    /// </summary>
    public FrontmatterResult ParseAndValidate(string markdown, IReadOnlyList<string> requiredFields)
    {
        var result = Parse(markdown);

        if (!result.HasFrontmatter || result.Frontmatter == null)
        {
            if (requiredFields.Count > 0)
            {
                var errors = requiredFields.Select(f => $"Required field '{f}' is missing").ToList();
                return FrontmatterResult.ValidationError(result.Body, errors);
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
            return FrontmatterResult.ValidationError(
                result.Body,
                validationErrors,
                result.Frontmatter);
        }

        return result;
    }

    /// <summary>
    /// Gets a typed value from the frontmatter.
    /// </summary>
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

    private static Dictionary<string, object?> NormalizeFrontmatter(Dictionary<string, object?> frontmatter)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in frontmatter)
        {
            result[kvp.Key] = NormalizeValue(kvp.Value);
        }

        return result;
    }

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
public sealed class FrontmatterResult
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
    /// Parsing or validation errors if any occurred.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Creates a successful result with frontmatter.
    /// </summary>
    public static FrontmatterResult Success(Dictionary<string, object?> frontmatter, string body)
    {
        return new FrontmatterResult
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
    public static FrontmatterResult NoFrontmatter(string content)
    {
        return new FrontmatterResult
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
    public static FrontmatterResult ParseError(string content, string error)
    {
        return new FrontmatterResult
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
    public static FrontmatterResult ValidationError(
        string body,
        IReadOnlyList<string> errors,
        Dictionary<string, object?>? frontmatter = null)
    {
        return new FrontmatterResult
        {
            HasFrontmatter = frontmatter != null,
            Frontmatter = frontmatter,
            Body = body,
            IsSuccess = false,
            Errors = errors
        };
    }
}
