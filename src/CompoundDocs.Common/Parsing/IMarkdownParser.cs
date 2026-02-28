using Markdig.Syntax;

namespace CompoundDocs.Common.Parsing;

/// <summary>
/// Parses markdown documents, extracting structure, headers, links, and code blocks.
/// </summary>
public interface IMarkdownParser
{
    /// <summary>
    /// Parses a markdown document and returns its AST.
    /// </summary>
    MarkdownDocument Parse(string markdown);

    /// <summary>
    /// Extracts all headers from a markdown document with their hierarchy.
    /// </summary>
    IReadOnlyList<HeaderInfo> ExtractHeaders(MarkdownDocument document);

    /// <summary>
    /// Extracts all markdown links from a document.
    /// </summary>
    IReadOnlyList<LinkInfo> ExtractLinks(MarkdownDocument document);

    /// <summary>
    /// Extracts all fenced code blocks from a markdown document.
    /// </summary>
    IReadOnlyList<ParsedCodeBlock> ExtractCodeBlocks(MarkdownDocument document);

    /// <summary>
    /// Chunks a document at header boundaries (H2 and H3).
    /// </summary>
    IReadOnlyList<ChunkInfo> ChunkByHeaders(string markdown, int maxLinesPerChunk = 500);
}
