using CompoundDocs.McpServer.Processing;

namespace CompoundDocs.Tests.Unit.Processing;

/// <summary>
/// Unit tests for <see cref="IndexResult"/> and <see cref="IndexResultBuilder"/>.
/// </summary>
public sealed class IndexResultTests
{
    #region IndexResult Default Properties

    [Fact]
    public void IndexResult_DefaultProperties_HaveExpectedValues()
    {
        // Act
        var result = new IndexResult();

        // Assert
        result.DocumentId.ShouldBe(string.Empty);
        result.FilePath.ShouldBe(string.Empty);
        result.ChunkCount.ShouldBe(0);
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldBeEmpty();
        result.Warnings.ShouldBeEmpty();
        result.ProcessingTimeMs.ShouldBe(0);
        result.EmbeddingTimeMs.ShouldBe(0);
        result.DocType.ShouldBeNull();
        result.Title.ShouldBeNull();
    }

    #endregion

    #region IndexResult.Success

    [Fact]
    public void Success_WithRequiredParameters_ReturnsSuccessResult()
    {
        // Act
        var result = IndexResult.Success("doc-1", "/docs/readme.md", 5, 100, 50);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.DocumentId.ShouldBe("doc-1");
        result.FilePath.ShouldBe("/docs/readme.md");
        result.ChunkCount.ShouldBe(5);
        result.ProcessingTimeMs.ShouldBe(100);
        result.EmbeddingTimeMs.ShouldBe(50);
        result.Errors.ShouldBeEmpty();
        result.Warnings.ShouldBeEmpty();
        result.DocType.ShouldBeNull();
        result.Title.ShouldBeNull();
    }

    [Fact]
    public void Success_WithAllOptionalParameters_SetsAllProperties()
    {
        // Arrange
        var warnings = new List<string> { "Low quality chunk detected" };

        // Act
        var result = IndexResult.Success(
            "doc-2",
            "/docs/guide.md",
            10,
            200,
            75,
            warnings: warnings,
            docType: "markdown",
            title: "Getting Started Guide");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Warnings.ShouldBe(warnings);
        result.DocType.ShouldBe("markdown");
        result.Title.ShouldBe("Getting Started Guide");
    }

    [Fact]
    public void Success_WithNullWarnings_DefaultsToEmptyList()
    {
        // Act
        var result = IndexResult.Success("doc-1", "/docs/readme.md", 5, 100, 50, warnings: null);

        // Assert
        result.Warnings.ShouldBeEmpty();
    }

    #endregion

    #region IndexResult.Failure (list overload)

    [Fact]
    public void Failure_WithErrorList_ReturnsFailedResult()
    {
        // Arrange
        var errors = new List<string> { "Parse error", "Validation error" };

        // Act
        var result = IndexResult.Failure("/docs/broken.md", errors, processingTimeMs: 42);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.FilePath.ShouldBe("/docs/broken.md");
        result.DocumentId.ShouldBe(string.Empty);
        result.ChunkCount.ShouldBe(0);
        result.EmbeddingTimeMs.ShouldBe(0);
        result.ProcessingTimeMs.ShouldBe(42);
        result.Errors.Count.ShouldBe(2);
        result.Errors[0].ShouldBe("Parse error");
        result.Errors[1].ShouldBe("Validation error");
    }

    [Fact]
    public void Failure_WithErrorList_DefaultProcessingTimeIsZero()
    {
        // Act
        var result = IndexResult.Failure("/docs/broken.md", new List<string> { "err" });

        // Assert
        result.ProcessingTimeMs.ShouldBe(0);
    }

    [Fact]
    public void Failure_WithWarnings_IncludesWarningsInResult()
    {
        // Arrange
        var errors = new List<string> { "Fatal error" };
        var warnings = new List<string> { "Partial parse completed" };

        // Act
        var result = IndexResult.Failure("/docs/broken.md", errors, warnings: warnings);

        // Assert
        result.Warnings.ShouldBe(warnings);
    }

    [Fact]
    public void Failure_WithNullWarnings_DefaultsToEmptyList()
    {
        // Act
        var result = IndexResult.Failure("/docs/broken.md", new List<string> { "err" }, warnings: null);

        // Assert
        result.Warnings.ShouldBeEmpty();
    }

    #endregion

    #region IndexResult.Failure (single string overload)

