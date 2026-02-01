using CompoundDocs.McpServer.Processing;

namespace CompoundDocs.Tests.Processing;

/// <summary>
/// Unit tests for DocumentParser.
/// </summary>
public sealed class DocumentParserTests
{
    private readonly DocumentParser _sut;

    public DocumentParserTests()
    {
        _sut = new DocumentParser();
    }

    #region Parse Tests

    [Fact]
    public void Parse_WithValidFrontmatter_ExtractsFrontmatterAndBody()
    {
        // Arrange
        var markdown = """
            ---
            title: Test Document
            doc_type: spec
            ---

            # Introduction

            This is the document body.
            """;

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.HasFrontmatter.ShouldBeTrue();
        result.Frontmatter.ShouldNotBeNull();
        result.Frontmatter.ShouldContainKey("title");
        result.Frontmatter["title"].ShouldBe("Test Document");
        result.Body.ShouldStartWith("# Introduction");
    }

    [Fact]
    public void Parse_WithNoFrontmatter_ReturnsEntireContentAsBody()
    {
        // Arrange
        var markdown = """
            # Document Without Frontmatter

            This document has no YAML frontmatter.
            """;

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.HasFrontmatter.ShouldBeFalse();
        result.Frontmatter.ShouldBeNull();
        result.Body.ShouldBe(markdown);
    }

    [Fact]
    public void Parse_WithEmptyDocument_ReturnsEmptyResult()
    {
        // Act
        var result = _sut.Parse(string.Empty);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.HasFrontmatter.ShouldBeFalse();
        result.Body.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_WithNullDocument_ReturnsEmptyResult()
    {
        // Act
        var result = _sut.Parse(null!);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.HasFrontmatter.ShouldBeFalse();
    }

    [Fact]
    public void Parse_WithUnclosedFrontmatter_ReturnsNoFrontmatter()
    {
        // Arrange
        var markdown = """
            ---
            title: Unclosed

            # Content without closing frontmatter
            """;

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.HasFrontmatter.ShouldBeFalse();
        result.Body.ShouldBe(markdown);
    }

    [Fact]
    public void Parse_WithComplexFrontmatter_ParsesNestedStructures()
    {
        // Arrange
        var markdown = """
            ---
            title: Complex Doc
            tags:
              - api
              - design
            metadata:
              version: 1.0
            ---

            Body content.
            """;

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.HasFrontmatter.ShouldBeTrue();
        result.Frontmatter.ShouldNotBeNull();
        result.Frontmatter.ShouldContainKey("title");
        result.Frontmatter.ShouldContainKey("tags");
    }

    #endregion

    #region ParseDetailed Tests

    [Fact]
    public void ParseDetailed_ExtractsHeaders()
    {
        // Arrange
        var markdown = """
            ---
            title: Test Document
            ---

            # Main Title

            ## Section One

            Content here.

            ## Section Two

            More content.
            """;

        // Act
        var result = _sut.ParseDetailed(markdown);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Headers.Count.ShouldBeGreaterThan(0);
        result.Headers.Any(h => h.Text == "Main Title").ShouldBeTrue();
    }

    [Fact]
    public void ParseDetailed_ExtractsLinks()
    {
        // Arrange
        var markdown = """
            # Document

            See [related doc](./related.md) for more info.
            Also check [another doc](../other/doc.md).
            """;

        // Act
        var result = _sut.ParseDetailed(markdown);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Links.Count.ShouldBe(2);
        result.Links[0].Url.ShouldBe("./related.md");
    }

    [Fact]
    public void ParseDetailed_ExtractsTitleFromFrontmatter()
    {
        // Arrange
        var markdown = """
            ---
            title: Frontmatter Title
            ---

            # Header Title

            Content.
            """;

        // Act
        var result = _sut.ParseDetailed(markdown);

        // Assert
        result.Title.ShouldBe("Frontmatter Title");
    }

    [Fact]
    public void ParseDetailed_ExtractsTitleFromH1WhenNoFrontmatter()
    {
        // Arrange
        var markdown = """
            # Header Title

            Content without frontmatter.
            """;

        // Act
        var result = _sut.ParseDetailed(markdown);

        // Assert
        result.Title.ShouldBe("Header Title");
    }

    #endregion

    #region ExtractCodeBlocks Tests

    [Fact]
    public void ExtractCodeBlocks_FindsCodeBlocks()
    {
        // Arrange
        var body = """
            # Code Example

            ```csharp
            public class Foo { }
            ```

            Some text.

            ```javascript
            const x = 1;
            ```
            """;

        // Act
        var codeBlocks = _sut.ExtractCodeBlocks(body);

        // Assert
        codeBlocks.Count.ShouldBe(2);
        codeBlocks[0].Language.ShouldBe("csharp");
        codeBlocks[1].Language.ShouldBe("javascript");
    }

    [Fact]
    public void ExtractCodeBlocks_HandlesCodeBlocksWithNoLanguage()
    {
        // Arrange
        var body = """
            ```
            plain code
            ```
            """;

        // Act
        var codeBlocks = _sut.ExtractCodeBlocks(body);

        // Assert
        codeBlocks.Count.ShouldBe(1);
        codeBlocks[0].Language.ShouldBeNull();
    }

    [Fact]
    public void ExtractCodeBlocks_ReturnsEmptyForNoCodeBlocks()
    {
        // Arrange
        var body = "Just plain text without code blocks.";

        // Act
        var codeBlocks = _sut.ExtractCodeBlocks(body);

        // Assert
        codeBlocks.Count.ShouldBe(0);
    }

    #endregion

    #region IsValidDocument Tests

    [Fact]
    public void IsValidDocument_ReturnsTrueForValidDocument()
    {
        // Arrange
        var markdown = """
            ---
            title: Valid Doc
            ---

            Content here.
            """;

        // Act
        var isValid = _sut.IsValidDocument(markdown);

        // Assert
        isValid.ShouldBeTrue();
    }

    [Fact]
    public void IsValidDocument_ReturnsFalseForEmptyDocument()
    {
        // Act
        var isValid = _sut.IsValidDocument(string.Empty);

        // Assert
        isValid.ShouldBeFalse();
    }

    #endregion
}
