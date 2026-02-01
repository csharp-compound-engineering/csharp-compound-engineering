using CompoundDocs.McpServer.DocTypes;

namespace CompoundDocs.Tests.DocTypes;

/// <summary>
/// Unit tests for DocTypeValidationResult and ValidationError.
/// </summary>
public sealed class DocTypeValidationResultTests
{
    #region Success Tests

    [Fact]
    public void Success_ReturnsValidResult()
    {
        // Act
        var result = DocTypeValidationResult.Success("test-type");

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Success_SetsDocTypeId()
    {
        // Act
        var result = DocTypeValidationResult.Success("my-doc-type");

        // Assert
        result.DocTypeId.ShouldBe("my-doc-type");
    }

    [Fact]
    public void Success_HasNoErrors()
    {
        // Act
        var result = DocTypeValidationResult.Success("test-type");

        // Assert
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Success_WithWarnings_IncludesWarnings()
    {
        // Arrange
        var warnings = new List<string> { "Warning 1", "Warning 2" };

        // Act
        var result = DocTypeValidationResult.Success("test-type", warnings);

        // Assert
        result.Warnings.ShouldBe(warnings);
    }

    [Fact]
    public void Success_WithoutWarnings_DefaultsToEmptyList()
    {
        // Act
        var result = DocTypeValidationResult.Success("test-type");

        // Assert
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Success_WithNullWarnings_DefaultsToEmptyList()
    {
        // Act
        var result = DocTypeValidationResult.Success("test-type", null);

        // Assert
        result.Warnings.ShouldBeEmpty();
    }

    #endregion

    #region Failure Tests

    [Fact]
    public void Failure_ReturnsInvalidResult()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            new() { PropertyPath = "title", Message = "Required field missing" }
        };

        // Act
        var result = DocTypeValidationResult.Failure("test-type", errors);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Failure_SetsDocTypeId()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            new() { PropertyPath = "title", Message = "Error" }
        };

        // Act
        var result = DocTypeValidationResult.Failure("my-doc-type", errors);

        // Assert
        result.DocTypeId.ShouldBe("my-doc-type");
    }

