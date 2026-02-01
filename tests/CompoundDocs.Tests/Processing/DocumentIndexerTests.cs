using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.DocTypes;
using CompoundDocs.McpServer.Hooks;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.Processing;
using CompoundDocs.McpServer.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompoundDocs.Tests.Processing;

/// <summary>
/// Unit tests for DocumentIndexer components.
/// Note: Full DocumentIndexer integration tests require a PostgreSQL database
/// and are located in CompoundDocs.IntegrationTests.
/// These tests focus on the IndexResult, IndexResultBuilder, and DocumentIndexer components.
/// </summary>
public sealed class DocumentIndexerTests
{
    #region IndexResult Factory Method Tests

    [Fact]
    public void IndexResult_Success_CreatesSuccessfulResult()
    {
        // Act
        var result = IndexResult.Success(
            "doc-123",
            "test.md",
            5,
            100,
            50,
            new[] { "warning1" },
            "spec",
            "Test Title");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.DocumentId.ShouldBe("doc-123");
        result.FilePath.ShouldBe("test.md");
        result.ChunkCount.ShouldBe(5);
        result.ProcessingTimeMs.ShouldBe(100);
        result.EmbeddingTimeMs.ShouldBe(50);
        result.Warnings.Count.ShouldBe(1);
        result.DocType.ShouldBe("spec");
        result.Title.ShouldBe("Test Title");
    }

    [Fact]
    public void IndexResult_Success_WithNullWarnings_ReturnsEmptyList()
    {
        // Act
        var result = IndexResult.Success(
            "doc-123",
            "test.md",
            5,
            100,
            50);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Warnings.ShouldNotBeNull();
        result.Warnings.Count.ShouldBe(0);
    }

    [Fact]
    public void IndexResult_Failure_WithErrorList_CreatesFailedResult()
    {
        // Act
        var result = IndexResult.Failure(
            "test.md",
            new[] { "error1", "error2" },
            100);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.Count.ShouldBe(2);
        result.Errors[0].ShouldBe("error1");
        result.Errors[1].ShouldBe("error2");
        result.FilePath.ShouldBe("test.md");
        result.ProcessingTimeMs.ShouldBe(100);
        result.DocumentId.ShouldBeEmpty();
        result.ChunkCount.ShouldBe(0);
    }

