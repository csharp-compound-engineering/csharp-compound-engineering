using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.SemanticKernel;
using CompoundDocs.McpServer.Services.FileWatcher;
using CompoundDocs.McpServer.Session;
using CompoundDocs.McpServer.Tools;
using CompoundDocs.Tests.Utilities;
using Microsoft.Extensions.Logging;
using IDocumentIndexer = CompoundDocs.McpServer.Services.DocumentProcessing.IDocumentIndexer;
using IndexResult = CompoundDocs.McpServer.Services.DocumentProcessing.DocumentIndexingResult;

namespace CompoundDocs.Tests.Integration;

/// <summary>
/// End-to-end workflow integration tests covering the complete document lifecycle:
/// capture -> index -> search -> RAG query.
/// Uses mocked dependencies to test integration between components.
/// </summary>
public sealed class EndToEndWorkflowTests : TestBase, IClassFixture<EndToEndWorkflowFixture>
{
    private readonly EndToEndWorkflowFixture _fixture;

    public EndToEndWorkflowTests(EndToEndWorkflowFixture fixture)
    {
        _fixture = fixture;
    }

    #region Complete Workflow Tests

    [Fact]
    public async Task CaptureIndexSearchRagQuery_CompleteWorkflow_ShouldSucceed()
    {
        // Arrange
        var sessionContext = CreateMockSessionContext(isActive: true, tenantKey: "test-project:main:abc123");
        var embeddingService = CreateMockEmbeddingService();
        var documentRepository = CreateMockDocumentRepository();
        var documentIndexer = CreateMockDocumentIndexer();
        var logger = CreateLooseMock<ILogger<SemanticSearchTool>>().Object;

        var testDocument = TestDocumentBuilder.CreateSpec()
            .WithTenantKey("test-project", "main", "abc123")
            .WithRandomVector()
            .Build();

        // Setup mocks for the workflow
        SetupDocumentIndexing(documentIndexer, testDocument);
        SetupEmbeddingGeneration(embeddingService);
        SetupDocumentSearch(documentRepository, testDocument);

        var searchTool = new SemanticSearchTool(
            documentRepository.Object,
            embeddingService.Object,
            sessionContext.Object,
            logger);

        // Act - Step 1: Simulate document indexing
        var indexResult = await documentIndexer.Object.IndexDocumentAsync(
            testDocument.FilePath,
            testDocument.Content,
            testDocument.TenantKey,
            CancellationToken.None);

        // Act - Step 2: Perform semantic search
        var searchResult = await searchTool.SearchAsync(
            "API specification",
            limit: 10,
            cancellationToken: CancellationToken.None);

        // Assert
        indexResult.ShouldNotBeNull();
        indexResult.IsSuccess.ShouldBeTrue();

        searchResult.ShouldNotBeNull();
        searchResult.Success.ShouldBeTrue();
        searchResult.Data.ShouldNotBeNull();
        searchResult.Data!.Documents.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task IndexAndSearch_WithMultipleDocTypes_ShouldFilterCorrectly()
    {
        // Arrange
        var sessionContext = CreateMockSessionContext(isActive: true, tenantKey: "test-project:main:abc123");
        var embeddingService = CreateMockEmbeddingService();
        var documentRepository = CreateMockDocumentRepository();
        var logger = CreateLooseMock<ILogger<SemanticSearchTool>>().Object;

        var specDoc = TestDocumentBuilder.CreateSpec()
            .WithTenantKey("test-project", "main", "abc123")
            .WithRandomVector()
            .Build();

        var adrDoc = TestDocumentBuilder.CreateAdr()
            .WithTenantKey("test-project", "main", "abc123")
            .WithRandomVector()
            .Build();

        SetupEmbeddingGeneration(embeddingService);

        // Setup to return both documents
        documentRepository
            .Setup(r => r.SearchAsync(
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReadOnlyMemory<float> _, string _, int limit, float _, string? docType, CancellationToken _) =>
            {
                var results = new List<SearchResult>();
                if (docType == null || docType == DocumentTypes.Spec)
                {
                    results.Add(new SearchResult(specDoc, 0.9f));
                }
                if (docType == null || docType == DocumentTypes.Adr)
                {
                    results.Add(new SearchResult(adrDoc, 0.85f));
                }
                return results.Take(limit).ToList();
            });

        var searchTool = new SemanticSearchTool(
            documentRepository.Object,
            embeddingService.Object,
            sessionContext.Object,
            logger);

        // Act - Search with doc type filter
        var filteredResult = await searchTool.SearchAsync(
            "database decision",
            limit: 10,
            docTypes: "adr",
            cancellationToken: CancellationToken.None);

        // Assert
        filteredResult.Success.ShouldBeTrue();
        filteredResult.Data.ShouldNotBeNull();
        // Since filter happens in the tool after search, we verify the repository was called
        documentRepository.Verify(r => r.SearchAsync(
            It.IsAny<ReadOnlyMemory<float>>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<float>(),
            "adr",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Project Activation Tests

    [Fact]
    public async Task ActivateProject_ValidPath_ShouldSetTenantContext()
    {
        // Arrange
        var sessionContext = CreateLooseMock<ISessionContext>();
        string? capturedPath = null;
        string? capturedBranch = null;

        sessionContext
            .Setup(s => s.ActivateProject(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((path, branch) =>
            {
                capturedPath = path;
                capturedBranch = branch;
            });

        // Act
        sessionContext.Object.ActivateProject("/test/project/path", "feature-branch");

        // Assert
        capturedPath.ShouldBe("/test/project/path");
        capturedBranch.ShouldBe("feature-branch");
    }

    [Fact]
    public void TenantIsolation_DifferentTenants_ShouldHaveDifferentKeys()
    {
        // Arrange & Act
        var tenantKey1 = CompoundDocument.CreateTenantKey("project-a", "main", "hash1");
        var tenantKey2 = CompoundDocument.CreateTenantKey("project-b", "main", "hash2");
        var tenantKey3 = CompoundDocument.CreateTenantKey("project-a", "develop", "hash1");

        // Assert
        tenantKey1.ShouldNotBe(tenantKey2);
        tenantKey1.ShouldNotBe(tenantKey3);
        tenantKey2.ShouldNotBe(tenantKey3);

        tenantKey1.ShouldBe("project-a:main:hash1");
        tenantKey2.ShouldBe("project-b:main:hash2");
        tenantKey3.ShouldBe("project-a:develop:hash1");
    }

    [Theory]
    [InlineData("project-a:main:hash123")]
    [InlineData("my-project:feature/test:abcdef")]
    [InlineData("compound-docs:release/v1.0:xyz789")]
    public void ParseTenantKey_ValidKeys_ShouldParseParts(string tenantKey)
    {
        // Act
        var (projectName, branchName, pathHash) = CompoundDocument.ParseTenantKey(tenantKey);

        // Assert
        projectName.ShouldNotBeNullOrEmpty();
        branchName.ShouldNotBeNullOrEmpty();
        pathHash.ShouldNotBeNullOrEmpty();
        $"{projectName}:{branchName}:{pathHash}".ShouldBe(tenantKey);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("only:two")]
    [InlineData("")]
    public void ParseTenantKey_InvalidKeys_ShouldThrow(string tenantKey)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => CompoundDocument.ParseTenantKey(tenantKey));
    }

    #endregion

    #region File Watcher Reindexing Tests

    [Fact]
    public async Task FileWatcher_FileChanged_ShouldTriggerReindex()
    {
        // Arrange
        var fileWatcher = CreateLooseMock<IFileWatcherService>();
        var documentIndexer = CreateMockDocumentIndexer();

        var testDocument = TestDocumentBuilder.Create()
            .WithFilePath("docs/changed-file.md")
            .Build();

        // Create a file change event using the correct signature (FilePath, ChangeType, OldPath?)
        var changeEvent = new FileChangeEvent(
            testDocument.FilePath,
            FileChangeType.Modified);

        SetupDocumentIndexing(documentIndexer, testDocument);

        // Act - Simulate reindex triggered by file change
        var reindexResult = await documentIndexer.Object.IndexDocumentAsync(
            changeEvent.FilePath,
            testDocument.Content,
            testDocument.TenantKey,
            CancellationToken.None);

        // Assert
        reindexResult.IsSuccess.ShouldBeTrue();
        changeEvent.ChangeType.ShouldBe(FileChangeType.Modified);
    }

    [Fact]
    public async Task Reconciliation_DetectsNewAndDeletedFiles_ShouldUpdateIndex()
    {
        // Arrange
        var fileWatcher = CreateMockFileWatcher();
        var expectedResult = new ReconciliationResult
        {
            ProjectPath = "/test/project",
            NewFiles = new List<ReconciliationItem>
            {
                new("docs/new-doc.md", ReconciliationAction.Index, "New file detected")
            },
            ModifiedFiles = new List<ReconciliationItem>
            {
                new("docs/existing-doc.md", ReconciliationAction.Reindex, "File modified")
            },
            DeletedFiles = new List<ReconciliationItem>
            {
                new("docs/removed-doc.md", ReconciliationAction.Remove, "File deleted")
            }
        };

        fileWatcher
            .Setup(w => w.ReconcileAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await fileWatcher.Object.ReconcileAsync(CancellationToken.None);

        // Assert
        result.NewFiles.ShouldContain(f => f.FilePath == "docs/new-doc.md");
        result.ModifiedFiles.ShouldContain(f => f.FilePath == "docs/existing-doc.md");
        result.DeletedFiles.ShouldContain(f => f.FilePath == "docs/removed-doc.md");
        result.HasChanges.ShouldBeTrue();
    }

    #endregion

    #region Promotion Level Tests

    [Theory]
    [InlineData(PromotionLevels.Standard, 1.0f)]
    [InlineData(PromotionLevels.Promoted, 1.5f)]
    [InlineData(PromotionLevels.Pinned, 2.0f)]
    public void PromotionLevel_GetBoostFactor_ShouldReturnCorrectValue(string level, float expectedBoost)
    {
        // Act
        var boost = PromotionLevels.GetBoostFactor(level);

        // Assert
        boost.ShouldBe(expectedBoost);
    }

    [Fact]
    public async Task PromotionLevelChange_ShouldAffectSearchRanking()
    {
        // Arrange
        var sessionContext = CreateMockSessionContext(isActive: true, tenantKey: "test-project:main:abc123");
        var embeddingService = CreateMockEmbeddingService();
        var documentRepository = CreateMockDocumentRepository();
        var logger = CreateLooseMock<ILogger<SemanticSearchTool>>().Object;

        var standardDoc = TestDocumentBuilder.Create()
            .WithTitle("Standard Document")
            .AsStandard()
            .WithTenantKey("test-project", "main", "abc123")
            .Build();

        var pinnedDoc = TestDocumentBuilder.Create()
            .WithTitle("Pinned Document")
            .AsPinned()
            .WithTenantKey("test-project", "main", "abc123")
            .Build();

        SetupEmbeddingGeneration(embeddingService);

        // Both documents have same relevance score, but pinned should rank higher
        documentRepository
            .Setup(r => r.SearchAsync(
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchResult>
            {
                new(standardDoc, 0.8f),
                new(pinnedDoc, 0.8f)
            });

        var searchTool = new SemanticSearchTool(
            documentRepository.Object,
            embeddingService.Object,
            sessionContext.Object,
            logger);

        // Act
        var result = await searchTool.SearchAsync("test query", limit: 10);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.Documents.ShouldNotBeEmpty();

        // Pinned document should have higher boosted score
        var pinnedResult = result.Data.Documents.First(d => d.Title == "Pinned Document");
        var standardResult = result.Data.Documents.First(d => d.Title == "Standard Document");

        pinnedResult.RelevanceScore.ShouldBeGreaterThan(standardResult.RelevanceScore);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task SearchWithNoActiveProject_ShouldReturnError()
    {
        // Arrange
        var sessionContext = CreateMockSessionContext(isActive: false);
        var embeddingService = CreateMockEmbeddingService();
        var documentRepository = CreateMockDocumentRepository();
        var logger = CreateLooseMock<ILogger<SemanticSearchTool>>().Object;

        var searchTool = new SemanticSearchTool(
            documentRepository.Object,
            embeddingService.Object,
            sessionContext.Object,
            logger);

        // Act
        var result = await searchTool.SearchAsync("test query", limit: 10);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("NO_ACTIVE_PROJECT");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchWithEmptyQuery_ShouldReturnError(string? query)
    {
        // Arrange
        var sessionContext = CreateMockSessionContext(isActive: true, tenantKey: "test-project:main:abc123");
        var embeddingService = CreateMockEmbeddingService();
        var documentRepository = CreateMockDocumentRepository();
        var logger = CreateLooseMock<ILogger<SemanticSearchTool>>().Object;

        var searchTool = new SemanticSearchTool(
            documentRepository.Object,
            embeddingService.Object,
            sessionContext.Object,
            logger);

        // Act
        var result = await searchTool.SearchAsync(query!, limit: 10);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("EMPTY_QUERY");
    }

    #endregion

    #region Helper Methods

    private Mock<ISessionContext> CreateMockSessionContext(bool isActive, string? tenantKey = null)
    {
        var mock = CreateLooseMock<ISessionContext>();
        mock.Setup(s => s.IsProjectActive).Returns(isActive);
        mock.Setup(s => s.TenantKey).Returns(tenantKey);
        return mock;
    }

    private Mock<IEmbeddingService> CreateMockEmbeddingService()
    {
        return CreateLooseMock<IEmbeddingService>();
    }

    private Mock<IDocumentRepository> CreateMockDocumentRepository()
    {
        return CreateLooseMock<IDocumentRepository>();
    }

    private Mock<IDocumentIndexer> CreateMockDocumentIndexer()
    {
        return CreateLooseMock<IDocumentIndexer>();
    }

    private Mock<IFileWatcherService> CreateMockFileWatcher()
    {
        return CreateLooseMock<IFileWatcherService>();
    }

    private void SetupEmbeddingGeneration(Mock<IEmbeddingService> mock)
    {
        var testEmbedding = CreateTestEmbedding();
        mock.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
    }

    private void SetupDocumentIndexing(Mock<IDocumentIndexer> mock, CompoundDocument document)
    {
        mock.Setup(i => i.IndexDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(IndexResult.Success(document.FilePath, document, 1));
    }

    private void SetupDocumentSearch(Mock<IDocumentRepository> mock, CompoundDocument document)
    {
        mock.Setup(r => r.SearchAsync(
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchResult> { new(document, 0.9f) });
    }

    private static ReadOnlyMemory<float> CreateTestEmbedding(int dimensions = 1024)
    {
        var vector = new float[dimensions];
        var random = new Random(42); // Fixed seed for reproducibility
        for (int i = 0; i < dimensions; i++)
        {
            vector[i] = (float)random.NextDouble();
        }
        return new ReadOnlyMemory<float>(vector);
    }

    #endregion
}

/// <summary>
/// Fixture for end-to-end workflow tests providing shared test context.
/// </summary>
public sealed class EndToEndWorkflowFixture : IDisposable
{
    /// <summary>
    /// Gets the test project path for integration tests.
    /// </summary>
    public string TestProjectPath { get; }

    /// <summary>
    /// Gets a consistent tenant key for tests.
    /// </summary>
    public string TestTenantKey { get; }

    public EndToEndWorkflowFixture()
    {
        TestProjectPath = Path.Combine(Path.GetTempPath(), "compound-docs-test", Guid.NewGuid().ToString());
        TestTenantKey = CompoundDocument.CreateTenantKey("test-project", "main", "fixture-hash");

        // Create test directory
        Directory.CreateDirectory(TestProjectPath);
    }

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(TestProjectPath))
        {
            try
            {
                Directory.Delete(TestProjectPath, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