    [Fact]
    public void Failure_IncludesErrors()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            new() { PropertyPath = "title", Message = "Required field missing" },
            new() { PropertyPath = "doc_type", Message = "Invalid value" }
        };

        // Act
        var result = DocTypeValidationResult.Failure("test-type", errors);

        // Assert
        result.Errors.Count.ShouldBe(2);
        result.Errors[0].PropertyPath.ShouldBe("title");
        result.Errors[1].PropertyPath.ShouldBe("doc_type");
    }

    [Fact]
    public void Failure_WithWarnings_IncludesWarnings()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            new() { PropertyPath = "title", Message = "Error" }
        };
        var warnings = new List<string> { "Deprecated field used" };

        // Act
        var result = DocTypeValidationResult.Failure("test-type", errors, warnings);

        // Assert
        result.Warnings.ShouldBe(warnings);
    }

    [Fact]
    public void Failure_WithNullWarnings_DefaultsToEmptyList()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            new() { PropertyPath = "title", Message = "Error" }
        };

        // Act
        var result = DocTypeValidationResult.Failure("test-type", errors, null);

        // Assert
        result.Warnings.ShouldBeEmpty();
    }

    #endregion

    #region DocTypeNotFound Tests

    [Fact]
    public void DocTypeNotFound_ReturnsInvalidResult()
    {
        // Act
        var result = DocTypeValidationResult.DocTypeNotFound("unknown-type");

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void DocTypeNotFound_SetsDocTypeId()
    {
        // Act
        var result = DocTypeValidationResult.DocTypeNotFound("my-unknown-type");

        // Assert
        result.DocTypeId.ShouldBe("my-unknown-type");
    }

    [Fact]
    public void DocTypeNotFound_HasSingleError()
    {
        // Act
        var result = DocTypeValidationResult.DocTypeNotFound("unknown-type");

        // Assert
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void DocTypeNotFound_ErrorTypeIsInvalidDocType()
    {
        // Act
        var result = DocTypeValidationResult.DocTypeNotFound("unknown-type");

        // Assert
        result.Errors[0].ErrorType.ShouldBe(ValidationErrorType.InvalidDocType);
    }

    [Fact]
    public void DocTypeNotFound_ErrorMessageIncludesDocTypeId()
    {
        // Act
        var result = DocTypeValidationResult.DocTypeNotFound("special-type");

        // Assert
        result.Errors[0].Message.ShouldContain("special-type");
        result.Errors[0].Message.ShouldContain("not registered");
    }

    [Fact]
    public void DocTypeNotFound_ErrorPropertyPathIsDocType()
    {
        // Act
        var result = DocTypeValidationResult.DocTypeNotFound("unknown-type");

        // Assert
        result.Errors[0].PropertyPath.ShouldBe("doc_type");
    }

    #endregion

    #region Default Values Tests

    [Fact]
    public void DocTypeValidationResult_DefaultDocTypeId_IsEmptyString()
    {
        // Arrange & Act
        var result = new DocTypeValidationResult();

        // Assert
        result.DocTypeId.ShouldBe(string.Empty);
    }

    [Fact]
    public void DocTypeValidationResult_DefaultErrors_IsEmptyList()
    {
        // Arrange & Act
        var result = new DocTypeValidationResult();

        // Assert
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void DocTypeValidationResult_DefaultWarnings_IsEmptyList()
    {
        // Arrange & Act
        var result = new DocTypeValidationResult();

        // Assert
        result.Warnings.ShouldBeEmpty();
    }

    #endregion
}

/// <summary>
/// Unit tests for ValidationError.
/// </summary>
public sealed class ValidationErrorTests
{
    [Fact]
    public void ValidationError_DefaultErrorType_IsSchema()
    {
        // Arrange & Act
        var error = new ValidationError
        {
            PropertyPath = "test",
            Message = "Test error"
        };

        // Assert
        error.ErrorType.ShouldBe(ValidationErrorType.Schema);
    }

    [Fact]
    public void ValidationError_CanSetAllProperties()
    {
        // Arrange & Act
        var error = new ValidationError
        {
            PropertyPath = "my.property.path",
            Message = "This field is invalid",
            ErrorType = ValidationErrorType.InvalidType,
            Expected = "string",
            Actual = "number"
        };

        // Assert
        error.PropertyPath.ShouldBe("my.property.path");
        error.Message.ShouldBe("This field is invalid");
        error.ErrorType.ShouldBe(ValidationErrorType.InvalidType);
        error.Expected.ShouldBe("string");
        error.Actual.ShouldBe("number");
    }

    [Fact]
    public void ValidationError_ExpectedAndActual_DefaultToNull()
    {
        // Arrange & Act
        var error = new ValidationError
        {
            PropertyPath = "test",
            Message = "Test error"
        };

        // Assert
        error.Expected.ShouldBeNull();
        error.Actual.ShouldBeNull();
    }

    [Fact]
    public void ValidationError_ToString_ReturnsFormattedString()
    {
        // Arrange
        var error = new ValidationError
        {
            PropertyPath = "title",
            Message = "Required field is missing"
        };

        // Act
        var result = error.ToString();

        // Assert
        result.ShouldBe("title: Required field is missing");
    }

    [Theory]
    [InlineData(ValidationErrorType.RequiredField)]
    [InlineData(ValidationErrorType.InvalidType)]
    [InlineData(ValidationErrorType.Schema)]
    [InlineData(ValidationErrorType.InvalidDocType)]
    [InlineData(ValidationErrorType.InvalidEnum)]
    [InlineData(ValidationErrorType.PatternMismatch)]
    [InlineData(ValidationErrorType.OutOfRange)]
    public void ValidationError_CanSetAllErrorTypes(ValidationErrorType errorType)
    {
        // Arrange & Act
        var error = new ValidationError
        {
            PropertyPath = "test",
            Message = "Test",
            ErrorType = errorType
        };

        // Assert
        error.ErrorType.ShouldBe(errorType);
    }
}
