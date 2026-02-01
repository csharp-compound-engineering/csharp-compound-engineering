using CompoundDocs.Common.Parsing;

namespace CompoundDocs.Tests.Parsing;

/// <summary>
/// Unit tests for MarkdownParser.
/// </summary>
public sealed class MarkdownParserTests
{
    private readonly MarkdownParser _sut;

    public MarkdownParserTests()
    {
        _sut = new MarkdownParser();
    }

    #region Parse Tests

    [Fact]
    public void Parse_WithValidMarkdown_ReturnsMarkdownDocument()
    {
        // Arrange
        var markdown = "# Hello World\n\nThis is a paragraph.";

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Parse_WithEmptyString_ReturnsEmptyDocument()
    {
        // Arrange
        var markdown = string.Empty;

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_WithYamlFrontmatter_ParsesSuccessfully()
    {
        // Arrange
        var markdown = """
            ---
            title: Test Document
            doc_type: spec
            ---

            # Introduction

            Content here.
            """;

        // Act
        var result = _sut.Parse(markdown);

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
        var markdown = "# Main Title\n\nSome content.";
        var document = _sut.Parse(markdown);

        // Act
        var headers = _sut.ExtractHeaders(document);

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
        var markdown = """
            # Document Title

            ## Section One

            Content in section one.

            ### Subsection A

            Content in subsection A.

            ## Section Two

            Content in section two.
            """;
        var document = _sut.Parse(markdown);

        // Act
        var headers = _sut.ExtractHeaders(document);

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
        var markdown = "Just some plain text without any headers.";
        var document = _sut.Parse(markdown);

        // Act
        var headers = _sut.ExtractHeaders(document);

        // Assert
        headers.ShouldNotBeNull();
        headers.Count.ShouldBe(0);
    }

    [Fact]
    public void ExtractHeaders_RecordsLineNumbers()
    {
        // Arrange
        var markdown = "# Header on Line 0\n\n## Header on Line 2";
        var document = _sut.Parse(markdown);

        // Act
        var headers = _sut.ExtractHeaders(document);

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
        var markdown = """
            # Document

            See [related doc](./related.md) for more info.
            Also check [another doc](../other/doc.md).
            """;
        var document = _sut.Parse(markdown);

        // Act
        var links = _sut.ExtractLinks(document);

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
        var markdown = """
            # Document

            See [Google](https://google.com) for searching.
            Also see [local doc](./local.md).
            And [another external](http://example.com).
            """;
        var document = _sut.Parse(markdown);

        // Act
        var links = _sut.ExtractLinks(document);

        // Assert
        links.ShouldNotBeNull();
        links.Count.ShouldBe(1);
        links[0].Url.ShouldBe("./local.md");
    }

    [Fact]
    public void ExtractLinks_WithNoLinks_ReturnsEmptyList()
    {
        // Arrange
        var markdown = "# Document\n\nNo links here.";
        var document = _sut.Parse(markdown);

        // Act
        var links = _sut.ExtractLinks(document);

        // Assert
        links.ShouldNotBeNull();
        links.Count.ShouldBe(0);
    }

    [Fact]
    public void ExtractLinks_WithAnchorLinks_IncludesAnchorLinks()
    {
        // Arrange
        var markdown = """
            # Document

            See the [overview section](#overview) below.
            """;
        var document = _sut.Parse(markdown);

        // Act
        var links = _sut.ExtractLinks(document);

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
        var chunks = _sut.ChunkByHeaders(markdown);

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
        var markdown = "Just plain text without headers.\nMultiple lines.\nBut no headers.";

        // Act
        var chunks = _sut.ChunkByHeaders(markdown);

        // Assert
        chunks.ShouldNotBeNull();
        chunks.Count.ShouldBe(1);
        chunks[0].HeaderPath.ShouldBe(string.Empty);
    }

    [Fact]
    public void ChunkByHeaders_ChunksContainCorrectLineRanges()
    {
        // Arrange
        var markdown = """
            ## First Section

            Line 2 content.
            Line 3 content.

            ## Second Section

            Line 7 content.
            """;

        // Act
        var chunks = _sut.ChunkByHeaders(markdown);

        // Assert
        chunks.Count.ShouldBe(2);
        chunks[0].StartLine.ShouldBe(0);
        chunks[0].EndLine.ShouldBeLessThan(chunks[1].StartLine);
    }

    [Fact]
    public void ChunkByHeaders_IncludesHeaderPathInChunks()
    {
        // Arrange
        var markdown = """
            # Main

            ## Sub Section

            Content here.
            """;

        // Act
        var chunks = _sut.ChunkByHeaders(markdown);

        // Assert
        chunks.ShouldNotBeNull();
        // Should chunk at H2 and H3 only
        var h2Chunk = chunks.FirstOrDefault(c => c.HeaderPath.Contains("Sub Section"));
        h2Chunk.ShouldNotBeNull();
    }

    #endregion
}
