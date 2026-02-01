using CompoundDocs.Common.Graph;
using CompoundDocs.Common.Parsing;
using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.Services.DocumentProcessing;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.IntegrationTests.Services;

/// <summary>
/// Integration tests for DocumentIndexer demonstrating full indexing pipeline.
/// Uses mocked dependencies to focus on indexer logic.
/// </summary>
public sealed class DocumentIndexerTests
{
    private readonly Mock<IDocumentProcessor> _documentProcessorMock;
    private readonly Mock<IDocumentRepository> _documentRepositoryMock;
    private readonly DocumentLinkGraph _linkGraph;
    private readonly DocumentIndexer _sut;

    public DocumentIndexerTests()
    {
        _documentProcessorMock = new Mock<IDocumentProcessor>();
        _documentRepositoryMock = new Mock<IDocumentRepository>();
        _linkGraph = new DocumentLinkGraph();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<DocumentIndexer>();

        _sut = new DocumentIndexer(
            _documentProcessorMock.Object,
            _documentRepositoryMock.Object,
            _linkGraph,
            logger);
    }

    #region IndexDocumentAsync Tests

    [Fact]
    public async Task IndexDocumentAsync_WithValidDocument_ReturnsSuccessResult()
    {
        // Arrange
        var filePath = "docs/test.md";
        var content = """
            ---
            title: Test Document
            doc_type: spec
            ---

            # Test Document

            This is test content.
            """;
        var tenantKey = "test-project:main:abc123";
        var documentId = Guid.NewGuid().ToString();

        var processedDoc = ProcessedDocument.Success(
            filePath: filePath,
            title: "Test Document",
            docType: "spec",
            promotionLevel: "standard",
            content: "# Test Document\n\nThis is test content.",
            embedding: new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f }),
            chunks: Array.Empty<ProcessedChunk>(),
            links: Array.Empty<LinkInfo>(),
            frontmatter: new Dictionary<string, object?> { ["title"] = "Test Document", ["doc_type"] = "spec" },
            validationResult: new DocumentValidationResult { IsValid = true },
            tenantKey: tenantKey);

        _documentProcessorMock
            .Setup(x => x.ProcessDocumentAsync(filePath, content, tenantKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(processedDoc);

        var compoundDoc = new CompoundDocument
        {
            Id = documentId,
            TenantKey = tenantKey,
            FilePath = filePath,
            Title = "Test Document",
            DocType = "spec"
        };

        _documentRepositoryMock
            .Setup(x => x.GetByTenantKeyAsync(tenantKey, filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompoundDocument?)null);

        _documentRepositoryMock
            .Setup(x => x.UpsertAsync(It.IsAny<CompoundDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(compoundDoc);

        // Act
        var result = await _sut.IndexDocumentAsync(filePath, content, tenantKey);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.FilePath.ShouldBe(filePath);
        result.Document.ShouldNotBeNull();
        result.Document!.Title.ShouldBe("Test Document");
    }

    [Fact]
    public async Task IndexDocumentAsync_WithChunkedDocument_CreatesChunks()
    {
        // Arrange
        var filePath = "docs/large.md";
        var content = "Large document content...";
        var tenantKey = "test-project:main:abc123";
        var documentId = Guid.NewGuid().ToString();

        var chunks = new List<ProcessedChunk>
        {
            new()
            {
                Index = 0,
                HeaderPath = "Section One",
                StartLine = 0,
                EndLine = 50,
                Content = "Section one content...",
                Embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f })
            },
            new()
            {
                Index = 1,
                HeaderPath = "Section Two",
                StartLine = 51,
                EndLine = 100,
                Content = "Section two content...",
                Embedding = new ReadOnlyMemory<float>(new float[] { 0.3f, 0.4f })
            }
        };

        var processedDoc = ProcessedDocument.Success(
            filePath: filePath,
            title: "Large Document",
            docType: "doc",
            promotionLevel: "standard",
            content: content,
            embedding: new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f }),
            chunks: chunks,
            links: Array.Empty<LinkInfo>(),
            frontmatter: null,
            validationResult: null,
            tenantKey: tenantKey);

        _documentProcessorMock
            .Setup(x => x.ProcessDocumentAsync(filePath, content, tenantKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(processedDoc);

        var compoundDoc = new CompoundDocument
        {
            Id = documentId,
            TenantKey = tenantKey,
            FilePath = filePath
        };

        _documentRepositoryMock
            .Setup(x => x.GetByTenantKeyAsync(tenantKey, filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompoundDocument?)null);

        _documentRepositoryMock
            .Setup(x => x.UpsertAsync(It.IsAny<CompoundDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(compoundDoc);

        _documentRepositoryMock
            .Setup(x => x.UpsertChunksAsync(It.IsAny<IEnumerable<DocumentChunk>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        // Act
        var result = await _sut.IndexDocumentAsync(filePath, content, tenantKey);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ChunkCount.ShouldBe(2);

        _documentRepositoryMock.Verify(
            x => x.UpsertChunksAsync(It.Is<IEnumerable<DocumentChunk>>(c => c.Count() == 2), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IndexDocumentAsync_WithLinks_UpdatesLinkGraph()
    {
        // Arrange
        var filePath = "docs/source.md";
        var content = "Source document with links...";
        var tenantKey = "test-project:main:abc123";
        var documentId = Guid.NewGuid().ToString();

        var links = new List<LinkInfo>
        {
            new("./target1.md", "Target 1", 5, 10),
            new("../other/target2.md", "Target 2", 10, 15)
        };

        var processedDoc = ProcessedDocument.Success(
            filePath: filePath,
            title: "Source Document",
            docType: "doc",
            promotionLevel: "standard",
            content: content,
            embedding: new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f }),
            chunks: Array.Empty<ProcessedChunk>(),
            links: links,
            frontmatter: null,
            validationResult: null,
            tenantKey: tenantKey);

        _documentProcessorMock
            .Setup(x => x.ProcessDocumentAsync(filePath, content, tenantKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(processedDoc);

        var compoundDoc = new CompoundDocument
        {
            Id = documentId,
            TenantKey = tenantKey,
            FilePath = filePath
        };

        _documentRepositoryMock
            .Setup(x => x.GetByTenantKeyAsync(tenantKey, filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompoundDocument?)null);

        _documentRepositoryMock
            .Setup(x => x.UpsertAsync(It.IsAny<CompoundDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(compoundDoc);

        // Act
        var result = await _sut.IndexDocumentAsync(filePath, content, tenantKey);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // Verify link graph was updated
        var linkedDocs = _linkGraph.GetLinkedDocuments(filePath);
        linkedDocs.Count.ShouldBe(2);
    }

    [Fact]
    public async Task IndexDocumentAsync_WithProcessingError_ReturnsFailureResult()
    {
        // Arrange
        var filePath = "docs/invalid.md";
        var content = "Invalid content";
        var tenantKey = "test-project:main:abc123";

        var processedDoc = ProcessedDocument.Failure(filePath, tenantKey, "Processing failed: invalid frontmatter");

        _documentProcessorMock
            .Setup(x => x.ProcessDocumentAsync(filePath, content, tenantKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(processedDoc);

        // Act
        var result = await _sut.IndexDocumentAsync(filePath, content, tenantKey);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.ShouldContain("Processing failed");
    }

    #endregion

    #region IndexDocumentsAsync (Batch) Tests

    [Fact]
    public async Task IndexDocumentsAsync_WithMultipleDocuments_ProcessesAll()
    {
        // Arrange
        var tenantKey = "test-project:main:abc123";
        var documents = new[]
        {
            ("docs/doc1.md", "Content 1"),
            ("docs/doc2.md", "Content 2"),
            ("docs/doc3.md", "Content 3")
        };

        var processedDocs = documents.Select(d => ProcessedDocument.Success(
            filePath: d.Item1,
            title: $"Title for {d.Item1}",
            docType: "doc",
            promotionLevel: "standard",
            content: d.Item2,
            embedding: new ReadOnlyMemory<float>(new float[] { 0.1f }),
            chunks: Array.Empty<ProcessedChunk>(),
            links: Array.Empty<LinkInfo>(),
            frontmatter: null,
            validationResult: null,
            tenantKey: tenantKey)).ToList();

        _documentProcessorMock
            .Setup(x => x.ProcessDocumentsAsync(It.IsAny<IEnumerable<(string, string)>>(), tenantKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(processedDocs);

        _documentRepositoryMock
            .Setup(x => x.GetByTenantKeyAsync(tenantKey, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompoundDocument?)null);

        _documentRepositoryMock
            .Setup(x => x.UpsertAsync(It.IsAny<CompoundDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompoundDocument doc, CancellationToken _) =>
            {
                doc.Id = Guid.NewGuid().ToString();
                return doc;
            });

        _documentRepositoryMock
            .Setup(x => x.DeleteChunksAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var results = await _sut.IndexDocumentsAsync(documents, tenantKey);

        // Assert
        results.Count.ShouldBe(3);
        results.All(r => r.IsSuccess).ShouldBeTrue();
    }

    [Fact]
    public async Task IndexDocumentsAsync_WithMixedResults_ReportsPartialSuccess()
    {
        // Arrange
        var tenantKey = "test-project:main:abc123";
        var documents = new[]
        {
            ("docs/good.md", "Good content"),
            ("docs/bad.md", "Bad content")
        };

        var processedDocs = new List<ProcessedDocument>
        {
            ProcessedDocument.Success(
                filePath: "docs/good.md",
                title: "Good Document",
                docType: "doc",
                promotionLevel: "standard",
                content: "Good content",
                embedding: new ReadOnlyMemory<float>(new float[] { 0.1f }),
                chunks: Array.Empty<ProcessedChunk>(),
                links: Array.Empty<LinkInfo>(),
                frontmatter: null,
                validationResult: null,
                tenantKey: tenantKey),
            ProcessedDocument.Failure("docs/bad.md", tenantKey, "Invalid document")
        };

        _documentProcessorMock
            .Setup(x => x.ProcessDocumentsAsync(It.IsAny<IEnumerable<(string, string)>>(), tenantKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(processedDocs);

        _documentRepositoryMock
            .Setup(x => x.GetByTenantKeyAsync(tenantKey, "docs/good.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompoundDocument?)null);

        _documentRepositoryMock
            .Setup(x => x.UpsertAsync(It.IsAny<CompoundDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompoundDocument doc, CancellationToken _) =>
            {
                doc.Id = Guid.NewGuid().ToString();
                return doc;
            });

        _documentRepositoryMock
            .Setup(x => x.DeleteChunksAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var results = await _sut.IndexDocumentsAsync(documents, tenantKey);

        // Assert
        results.Count.ShouldBe(2);
        results.Count(r => r.IsSuccess).ShouldBe(1);
        results.Count(r => !r.IsSuccess).ShouldBe(1);
    }

    #endregion

    #region DeleteDocumentAsync Tests

    [Fact]
    public async Task DeleteDocumentAsync_WithExistingDocument_DeletesAndReturnsTrue()
    {
        // Arrange
        var filePath = "docs/to-delete.md";
        var tenantKey = "test-project:main:abc123";
        var documentId = Guid.NewGuid().ToString();

        var existingDoc = new CompoundDocument
        {
            Id = documentId,
            TenantKey = tenantKey,
            FilePath = filePath
        };

        _documentRepositoryMock
            .Setup(x => x.GetByTenantKeyAsync(tenantKey, filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDoc);

        _documentRepositoryMock
            .Setup(x => x.DeleteChunksAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _documentRepositoryMock
            .Setup(x => x.DeleteAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Add to link graph first
        _linkGraph.AddDocument(filePath);
        _linkGraph.AddLink(filePath, "docs/other.md");

        // Act
        var result = await _sut.DeleteDocumentAsync(tenantKey, filePath);

        // Assert
        result.ShouldBeTrue();
        _documentRepositoryMock.Verify(x => x.DeleteChunksAsync(documentId, It.IsAny<CancellationToken>()), Times.Once);
        _documentRepositoryMock.Verify(x => x.DeleteAsync(documentId, It.IsAny<CancellationToken>()), Times.Once);

        // Verify removed from link graph
        _linkGraph.GetLinkedDocuments(filePath).Count.ShouldBe(0);
    }

    [Fact]
    public async Task DeleteDocumentAsync_WithNonExistentDocument_ReturnsFalse()
    {
        // Arrange
        var filePath = "docs/nonexistent.md";
        var tenantKey = "test-project:main:abc123";

        _documentRepositoryMock
            .Setup(x => x.GetByTenantKeyAsync(tenantKey, filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompoundDocument?)null);

        // Act
        var result = await _sut.DeleteDocumentAsync(tenantKey, filePath);

        // Assert
        result.ShouldBeFalse();
        _documentRepositoryMock.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Full Pipeline Integration Tests

    [Fact]
    public async Task FullPipeline_IndexUpdateDelete_WorksCorrectly()
    {
        // Arrange
        var filePath = "docs/lifecycle.md";
        var tenantKey = "test-project:main:abc123";
        var documentId = Guid.NewGuid().ToString();

        // Initial document
        var initialContent = "Initial content";
        var initialProcessedDoc = ProcessedDocument.Success(
            filePath: filePath,
            title: "Initial Title",
            docType: "doc",
            promotionLevel: "standard",
            content: initialContent,
            embedding: new ReadOnlyMemory<float>(new float[] { 0.1f }),
            chunks: Array.Empty<ProcessedChunk>(),
            links: Array.Empty<LinkInfo>(),
            frontmatter: null,
            validationResult: null,
            tenantKey: tenantKey);

        _documentProcessorMock
            .SetupSequence(x => x.ProcessDocumentAsync(filePath, It.IsAny<string>(), tenantKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(initialProcessedDoc);

        var compoundDoc = new CompoundDocument
        {
            Id = documentId,
            TenantKey = tenantKey,
            FilePath = filePath,
            Title = "Initial Title"
        };

        _documentRepositoryMock
            .Setup(x => x.GetByTenantKeyAsync(tenantKey, filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompoundDocument?)null);

        _documentRepositoryMock
            .Setup(x => x.UpsertAsync(It.IsAny<CompoundDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(compoundDoc);

        // Act - Index
        var indexResult = await _sut.IndexDocumentAsync(filePath, initialContent, tenantKey);

        // Assert - Index
        indexResult.IsSuccess.ShouldBeTrue();
        indexResult.Document!.Title.ShouldBe("Initial Title");

        // Setup for delete
        _documentRepositoryMock
            .Setup(x => x.GetByTenantKeyAsync(tenantKey, filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(compoundDoc);

        _documentRepositoryMock
            .Setup(x => x.DeleteChunksAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _documentRepositoryMock
            .Setup(x => x.DeleteAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act - Delete
        var deleteResult = await _sut.DeleteDocumentAsync(tenantKey, filePath);

        // Assert - Delete
        deleteResult.ShouldBeTrue();
    }

    #endregion
}
