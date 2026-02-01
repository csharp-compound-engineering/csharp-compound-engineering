using CompoundDocs.McpServer.Options;
using CompoundDocs.McpServer.SemanticKernel;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CompoundDocs.Tests.Storage;

/// <summary>
/// Unit tests for vector dimension validation in EmbeddingService and VectorStoreFactory.
/// Ensures that embedding dimensions are validated against the expected 1024 dimensions
/// for mxbai-embed-large model.
/// </summary>
public sealed class DimensionValidationTests : TestBase
{
    private const int ExpectedDimensions = 1024;

    [Fact]
    public void OllamaConnectionOptions_ExpectedDimensionsIs1024()
    {
        // Assert - Verify the constant is set correctly
        OllamaConnectionOptions.EmbeddingDimensions.ShouldBe(ExpectedDimensions);
    }

    [Fact]
    public void OllamaConnectionOptions_EmbeddingModelIsMxbaiEmbedLarge()
    {
        // Assert - Verify the model name is correct
        OllamaConnectionOptions.EmbeddingModel.ShouldBe("mxbai-embed-large");
    }

    [Fact]
    public void VectorStoreFactory_ValidateDimensions_ThrowsForWrongDimensions()
    {
        // Arrange
        var options = CreateMockOptions();
        var logger = NullLogger<VectorStoreFactory>.Instance;
        using var factory = new VectorStoreFactory(options, logger);

        // Act & Assert - Wrong dimensions should throw
        var exception = Should.Throw<InvalidOperationException>(
            () => factory.ValidateDimensions(512));

        exception.Message.ShouldContain("1024");
        exception.Message.ShouldContain("512");
        exception.Message.ShouldContain("mxbai-embed-large");
    }

    [Fact]
    public void VectorStoreFactory_ValidateDimensions_DoesNotThrowForCorrectDimensions()
    {
        // Arrange
        var options = CreateMockOptions();
        var logger = NullLogger<VectorStoreFactory>.Instance;
        using var factory = new VectorStoreFactory(options, logger);

        // Act & Assert - Correct dimensions should not throw
        Should.NotThrow(() => factory.ValidateDimensions(ExpectedDimensions));
    }

    [Theory]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(768)]
    [InlineData(1536)]
    [InlineData(2048)]
    public void VectorStoreFactory_ValidateDimensions_ThrowsForVariousWrongDimensions(int wrongDimension)
    {
        // Arrange
        var options = CreateMockOptions();
        var logger = NullLogger<VectorStoreFactory>.Instance;
        using var factory = new VectorStoreFactory(options, logger);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(
            () => factory.ValidateDimensions(wrongDimension));

        exception.Message.ShouldContain(ExpectedDimensions.ToString());
        exception.Message.ShouldContain(wrongDimension.ToString());
    }

    [Fact]
    public void VectorStoreFactory_ValidateDimensions_ThrowsForZeroDimensions()
    {
        // Arrange
        var options = CreateMockOptions();
        var logger = NullLogger<VectorStoreFactory>.Instance;
        using var factory = new VectorStoreFactory(options, logger);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(
            () => factory.ValidateDimensions(0));

        exception.Message.ShouldContain("1024");
        exception.Message.ShouldContain("0");
    }

    [Fact]
    public void VectorStoreFactory_ValidateDimensions_ThrowsForNegativeDimensions()
    {
        // Arrange
        var options = CreateMockOptions();
        var logger = NullLogger<VectorStoreFactory>.Instance;
        using var factory = new VectorStoreFactory(options, logger);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(
            () => factory.ValidateDimensions(-1));

        exception.Message.ShouldContain("1024");
    }

    [Fact]
    public void VectorStoreFactory_ValidateDimensions_ErrorMessageIncludesModelName()
    {
        // Arrange
        var options = CreateMockOptions();
        var logger = NullLogger<VectorStoreFactory>.Instance;
        using var factory = new VectorStoreFactory(options, logger);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(
            () => factory.ValidateDimensions(512));

        // Should mention the expected model name
        exception.Message.ShouldContain("mxbai-embed-large");
    }

    [Fact]
    public void VectorStoreFactory_HnswParameters_MatchSpecification()
    {
        // Assert - Verify HNSW parameters match SPEC (Medium configuration)
        VectorStoreFactory.HnswM.ShouldBe(32, "m parameter should be 32 per SPEC");
        VectorStoreFactory.HnswEfConstruction.ShouldBe(128, "ef_construction should be 128 per SPEC");
        VectorStoreFactory.HnswEfSearch.ShouldBe(64, "ef_search should be 64 per SPEC");
    }

    private static IOptions<CompoundDocsServerOptions> CreateMockOptions()
    {
        var options = new CompoundDocsServerOptions
        {
            Postgres = new PostgresConnectionOptions
            {
                Host = "localhost",
                Port = 5432,
                Database = "test",
                Username = "test",
                Password = "test"
            },
            Ollama = new OllamaConnectionOptions
            {
                Host = "localhost",
                Port = 11434
            }
        };
        return Options.Create(options);
    }
}
