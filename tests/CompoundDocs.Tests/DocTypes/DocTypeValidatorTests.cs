using CompoundDocs.McpServer.DocTypes;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.Tests.DocTypes;

/// <summary>
/// Unit tests for DocTypeValidator.
/// </summary>
public sealed class DocTypeValidatorTests : TestBase
{
    private readonly Mock<ILogger<DocTypeValidator>> _mockLogger;

    public DocTypeValidatorTests()
    {
        _mockLogger = CreateLooseMock<ILogger<DocTypeValidator>>();
    }

    private DocTypeValidator CreateValidator() =>
        new(_mockLogger.Object);

    private static string GetValidJsonSchema() => """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "title": { "type": "string" },
                "doc_type": { "type": "string" },
                "tags": {
                    "type": "array",
                    "items": { "type": "string" }
                },
                "priority": {
                    "type": "string",
                    "enum": ["low", "medium", "high"]
                },
                "count": { "type": "integer", "minimum": 0, "maximum": 100 }
            },
            "required": ["title", "doc_type"]
        }
        """;

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new DocTypeValidator(null!));
    }

    [Fact]
    public void Constructor_WithValidLogger_Succeeds()
    {
        // Act
        var validator = CreateValidator();

        // Assert
        validator.ShouldNotBeNull();
    }

    #endregion

    #region ValidateRequiredFields Tests

    [Fact]
    public void ValidateRequiredFields_AllPresent_ReturnsNoErrors()
    {
        // Arrange
        var validator = CreateValidator();
        var frontmatter = new Dictionary<string, object?>
        {
            ["title"] = "Test Document",
            ["doc_type"] = "spec",
            ["tags"] = new[] { "test", "example" }
        };
        var requiredFields = new List<string> { "title", "doc_type", "tags" };

        // Act
        var errors = validator.ValidateRequiredFields("test-type", frontmatter, requiredFields);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void ValidateRequiredFields_MissingField_ReturnsError()
    {
        // Arrange
        var validator = CreateValidator();
        var frontmatter = new Dictionary<string, object?>
        {
            ["title"] = "Test Document"
        };
        var requiredFields = new List<string> { "title", "doc_type" };

        // Act
        var errors = validator.ValidateRequiredFields("test-type", frontmatter, requiredFields);

        // Assert
        errors.Count.ShouldBe(1);
        errors[0].PropertyPath.ShouldBe("doc_type");
        errors[0].ErrorType.ShouldBe(ValidationErrorType.RequiredField);
        errors[0].Message.ShouldContain("missing");
    }

    [Fact]
    public void ValidateRequiredFields_NullValue_ReturnsError()
    {
        // Arrange
        var validator = CreateValidator();
        var frontmatter = new Dictionary<string, object?>
        {
            ["title"] = "Test Document",
            ["doc_type"] = null
        };
        var requiredFields = new List<string> { "title", "doc_type" };

        // Act
        var errors = validator.ValidateRequiredFields("test-type", frontmatter, requiredFields);

        // Assert
        errors.Count.ShouldBe(1);
        errors[0].PropertyPath.ShouldBe("doc_type");
    }

    [Fact]
    public void ValidateRequiredFields_EmptyStringValue_ReturnsError()
    {
        // Arrange
        var validator = CreateValidator();
        var frontmatter = new Dictionary<string, object?>
        {
            ["title"] = "Test Document",
            ["doc_type"] = ""
        };
        var requiredFields = new List<string> { "title", "doc_type" };

        // Act
        var errors = validator.ValidateRequiredFields("test-type", frontmatter, requiredFields);

        // Assert
        errors.Count.ShouldBe(1);
        errors[0].PropertyPath.ShouldBe("doc_type");
        errors[0].Message.ShouldContain("cannot be empty");
    }

    [Fact]
    public void ValidateRequiredFields_WhitespaceStringValue_ReturnsError()
    {
        // Arrange
        var validator = CreateValidator();
        var frontmatter = new Dictionary<string, object?>
        {
            ["title"] = "Test Document",
            ["doc_type"] = "   "
        };
        var requiredFields = new List<string> { "title", "doc_type" };

        // Act
        var errors = validator.ValidateRequiredFields("test-type", frontmatter, requiredFields);

        // Assert
        errors.Count.ShouldBe(1);
        errors[0].PropertyPath.ShouldBe("doc_type");
    }

    [Fact]
    public void ValidateRequiredFields_MultipleMissingFields_ReturnsAllErrors()
    {
        // Arrange
        var validator = CreateValidator();
        var frontmatter = new Dictionary<string, object?>();
        var requiredFields = new List<string> { "title", "doc_type", "tags" };

        // Act
        var errors = validator.ValidateRequiredFields("test-type", frontmatter, requiredFields);

        // Assert
        errors.Count.ShouldBe(3);
    }

    [Fact]
    public void ValidateRequiredFields_EmptyRequiredFields_ReturnsNoErrors()
    {
        // Arrange
        var validator = CreateValidator();
        var frontmatter = new Dictionary<string, object?>
        {
            ["title"] = "Test Document"
        };
        var requiredFields = new List<string>();

        // Act
        var errors = validator.ValidateRequiredFields("test-type", frontmatter, requiredFields);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void ValidateRequiredFields_WithNullFrontmatter_ThrowsArgumentNullException()
    {
        // Arrange
        var validator = CreateValidator();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            validator.ValidateRequiredFields("test-type", null!, ["title"]));
    }

    [Fact]
    public void ValidateRequiredFields_WithNullRequiredFields_ThrowsArgumentNullException()
    {
        // Arrange
        var validator = CreateValidator();
        var frontmatter = new Dictionary<string, object?>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            validator.ValidateRequiredFields("test-type", frontmatter, null!));
    }

    [Fact]
    public void ValidateRequiredFields_NonStringValuePresent_ReturnsNoError()
    {
        // Arrange
        var validator = CreateValidator();
        var frontmatter = new Dictionary<string, object?>
        {
            ["title"] = "Test",
            ["count"] = 42 // Non-string value
        };
        var requiredFields = new List<string> { "title", "count" };

        // Act
        var errors = validator.ValidateRequiredFields("test-type", frontmatter, requiredFields);

        // Assert
        errors.ShouldBeEmpty();
    }

    #endregion

    #region ValidateAsync Tests

    [Fact]
    public async Task ValidateAsync_ValidFrontmatter_ReturnsSuccess()
    {
        // Arrange
        var validator = CreateValidator();
        var schema = GetValidJsonSchema();
        var frontmatter = new Dictionary<string, object?>
        {
            ["title"] = "Test Document",
            ["doc_type"] = "spec"
        };

        // Act
        var result = await validator.ValidateAsync("test-type", schema, frontmatter);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.DocTypeId.ShouldBe("test-type");
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_InvalidType_ReturnsFailure()
    {
        // Arrange
        var validator = CreateValidator();
        var schema = GetValidJsonSchema();
        var frontmatter = new Dictionary<string, object?>
        {
            ["title"] = 123, // Should be string
            ["doc_type"] = "spec"
        };

        // Act
        var result = await validator.ValidateAsync("test-type", schema, frontmatter);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_InvalidEnum_ReturnsFailure()
    {
        // Arrange
        var validator = CreateValidator();
        var schema = GetValidJsonSchema();
        var frontmatter = new Dictionary<string, object?>
        {
            ["title"] = "Test",
            ["doc_type"] = "spec",
            ["priority"] = "invalid-value"
        };

        // Act
        var result = await validator.ValidateAsync("test-type", schema, frontmatter);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidateAsync_NullDocTypeId_ThrowsArgumentException()
    {
        // Arrange
        var validator = CreateValidator();
        var schema = GetValidJsonSchema();
        var frontmatter = new Dictionary<string, object?>();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await validator.ValidateAsync(null!, schema, frontmatter));
    }

    [Fact]
    public async Task ValidateAsync_EmptyDocTypeId_ThrowsArgumentException()
    {
        // Arrange
        var validator = CreateValidator();
        var schema = GetValidJsonSchema();
        var frontmatter = new Dictionary<string, object?>();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await validator.ValidateAsync("", schema, frontmatter));
    }

    [Fact]
    public async Task ValidateAsync_NullSchema_ThrowsArgumentException()
    {
        // Arrange
        var validator = CreateValidator();
        var frontmatter = new Dictionary<string, object?>();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await validator.ValidateAsync("test-type", null!, frontmatter));
    }

    [Fact]
    public async Task ValidateAsync_EmptySchema_ThrowsArgumentException()
    {
        // Arrange
        var validator = CreateValidator();
        var frontmatter = new Dictionary<string, object?>();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await validator.ValidateAsync("test-type", "", frontmatter));
    }

    [Fact]
    public async Task ValidateAsync_NullFrontmatter_ThrowsArgumentNullException()
    {
        // Arrange
        var validator = CreateValidator();
        var schema = GetValidJsonSchema();

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await validator.ValidateAsync("test-type", schema, null!));
    }

    [Fact]
    public async Task ValidateAsync_InvalidSchemaJson_ReturnsFailureWithSchemaError()
    {
        // Arrange
        var validator = CreateValidator();
        var invalidSchema = "{ invalid json }";
        var frontmatter = new Dictionary<string, object?>
        {
            ["title"] = "Test"
        };

        // Act
        var result = await validator.ValidateAsync("test-type", invalidSchema, frontmatter);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
        result.Errors[0].ErrorType.ShouldBe(ValidationErrorType.Schema);
    }

    [Fact]
    public async Task ValidateAsync_CachesSchema()
    {
        // Arrange
        var validator = CreateValidator();
        var schema = GetValidJsonSchema();
        var frontmatter = new Dictionary<string, object?>
        {
            ["title"] = "Test",
            ["doc_type"] = "spec"
        };

        // Act - Call twice to exercise caching
        await validator.ValidateAsync("test-type", schema, frontmatter);
        var result = await validator.ValidateAsync("test-type", schema, frontmatter);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_OutOfRangeValue_ReturnsFailure()
    {
        // Arrange
        var validator = CreateValidator();
        var schema = GetValidJsonSchema();
        var frontmatter = new Dictionary<string, object?>
        {
            ["title"] = "Test",
            ["doc_type"] = "spec",
            ["count"] = 200 // Max is 100
        };

        // Act
        var result = await validator.ValidateAsync("test-type", schema, frontmatter);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    #endregion

    #region ClearCache Tests

    [Fact]
    public void ClearCache_DoesNotThrow()
    {
        // Arrange
        var validator = CreateValidator();

        // Act & Assert
        Should.NotThrow(() => validator.ClearCache());
    }

    [Fact]
    public async Task ClearCache_AllowsRevalidation()
    {
        // Arrange
        var validator = CreateValidator();
        var schema = GetValidJsonSchema();
        var frontmatter = new Dictionary<string, object?>
        {
            ["title"] = "Test",
            ["doc_type"] = "spec"
        };

        // First validation to populate cache
        await validator.ValidateAsync("test-type", schema, frontmatter);

        // Act
        validator.ClearCache();

        // Assert - Should still work after clear
        var result = await validator.ValidateAsync("test-type", schema, frontmatter);
        result.IsValid.ShouldBeTrue();
    }

    #endregion
}
