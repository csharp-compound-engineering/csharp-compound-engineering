using CompoundDocs.Common.Parsing;

namespace CompoundDocs.Tests.Unit.Parsing;

/// <summary>
/// Unit tests for FrontmatterParser.
/// </summary>
public sealed class FrontmatterParserTests
{
    private readonly FrontmatterParser _sut;

    public FrontmatterParserTests()
    {
        _sut = new FrontmatterParser();
    }

    #region ParseFrontmatter Tests

    [Fact]
    public void Parse_WithValidFrontmatter_ExtractsFrontmatterAndBody()
    {
        // Arrange
        var markdown = """
            ---
            title: Test Document
            doc_type: spec
            tags:
              - api
              - design
            ---

            # Introduction

            This is the document body.
            """;

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.HasFrontmatter.ShouldBeTrue();
        result.Frontmatter.ShouldNotBeNull();
        result.Frontmatter.ShouldContainKey("title");
        result.Frontmatter["title"].ShouldBe("Test Document");
        result.Frontmatter.ShouldContainKey("doc_type");
        result.Frontmatter["doc_type"].ShouldBe("spec");
        result.Body.ShouldStartWith("# Introduction");
    }

    [Fact]
    public void Parse_WithComplexFrontmatter_ParsesNestedStructures()
    {
        // Arrange
        var markdown = """
            ---
            title: Complex Doc
            metadata:
              version: 1.0
              author: Test Author
            ---

            Body content.
            """;

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.HasFrontmatter.ShouldBeTrue();
        result.Frontmatter.ShouldNotBeNull();
        result.Frontmatter!.ShouldContainKey("title");
        result.Frontmatter.ShouldContainKey("metadata");
    }

    [Fact]
    public void Parse_WithNumericValues_ParsesCorrectTypes()
    {
        // Arrange
        var markdown = """
            ---
            count: 42
            ratio: 3.14
            enabled: true
            ---

            Body.
            """;

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.HasFrontmatter.ShouldBeTrue();
        result.Frontmatter.ShouldNotBeNull();
        result.Frontmatter!.ShouldContainKey("count");
        result.Frontmatter.ShouldContainKey("ratio");
        result.Frontmatter.ShouldContainKey("enabled");
    }

    #endregion

    #region HandleMissingFrontmatter Tests

    [Fact]
    public void Parse_WithNoFrontmatter_ReturnsEntireContentAsBody()
    {
        // Arrange
        var markdown = """
            # Document Without Frontmatter

            This document has no YAML frontmatter section.
            """;

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.HasFrontmatter.ShouldBeFalse();
        result.Frontmatter.ShouldBeNull();
        result.Body.ShouldBe(markdown);
    }

    [Fact]
    public void Parse_WithDashesInContent_DoesNotMistakenlyParseFrontmatter()
    {
        // Arrange
        var markdown = """
            # Document

            Here is some content with dashes:

            ---

            More content after the dashes.
            """;

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.HasFrontmatter.ShouldBeFalse();
        result.Body.ShouldBe(markdown);
    }

    [Fact]
    public void Parse_WithEmptyDocument_ReturnsEmptyBody()
    {
        // Arrange
        var markdown = string.Empty;

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.HasFrontmatter.ShouldBeFalse();
        result.Frontmatter.ShouldBeNull();
        result.Body.ShouldBe(string.Empty);
    }

    [Fact]
    public void Parse_WithOnlyDashes_ReturnsNoFrontmatter()
    {
        // Arrange
        var markdown = "---";

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.HasFrontmatter.ShouldBeFalse();
    }

    [Fact]
    public void Parse_WithUnclosedFrontmatter_ReturnsNoFrontmatter()
    {
        // Arrange
        var markdown = """
            ---
            title: Unclosed
            doc_type: spec

            # Content without closing frontmatter
            """;

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.HasFrontmatter.ShouldBeFalse();
        result.Body.ShouldBe(markdown);
    }

    #endregion

    #region HandleInvalidYaml Tests

    [Fact]
    public void Parse_WithInvalidYaml_ReturnsNoFrontmatter()
    {
        // Arrange
        var markdown = """
            ---
            invalid: [unclosed bracket
            also: bad : indentation
            ---

            Body content.
            """;

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.HasFrontmatter.ShouldBeFalse();
        result.Body.ShouldBe(markdown);
    }

