using CompoundDocs.McpServer.DocTypes;
using CompoundDocs.McpServer.Processing;

namespace CompoundDocs.Tests.Processing;

/// <summary>
/// Unit tests for DocumentValidator.
/// </summary>
public sealed class DocumentValidatorTests
{
    private readonly DocumentValidator _sut;
    private readonly Mock<IDocTypeRegistry> _mockRegistry;

    public DocumentValidatorTests()
    {
        _mockRegistry = new Mock<IDocTypeRegistry>(MockBehavior.Loose);
        _sut = new DocumentValidator(_mockRegistry.Object);
    }

    #region Validate ParsedDocument Tests

    [Fact]
    public void Validate_WithValidDocument_ReturnsSuccess()
    {
        // Arrange
        var parsedDocument = new ParsedDocument
        {
            IsSuccess = true,
            HasFrontmatter = true,
            Frontmatter = new Dictionary<string, object?>
            {
                ["title"] = "Test Doc",
                ["doc_type"] = "spec"
            },
            Body = "# Content"
        };

        _mockRegistry.Setup(r => r.GetDocType("spec"))
            .Returns(DocTypeDefinition.CreateBuiltIn("spec", "Specification", "Spec documents"));

        // Act
        var result = _sut.Validate(parsedDocument);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.DocType.ShouldBe("spec");
    }

    [Fact]
    public void Validate_WithFailedParsing_ReturnsFailure()
    {
        // Arrange
        var parsedDocument = new ParsedDocument
        {
            IsSuccess = false,
            Error = "Parse error"
        };

        // Act
        var result = _sut.Validate(parsedDocument);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("Parse error"));
    }

    [Fact]
    public void Validate_WithNoDocType_AddsWarning()
    {
        // Arrange
        var parsedDocument = new ParsedDocument
        {
            IsSuccess = true,
            HasFrontmatter = true,
            Frontmatter = new Dictionary<string, object?>
            {
                ["title"] = "Test Doc"
            },
            Body = "# Content"
        };

        // Act
        var result = _sut.Validate(parsedDocument);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Warnings.ShouldContain(w => w.Contains("No doc_type"));
    }

    [Fact]
    public void Validate_WithUnknownDocType_AddsWarning()
    {
        // Arrange
        var parsedDocument = new ParsedDocument
        {
            IsSuccess = true,
            HasFrontmatter = true,
            Frontmatter = new Dictionary<string, object?>
            {
                ["doc_type"] = "unknown_type"
            },
            Body = "# Content"
        };

        _mockRegistry.Setup(r => r.GetDocType("unknown_type"))
            .Returns((DocTypeDefinition?)null);

        // Act
        var result = _sut.Validate(parsedDocument);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Warnings.ShouldContain(w => w.Contains("Unknown doc_type"));
    }

    [Fact]
    public void Validate_WithMissingRequiredFields_ReturnsFailure()
    {
        // Arrange
        var parsedDocument = new ParsedDocument
        {
            IsSuccess = true,
            HasFrontmatter = true,
            Frontmatter = new Dictionary<string, object?>
            {
                ["doc_type"] = "spec"
            },
            Body = "# Content"
        };

        var docType = DocTypeDefinition.CreateBuiltIn(
            "spec",
            "Specification",
            "Spec documents",
            requiredFields: new[] { "title", "version" });

        _mockRegistry.Setup(r => r.GetDocType("spec"))
            .Returns(docType);

        // Act
        var result = _sut.Validate(parsedDocument);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(2); // title and version missing
    }

    #endregion

    #region ValidateRequiredFields Tests

    [Fact]
    public void ValidateRequiredFields_WithAllFieldsPresent_ReturnsSuccess()
    {
        // Arrange
        var frontmatter = new Dictionary<string, object?>
        {
            ["title"] = "Test",
            ["version"] = "1.0"
        };
        var requiredFields = new List<string> { "title", "version" };

        // Act
        var result = _sut.ValidateRequiredFields(frontmatter, requiredFields);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
    }

    [Fact]
    public void ValidateRequiredFields_WithMissingFields_ReturnsFailure()
    {
        // Arrange
        var frontmatter = new Dictionary<string, object?>
        {
            ["title"] = "Test"
        };
        var requiredFields = new List<string> { "title", "version", "author" };

        // Act
        var result = _sut.ValidateRequiredFields(frontmatter, requiredFields);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(2);
    }

    [Fact]
    public void ValidateRequiredFields_WithNullFrontmatter_ReturnsFailure()
    {
        // Arrange
        var requiredFields = new List<string> { "title" };

        // Act
        var result = _sut.ValidateRequiredFields(null, requiredFields);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void ValidateRequiredFields_WithEmptyRequiredFields_ReturnsSuccess()
    {
        // Arrange
        var frontmatter = new Dictionary<string, object?>();
        var requiredFields = new List<string>();

        // Act
        var result = _sut.ValidateRequiredFields(frontmatter, requiredFields);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    #endregion

    #region ValidateFieldType Tests

    [Fact]
    public void ValidateFieldType_WithCorrectType_ReturnsTrue()
    {
        // Arrange
        var frontmatter = new Dictionary<string, object?>
        {
            ["count"] = 42
        };

        // Act
        var isValid = _sut.ValidateFieldType(frontmatter, "count", typeof(int));

        // Assert
        isValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateFieldType_WithConvertibleType_ReturnsTrue()
    {
        // Arrange
        var frontmatter = new Dictionary<string, object?>
        {
            ["count"] = "42"
        };

        // Act
        var isValid = _sut.ValidateFieldType(frontmatter, "count", typeof(int));

        // Assert
        isValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateFieldType_WithMissingField_ReturnsFalse()
    {
        // Arrange
        var frontmatter = new Dictionary<string, object?>();

        // Act
        var isValid = _sut.ValidateFieldType(frontmatter, "count", typeof(int));

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public void ValidateFieldType_WithNullFrontmatter_ReturnsFalse()
    {
        // Act
        var isValid = _sut.ValidateFieldType(null, "count", typeof(int));

        // Assert
        isValid.ShouldBeFalse();
    }

    #endregion

    #region DocumentValidationResult Tests

    [Fact]
    public void DocumentValidationResult_Success_CreatesValidResult()
    {
        // Act
        var result = DocumentValidationResult.Success("spec", new[] { "warning1" });

        // Assert
        result.IsValid.ShouldBeTrue();
        result.DocType.ShouldBe("spec");
        result.Warnings.Count.ShouldBe(1);
    }

    [Fact]
    public void DocumentValidationResult_Failure_CreatesInvalidResult()
    {
        // Act
        var result = DocumentValidationResult.Failure(
            "spec",
            new[] { "error1", "error2" },
            new[] { "warning1" });

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(2);
        result.Warnings.Count.ShouldBe(1);
    }

    #endregion
}
