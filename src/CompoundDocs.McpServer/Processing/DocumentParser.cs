using System.Text.RegularExpressions;
using CompoundDocs.Common.Parsing;

namespace CompoundDocs.McpServer.Processing;

/// <summary>
/// Parses markdown documents with YAML frontmatter, extracting metadata and content.
/// Handles document structure parsing including code blocks and headers.
/// </summary>
public sealed partial class DocumentParser
{
    private readonly FrontmatterParser _frontmatterParser;
    private readonly MarkdownParser _markdownParser;

    /// <summary>
    /// Regular expression pattern for matching YAML frontmatter delimiters.
    /// </summary>
    private const string FrontmatterPattern = @"^---\s*\r?\n([\s\S]*?)\r?\n---\s*(?:\r?\n|$)";

    /// <summary>
    /// Regular expression pattern for matching fenced code blocks.
    /// </summary>
    private const string CodeBlockPattern = @"```(\w*)\r?\n([\s\S]*?)```";

    /// <summary>
    /// Creates a new instance of DocumentParser.
    /// </summary>
    /// <param name="frontmatterParser">Optional frontmatter parser. If null, a new instance is created.</param>
    /// <param name="markdownParser">Optional markdown parser. If null, a new instance is created.</param>
    public DocumentParser(FrontmatterParser? frontmatterParser = null, MarkdownParser? markdownParser = null)
    {
        _frontmatterParser = frontmatterParser ?? new FrontmatterParser();
        _markdownParser = markdownParser ?? new MarkdownParser();
    }

    /// <summary>
    /// Parses a markdown document, extracting frontmatter and body content.
    /// </summary>
    /// <param name="content">The raw markdown content.</param>
    /// <returns>The parsed document result.</returns>
    public ParsedDocument Parse(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return ParsedDocument.Empty();
        }