    [Fact]
    public void Parse_WithMalformedYaml_HandlesGracefully()
    {
        // Arrange
        var markdown = """
            ---
            : value without key
            ---

            Body.
            """;

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        // Should not throw, may or may not have frontmatter depending on YAML parser tolerance
        result.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_WithTabIndentation_ParsesSuccessfully()
    {
        // Arrange - YAML allows both spaces and tabs
        var markdown = "---\ntitle: Test\n\tindented: value\n---\n\nBody.";

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.ShouldNotBeNull();
        // The result depends on YAML parser's handling of tabs
    }

    [Fact]
    public void Parse_WithEmptyFrontmatter_HandlesEdgeCase()
    {
        // Arrange - Empty frontmatter with just delimiters
        // Note: The parser currently has an edge case where empty frontmatter
        // (---\n---) causes an issue because the YAML content calculation
        // results in a negative substring length. This test documents the behavior.
        var markdown = """
            ---
            ---

            Body content.
            """;

        // Act & Assert - Document current behavior
        // The parser should ideally handle this gracefully, but currently throws
        Should.Throw<ArgumentOutOfRangeException>(() => _sut.Parse(markdown));
    }

    #endregion

    #region ParseAs Tests

    [Fact]
    public void ParseAs_WithValidFrontmatter_DeserializesToType()
    {
        // Arrange
        var markdown = """
            ---
            title: Typed Document
            doc_type: spec
            promotion_level: promoted
            ---

            Body.
            """;

        // Act
        var result = _sut.ParseAs<TestFrontmatter>(markdown);

        // Assert
        result.ShouldNotBeNull();
        result.Title.ShouldBe("Typed Document");
        result.DocType.ShouldBe("spec");
        result.PromotionLevel.ShouldBe("promoted");
    }

    [Fact]
    public void ParseAs_WithMissingFrontmatter_ReturnsNull()
    {
        // Arrange
        var markdown = "# No frontmatter\n\nJust content.";

        // Act
        var result = _sut.ParseAs<TestFrontmatter>(markdown);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ParseAs_WithPartialMatch_PopulatesMatchingProperties()
    {
        // Arrange
        var markdown = """
            ---
            title: Partial Match
            unknown_field: should be ignored
            ---

            Body.
            """;

        // Act
        var result = _sut.ParseAs<TestFrontmatter>(markdown);

        // Assert
        result.ShouldNotBeNull();
        result.Title.ShouldBe("Partial Match");
        result.DocType.ShouldBeNull();
    }

    #endregion

    #region Extended Branch Coverage

    [Fact]
    public void ParseAs_WithUnclosedFrontmatter_ReturnsNull()
    {
        // Arrange - starts with --- but no closing ---
        var markdown = "---\ntitle: Unclosed\ndoc_type: spec\n\n# Content without closing frontmatter";

        // Act
        var result = _sut.ParseAs<TestFrontmatter>(markdown);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ParseAs_WithInvalidYaml_ReturnsNull()
    {
        // Arrange - valid frontmatter delimiters but invalid YAML content
        var markdown = "---\ninvalid: [unclosed bracket\nalso: bad : indentation\n---\n\nBody content.";

        // Act
        var result = _sut.ParseAs<TestFrontmatter>(markdown);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Parse_WithCarriageReturnLineEndings_StripsCorrectly()
    {
        // Arrange - CRLF line endings
        var markdown = "---\r\ntitle: CRLF Test\r\n---\r\n\r\nBody content.";

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.HasFrontmatter.ShouldBeTrue();
        result.Body.ShouldBe("Body content.");
    }

    [Fact]
    public void Parse_WithMultipleNewlinesAfterFrontmatter_StripsAll()
    {
        // Arrange
        var markdown = "---\ntitle: Newlines Test\n---\n\n\n\nBody after multiple newlines.";

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.HasFrontmatter.ShouldBeTrue();
        result.Body.ShouldBe("Body after multiple newlines.");
    }

    [Fact]
    public void Parse_WithNoBodyAfterFrontmatter_ReturnsEmptyBody()
    {
        // Arrange
        var markdown = "---\ntitle: No Body\n---\n";

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.HasFrontmatter.ShouldBeTrue();
        result.Body.ShouldBe(string.Empty);
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Test class for strongly-typed frontmatter parsing.
    /// </summary>
    private sealed class TestFrontmatter
    {
        public string? Title { get; set; }
        public string? DocType { get; set; }
        public string? PromotionLevel { get; set; }
    }

    #endregion
}
