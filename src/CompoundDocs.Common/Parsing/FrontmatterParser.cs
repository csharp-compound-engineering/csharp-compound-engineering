using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CompoundDocs.Common.Parsing;

/// <summary>
/// Parses YAML frontmatter from markdown documents.
/// </summary>
public sealed class FrontmatterParser
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
        if (!markdown.StartsWith("---"))
        {
            return new FrontmatterResult(null, markdown, false);
        }

        var endIndex = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (endIndex == -1)
        {
            return new FrontmatterResult(null, markdown, false);
        }

        var yamlContent = markdown.Substring(4, endIndex - 4);
        var bodyStartIndex = endIndex + 4;

        // Skip any leading newlines after frontmatter
        while (bodyStartIndex < markdown.Length &&
               (markdown[bodyStartIndex] == '\n' || markdown[bodyStartIndex] == '\r'))
        {
            bodyStartIndex++;
        }

        var body = markdown[bodyStartIndex..];

        try
        {
            var frontmatter = _deserializer.Deserialize<Dictionary<string, object?>>(yamlContent);
            return new FrontmatterResult(frontmatter, body, true);
        }
        catch (Exception)
        {
            return new FrontmatterResult(null, markdown, false);
        }
    }

    /// <summary>
    /// Deserializes frontmatter to a strongly-typed object.
    /// </summary>
    public T? ParseAs<T>(string markdown) where T : class
    {
        if (!markdown.StartsWith("---"))
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
}

public sealed record FrontmatterResult(
    Dictionary<string, object?>? Frontmatter,
    string Body,
    bool HasFrontmatter);
