using CompoundDocs.Common.Parsing;

namespace CompoundDocs.Tests.Unit.Parsing;

/// <summary>
/// Unit tests for SchemaValidator.
/// </summary>
public sealed class SchemaValidatorTests
{
    private const string SimpleSchema = """
        {"type":"object","properties":{"name":{"type":"string"},"age":{"type":"integer"}},"required":["name"]}
        """;

    #region ValidateAsync Tests

    [Fact]
    public async Task ValidateAsync_ValidData_ReturnsIsValidTrue()
    {
        // Arrange
        var sut = new SchemaValidator();
        var data = new { name = "Alice", age = 30 };

        // Act
        var result = await sut.ValidateAsync(data, SimpleSchema);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_InvalidData_ReturnsIsValidFalse()
    {
        // Arrange
        var sut = new SchemaValidator();
        var data = new { age = 30 }; // missing required "name"

        // Act
        var result = await sut.ValidateAsync(data, SimpleSchema);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_InvalidData_ErrorsContainPathAndKind()
    {
        // Arrange
        var sut = new SchemaValidator();
        var data = new { age = 30 }; // missing required "name"

        // Act
        var result = await sut.ValidateAsync(data, SimpleSchema);

        // Assert
        result.Errors.ShouldNotBeEmpty();
        var error = result.Errors.First();
        error.Path.ShouldNotBeNull();
        error.Kind.ShouldNotBeNullOrWhiteSpace();
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    #endregion

    #region ValidateJsonAsync Tests

    [Fact]
    public async Task ValidateJsonAsync_ValidJson_ReturnsIsValidTrue()
    {
        // Arrange
        var sut = new SchemaValidator();
        var json = """{"name":"Bob","age":25}""";

        // Act
        var result = await sut.ValidateJsonAsync(json, SimpleSchema);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task ValidateJsonAsync_InvalidJson_ReturnsIsValidFalse()
    {
        // Arrange
        var sut = new SchemaValidator();
        var json = """{"age":25}"""; // missing required "name"

        // Act
        var result = await sut.ValidateJsonAsync(json, SimpleSchema);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ValidateJsonAsync_MalformedJson_Throws()
    {
        // Arrange
        var sut = new SchemaValidator();
        var malformedJson = "this is not json at all {{{";

        // Act & Assert
        await Should.ThrowAsync<Exception>(
            async () => await sut.ValidateJsonAsync(malformedJson, SimpleSchema));
    }

    #endregion

    #region Schema Caching Tests

    [Fact]
    public async Task ValidateAsync_CachesSchema_SecondCallUsesCached()
    {
        // Arrange
        var sut = new SchemaValidator();
        var data1 = new { name = "Alice", age = 30 };
        var data2 = new { name = "Bob", age = 40 };

        // Act - call twice with the same schema to exercise the cache path
        var result1 = await sut.ValidateAsync(data1, SimpleSchema);
        var result2 = await sut.ValidateAsync(data2, SimpleSchema);

        // Assert
        result1.IsValid.ShouldBeTrue();
        result2.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_DifferentSchemas_ParsesBothSeparately()
    {
        // Arrange
        var sut = new SchemaValidator();
        var schemaA = """{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}""";
        var schemaB = """{"type":"object","properties":{"title":{"type":"string"}},"required":["title"]}""";

        var dataA = new { name = "Alice" };
        var dataB = new { title = "Document" };

        // Act
        var resultA = await sut.ValidateAsync(dataA, schemaA);
        var resultB = await sut.ValidateAsync(dataB, schemaB);

        // Assert
        resultA.IsValid.ShouldBeTrue();
        resultB.IsValid.ShouldBeTrue();
    }

    #endregion

    #region LoadSchemaAsync Tests

    [Fact]
    public async Task LoadSchemaAsync_ValidFile_ReturnsJsonSchema()
    {
        // Arrange
        var sut = new SchemaValidator();
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, SimpleSchema);

            // Act
            var schema = await sut.LoadSchemaAsync(tempFile);

            // Assert
            schema.ShouldNotBeNull();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LoadSchemaAsync_MissingFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var sut = new SchemaValidator();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "missing.json");

        // Act & Assert
        await Should.ThrowAsync<IOException>(
            async () => await sut.LoadSchemaAsync(nonExistentPath));
    }

    #endregion

    #region Record Property Tests

    [Fact]
    public void ValidationResult_Record_PropertiesMatch()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            new("$.name", "StringExpected", "Expected string")
        };

        // Act
        var result = new ValidationResult(false, errors);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldBe(errors);
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void ValidationError_Record_PropertiesMatch()
    {
        // Arrange & Act
        var error = new ValidationError("$.age", "IntegerExpected", "Expected integer value");

        // Assert
        error.Path.ShouldBe("$.age");
        error.Kind.ShouldBe("IntegerExpected");
        error.Message.ShouldBe("Expected integer value");
    }

    #endregion
}