    [Fact]
    public void Failure_WithSingleErrorString_WrapsInList()
    {
        // Act
        var result = IndexResult.Failure("/docs/broken.md", "Something went wrong");

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].ShouldBe("Something went wrong");
    }

    [Fact]
    public void Failure_WithSingleErrorString_DefaultProcessingTimeIsZero()
    {
        // Act
        var result = IndexResult.Failure("/docs/broken.md", "err");

        // Assert
        result.ProcessingTimeMs.ShouldBe(0);
    }

    [Fact]
    public void Failure_WithSingleErrorStringAndProcessingTime_SetsProcessingTime()
    {
        // Act
        var result = IndexResult.Failure("/docs/broken.md", "err", processingTimeMs: 99);

        // Assert
        result.ProcessingTimeMs.ShouldBe(99);
    }

    #endregion

    #region IndexResult.ToString

    [Fact]
    public void ToString_WhenSuccess_ReturnsSuccessFormat()
    {
        // Arrange
        var result = IndexResult.Success("doc-1", "/docs/readme.md", 5, 100, 50);

        // Act
        var str = result.ToString();

        // Assert
        str.ShouldBe("IndexResult[Success]: /docs/readme.md - 5 chunks in 100ms");
    }

    [Fact]
    public void ToString_WhenFailure_ReturnsFailureFormatWithJoinedErrors()
    {
        // Arrange
        var errors = new List<string> { "Error A", "Error B" };
        var result = IndexResult.Failure("/docs/broken.md", errors);

        // Act
        var str = result.ToString();

        // Assert
        str.ShouldBe("IndexResult[Failed]: /docs/broken.md - Error A; Error B");
    }

    [Fact]
    public void ToString_WhenFailureWithSingleError_ReturnsFailureFormatWithSingleError()
    {
        // Arrange
        var result = IndexResult.Failure("/docs/broken.md", "Only one error");

        // Act
        var str = result.ToString();

        // Assert
        str.ShouldBe("IndexResult[Failed]: /docs/broken.md - Only one error");
    }

    #endregion

    #region IndexResultBuilder Fluent API

    [Fact]
    public void Builder_FluentMethods_ReturnSameInstance()
    {
        // Arrange
        var builder = new IndexResultBuilder();

        // Act & Assert - each fluent method returns the same builder
        builder.WithDocumentId("doc-1").ShouldBeSameAs(builder);
        builder.WithFilePath("/docs/readme.md").ShouldBeSameAs(builder);
        builder.WithChunkCount(3).ShouldBeSameAs(builder);
        builder.WithDocType("markdown").ShouldBeSameAs(builder);
        builder.WithTitle("My Title").ShouldBeSameAs(builder);
        builder.WithError("error").ShouldBeSameAs(builder);
        builder.WithErrors(["err2"]).ShouldBeSameAs(builder);
        builder.WithWarning("warn").ShouldBeSameAs(builder);
        builder.WithWarnings(["warn2"]).ShouldBeSameAs(builder);
    }

    [Fact]
    public void Builder_WithAllProperties_BuildsCorrectResult()
    {
        // Arrange & Act
        var result = new IndexResultBuilder()
            .WithDocumentId("doc-99")
            .WithFilePath("/docs/api.md")
            .WithChunkCount(12)
            .WithDocType("api-reference")
            .WithTitle("API Reference")
            .WithWarning("Minor issue")
            .Build();

        // Assert
        result.DocumentId.ShouldBe("doc-99");
        result.FilePath.ShouldBe("/docs/api.md");
        result.ChunkCount.ShouldBe(12);
        result.IsSuccess.ShouldBeTrue();
        result.DocType.ShouldBe("api-reference");
        result.Title.ShouldBe("API Reference");
        result.Warnings.Count.ShouldBe(1);
        result.Warnings[0].ShouldBe("Minor issue");
        result.Errors.ShouldBeEmpty();
        result.ProcessingTimeMs.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Builder_WithError_SetsIsSuccessToFalse()
    {
        // Act
        var result = new IndexResultBuilder()
            .WithFilePath("/docs/broken.md")
            .WithError("Something failed")
            .Build();

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].ShouldBe("Something failed");
    }

    [Fact]
    public void Builder_WithMultipleErrors_AccumulatesAllErrors()
    {
        // Act
        var result = new IndexResultBuilder()
            .WithError("Error 1")
            .WithError("Error 2")
            .WithErrors(new[] { "Error 3", "Error 4" })
            .Build();

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.Count.ShouldBe(4);
    }

    [Fact]
    public void Builder_WithEmptyErrors_DoesNotSetFailure()
    {
        // Act
        var result = new IndexResultBuilder()
            .WithErrors(Enumerable.Empty<string>())
            .Build();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Builder_WithWarnings_AccumulatesWarnings()
    {
        // Act
        var result = new IndexResultBuilder()
            .WithWarning("Warn 1")
            .WithWarnings(new[] { "Warn 2", "Warn 3" })
            .Build();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Warnings.Count.ShouldBe(3);
        result.Warnings[0].ShouldBe("Warn 1");
        result.Warnings[1].ShouldBe("Warn 2");
        result.Warnings[2].ShouldBe("Warn 3");
    }

    [Fact]
    public void Builder_EmbeddingTimer_RecordsElapsedTime()
    {
        // Arrange
        var builder = new IndexResultBuilder()
            .WithDocumentId("doc-1")
            .WithFilePath("/docs/readme.md");

        // Act
        builder.StartEmbeddingTimer();
        Thread.Sleep(15);
        builder.StopEmbeddingTimer();

        var result = builder.Build();

        // Assert
        result.EmbeddingTimeMs.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Builder_WithoutStartingEmbeddingTimer_EmbeddingTimeMsIsZero()
    {
        // Act
        var result = new IndexResultBuilder()
            .WithDocumentId("doc-1")
            .WithFilePath("/docs/readme.md")
            .Build();

        // Assert
        result.EmbeddingTimeMs.ShouldBe(0);
    }

    [Fact]
    public void Builder_Build_StopsTotalStopwatchAndRecordsProcessingTime()
    {
        // Arrange
        var builder = new IndexResultBuilder()
            .WithDocumentId("doc-1")
            .WithFilePath("/docs/readme.md");

        Thread.Sleep(10);

        // Act
        var result = builder.Build();

        // Assert
        result.ProcessingTimeMs.ShouldBeGreaterThanOrEqualTo(0);
    }

    #endregion
}
