using CompoundDocs.McpServer.Processing;

namespace CompoundDocs.Tests.Processing;

/// <summary>
/// Unit tests for FrontmatterParser in the Processing namespace.
/// </summary>
public sealed class ProcessingFrontmatterParserTests
{
    private readonly FrontmatterParser _sut;

    public ProcessingFrontmatterParserTests()
    {
        _sut = new FrontmatterParser();
    }

    #region Parse Tests

    [Fact]
    public void Parse_WithValidFrontmatter_ReturnsSuccess()
    {
        // Arrange
        var markdown = """
            ---
            title: Test Document
            doc_type: spec
            ---

            # Body Content
            """;

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.HasFrontmatter.ShouldBeTrue();
        result.Frontmatter.ShouldNotBeNull();
        result.Frontmatter!["title"].ShouldBe("Test Document");
    }

    [Fact]
    public void Parse_WithNumericValues_ParsesCorrectly()
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
        result.IsSuccess.ShouldBeTrue();
        result.Frontmatter.ShouldNotBeNull();
        result.Frontmatter!.ContainsKey("count").ShouldBeTrue();
        result.Frontmatter!.ContainsKey("ratio").ShouldBeTrue();
        result.Frontmatter!.ContainsKey("enabled").ShouldBeTrue();
    }

    [Fact]
    public void Parse_WithListValues_ParsesCorrectly()
    {
        // Arrange
        var markdown = """
            ---
            tags:
              - api
              - design
              - backend
            ---

            Body.
            """;

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Frontmatter.ShouldNotBeNull();
        result.Frontmatter!.ContainsKey("tags").ShouldBeTrue();
    }

    [Fact]
    public void Parse_WithNestedObjects_ParsesCorrectly()
    {
        // Arrange
        var markdown = """
            ---
            metadata:
              version: 1.0
              author: Test Author
            ---

            Body.
            """;

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Frontmatter.ShouldNotBeNull();
        result.Frontmatter!.ContainsKey("metadata").ShouldBeTrue();
    }

    [Fact]
    public void Parse_WithNoFrontmatter_ReturnsNoFrontmatterResult()
    {
        // Arrange
        var markdown = """
            # Document Without Frontmatter

            Just content here.
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
    public void Parse_WithEmptyDocument_ReturnsNoFrontmatter()
    {
        // Act
        var result = _sut.Parse(string.Empty);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.HasFrontmatter.ShouldBeFalse();
    }

    [Fact]
    public void Parse_WithInvalidYaml_ReturnsParseError()
    {
        // Arrange
        var markdown = """
            ---
            invalid: [unclosed bracket
            ---

            Body.
            """;

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Parse_WithUnclosedFrontmatter_ReturnsNoFrontmatter()
    {
        // Arrange
        var markdown = """
            ---
            title: Unclosed

            Content without closing delimiter.
            """;

        // Act
        var result = _sut.Parse(markdown);

        // Assert
        result.HasFrontmatter.ShouldBeFalse();
        result.Body.ShouldBe(markdown);
    }

    #endregion

    #region ParseAndValidate Tests

    [Fact]
    public void ParseAndValidate_WithAllRequiredFields_ReturnsSuccess()
    {
        // Arrange
        var markdown = """
            ---
            title: Test Doc
            doc_type: spec
            ---

            Body.
            """;
        var requiredFields = new List<string> { "title", "doc_type" };

        // Act
        var result = _sut.ParseAndValidate(markdown, requiredFields);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ParseAndValidate_WithMissingRequiredFields_ReturnsValidationErrors()
    {
        // Arrange
        var markdown = """
            ---
            title: Test Doc
            ---

            Body.
            """;
        var requiredFields = new List<string> { "title", "doc_type", "author" };

        // Act
        var result = _sut.ParseAndValidate(markdown, requiredFields);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.Count.ShouldBe(2); // doc_type and author missing
    }

    [Fact]
    public void ParseAndValidate_WithNoFrontmatterAndRequiredFields_ReturnsErrors()
    {
        // Arrange
        var markdown = "# No Frontmatter";
        var requiredFields = new List<string> { "title" };

        // Act
        var result = _sut.ParseAndValidate(markdown, requiredFields);

        // Assert
        result.IsSuccess.ShouldBeFalse();
    }

    #endregion

    #region Helper Method Tests

    [Fact]
    public void GetValue_WithExistingKey_ReturnsValue()
    {
        // Arrange
        var frontmatter = new Dictionary<string, object?>
        {
            ["title"] = "Test Title",
            ["count"] = 42
        };

        // Act
        var title = FrontmatterParser.GetValue(frontmatter, "title", string.Empty);
        var count = FrontmatterParser.GetValue(frontmatter, "count", 0);

        // Assert
        title.ShouldBe("Test Title");
        count.ShouldBe(42);
    }

    [Fact]
    public void GetValue_WithMissingKey_ReturnsDefault()
    {
        // Arrange
        var frontmatter = new Dictionary<string, object?>();

        // Act
        var title = FrontmatterParser.GetValue(frontmatter, "title", "default");

        // Assert
        title.ShouldBe("default");
    }

    [Fact]
    public void GetStringList_WithList_ReturnsList()
    {
        // Arrange
        var frontmatter = new Dictionary<string, object?>
        {
            ["tags"] = new List<object?> { "api", "design", "backend" }
        };

        // Act
        var tags = FrontmatterParser.GetStringList(frontmatter, "tags");

        // Assert
        tags.Count.ShouldBe(3);
        tags.ShouldContain("api");
    }

    [Fact]
    public void GetStringList_WithMissingKey_ReturnsEmptyList()
    {
        // Arrange
        var frontmatter = new Dictionary<string, object?>();

        // Act
        var tags = FrontmatterParser.GetStringList(frontmatter, "tags");

        // Assert
        tags.Count.ShouldBe(0);
    }

    #endregion
}
