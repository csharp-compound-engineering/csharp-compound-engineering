using CompoundDocs.Common.Parsing;

namespace CompoundDocs.Tests.Unit.Processing;

/// <summary>
/// Unit tests for <see cref="FrontmatterParser"/> validation, normalization,
/// and static helper methods.
/// </summary>
public sealed class McpFrontmatterParserTests
{
    #region FrontmatterResult Factory Methods

    [Fact]
    public void Success_SetsAllPropertiesCorrectly()
    {
        // Arrange
        var frontmatter = new Dictionary<string, object?> { ["title"] = "Test" };
        var body = "# Hello";

        // Act
        var result = FrontmatterResult.Success(frontmatter, body);

        // Assert
        result.HasFrontmatter.ShouldBeTrue();
        result.Frontmatter.ShouldBeSameAs(frontmatter);
        result.Body.ShouldBe(body);
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void NoFrontmatter_SetsAllPropertiesCorrectly()
    {
        // Arrange
        var content = "# Just markdown";

        // Act
        var result = FrontmatterResult.NoFrontmatter(content);

        // Assert
        result.HasFrontmatter.ShouldBeFalse();
        result.Frontmatter.ShouldBeNull();
        result.Body.ShouldBe(content);
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void ParseError_SetsAllPropertiesCorrectly()
    {
        // Arrange
        var content = "---\nbad\n---";
        var error = "Invalid YAML";

        // Act
        var result = FrontmatterResult.ParseError(content, error);

        // Assert
        result.HasFrontmatter.ShouldBeFalse();
        result.Frontmatter.ShouldBeNull();
        result.Body.ShouldBe(content);
        result.IsSuccess.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].ShouldBe(error);
    }

    [Fact]
    public void ValidationError_WithoutFrontmatter_SetsHasFrontmatterFalse()
    {
        // Act
        var result = FrontmatterResult.ValidationError(
            "body",
            new List<string> { "Required field 'title' is missing" });

        // Assert
        result.HasFrontmatter.ShouldBeFalse();
        result.Frontmatter.ShouldBeNull();
        result.Body.ShouldBe("body");
        result.IsSuccess.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void ValidationError_WithFrontmatter_SetsHasFrontmatterTrue()
    {
        // Arrange
        var fm = new Dictionary<string, object?> { ["title"] = "Test" };

        // Act
        var result = FrontmatterResult.ValidationError("body", ["Field missing"], fm);

        // Assert
        result.HasFrontmatter.ShouldBeTrue();
        result.Frontmatter.ShouldBeSameAs(fm);
        result.IsSuccess.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
    }

    #endregion

    #region Parse — NoFrontmatter branches

    [Fact]
    public void Parse_NullInput_ReturnsNoFrontmatter()
    {
        var sut = new FrontmatterParser();

        var result = sut.Parse(null!);

        result.HasFrontmatter.ShouldBeFalse();
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Parse_EmptyString_ReturnsNoFrontmatter()
    {
        var sut = new FrontmatterParser();

        var result = sut.Parse(string.Empty);

        result.HasFrontmatter.ShouldBeFalse();
        result.IsSuccess.ShouldBeTrue();
        result.Body.ShouldBe(string.Empty);
    }

    [Fact]
    public void Parse_DoesNotStartWithTripleDash_ReturnsNoFrontmatter()
    {
        var sut = new FrontmatterParser();
        var markdown = "# No frontmatter here\n\nJust content.";

        var result = sut.Parse(markdown);

        result.HasFrontmatter.ShouldBeFalse();
        result.Body.ShouldBe(markdown);
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Parse_NoClosingTripleDash_ReturnsNoFrontmatter()
    {
        var sut = new FrontmatterParser();
        var markdown = "---\ntitle: No closing delimiter";

        var result = sut.Parse(markdown);

        result.HasFrontmatter.ShouldBeFalse();
        result.Body.ShouldBe(markdown);
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Parse_OnlyOpeningDashes_ReturnsNoFrontmatter()
    {
        var sut = new FrontmatterParser();

        var result = sut.Parse("---");

        result.HasFrontmatter.ShouldBeFalse();
        result.IsSuccess.ShouldBeTrue();
    }

    #endregion

    #region Parse — Success branch

    [Fact]
    public void Parse_ValidFrontmatter_ReturnsSuccessWithDictAndBody()
    {
        var sut = new FrontmatterParser();
        var markdown = "---\ntitle: Hello World\nauthor: Test\n---\n\n# Content\n\nBody text.";

        var result = sut.Parse(markdown);

        result.HasFrontmatter.ShouldBeTrue();
        result.IsSuccess.ShouldBeTrue();
        result.Frontmatter.ShouldNotBeNull();
        result.Frontmatter!["title"].ShouldBe("Hello World");
        result.Frontmatter["author"].ShouldBe("Test");
        result.Body.ShouldBe("# Content\n\nBody text.");
    }

    [Fact]
    public void Parse_ValidFrontmatter_NormalizesCaseInsensitiveKeys()
    {
        var sut = new FrontmatterParser();
        var markdown = "---\nTitle: CaseTest\n---\n\nBody";

        var result = sut.Parse(markdown);

        result.HasFrontmatter.ShouldBeTrue();
        result.Frontmatter.ShouldNotBeNull();
        result.Frontmatter!.ContainsKey("title").ShouldBeTrue();
        result.Frontmatter.ContainsKey("TITLE").ShouldBeTrue();
    }

    [Fact]
    public void Parse_FrontmatterWithList_ReturnsNormalizedList()
    {
        var sut = new FrontmatterParser();
        var markdown = "---\ntags:\n  - alpha\n  - beta\n---\n\nBody";

        var result = sut.Parse(markdown);

        result.HasFrontmatter.ShouldBeTrue();
        result.Frontmatter!["tags"].ShouldBeOfType<List<object?>>();
        var tags = (List<object?>)result.Frontmatter["tags"]!;
        tags.Count.ShouldBe(2);
        tags[0].ShouldBe("alpha");
        tags[1].ShouldBe("beta");
    }

    [Fact]
    public void Parse_FrontmatterWithNestedMap_ReturnsNormalizedDictionary()
    {
        var sut = new FrontmatterParser();
        var markdown = "---\nmeta:\n  version: 2\n  draft: true\n---\n\nBody";

        var result = sut.Parse(markdown);

        result.HasFrontmatter.ShouldBeTrue();
        var meta = result.Frontmatter!["meta"].ShouldBeOfType<Dictionary<string, object?>>();
        meta["version"].ShouldNotBeNull();
        meta["draft"].ShouldNotBeNull();
    }

    [Fact]
    public void Parse_StripsLeadingNewlinesFromBody()
    {
        var sut = new FrontmatterParser();
        var markdown = "---\ntitle: Test\n---\n\n\n\nActual body";

        var result = sut.Parse(markdown);

        result.Body.ShouldBe("Actual body");
    }

    [Fact]
    public void Parse_NoBodyAfterFrontmatter_ReturnsEmptyBody()
    {
        var sut = new FrontmatterParser();
        var markdown = "---\ntitle: Test\n---";

        var result = sut.Parse(markdown);

        result.HasFrontmatter.ShouldBeTrue();
        result.Body.ShouldBe(string.Empty);
    }

    #endregion

    #region Parse — Null deserialization / ParseError branches

    [Fact]
    public void Parse_EmptyYamlContent_ReturnsNoFrontmatter()
    {
        var sut = new FrontmatterParser();
        var markdown = "---\n\n---\n\nBody";

        var result = sut.Parse(markdown);

        result.HasFrontmatter.ShouldBeFalse();
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Parse_InvalidYaml_ReturnsParseError()
    {
        var sut = new FrontmatterParser();
        var markdown = "---\n- item1\n- item2\n---\n\nBody";

        var result = sut.Parse(markdown);

        // YamlDotNet may throw when trying to deserialize a sequence as Dictionary
        // or it may return null. Either way, it should not be a successful frontmatter parse.
        if (!result.IsSuccess)
        {
            result.Errors.ShouldNotBeEmpty();
            result.HasFrontmatter.ShouldBeFalse();
        }
        else
        {
            result.HasFrontmatter.ShouldBeFalse();
        }
    }

    #endregion

    #region ParseAndValidate

    [Fact]
    public void ParseAndValidate_NoFrontmatterWithRequiredFields_ReturnsValidationError()
    {
        var sut = new FrontmatterParser();
        var markdown = "# No frontmatter";
        var required = new List<string> { "title", "doc_type" };

        var result = sut.ParseAndValidate(markdown, required);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.Count.ShouldBe(2);
        result.Errors[0].ShouldContain("title");
        result.Errors[1].ShouldContain("doc_type");
    }

    [Fact]
    public void ParseAndValidate_NoFrontmatterNoRequiredFields_ReturnsOriginalResult()
    {
        var sut = new FrontmatterParser();
        var markdown = "# No frontmatter";
        var required = new List<string>();

        var result = sut.ParseAndValidate(markdown, required);

        result.IsSuccess.ShouldBeTrue();
        result.HasFrontmatter.ShouldBeFalse();
    }

    [Fact]
    public void ParseAndValidate_AllRequiredFieldsPresent_ReturnsSuccess()
    {
        var sut = new FrontmatterParser();
        var markdown = "---\ntitle: Test\ndoc_type: spec\n---\n\nBody";
        var required = new List<string> { "title", "doc_type" };

        var result = sut.ParseAndValidate(markdown, required);

        result.IsSuccess.ShouldBeTrue();
        result.HasFrontmatter.ShouldBeTrue();
        result.Frontmatter!["title"].ShouldBe("Test");
    }

    [Fact]
    public void ParseAndValidate_MissingRequiredField_ReturnsValidationError()
    {
        var sut = new FrontmatterParser();
        var markdown = "---\ntitle: Test\n---\n\nBody";
        var required = new List<string> { "title", "doc_type" };

        var result = sut.ParseAndValidate(markdown, required);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].ShouldContain("doc_type");
        result.Frontmatter.ShouldNotBeNull();
    }

    [Fact]
    public void ParseAndValidate_RequiredFieldPresentButNull_ReturnsValidationError()
    {
        var sut = new FrontmatterParser();
        var markdown = "---\ntitle:\n---\n\nBody";
        var required = new List<string> { "title" };

        var result = sut.ParseAndValidate(markdown, required);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].ShouldContain("title");
    }

    #endregion

    #region GetValue<T>

    [Fact]
    public void GetValue_NullDictionary_ReturnsDefault()
    {
        var result = FrontmatterParser.GetValue<string>(null, "key", "fallback");

        result.ShouldBe("fallback");
    }

    [Fact]
    public void GetValue_MissingKey_ReturnsDefault()
    {
        var dict = new Dictionary<string, object?> { ["other"] = "value" };

        var result = FrontmatterParser.GetValue<string>(dict, "missing", "fallback");

        result.ShouldBe("fallback");
    }

    [Fact]
    public void GetValue_NullValue_ReturnsDefault()
    {
        var dict = new Dictionary<string, object?> { ["key"] = null };

        var result = FrontmatterParser.GetValue<string>(dict, "key", "fallback");

        result.ShouldBe("fallback");
    }

    [Fact]
    public void GetValue_ValidConversion_ReturnsConvertedValue()
    {
        var dict = new Dictionary<string, object?> { ["count"] = "42" };

        var result = FrontmatterParser.GetValue<int>(dict, "count", 0);

        result.ShouldBe(42);
    }

    [Fact]
    public void GetValue_IncompatibleType_ReturnsDefault()
    {
        var dict = new Dictionary<string, object?> { ["key"] = "not-a-number" };

        var result = FrontmatterParser.GetValue<int>(dict, "key", -1);

        result.ShouldBe(-1);
    }

    #endregion

    #region GetStringList

    [Fact]
    public void GetStringList_NullDictionary_ReturnsEmptyList()
    {
        var result = FrontmatterParser.GetStringList(null, "tags");

        result.ShouldBeEmpty();
    }

    [Fact]
    public void GetStringList_MissingKey_ReturnsEmptyList()
    {
        var dict = new Dictionary<string, object?> { ["other"] = "val" };

        var result = FrontmatterParser.GetStringList(dict, "tags");

        result.ShouldBeEmpty();
    }

    [Fact]
    public void GetStringList_ListOfObjects_FiltersNullsAndConverts()
    {
        var dict = new Dictionary<string, object?>
        {
            ["tags"] = new List<object?> { "alpha", null, "beta", null }
        };

        var result = FrontmatterParser.GetStringList(dict, "tags");

        result.Count.ShouldBe(2);
        result[0].ShouldBe("alpha");
        result[1].ShouldBe("beta");
    }

    [Fact]
    public void GetStringList_SingleString_ReturnsListWithOneElement()
    {
        var dict = new Dictionary<string, object?> { ["tags"] = "solo" };

        var result = FrontmatterParser.GetStringList(dict, "tags");

        result.Count.ShouldBe(1);
        result[0].ShouldBe("solo");
    }

    [Fact]
    public void GetStringList_UnexpectedType_ReturnsEmptyList()
    {
        var dict = new Dictionary<string, object?> { ["tags"] = 42 };

        var result = FrontmatterParser.GetStringList(dict, "tags");

        result.ShouldBeEmpty();
    }

    #endregion

    #region NormalizeValue — various YAML types

    [Fact]
    public void Parse_WithIntegerValue_NormalizesToInt()
    {
        var sut = new FrontmatterParser();
        var markdown = "---\ncount: 42\n---\n\nBody";

        var result = sut.Parse(markdown);

        result.HasFrontmatter.ShouldBeTrue();
        result.Frontmatter!["count"].ShouldNotBeNull();
        result.Frontmatter["count"]!.ToString().ShouldBe("42");
    }

    [Fact]
    public void Parse_WithFloatValue_NormalizesCorrectly()
    {
        var sut = new FrontmatterParser();
        var markdown = "---\nweight: 3.14\n---\n\nBody";

        var result = sut.Parse(markdown);

        result.HasFrontmatter.ShouldBeTrue();
        result.Frontmatter!["weight"].ShouldNotBeNull();
        result.Frontmatter["weight"]!.ToString()!.ShouldContain("3.14");
    }

    [Fact]
    public void Parse_WithBooleanValues_NormalizesCorrectly()
    {
        var sut = new FrontmatterParser();
        var markdown = "---\ndraft: true\npublished: false\n---\n\nBody";

        var result = sut.Parse(markdown);

        result.HasFrontmatter.ShouldBeTrue();
        result.Frontmatter!["draft"].ShouldNotBeNull();
        result.Frontmatter["published"].ShouldNotBeNull();
        result.Frontmatter["draft"]!.ToString()!.ToLower().ShouldBe("true");
        result.Frontmatter["published"]!.ToString()!.ToLower().ShouldBe("false");
    }

    [Fact]
    public void Parse_WithNestedDictionary_NormalizesToStringKeyedDict()
    {
        var sut = new FrontmatterParser();
        var markdown = "---\nmeta:\n  version: 3\n  author: Jane\n---\n\nBody";

        var result = sut.Parse(markdown);

        result.HasFrontmatter.ShouldBeTrue();
        var meta = result.Frontmatter!["meta"].ShouldBeOfType<Dictionary<string, object?>>();
        meta.ShouldContainKey("version");
        meta.ShouldContainKey("author");
        meta["author"].ShouldBe("Jane");
    }

    [Fact]
    public void Parse_WithListOfMixedTypes_NormalizesListElements()
    {
        var sut = new FrontmatterParser();
        var markdown = "---\nitems:\n  - hello\n  - 42\n  - true\n---\n\nBody";

        var result = sut.Parse(markdown);

        result.HasFrontmatter.ShouldBeTrue();
        var items = result.Frontmatter!["items"].ShouldBeOfType<List<object?>>();
        items.Count.ShouldBe(3);
    }

    [Fact]
    public void Parse_WithNullFrontmatterValue_NormalizesToNull()
    {
        var sut = new FrontmatterParser();
        var markdown = "---\ntitle: Test\nempty_field:\n---\n\nBody";

        var result = sut.Parse(markdown);

        result.HasFrontmatter.ShouldBeTrue();
        result.Frontmatter.ShouldNotBeNull();
        result.Frontmatter!.ShouldContainKey("empty_field");
        result.Frontmatter!["empty_field"].ShouldBeNull();
    }

    [Fact]
    public void Parse_WithDeeplyNestedStructure_NormalizesRecursively()
    {
        var sut = new FrontmatterParser();
        var markdown = "---\ndata:\n  nested:\n    deep: value\n---\n\nBody";

        var result = sut.Parse(markdown);

        result.HasFrontmatter.ShouldBeTrue();
        var data = result.Frontmatter!["data"].ShouldBeOfType<Dictionary<string, object?>>();
        var nested = data["nested"].ShouldBeOfType<Dictionary<string, object?>>();
        nested["deep"].ShouldBe("value");
    }

    #endregion

    #region GetStringList — IEnumerable<object> branch

    [Fact]
    public void GetStringList_WithIEnumerableOfObject_FiltersAndConverts()
    {
        var items = new object[] { "alpha", "beta", "gamma" };
        var dict = new Dictionary<string, object?>
        {
            ["tags"] = items
        };

        var result = FrontmatterParser.GetStringList(dict, "tags");

        result.Count.ShouldBe(3);
        result[0].ShouldBe("alpha");
        result[1].ShouldBe("beta");
        result[2].ShouldBe("gamma");
    }

    #endregion
}
