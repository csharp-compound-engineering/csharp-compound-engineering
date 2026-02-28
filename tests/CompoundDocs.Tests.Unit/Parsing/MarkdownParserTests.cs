using CompoundDocs.Common.Parsing;
using Markdig.Helpers;
using Markdig.Syntax;

namespace CompoundDocs.Tests.Unit.Parsing;

/// <summary>
/// Unit tests for MarkdownParser.
/// </summary>
public sealed class MarkdownParserTests
{
    #region Parse Tests

    [Fact]
    public void Parse_WithValidMarkdown_ReturnsMarkdownDocument()
    {
        // Arrange
        var sut = new MarkdownParser();
        var markdown = "# Hello World\n\nThis is a paragraph.";

        // Act
        var result = sut.Parse(markdown);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Parse_WithEmptyString_ReturnsEmptyDocument()
    {
        // Arrange
        var sut = new MarkdownParser();
        var markdown = string.Empty;

        // Act
        var result = sut.Parse(markdown);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_WithYamlFrontmatter_ParsesSuccessfully()
    {
        // Arrange
        var sut = new MarkdownParser();
        var markdown = """
            ---
            title: Test Document
            doc_type: spec
            ---

            # Introduction

            Content here.
            """;

        // Act
        var result = sut.Parse(markdown);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBeGreaterThan(0);
    }

    #endregion

    #region ExtractHeaders Tests

    [Fact]
    public void ExtractHeaders_WithSingleHeader_ReturnsSingleHeader()
    {
        // Arrange
        var sut = new MarkdownParser();
        var markdown = "# Main Title\n\nSome content.";
        var document = sut.Parse(markdown);

        // Act
        var headers = sut.ExtractHeaders(document);

        // Assert
        headers.ShouldNotBeNull();
        headers.Count.ShouldBe(1);
        headers[0].Text.ShouldBe("Main Title");
        headers[0].Level.ShouldBe(1);
    }

    [Fact]
    public void ExtractHeaders_WithNestedHeaders_ReturnsHierarchicalPaths()
    {
        // Arrange
        var sut = new MarkdownParser();
        var markdown = """
            # Document Title

            ## Section One

            Content in section one.

            ### Subsection A

            Content in subsection A.

            ## Section Two

            Content in section two.
            """;
        var document = sut.Parse(markdown);

        // Act
        var headers = sut.ExtractHeaders(document);

        // Assert
        headers.ShouldNotBeNull();
        headers.Count.ShouldBe(4);

        headers[0].Text.ShouldBe("Document Title");
        headers[0].HeaderPath.ShouldBe("Document Title");
        headers[0].Level.ShouldBe(1);

        headers[1].Text.ShouldBe("Section One");
        headers[1].HeaderPath.ShouldBe("Document Title > Section One");
        headers[1].Level.ShouldBe(2);

        headers[2].Text.ShouldBe("Subsection A");
        headers[2].HeaderPath.ShouldBe("Document Title > Section One > Subsection A");
        headers[2].Level.ShouldBe(3);

        headers[3].Text.ShouldBe("Section Two");
        headers[3].HeaderPath.ShouldBe("Document Title > Section Two");
        headers[3].Level.ShouldBe(2);
    }

    [Fact]
    public void ExtractHeaders_WithNoHeaders_ReturnsEmptyList()
    {
        // Arrange
        var sut = new MarkdownParser();
        var markdown = "Just some plain text without any headers.";
        var document = sut.Parse(markdown);

        // Act
        var headers = sut.ExtractHeaders(document);

        // Assert
        headers.ShouldNotBeNull();
        headers.Count.ShouldBe(0);
    }

    [Fact]
    public void ExtractHeaders_RecordsLineNumbers()
    {
        // Arrange
        var sut = new MarkdownParser();
        var markdown = "# Header on Line 0\n\n## Header on Line 2";
        var document = sut.Parse(markdown);

        // Act
        var headers = sut.ExtractHeaders(document);

        // Assert
        headers.Count.ShouldBe(2);
        headers[0].Line.ShouldBe(0);
        headers[1].Line.ShouldBe(2);
    }

    #endregion

    #region ExtractLinks Tests

    [Fact]
    public void ExtractLinks_WithInternalLinks_ReturnsRelativeLinks()
    {
        // Arrange
        var sut = new MarkdownParser();
        var markdown = """
            # Document

            See [related doc](./related.md) for more info.
            Also check [another doc](../other/doc.md).
            """;
        var document = sut.Parse(markdown);

        // Act
        var links = sut.ExtractLinks(document);

        // Assert
        links.ShouldNotBeNull();
        links.Count.ShouldBe(2);
        links[0].Url.ShouldBe("./related.md");
        links[0].Text.ShouldBe("related doc");
        links[1].Url.ShouldBe("../other/doc.md");
    }

    [Fact]
    public void ExtractLinks_WithExternalLinks_ExcludesHttpLinks()
    {
        // Arrange
        var sut = new MarkdownParser();
        var markdown = """
            # Document

            See [Google](https://google.com) for searching.
            Also see [local doc](./local.md).
            And [another external](http://example.com).
            """;
        var document = sut.Parse(markdown);

        // Act
        var links = sut.ExtractLinks(document);

        // Assert
        links.ShouldNotBeNull();
        links.Count.ShouldBe(1);
        links[0].Url.ShouldBe("./local.md");
    }

    [Fact]
    public void ExtractLinks_WithNoLinks_ReturnsEmptyList()
    {
        // Arrange
        var sut = new MarkdownParser();
        var markdown = "# Document\n\nNo links here.";
        var document = sut.Parse(markdown);

        // Act
        var links = sut.ExtractLinks(document);

        // Assert
        links.ShouldNotBeNull();
        links.Count.ShouldBe(0);
    }

    [Fact]
    public void ExtractLinks_WithAnchorLinks_IncludesAnchorLinks()
    {
        // Arrange
        var sut = new MarkdownParser();
        var markdown = """
            # Document

            See the [overview section](#overview) below.
            """;
        var document = sut.Parse(markdown);

        // Act
        var links = sut.ExtractLinks(document);

        // Assert
        links.ShouldNotBeNull();
        links.Count.ShouldBe(1);
        links[0].Url.ShouldBe("#overview");
    }

    #endregion

    #region ChunkByHeaders Tests

    [Fact]
    public void ChunkByHeaders_WithMultipleSections_CreatesChunks()
    {
        // Arrange
        var sut = new MarkdownParser();
        var markdown = """
            # Document Title

            Introduction text.

            ## Section One

            Content in section one.
            More content here.

            ## Section Two

            Content in section two.

            ### Subsection

            Subsection content.
            """;

        // Act
        var chunks = sut.ChunkByHeaders(markdown);

        // Assert
        chunks.ShouldNotBeNull();
        chunks.Count.ShouldBeGreaterThan(0);

        // All chunks should have content
        foreach (var chunk in chunks)
        {
            chunk.Content.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void ChunkByHeaders_WithNoHeaders_ReturnsSingleChunk()
    {
        // Arrange
        var sut = new MarkdownParser();
        var markdown = "Just plain text without headers.\nMultiple lines.\nBut no headers.";

        // Act
        var chunks = sut.ChunkByHeaders(markdown);

        // Assert
        chunks.ShouldNotBeNull();
        chunks.Count.ShouldBe(1);
        chunks[0].HeaderPath.ShouldBe(string.Empty);
    }

    [Fact]
    public void ChunkByHeaders_ChunksContainCorrectLineRanges()
    {
        // Arrange
        var sut = new MarkdownParser();
        var markdown = """
            ## First Section

            Line 2 content.
            Line 3 content.

            ## Second Section

            Line 7 content.
            """;

        // Act
        var chunks = sut.ChunkByHeaders(markdown);

        // Assert
        chunks.Count.ShouldBe(2);
        chunks[0].StartLine.ShouldBe(0);
        chunks[0].EndLine.ShouldBeLessThan(chunks[1].StartLine);
    }

    [Fact]
    public void ChunkByHeaders_IncludesHeaderPathInChunks()
    {
        // Arrange
        var sut = new MarkdownParser();
        var markdown = """
            # Main

            ## Sub Section

            Content here.
            """;

        // Act
        var chunks = sut.ChunkByHeaders(markdown);

        // Assert
        chunks.ShouldNotBeNull();
        // Should chunk at H2 and H3 only
        var h2Chunk = chunks.FirstOrDefault(c => c.HeaderPath.Contains("Sub Section"));
        h2Chunk.ShouldNotBeNull();
    }

    #endregion

    #region Extended Branch Coverage

    [Fact]
    public void ChunkByHeaders_WithH4Headers_OnlyChunksAtH2AndH3()
    {
        // Arrange - H4 headers should not create chunk boundaries
        var sut = new MarkdownParser();
        var markdown = "## Section One\n\nContent.\n\n#### Deep Header\n\nMore content.\n\n## Section Two\n\nFinal.";

        // Act
        var chunks = sut.ChunkByHeaders(markdown);

        // Assert - Should have 2 chunks (at ## boundaries), not 3
        chunks.Count.ShouldBe(2);
        chunks[0].HeaderPath.ShouldContain("Section One");
        chunks[1].HeaderPath.ShouldContain("Section Two");
    }

    [Fact]
    public void ExtractHeaders_WithSameLevelSiblings_ResetsSiblingPaths()
    {
        // Arrange - H2 siblings after H3 should reset path correctly
        var sut = new MarkdownParser();
        var markdown = "# Root\n\n## A\n\n### A1\n\n### A2\n\n## B\n\n### B1";
        var document = sut.Parse(markdown);

        // Act
        var headers = sut.ExtractHeaders(document);

        // Assert
        headers.Count.ShouldBe(6);
        headers[3].Text.ShouldBe("A2");
        headers[3].HeaderPath.ShouldBe("Root > A > A2");
        headers[4].Text.ShouldBe("B");
        headers[4].HeaderPath.ShouldBe("Root > B");
        headers[5].Text.ShouldBe("B1");
        headers[5].HeaderPath.ShouldBe("Root > B > B1");
    }

    [Fact]
    public void ChunkByHeaders_WithOnlyH1Header_ChunksAtH1()
    {
        // Arrange - H1 is level 1, <= 3, so should chunk
        var sut = new MarkdownParser();
        var markdown = "# Only H1\n\nContent under H1.";

        // Act
        var chunks = sut.ChunkByHeaders(markdown);

        // Assert
        chunks.Count.ShouldBe(1);
        chunks[0].HeaderPath.ShouldContain("Only H1");
    }

    [Fact]
    public void ExtractHeaders_SpanPositionsAreRecorded()
    {
        // Arrange
        var sut = new MarkdownParser();
        var markdown = "# Title\n\nSome text.";
        var document = sut.Parse(markdown);

        // Act
        var headers = sut.ExtractHeaders(document);

        // Assert
        headers.Count.ShouldBe(1);
        headers[0].SpanStart.ShouldBeGreaterThanOrEqualTo(0);
        headers[0].SpanEnd.ShouldBeGreaterThan(headers[0].SpanStart);
    }

    [Fact]
    public void ChunkByHeaders_ChunksHaveSequentialIndexes()
    {
        // Arrange
        var sut = new MarkdownParser();
        var markdown = "## A\n\nContent A.\n\n## B\n\nContent B.\n\n## C\n\nContent C.";

        // Act
        var chunks = sut.ChunkByHeaders(markdown);

        // Assert
        chunks.Count.ShouldBe(3);
        chunks[0].Index.ShouldBe(0);
        chunks[1].Index.ShouldBe(1);
        chunks[2].Index.ShouldBe(2);
    }

    #endregion

    #region GetHeaderText — Null Inline Branch (Line 122)

    [Fact]
    public void ExtractHeaders_WithHeadingBlockWithNullInline_ReturnsEmptyTextForThatHeader()
    {
        // Arrange - Create a MarkdownDocument with a HeadingBlock that has null Inline.
        // A HeadingBlock created directly without processing inline content will have Inline = null.
        var sut = new MarkdownParser();
        var document = new MarkdownDocument();
        var headingBlock = new HeadingBlock(null!)
        {
            Level = 2,
            Line = 0
        };
        // Do NOT call ProcessInlines - this leaves heading.Inline as null
        document.Add(headingBlock);

        // Act
        var headers = sut.ExtractHeaders(document);

        // Assert
        headers.Count.ShouldBe(1);
        headers[0].Text.ShouldBe(string.Empty);
        headers[0].Level.ShouldBe(2);
    }

    #endregion

    #region ExtractCodeBlocks Tests

    [Fact]
    public void ExtractCodeBlocks_SingleFencedCodeBlock_ExtractsCorrectly()
    {
        // Arrange
        var sut = new MarkdownParser();
        var markdown = "## Section\n\n```csharp\nvar x = 1;\n```";
        var document = sut.Parse(markdown);

        // Act
        var codeBlocks = sut.ExtractCodeBlocks(document);

        // Assert
        codeBlocks.Count.ShouldBe(1);
        codeBlocks[0].Language.ShouldBe("csharp");
        codeBlocks[0].Code.ShouldContain("var x = 1;");
    }

    [Fact]
    public void ExtractCodeBlocks_MultipleCodeBlocks_ExtractsAll()
    {
        // Arrange
        var sut = new MarkdownParser();
        var markdown = "```python\nprint('hello')\n```\n\nSome text.\n\n```json\n{\"key\": \"value\"}\n```";
        var document = sut.Parse(markdown);

        // Act
        var codeBlocks = sut.ExtractCodeBlocks(document);

        // Assert
        codeBlocks.Count.ShouldBe(2);
        codeBlocks[0].Language.ShouldBe("python");
        codeBlocks[0].Code.ShouldContain("print('hello')");
        codeBlocks[1].Language.ShouldBe("json");
        codeBlocks[1].Code.ShouldContain("\"key\"");
    }

    [Fact]
    public void ExtractCodeBlocks_NoLanguage_ReturnsEmptyString()
    {
        // Arrange
        var sut = new MarkdownParser();
        var markdown = "```\nsome code\n```";
        var document = sut.Parse(markdown);

        // Act
        var codeBlocks = sut.ExtractCodeBlocks(document);

        // Assert
        codeBlocks.Count.ShouldBe(1);
        codeBlocks[0].Language.ShouldBe(string.Empty);
    }

    [Fact]
    public void ExtractCodeBlocks_NoCodeBlocks_ReturnsEmptyList()
    {
        // Arrange
        var sut = new MarkdownParser();
        var markdown = "## Section\n\nJust plain text, no code blocks.";
        var document = sut.Parse(markdown);

        // Act
        var codeBlocks = sut.ExtractCodeBlocks(document);

        // Assert
        codeBlocks.ShouldBeEmpty();
    }

    [Fact]
    public void ExtractCodeBlocks_WithNullInfo_ReturnsEmptyLanguage()
    {
        // Arrange - Markdig sets Info="" for no-language blocks, so we must construct one
        // with Info=null directly to cover the null-coalescing branch.
        var sut = new MarkdownParser();
        var document = new MarkdownDocument();
        var codeBlock = new FencedCodeBlock(null!)
        {
            Info = null,
            Lines = new StringLineGroup("var x = 1;")
        };
        document.Add(codeBlock);

        // Act
        var codeBlocks = sut.ExtractCodeBlocks(document);

        // Assert
        codeBlocks.Count.ShouldBe(1);
        codeBlocks[0].Language.ShouldBe(string.Empty);
    }

    #endregion
}
