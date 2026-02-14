using System.Reflection;
using CompoundDocs.Common.Parsing;
using CompoundDocs.McpServer.Processing;

namespace CompoundDocs.Tests.Unit.Processing;

/// <summary>
/// Unit tests for DocumentParser, ParsedDocument, DetailedParsedDocument, and CodeBlockInfo.
/// </summary>
public sealed class DocumentParserTests
{
    private readonly DocumentParser _sut;

    public DocumentParserTests()
    {
        _sut = new DocumentParser();
    }

    #region Parse - Null and Empty Input

    [Fact]
    public void Parse_WithNullContent_ReturnsEmpty()
    {
        // Act
        var result = _sut.Parse(null!);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Body.ShouldBe(string.Empty);
        result.RawContent.ShouldBe(string.Empty);
        result.HasFrontmatter.ShouldBeFalse();
        result.Frontmatter.ShouldBeNull();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void Parse_WithEmptyString_ReturnsEmpty()
    {
        // Act
        var result = _sut.Parse(string.Empty);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Body.ShouldBe(string.Empty);
        result.RawContent.ShouldBe(string.Empty);
        result.HasFrontmatter.ShouldBeFalse();
    }

    #endregion

    #region Parse - Content Without Frontmatter

    [Fact]
    public void Parse_WithPlainMarkdown_ReturnsSuccessWithoutFrontmatter()
    {
        // Arrange
        var content = "# Hello World\n\nSome content here.";

        // Act
        var result = _sut.Parse(content);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.HasFrontmatter.ShouldBeFalse();
        result.Frontmatter.ShouldBeNull();
        result.Body.ShouldBe(content);
        result.RawContent.ShouldBe(content);
    }

    [Fact]
    public void Parse_WithDashesInMiddleOfContent_DoesNotParseFrontmatter()
    {
        // Arrange
        var content = "# Title\n\nSome content\n---\nMore content";

        // Act
        var result = _sut.Parse(content);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.HasFrontmatter.ShouldBeFalse();
        result.Body.ShouldBe(content);
    }

    [Fact]
    public void Parse_WithUnclosedFrontmatter_TreatsEntireContentAsBody()
    {
        // Arrange
        var content = "---\ntitle: Unclosed\n\n# Content without closing frontmatter";

        // Act
        var result = _sut.Parse(content);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.HasFrontmatter.ShouldBeFalse();
    }

    #endregion

    #region Parse - Content With Frontmatter

    [Fact]
    public void Parse_WithValidFrontmatter_ExtractsFrontmatterAndBody()
    {
        // Arrange
        var content = "---\ntitle: My Doc\ntags:\n  - csharp\n---\n\n# My Doc\n\nBody text.";

        // Act
        var result = _sut.Parse(content);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.HasFrontmatter.ShouldBeTrue();
        result.Frontmatter.ShouldNotBeNull();
        result.Frontmatter!.ShouldContainKey("title");
        result.Body.ShouldContain("# My Doc");
        result.Body.ShouldContain("Body text.");
        result.RawContent.ShouldBe(content);
    }

    [Fact]
    public void Parse_WithFrontmatterOnly_ReturnsEmptyBody()
    {
        // Arrange
        var content = "---\ntitle: No Body\n---\n";

        // Act
        var result = _sut.Parse(content);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.HasFrontmatter.ShouldBeTrue();
        result.Frontmatter.ShouldNotBeNull();
    }

    #endregion

    #region ParsedDocument.Empty

    [Fact]
    public void ParsedDocument_Empty_ReturnsSuccessfulEmptyDocument()
    {
        // Act
        var result = ParsedDocument.Empty();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.HasFrontmatter.ShouldBeFalse();
        result.Frontmatter.ShouldBeNull();
        result.Body.ShouldBe(string.Empty);
        result.RawContent.ShouldBe(string.Empty);
        result.Error.ShouldBeNull();
    }

    #endregion

    #region ParsedDocument.Failure

    [Fact]
    public void ParsedDocument_Failure_ReturnsFailedDocumentWithError()
    {
        // Act
        var result = ParsedDocument.Failure("something went wrong");

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.HasFrontmatter.ShouldBeFalse();
        result.Body.ShouldBe(string.Empty);
        result.RawContent.ShouldBe(string.Empty);
        result.Error.ShouldBe("something went wrong");
    }

    [Fact]
    public void ParsedDocument_Failure_PreservesErrorMessage()
    {
        // Act
        var result = ParsedDocument.Failure("custom error: invalid YAML");

        // Assert
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("custom error");
        result.Error.ShouldContain("invalid YAML");
    }

    #endregion

    #region ExtractCodeBlocks - Null and Empty Input

    [Fact]
    public void ExtractCodeBlocks_WithNull_ReturnsEmptyList()
    {
        // Act
        var result = _sut.ExtractCodeBlocks(null!);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ExtractCodeBlocks_WithEmptyString_ReturnsEmptyList()
    {
        // Act
        var result = _sut.ExtractCodeBlocks(string.Empty);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ExtractCodeBlocks_WithNoCodeBlocks_ReturnsEmptyList()
    {
        // Arrange
        var body = "# Title\n\nJust some plain text, no code blocks at all.";

        // Act
        var result = _sut.ExtractCodeBlocks(body);

        // Assert
        result.ShouldBeEmpty();
    }

    #endregion

    #region ExtractCodeBlocks - Single Code Block

    [Fact]
    public void ExtractCodeBlocks_WithSingleCodeBlock_ExtractsLanguageAndCode()
    {
        // Arrange
        var body = "Some text\n\n```csharp\nvar x = 1;\n```\n\nMore text";

        // Act
        var result = _sut.ExtractCodeBlocks(body);

        // Assert
        result.Count.ShouldBe(1);
        result[0].Index.ShouldBe(0);
        result[0].Language.ShouldBe("csharp");
        result[0].Code.ShouldBe("var x = 1;");
    }

    [Fact]
    public void ExtractCodeBlocks_WithSingleCodeBlock_CalculatesStartLine()
    {
        // Arrange
        var body = "Line 0\nLine 1\n```csharp\nvar x = 1;\n```";

        // Act
        var result = _sut.ExtractCodeBlocks(body);

        // Assert
        result.Count.ShouldBe(1);
        result[0].StartLine.ShouldBe(2);
    }

    #endregion

    #region ExtractCodeBlocks - Multiple Code Blocks

    [Fact]
    public void ExtractCodeBlocks_WithMultipleCodeBlocks_ExtractsAllWithSequentialIndices()
    {
        // Arrange
        var body = "Text\n\n```python\nprint('hello')\n```\n\nMiddle\n\n```javascript\nconsole.log('hi');\n```";

        // Act
        var result = _sut.ExtractCodeBlocks(body);

        // Assert
        result.Count.ShouldBe(2);
        result[0].Index.ShouldBe(0);
        result[0].Language.ShouldBe("python");
        result[0].Code.ShouldBe("print('hello')");
        result[1].Index.ShouldBe(1);
        result[1].Language.ShouldBe("javascript");
        result[1].Code.ShouldBe("console.log('hi');");
    }

    #endregion

    #region ExtractCodeBlocks - Language Handling

    [Fact]
    public void ExtractCodeBlocks_WithNoLanguageSpecified_SetsLanguageToNull()
    {
        // Arrange
        var body = "Text\n\n```\nsome code\n```";

        // Act
        var result = _sut.ExtractCodeBlocks(body);

        // Assert
        result.Count.ShouldBe(1);
        result[0].Language.ShouldBeNull();
        result[0].Code.ShouldBe("some code");
    }

    [Fact]
    public void ExtractCodeBlocks_WithVariousLanguages_ParsesAllCorrectly()
    {
        // Arrange
        var body = "```rust\nfn main() {}\n```\n\n```go\nfunc main() {}\n```\n\n```yaml\nkey: value\n```";

        // Act
        var result = _sut.ExtractCodeBlocks(body);

        // Assert
        result.Count.ShouldBe(3);
        result[0].Language.ShouldBe("rust");
        result[1].Language.ShouldBe("go");
        result[2].Language.ShouldBe("yaml");
    }

    #endregion

    #region ExtractCodeBlocks - Line Number Calculation

    [Fact]
    public void ExtractCodeBlocks_WithMultilineCode_CalculatesEndLineCorrectly()
    {
        // Arrange
        var body = "```csharp\nline1\nline2\nline3\n```";

        // Act
        var result = _sut.ExtractCodeBlocks(body);

        // Assert
        result.Count.ShouldBe(1);
        result[0].StartLine.ShouldBe(0);
        result[0].EndLine.ShouldBeGreaterThan(result[0].StartLine);
    }

    [Fact]
    public void ExtractCodeBlocks_AtStartOfBody_StartsAtLineZero()
    {
        // Arrange
        var body = "```csharp\nvar x = 1;\n```";

        // Act
        var result = _sut.ExtractCodeBlocks(body);

        // Assert
        result[0].StartLine.ShouldBe(0);
    }

    #endregion

    #region ExtractCodeBlocks - Trimming

    [Fact]
    public void ExtractCodeBlocks_CodeIsTrimmedAtEnd()
    {
        // Arrange
        var body = "```csharp\nvar x = 1;\n\n```";

        // Act
        var result = _sut.ExtractCodeBlocks(body);

        // Assert
        result.Count.ShouldBe(1);
        result[0].Code.ShouldBe("var x = 1;");
    }

    #endregion

    #region IsValidDocument Tests

    [Fact]
    public void IsValidDocument_WithNull_ReturnsFalse()
    {
        // Act
        var result = _sut.IsValidDocument(null!);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsValidDocument_WithEmptyString_ReturnsFalse()
    {
        // Act
        var result = _sut.IsValidDocument(string.Empty);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsValidDocument_WithValidPlainMarkdown_ReturnsTrue()
    {
        // Act
        var result = _sut.IsValidDocument("# Title\n\nSome content.");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsValidDocument_WithValidFrontmatter_ReturnsTrue()
    {
        // Act
        var result = _sut.IsValidDocument("---\ntitle: Test\n---\n\n# Title");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsValidDocument_WithWhitespaceOnly_ReturnsTrue()
    {
        // Whitespace is not null or empty, so Parse succeeds
        var result = _sut.IsValidDocument("   \n\n  ");

        // Assert
        result.ShouldBeTrue();
    }

    #endregion

    #region ParseDetailed - Basic Cases

    [Fact]
    public void ParseDetailed_WithEmptyContent_ReturnsSuccessWithEmptyCollections()
    {
        // Act
        var result = _sut.ParseDetailed(string.Empty);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Body.ShouldBe(string.Empty);
        result.Headers.ShouldBeEmpty();
        result.Links.ShouldBeEmpty();
        result.CodeBlocks.ShouldBeEmpty();
        result.Title.ShouldBe(string.Empty);
    }

    [Fact]
    public void ParseDetailed_WithHeaders_ExtractsHeaders()
    {
        // Arrange
        var content = "# Main Title\n\n## Section One\n\nContent.\n\n## Section Two\n\nMore content.";

        // Act
        var result = _sut.ParseDetailed(content);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Headers.Count.ShouldBeGreaterThanOrEqualTo(3);
        result.Headers.ShouldContain(h => h.Text == "Main Title" && h.Level == 1);
        result.Headers.ShouldContain(h => h.Text == "Section One" && h.Level == 2);
        result.Headers.ShouldContain(h => h.Text == "Section Two" && h.Level == 2);
    }

    #endregion

    #region ParseDetailed - Links

    [Fact]
    public void ParseDetailed_WithInternalLinks_ExtractsLinks()
    {
        // Arrange
        var content = "# Title\n\nSee [other doc](./other.md) and [another](../docs/another.md).";

        // Act
        var result = _sut.ParseDetailed(content);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Links.Count.ShouldBe(2);
        result.Links.ShouldContain(l => l.Url == "./other.md");
        result.Links.ShouldContain(l => l.Url == "../docs/another.md");
    }

    [Fact]
    public void ParseDetailed_ExternalLinksAreNotIncluded()
    {
        // Arrange
        var content = "# Title\n\n[internal](./local.md) and [external](https://example.com).";

        // Act
        var result = _sut.ParseDetailed(content);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Links.ShouldContain(l => l.Url == "./local.md");
        result.Links.ShouldNotContain(l => l.Url.StartsWith("https://"));
    }

    #endregion

    #region ParseDetailed - Code Blocks

    [Fact]
    public void ParseDetailed_WithCodeBlocks_ExtractsCodeBlocks()
    {
        // Arrange
        var content = "# Title\n\n```csharp\nvar x = 1;\n```\n\n```python\nprint('hi')\n```";

        // Act
        var result = _sut.ParseDetailed(content);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.CodeBlocks.Count.ShouldBe(2);
        result.CodeBlocks[0].Language.ShouldBe("csharp");
        result.CodeBlocks[1].Language.ShouldBe("python");
    }

    #endregion

    #region ParseDetailed - Title Extraction

    [Fact]
    public void ParseDetailed_WithTitleInFrontmatter_ExtractsTitleFromFrontmatter()
    {
        // Arrange
        var content = "---\ntitle: Frontmatter Title\n---\n\n# Header Title\n\nBody.";

        // Act
        var result = _sut.ParseDetailed(content);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Title.ShouldBe("Frontmatter Title");
    }

    [Fact]
    public void ParseDetailed_WithNoFrontmatterTitle_FallsBackToFirstH1()
    {
        // Arrange
        var content = "# First H1 Header\n\n## Section\n\nBody.";

        // Act
        var result = _sut.ParseDetailed(content);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Title.ShouldBe("First H1 Header");
    }

    [Fact]
    public void ParseDetailed_WithNoTitleAnywhere_ReturnsEmptyTitle()
    {
        // Arrange
        var content = "## Only H2\n\nSome body without an H1.";

        // Act
        var result = _sut.ParseDetailed(content);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Title.ShouldBe(string.Empty);
    }

    #endregion

    #region ParseDetailed - Frontmatter Preservation

    [Fact]
    public void ParseDetailed_WithFrontmatter_PreservesFrontmatterFields()
    {
        // Arrange
        var content = "---\ntitle: Doc\ntags:\n  - api\n---\n\n# Doc\n\nBody.";

        // Act
        var result = _sut.ParseDetailed(content);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.HasFrontmatter.ShouldBeTrue();
        result.Frontmatter.ShouldNotBeNull();
        result.Frontmatter!.ShouldContainKey("title");
        result.RawContent.ShouldBe(content);
    }

    [Fact]
    public void ParseDetailed_WithoutFrontmatter_SetsHasFrontmatterFalse()
    {
        // Arrange
        var content = "# Just a heading\n\nBody content.";

        // Act
        var result = _sut.ParseDetailed(content);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.HasFrontmatter.ShouldBeFalse();
        result.Frontmatter.ShouldBeNull();
    }

    #endregion

    #region CodeBlockInfo Tests

    [Fact]
    public void CodeBlockInfo_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var info = new CodeBlockInfo();

        // Assert
        info.Index.ShouldBe(0);
        info.Language.ShouldBeNull();
        info.Code.ShouldBe(string.Empty);
        info.StartLine.ShouldBe(0);
        info.EndLine.ShouldBe(0);
    }

    [Fact]
    public void CodeBlockInfo_InitProperties_SetCorrectly()
    {
        // Arrange & Act
        var info = new CodeBlockInfo
        {
            Index = 3,
            Language = "rust",
            Code = "fn main() {}",
            StartLine = 10,
            EndLine = 12
        };

        // Assert
        info.Index.ShouldBe(3);
        info.Language.ShouldBe("rust");
        info.Code.ShouldBe("fn main() {}");
        info.StartLine.ShouldBe(10);
        info.EndLine.ShouldBe(12);
    }

    #endregion

    #region DetailedParsedDocument Tests

    [Fact]
    public void DetailedParsedDocument_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var doc = new DetailedParsedDocument();

        // Assert
        doc.HasFrontmatter.ShouldBeFalse();
        doc.Frontmatter.ShouldBeNull();
        doc.Body.ShouldBe(string.Empty);
        doc.RawContent.ShouldBe(string.Empty);
        doc.IsSuccess.ShouldBeFalse();
        doc.Error.ShouldBeNull();
        doc.Title.ShouldBe(string.Empty);
        doc.Headers.ShouldBeEmpty();
        doc.Links.ShouldBeEmpty();
        doc.CodeBlocks.ShouldBeEmpty();
    }

    [Fact]
    public void DetailedParsedDocument_InitProperties_SetCorrectly()
    {
        // Arrange & Act
        var doc = new DetailedParsedDocument
        {
            HasFrontmatter = true,
            Body = "body",
            RawContent = "raw",
            IsSuccess = true,
            Title = "My Title",
            Error = null
        };

        // Assert
        doc.HasFrontmatter.ShouldBeTrue();
        doc.Body.ShouldBe("body");
        doc.RawContent.ShouldBe("raw");
        doc.IsSuccess.ShouldBeTrue();
        doc.Title.ShouldBe("My Title");
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullParameters_CreatesWorkingParser()
    {
        // Act
        var parser = new DocumentParser(null, null);

        // Assert - verify it works by parsing a document
        var result = parser.Parse("# Test");
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithDefaultParameters_CreatesWorkingParser()
    {
        // Act
        var parser = new DocumentParser();

        // Assert
        var result = parser.Parse("---\ntitle: Test\n---\n\nBody");
        result.IsSuccess.ShouldBeTrue();
        result.HasFrontmatter.ShouldBeTrue();
    }

    #endregion

    #region ExtractTitle - Frontmatter Without Title Key (Line 195)

    [Fact]
    public void ParseDetailed_WithFrontmatterMissingTitleKey_FallsBackToFirstH1()
    {
        // Arrange - frontmatter has keys but no "title" key
        var content = "---\nauthor: Jane Doe\ntags:\n  - guide\n---\n\n# Fallback Header\n\nBody content.";

        // Act
        var result = _sut.ParseDetailed(content);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.HasFrontmatter.ShouldBeTrue();
        result.Frontmatter.ShouldNotBeNull();
        result.Frontmatter!.ShouldNotContainKey("title");
        result.Title.ShouldBe("Fallback Header");
    }

    [Fact]
    public void ParseDetailed_WithFrontmatterNonStringTitle_FallsBackToFirstH1()
    {
        // Arrange - frontmatter has a "title" key but its value is not a string (it's a list)
        var content = "---\ntitle:\n  - part one\n  - part two\n---\n\n# Real Title\n\nBody.";

        // Act
        var result = _sut.ParseDetailed(content);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.HasFrontmatter.ShouldBeTrue();
        // Title from frontmatter is not a simple string, so it should fall back to H1
        result.Title.ShouldBe("Real Title");
    }

    [Fact]
    public void ParseDetailed_WithFrontmatterNullTitle_FallsBackToFirstH1()
    {
        // Arrange - frontmatter has title key with null/empty value
        var content = "---\ntitle:\ndescription: A doc\n---\n\n# Header Title\n\nBody.";

        // Act
        var result = _sut.ParseDetailed(content);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.HasFrontmatter.ShouldBeTrue();
        // null title value should fall back to H1
        result.Title.ShouldBe("Header Title");
    }

    #endregion

    #region ExtractTitle - Frontmatter Without Title And No H1

    [Fact]
    public void ParseDetailed_WithFrontmatterNoTitleAndNoH1_ReturnsEmptyTitle()
    {
        // Arrange - frontmatter present but no title key, and body has no H1
        var content = "---\nauthor: Someone\n---\n\n## Only H2 Here\n\nBody content.";

        // Act
        var result = _sut.ParseDetailed(content);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.HasFrontmatter.ShouldBeTrue();
        result.Title.ShouldBe(string.Empty);
    }

    #endregion

    #region Parse - Catch Block (Lines 75-77)

    [Fact]
    public void Parse_WhenFrontmatterParserIsNull_ReturnsFailureWithErrorMessage()
    {
        // Arrange - Use reflection to set _frontmatterParser to null,
        // causing a NullReferenceException that triggers the catch block.
        var parser = new DocumentParser();
        var field = typeof(DocumentParser).GetField("_frontmatterParser", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(parser, null);

        var content = "---\ntitle: Test\n---\n\nBody content.";

        // Act
        var result = parser.Parse(content);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.ShouldContain("Failed to parse document");
        result.HasFrontmatter.ShouldBeFalse();
        result.Body.ShouldBe(string.Empty);
    }

    #endregion

    #region ParseDetailed - Failure Path (Lines 91-99) and Catch Block (Lines 125-135)

    [Fact]
    public void ParseDetailed_WhenParseReturnsFailure_ReturnsDetailedFailureWithError()
    {
        // Arrange - Use reflection to null out _frontmatterParser,
        // causing Parse() to return a Failure, then ParseDetailed hits the !IsSuccess branch.
        var parser = new DocumentParser();
        var field = typeof(DocumentParser).GetField("_frontmatterParser", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(parser, null);

        var content = "---\ntitle: Test\n---\n\nBody content.";

        // Act
        var result = parser.ParseDetailed(content);

        // Assert - should hit the !basicResult.IsSuccess branch (lines 90-100)
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.ShouldContain("Failed to parse document");
        result.HasFrontmatter.ShouldBeFalse();
        result.Body.ShouldBe(content);
        result.RawContent.ShouldBe(content);
    }

    [Fact]
    public void ParseDetailed_WhenMarkdownParserIsNull_ReturnsCatchBlockFailure()
    {
        // Arrange - Use reflection to null out _markdownParser,
        // causing a NullReferenceException in ParseDetailed's try block (line 104).
        var parser = new DocumentParser();
        var field = typeof(DocumentParser).GetField("_markdownParser", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(parser, null);

        var content = "# Hello\n\nSome body content.";

        // Act
        var result = parser.ParseDetailed(content);

        // Assert - should hit the catch block (lines 125-135)
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.ShouldContain("Failed to parse document structure");
        result.HasFrontmatter.ShouldBeFalse();
        result.Body.ShouldBe(content);
        result.RawContent.ShouldBe(content);
    }

    [Fact]
    public void ParseDetailed_WhenMarkdownParserIsNullOnFrontmatterDoc_PreservesFrontmatter()
    {
        // Arrange - Null out _markdownParser, but frontmatter parsing still succeeds
        // because _frontmatterParser is intact.
        var parser = new DocumentParser();
        var field = typeof(DocumentParser).GetField("_markdownParser", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(parser, null);

        var content = "---\ntitle: My Title\n---\n\n# Content";

        // Act
        var result = parser.ParseDetailed(content);

        // Assert - catch block should preserve frontmatter from basicResult
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.ShouldContain("Failed to parse document structure");
        result.HasFrontmatter.ShouldBeTrue();
        result.Frontmatter.ShouldNotBeNull();
        result.Frontmatter!.ShouldContainKey("title");
    }

    #endregion

    #region ParseDetailed - Null Content

    [Fact]
    public void ParseDetailed_WithNullContent_ReturnsSuccessWithEmptyCollections()
    {
        // Act
        var result = _sut.ParseDetailed(null!);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Body.ShouldBe(string.Empty);
        result.Headers.ShouldBeEmpty();
        result.Links.ShouldBeEmpty();
        result.CodeBlocks.ShouldBeEmpty();
        result.Title.ShouldBe(string.Empty);
    }

    #endregion
}