        try
        {
            // Try to parse frontmatter using the common parser
            var frontmatterResult = _frontmatterParser.Parse(content);

            if (frontmatterResult.HasFrontmatter && frontmatterResult.Frontmatter != null)
            {
                return new ParsedDocument
                {
                    HasFrontmatter = true,
                    Frontmatter = frontmatterResult.Frontmatter,
                    Body = frontmatterResult.Body,
                    RawContent = content,
                    IsSuccess = true
                };
            }

            // No frontmatter found - return entire content as body
            return new ParsedDocument
            {
                HasFrontmatter = false,
                Frontmatter = null,
                Body = content,
                RawContent = content,
                IsSuccess = true
            };
        }
        catch (Exception ex)
        {
            return ParsedDocument.Failure($"Failed to parse document: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses a document and extracts detailed structural information.
    /// </summary>
    /// <param name="content">The raw markdown content.</param>
    /// <returns>The detailed parsed document with structure information.</returns>
    public DetailedParsedDocument ParseDetailed(string content)
    {
        var basicResult = Parse(content);

        if (!basicResult.IsSuccess)
        {
            return new DetailedParsedDocument
            {
                HasFrontmatter = false,
                Body = content,
                RawContent = content,
                IsSuccess = false,
                Error = basicResult.Error
            };
        }

        try
        {
            var document = _markdownParser.Parse(basicResult.Body);
            var headers = _markdownParser.ExtractHeaders(document);
            var links = _markdownParser.ExtractLinks(document);
            var codeBlocks = ExtractCodeBlocks(basicResult.Body);

            // Extract title from frontmatter or first H1 header
            var title = ExtractTitle(basicResult.Frontmatter, headers);

            return new DetailedParsedDocument
            {
                HasFrontmatter = basicResult.HasFrontmatter,
                Frontmatter = basicResult.Frontmatter,
                Body = basicResult.Body,
                RawContent = content,
                IsSuccess = true,
                Title = title,
                Headers = headers,
                Links = links,
                CodeBlocks = codeBlocks
            };
        }
        catch (Exception ex)
        {
            return new DetailedParsedDocument
            {
                HasFrontmatter = basicResult.HasFrontmatter,
                Frontmatter = basicResult.Frontmatter,
                Body = basicResult.Body,
                RawContent = content,
                IsSuccess = false,
                Error = $"Failed to parse document structure: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Extracts code blocks from the document body.
    /// </summary>
    /// <param name="body">The document body without frontmatter.</param>
    /// <returns>List of code blocks found in the document.</returns>
    public IReadOnlyList<CodeBlockInfo> ExtractCodeBlocks(string body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return [];
        }

        var codeBlocks = new List<CodeBlockInfo>();
        var regex = CodeBlockRegex();
        var matches = regex.Matches(body);

        var index = 0;
        foreach (Match match in matches)
        {
            var language = match.Groups[1].Value;
            var code = match.Groups[2].Value;

            // Calculate line number
            var textBeforeMatch = body[..match.Index];
            var lineNumber = textBeforeMatch.Count(c => c == '\n');

            codeBlocks.Add(new CodeBlockInfo
            {
                Index = index++,
                Language = string.IsNullOrWhiteSpace(language) ? null : language,
                Code = code.TrimEnd(),
                StartLine = lineNumber,
                EndLine = lineNumber + code.Count(c => c == '\n')
            });
        }

        return codeBlocks;
    }

    /// <summary>
    /// Extracts the document title from frontmatter or the first H1 header.
    /// </summary>
    /// <param name="frontmatter">The frontmatter dictionary.</param>
    /// <param name="headers">The extracted headers.</param>
    /// <returns>The document title, or an empty string if not found.</returns>
    private static string ExtractTitle(
        Dictionary<string, object?>? frontmatter,
        IReadOnlyList<HeaderInfo> headers)
    {
        // Try to get title from frontmatter first
        if (frontmatter != null)
        {
            if (frontmatter.TryGetValue("title", out var titleValue) && titleValue is string title)
            {
                return title;
            }
        }

        // Fall back to first H1 header
        var h1Header = headers.FirstOrDefault(h => h.Level == 1);
        return h1Header?.Text ?? string.Empty;
    }

    /// <summary>
    /// Validates that the document has valid structure.
    /// </summary>
    /// <param name="content">The document content.</param>
    /// <returns>True if the document has valid structure.</returns>
    public bool IsValidDocument(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return false;
        }

        var result = Parse(content);
        return result.IsSuccess;
    }

    [GeneratedRegex(CodeBlockPattern, RegexOptions.Multiline)]
    private static partial Regex CodeBlockRegex();
}

/// <summary>
/// Result of parsing a markdown document.
/// </summary>
public sealed class ParsedDocument
{
    /// <summary>
    /// Whether the document has YAML frontmatter.
    /// </summary>
    public bool HasFrontmatter { get; init; }

    /// <summary>
    /// The parsed frontmatter as a dictionary.
    /// </summary>
    public Dictionary<string, object?>? Frontmatter { get; init; }

    /// <summary>
    /// The document body without frontmatter.
    /// </summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// The raw document content including frontmatter.
    /// </summary>
    public string RawContent { get; init; } = string.Empty;

    /// <summary>
    /// Whether parsing was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if parsing failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Creates an empty parsed document result.
    /// </summary>
    public static ParsedDocument Empty() => new()
    {
        HasFrontmatter = false,
        Body = string.Empty,
        RawContent = string.Empty,
        IsSuccess = true
    };

    /// <summary>
    /// Creates a failed parsed document result.
    /// </summary>
    /// <param name="error">The error message.</param>
    public static ParsedDocument Failure(string error) => new()
    {
        HasFrontmatter = false,
        Body = string.Empty,
        RawContent = string.Empty,
        IsSuccess = false,
        Error = error
    };
}

/// <summary>
/// Extended result of parsing a markdown document with structural information.
/// </summary>
public sealed class DetailedParsedDocument
{
    /// <summary>
    /// Whether the document has YAML frontmatter.
    /// </summary>
    public bool HasFrontmatter { get; init; }

    /// <summary>
    /// The parsed frontmatter as a dictionary.
    /// </summary>
    public Dictionary<string, object?>? Frontmatter { get; init; }

    /// <summary>
    /// The document body without frontmatter.
    /// </summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// The raw document content including frontmatter.
    /// </summary>
    public string RawContent { get; init; } = string.Empty;

    /// <summary>
    /// Whether parsing was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if parsing failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// The document title extracted from frontmatter or first H1.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Headers extracted from the document.
    /// </summary>
    public IReadOnlyList<HeaderInfo> Headers { get; init; } = [];

    /// <summary>
    /// Internal links extracted from the document.
    /// </summary>
    public IReadOnlyList<LinkInfo> Links { get; init; } = [];

    /// <summary>
    /// Code blocks extracted from the document.
    /// </summary>
    public IReadOnlyList<CodeBlockInfo> CodeBlocks { get; init; } = [];
}

/// <summary>
/// Information about a code block in the document.
/// </summary>
public sealed class CodeBlockInfo
{
    /// <summary>
    /// The index of this code block in the document.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// The language identifier for the code block (e.g., "csharp", "python").
    /// Null if no language was specified.
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// The code content of the block.
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// The starting line number of the code block (0-indexed).
    /// </summary>
    public int StartLine { get; init; }

    /// <summary>
    /// The ending line number of the code block (0-indexed).
    /// </summary>
    public int EndLine { get; init; }
}
