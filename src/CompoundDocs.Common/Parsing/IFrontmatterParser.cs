namespace CompoundDocs.Common.Parsing;

/// <summary>
/// Parses YAML frontmatter from markdown documents.
/// </summary>
public interface IFrontmatterParser
{
    /// <summary>
    /// Extracts frontmatter from a markdown document.
    /// </summary>
    FrontmatterResult Parse(string markdown);

    /// <summary>
    /// Deserializes frontmatter to a strongly-typed object.
    /// </summary>
    T? ParseAs<T>(string markdown) where T : class;

    /// <summary>
    /// Parses YAML frontmatter and validates required fields.
    /// </summary>
    FrontmatterResult ParseAndValidate(string markdown, IReadOnlyList<string> requiredFields);
}
