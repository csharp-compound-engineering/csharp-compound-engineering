using CompoundDocs.McpServer.Options;
using CompoundDocs.McpServer.SemanticKernel;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CompoundDocs.Tests.Storage;

/// <summary>
/// Unit tests for VectorStoreFactory.
/// Tests collection creation, HNSW configuration constants, and dimension validation.
/// </summary>
public sealed class VectorStoreFactoryTests : IDisposable
{
    private readonly VectorStoreFactory _factory;

    public VectorStoreFactoryTests()
    {
        var options = CreateMockOptions();
        var logger = NullLogger<VectorStoreFactory>.Instance;
        _factory = new VectorStoreFactory(options, logger);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    #region HNSW Configuration Constants

    [Fact]
    public void HnswM_IsConfiguredCorrectly()
    {
        // Assert - Per spec: m=32 for Medium configuration
        VectorStoreFactory.HnswM.ShouldBe(32);
    }

    [Fact]
    public void HnswEfConstruction_IsConfiguredCorrectly()
    {
        // Assert - Per spec: ef_construction=128 for Medium configuration
        VectorStoreFactory.HnswEfConstruction.ShouldBe(128);
    }

    [Fact]
    public void HnswEfSearch_IsConfiguredCorrectly()
    {
        // Assert - Per spec: ef_search=64 for Medium configuration
        VectorStoreFactory.HnswEfSearch.ShouldBe(64);
    }

    #endregion

    #region Collection Name Constants

    [Fact]
    public void DocumentsCollectionName_IsCorrect()
    {
        VectorStoreFactory.DocumentsCollectionName.ShouldBe("compound_documents");
    }

    [Fact]
    public void DocumentChunksCollectionName_IsCorrect()
    {
        VectorStoreFactory.DocumentChunksCollectionName.ShouldBe("document_chunks");
    }

    [Fact]
    public void ExternalDocumentsCollectionName_IsCorrect()
    {
        VectorStoreFactory.ExternalDocumentsCollectionName.ShouldBe("external_documents");
    }

    [Fact]
    public void ExternalDocumentChunksCollectionName_IsCorrect()
    {
        VectorStoreFactory.ExternalDocumentChunksCollectionName.ShouldBe("external_document_chunks");
    }

    #endregion

    #region Collection Creation

    [Fact]
    public void CreateDocumentsCollection_ReturnsNonNullCollection()
    {
        // Act
        using var collection = _factory.CreateDocumentsCollection();

        // Assert
        collection.ShouldNotBeNull();
    }

    [Fact]
    public void CreateDocumentChunksCollection_ReturnsNonNullCollection()
    {
        // Act
        using var collection = _factory.CreateDocumentChunksCollection();

        // Assert
        collection.ShouldNotBeNull();
    }

    [Fact]
    public void CreateExternalDocumentsCollection_ReturnsNonNullCollection()
    {
        // Act
        using var collection = _factory.CreateExternalDocumentsCollection();

        // Assert
        collection.ShouldNotBeNull();
    }

    [Fact]
    public void CreateExternalDocumentChunksCollection_ReturnsNonNullCollection()
    {
        // Act
        using var collection = _factory.CreateExternalDocumentChunksCollection();

        // Assert
        collection.ShouldNotBeNull();
    }

    #endregion

    #region Data Source

    [Fact]
    public void DataSource_ReturnsNonNull()
    {
        // Assert
        _factory.DataSource.ShouldNotBeNull();
    }

    [Fact]
    public void ConnectionString_ReturnsNonEmpty()
    {
        // Assert
        _factory.ConnectionString.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void ConnectionString_ContainsExpectedComponents()
    {
        // Assert
        var connectionString = _factory.ConnectionString;
        connectionString.ShouldContain("Host=");
        connectionString.ShouldContain("Port=");
        connectionString.ShouldContain("Database=");
        connectionString.ShouldContain("Username=");
    }

    #endregion

    #region Dimension Validation

    [Fact]
    public void ValidateDimensions_AcceptsCorrectDimensions()
    {
        // Act & Assert - Should not throw for 1024 dimensions
        Should.NotThrow(() => _factory.ValidateDimensions(1024));
    }

    [Fact]
    public void ValidateDimensions_ThrowsForIncorrectDimensions()
    {
        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(
            () => _factory.ValidateDimensions(768));

        exception.Message.ShouldContain("1024");
        exception.Message.ShouldContain("768");
        exception.Message.ShouldContain("mxbai-embed-large");
    }

    [Fact]
    public void ValidateDimensions_ThrowsForZeroDimensions()
    {
        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(
            () => _factory.ValidateDimensions(0));

        exception.Message.ShouldContain("1024");
        exception.Message.ShouldContain("0");
    }

    [Fact]
    public void ValidateDimensions_ThrowsForNegativeDimensions()
    {
        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(
            () => _factory.ValidateDimensions(-1));

        exception.Message.ShouldContain("1024");
    }

    #endregion

    #region Constructor Validation

    [Fact]
    public void Constructor_ThrowsForNullOptions()
    {
        // Arrange
        IOptions<CompoundDocsServerOptions>? nullOptions = null;
        var logger = NullLogger<VectorStoreFactory>.Instance;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new VectorStoreFactory(nullOptions!, logger));
    }

    [Fact]
    public void Constructor_ThrowsForNullLogger()
    {
        // Arrange
        var options = CreateMockOptions();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new VectorStoreFactory(options, null!));
    }

    #endregion

    private static IOptions<CompoundDocsServerOptions> CreateMockOptions()
    {
        var options = new CompoundDocsServerOptions
        {
            Postgres = new PostgresConnectionOptions
            {
                Host = "localhost",
                Port = 5432,
                Database = "test_db",
                Username = "test_user",
                Password = "test_password"
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
