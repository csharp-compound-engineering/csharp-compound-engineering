using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace CompoundDocs.Common.Parsing;

/// <summary>
/// Parses markdown documents using Markdig, extracting structure and links.
/// </summary>
public sealed class MarkdownParser
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownParser()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseYamlFrontMatter()
            .UseAdvancedExtensions()
            .Build();
    }

    /// <summary>
    /// Parses a markdown document and returns its AST.
    /// </summary>
    public MarkdownDocument Parse(string markdown)
    {
        return Markdown.Parse(markdown, _pipeline);
    }

    /// <summary>
    /// Extracts all headers from a markdown document with their hierarchy.
    /// </summary>
    public IReadOnlyList<HeaderInfo> ExtractHeaders(MarkdownDocument document)
    {
        var headers = new List<HeaderInfo>();
        var stack = new Stack<HeaderInfo>();

        foreach (var block in document.Descendants<HeadingBlock>())
        {
            var text = GetHeaderText(block);
            var level = block.Level;
            var line = block.Line;

            // Pop stack until we find a parent (lower level number = higher in hierarchy)
            while (stack.Count > 0 && stack.Peek().Level >= level)
            {
                stack.Pop();
            }

            var parent = stack.Count > 0 ? stack.Peek() : null;
            var headerPath = parent != null
                ? $"{parent.HeaderPath} > {text}"
                : text;

            var header = new HeaderInfo(level, text, headerPath, line, block.Span.Start, block.Span.End);
            headers.Add(header);
            stack.Push(header);
        }

        return headers;
    }

    /// <summary>
    /// Extracts all markdown links from a document.
    /// </summary>
    public IReadOnlyList<LinkInfo> ExtractLinks(MarkdownDocument document)
    {
        var links = new List<LinkInfo>();

        foreach (var link in document.Descendants<LinkInline>())
        {
            if (link.Url == null) continue;

            // Skip external URLs
            if (link.Url.StartsWith("http://") || link.Url.StartsWith("https://"))
                continue;

            var text = GetLinkText(link);
            links.Add(new LinkInfo(link.Url, text, link.Line, link.Span.Start));
        }

        return links;
    }

    /// <summary>
    /// Extracts all fenced code blocks from a markdown document.
    /// </summary>
    public IReadOnlyList<ParsedCodeBlock> ExtractCodeBlocks(MarkdownDocument document)
    {
        var codeBlocks = new List<ParsedCodeBlock>();

        foreach (var block in document.Descendants<FencedCodeBlock>())
        {
            var language = block.Info ?? string.Empty;
            var code = block.Lines.ToString();
            codeBlocks.Add(new ParsedCodeBlock(language, code, block.Line, block.Span.Start));
        }

        return codeBlocks;
    }

    /// <summary>
    /// Chunks a document at header boundaries (H2 and H3).
    /// </summary>
    public IReadOnlyList<ChunkInfo> ChunkByHeaders(string markdown, int maxLinesPerChunk = 500)
    {
        var document = Parse(markdown);
        var lines = markdown.Split('\n');
        var headers = ExtractHeaders(document);
        var chunks = new List<ChunkInfo>();

        if (headers.Count == 0)
        {
            // No headers, return entire document as one chunk
            chunks.Add(new ChunkInfo(0, "", 0, lines.Length - 1, markdown));
            return chunks;
        }

        // Only chunk at H2 (##) and H3 (###) headers
        var chunkHeaders = headers.Where(h => h.Level <= 3).ToList();

        for (int i = 0; i < chunkHeaders.Count; i++)
        {
            var header = chunkHeaders[i];
            var startLine = header.Line;
            var endLine = i + 1 < chunkHeaders.Count
                ? chunkHeaders[i + 1].Line - 1
                : lines.Length - 1;

            var content = string.Join('\n', lines.Skip(startLine).Take(endLine - startLine + 1));
            chunks.Add(new ChunkInfo(i, header.HeaderPath, startLine, endLine, content));
        }

        return chunks;
    }

    private static string GetHeaderText(HeadingBlock heading)
    {
        if (heading.Inline == null) return string.Empty;

        var text = new System.Text.StringBuilder();
        foreach (var inline in heading.Inline)
        {
            if (inline is LiteralInline literal)
            {
                text.Append(literal.Content);
            }
        }
        return text.ToString();
    }

    private static string GetLinkText(LinkInline link)
    {
        var text = new System.Text.StringBuilder();
        foreach (var child in link)
        {
            if (child is LiteralInline literal)
            {
                text.Append(literal.Content);
            }
        }
        return text.ToString();
    }
}

public sealed record HeaderInfo(
    int Level,
    string Text,
    string HeaderPath,
    int Line,
    int SpanStart,
    int SpanEnd);

public sealed record LinkInfo(
    string Url,
    string Text,
    int Line,
    int Position);

public sealed record ChunkInfo(
    int Index,
    string HeaderPath,
    int StartLine,
    int EndLine,
    string Content);

public sealed record ParsedCodeBlock(
    string Language,
    string Code,
    int Line,
    int SpanStart);