    [Fact]
    public void IndexResult_Failure_WithSingleError_CreatesFailedResult()
    {
        // Act
        var result = IndexResult.Failure("test.md", "Something went wrong");

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].ShouldBe("Something went wrong");
    }

    [Fact]
    public void IndexResult_Failure_DefaultProcessingTime_IsZero()
    {
        // Act
        var result = IndexResult.Failure("test.md", "error");

        // Assert
        result.ProcessingTimeMs.ShouldBe(0);
    }

    [Fact]
    public void IndexResult_ToString_ReturnsCorrectFormat_ForSuccess()
    {
        // Arrange
        var result = IndexResult.Success("doc-123", "test.md", 5, 100, 50);

        // Act
        var str = result.ToString();

        // Assert
        str.ShouldContain("Success");
        str.ShouldContain("test.md");
        str.ShouldContain("5 chunks");
    }

    [Fact]
    public void IndexResult_ToString_ReturnsCorrectFormat_ForFailure()
    {
        // Arrange
        var result = IndexResult.Failure("test.md", "File not found");

        // Act
        var str = result.ToString();

        // Assert
        str.ShouldContain("Failed");
        str.ShouldContain("test.md");
        str.ShouldContain("File not found");
    }

    #endregion

    #region IndexResultBuilder Tests

    [Fact]
    public void IndexResultBuilder_BuildsCorrectResult()
    {
        // Arrange & Act
        var result = new IndexResultBuilder()
            .WithFilePath("test.md")
            .WithDocumentId("doc-123")
            .WithChunkCount(5)
            .WithDocType("spec")
            .WithTitle("Test")
            .Build();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.FilePath.ShouldBe("test.md");
        result.DocumentId.ShouldBe("doc-123");
        result.ChunkCount.ShouldBe(5);
        result.DocType.ShouldBe("spec");
        result.Title.ShouldBe("Test");
    }

    [Fact]
    public void IndexResultBuilder_WithError_MarksResultAsFailed()
    {
        // Arrange & Act
        var result = new IndexResultBuilder()
            .WithFilePath("test.md")
            .WithError("Something went wrong")
            .Build();

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].ShouldBe("Something went wrong");
    }

    [Fact]
    public void IndexResultBuilder_WithMultipleErrors_AccumulatesErrors()
    {
        // Arrange & Act
        var result = new IndexResultBuilder()
            .WithFilePath("test.md")
            .WithError("Error 1")
            .WithError("Error 2")
            .WithErrors(new[] { "Error 3", "Error 4" })
            .Build();

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.Count.ShouldBe(4);
    }

    [Fact]
    public void IndexResultBuilder_WithWarnings_AccumulatesWarnings()
    {
        // Arrange & Act
        var result = new IndexResultBuilder()
            .WithFilePath("test.md")
            .WithWarning("Warning 1")
            .WithWarnings(new[] { "Warning 2", "Warning 3" })
            .Build();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Warnings.Count.ShouldBe(3);
    }

    [Fact]
    public void IndexResultBuilder_RecordsProcessingTime()
    {
        // Arrange
        var builder = new IndexResultBuilder();

        // Act - simulate some processing
        System.Threading.Thread.Sleep(10);
        var result = builder.Build();

        // Assert
        result.ProcessingTimeMs.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void IndexResultBuilder_EmbeddingTimer_RecordsEmbeddingTime()
    {
        // Arrange
        var builder = new IndexResultBuilder();

        // Act
        builder.StartEmbeddingTimer();
        System.Threading.Thread.Sleep(10);
        builder.StopEmbeddingTimer();
        var result = builder.Build();

        // Assert
        result.EmbeddingTimeMs.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void IndexResultBuilder_EmbeddingTimer_NotStarted_ReturnsZero()
    {
        // Arrange & Act
        var result = new IndexResultBuilder()
            .WithFilePath("test.md")
            .Build();

        // Assert
        result.EmbeddingTimeMs.ShouldBe(0);
    }

    [Fact]
    public void IndexResultBuilder_ChainedCalls_WorkCorrectly()
    {
        // Arrange & Act
        var result = new IndexResultBuilder()
            .WithFilePath("test.md")
            .WithDocumentId("doc-123")
            .WithChunkCount(3)
            .WithDocType("spec")
            .WithTitle("My Document")
            .WithWarning("Some warning")
            .Build();

        // Assert
        result.FilePath.ShouldBe("test.md");
        result.DocumentId.ShouldBe("doc-123");
        result.ChunkCount.ShouldBe(3);
        result.DocType.ShouldBe("spec");
        result.Title.ShouldBe("My Document");
        result.Warnings.Count.ShouldBe(1);
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void IndexResultBuilder_WithErrors_EmptyCollection_DoesNotMarkFailed()
    {
        // Arrange & Act
        var result = new IndexResultBuilder()
            .WithFilePath("test.md")
            .WithErrors([])
            .Build();

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    #endregion

    #region DocumentIndexer Tests

    private static DocumentIndexer CreateIndexer(
        Mock<IEmbeddingService>? embeddingServiceMock = null,
        Mock<IDocumentRepository>? repositoryMock = null,
        Mock<IDocTypeRegistry>? registryMock = null,
        DocumentHookExecutor? hookExecutor = null,
        string tenantKey = "test:main:abc123")
    {
        var documentParser = new DocumentParser();
        var chunkingStrategy = new ChunkingStrategy();
        var frontmatterParser = new FrontmatterParser();
        var documentValidator = new DocumentValidator(registryMock?.Object);
        var embeddingService = embeddingServiceMock?.Object ?? CreateMockEmbeddingService().Object;
        var repository = repositoryMock?.Object ?? CreateMockRepository().Object;
        var logger = NullLogger<DocumentIndexer>.Instance;

        return new DocumentIndexer(
            documentParser,
            chunkingStrategy,
            frontmatterParser,
            documentValidator,
            embeddingService,
            repository,
            hookExecutor,
            logger,
            tenantKey);
    }

    private static Mock<IEmbeddingService> CreateMockEmbeddingService()
    {
        var mock = new Mock<IEmbeddingService>(MockBehavior.Loose);
        var embedding = new ReadOnlyMemory<float>(new float[1024]);

        mock.Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        mock.Setup(s => s.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> contents, CancellationToken _) =>
                contents.Select(_ => new ReadOnlyMemory<float>(new float[1024])).ToList());

        mock.Setup(s => s.Dimensions).Returns(1024);

        return mock;
    }

    private static Mock<IDocumentRepository> CreateMockRepository()
    {
        var mock = new Mock<IDocumentRepository>(MockBehavior.Loose);

        mock.Setup(r => r.UpsertAsync(It.IsAny<CompoundDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompoundDocument doc, CancellationToken _) =>
            {
                if (string.IsNullOrEmpty(doc.Id))
                {
                    doc.Id = Guid.NewGuid().ToString();
                }
                return doc;
            });

        mock.Setup(r => r.UpsertChunksAsync(It.IsAny<IEnumerable<DocumentChunk>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<DocumentChunk> chunks, CancellationToken _) => chunks.Count());

        mock.Setup(r => r.DeleteChunksAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        mock.Setup(r => r.GetByTenantKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompoundDocument?)null);

        mock.Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompoundDocument?)null);

        mock.Setup(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        mock.Setup(r => r.GetAllForTenantAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CompoundDocument>());

        return mock;
    }

    [Fact]
    public async Task IndexContentAsync_WithValidDocument_ReturnsSuccess()
    {
        // Arrange
        var indexer = CreateIndexer();
        var content = """
            ---
            title: Test Document
            doc_type: spec
            ---

            # Test Document

            This is a test document body.
            """;

        // Act
        var result = await indexer.IndexContentAsync(content, "test.md");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.FilePath.ShouldBe("test.md");
        result.Title.ShouldBe("Test Document");
        result.DocType.ShouldBe("spec");
        result.DocumentId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task IndexContentAsync_WithNoFrontmatter_UsesH1Title()
    {
        // Arrange
        var indexer = CreateIndexer();
        var content = """
            # My Document Title

            Content without frontmatter.
            """;

        // Act
        var result = await indexer.IndexContentAsync(content, "test.md");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Title.ShouldBe("My Document Title");
    }

    [Fact]
    public async Task IndexContentAsync_WithEmptyContent_ReturnsSuccess()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act
        var result = await indexer.IndexContentAsync(string.Empty, "empty.md");

        // Assert
        // Empty content should parse successfully but with warning about no doc_type
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task IndexContentAsync_WithLargeContent_CreatesChunks()
    {
        // Arrange
        var repositoryMock = CreateMockRepository();
        var indexer = CreateIndexer(repositoryMock: repositoryMock);

        // Create content larger than default chunk size (1000 chars)
        var largeContent = "---\ntitle: Large Doc\n---\n\n" +
                          string.Join("\n\n", Enumerable.Range(1, 50).Select(i =>
                              $"Paragraph {i}: " + new string('x', 100)));

        // Act
        var result = await indexer.IndexContentAsync(largeContent, "large.md");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ChunkCount.ShouldBeGreaterThan(0);

        // Verify chunks were saved
        repositoryMock.Verify(
            r => r.UpsertChunksAsync(It.IsAny<IEnumerable<DocumentChunk>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IndexContentAsync_CallsEmbeddingService()
    {
        // Arrange
        var embeddingMock = CreateMockEmbeddingService();
        var indexer = CreateIndexer(embeddingServiceMock: embeddingMock);

        var content = """
            ---
            title: Test Doc
            ---

            Content here.
            """;

        // Act
        await indexer.IndexContentAsync(content, "test.md");

        // Assert
        embeddingMock.Verify(
            e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IndexContentAsync_WhenEmbeddingFails_ReturnsFailure()
    {
        // Arrange
        var embeddingMock = new Mock<IEmbeddingService>(MockBehavior.Strict);
        embeddingMock.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Embedding service unavailable"));

        var indexer = CreateIndexer(embeddingServiceMock: embeddingMock);

        var content = """
            ---
            title: Test Doc
            ---

            Content here.
            """;

        // Act
        var result = await indexer.IndexContentAsync(content, "test.md");

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("embedding"));
    }

    [Fact]
    public async Task IndexContentAsync_WithExistingDocument_UpdatesDocument()
    {
        // Arrange
        var existingDoc = new CompoundDocument
        {
            Id = "existing-doc-id",
            TenantKey = "test:main:abc123",
            FilePath = "test.md",
            Title = "Old Title"
        };

        var repositoryMock = CreateMockRepository();
        repositoryMock.Setup(r => r.GetByTenantKeyAsync("test:main:abc123", "test.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDoc);

        var indexer = CreateIndexer(repositoryMock: repositoryMock);

        var content = """
            ---
            title: New Title
            ---

            Updated content.
            """;

        // Act
        var result = await indexer.IndexContentAsync(content, "test.md");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.DocumentId.ShouldBe("existing-doc-id");

        // Verify old chunks were deleted before new ones added
        repositoryMock.Verify(
            r => r.DeleteChunksAsync("existing-doc-id", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IndexContentAsync_WithPromotionLevel_SetsCorrectLevel()
    {
        // Arrange
        CompoundDocument? savedDocument = null;
        var repositoryMock = CreateMockRepository();
        repositoryMock.Setup(r => r.UpsertAsync(It.IsAny<CompoundDocument>(), It.IsAny<CancellationToken>()))
            .Callback<CompoundDocument, CancellationToken>((doc, _) => savedDocument = doc)
            .ReturnsAsync((CompoundDocument doc, CancellationToken _) => doc);

        var indexer = CreateIndexer(repositoryMock: repositoryMock);

        var content = """
            ---
            title: Promoted Doc
            promotion_level: promoted
            ---

            Content here.
            """;

        // Act
        await indexer.IndexContentAsync(content, "test.md");

        // Assert
        savedDocument.ShouldNotBeNull();
        savedDocument.PromotionLevel.ShouldBe("promoted");
    }

    [Fact]
    public async Task DeleteAsync_WhenDocumentExists_ReturnsTrue()
    {
        // Arrange
        var document = new CompoundDocument
        {
            Id = "doc-to-delete",
            TenantKey = "test:main:abc123",
            FilePath = "delete.md"
        };

        var repositoryMock = CreateMockRepository();
        repositoryMock.Setup(r => r.GetByIdAsync("doc-to-delete", It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var indexer = CreateIndexer(repositoryMock: repositoryMock);

        // Act
        var result = await indexer.DeleteAsync("doc-to-delete");

        // Assert
        result.ShouldBeTrue();
        repositoryMock.Verify(r => r.DeleteChunksAsync("doc-to-delete", It.IsAny<CancellationToken>()), Times.Once);
        repositoryMock.Verify(r => r.DeleteAsync("doc-to-delete", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenDocumentNotExists_ReturnsFalse()
    {
        // Arrange
        var repositoryMock = CreateMockRepository();
        repositoryMock.Setup(r => r.GetByIdAsync("non-existent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompoundDocument?)null);

        var indexer = CreateIndexer(repositoryMock: repositoryMock);

        // Act
        var result = await indexer.DeleteAsync("non-existent");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ReindexAllAsync_WhenNoDocuments_ReturnsZero()
    {
        // Arrange
        var repositoryMock = CreateMockRepository();
        repositoryMock.Setup(r => r.GetAllForTenantAsync("test:main:abc123", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CompoundDocument>());

        var indexer = CreateIndexer(repositoryMock: repositoryMock);

        // Act
        var count = await indexer.ReindexAllAsync();

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task IndexContentAsync_WhenEmbeddingThrowsCancellation_ReturnsError()
    {
        // Arrange
        var embeddingMock = new Mock<IEmbeddingService>(MockBehavior.Strict);
        embeddingMock.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("Operation was cancelled"));

        var indexer = CreateIndexer(embeddingServiceMock: embeddingMock);

        var content = """
            ---
            title: Test Doc
            ---

            Content here.
            """;

        // Act
        var result = await indexer.IndexContentAsync(content, "test.md");

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("cancelled"));
    }

    [Fact]
    public async Task IndexContentAsync_RecordsProcessingAndEmbeddingTime()
    {
        // Arrange
        var indexer = CreateIndexer();

        var content = """
            ---
            title: Test Doc
            ---

            Content here.
            """;

        // Act
        var result = await indexer.IndexContentAsync(content, "test.md");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ProcessingTimeMs.ShouldBeGreaterThanOrEqualTo(0);
        result.EmbeddingTimeMs.ShouldBeGreaterThanOrEqualTo(0);
    }

    #endregion
}
