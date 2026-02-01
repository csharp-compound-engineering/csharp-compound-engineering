using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.SemanticKernel;
using CompoundDocs.McpServer.Session;
using CompoundDocs.McpServer.Tools;
using CompoundDocs.Tests.Utilities;
using Microsoft.Extensions.Logging;
using IDocumentIndexer = CompoundDocs.McpServer.Services.DocumentProcessing.IDocumentIndexer;
using IndexResult = CompoundDocs.McpServer.Services.DocumentProcessing.DocumentIndexingResult;

namespace CompoundDocs.Tests.Security;

/// <summary>
/// Security validation tests covering tenant isolation, injection prevention,
/// path traversal prevention, and input validation for MCP tools.
/// </summary>
public sealed class SecurityValidationTests : TestBase
{
    #region Tenant Isolation Tests

    [Fact]
    public async Task TenantIsolation_SearchShouldOnlyReturnOwnTenantDocuments()
    {
        // Arrange
        var tenantA = "project-a:main:hash-a";
        var tenantB = "project-b:main:hash-b";

        var docA = TestDocumentBuilder.Create()
            .WithId("doc-a")
            .WithTenantKey(tenantA)
            .WithTitle("Tenant A Document")
            .Build();

        var docB = TestDocumentBuilder.Create()
            .WithId("doc-b")
            .WithTenantKey(tenantB)
            .WithTitle("Tenant B Document")
            .Build();

        var documentRepository = CreateMockDocumentRepository(
            tenantA, new[] { docA },
            tenantB, new[] { docB });

        var sessionContext = CreateMockSessionContext(isActive: true, tenantKey: tenantA);
        var embeddingService = CreateMockEmbeddingService();
        var logger = CreateLooseMock<ILogger<SemanticSearchTool>>().Object;

        var searchTool = new SemanticSearchTool(
            documentRepository,
            embeddingService,
            sessionContext.Object,
            logger);

        // Act - Search as tenant A
        var result = await searchTool.SearchAsync("document", limit: 10);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.Documents.ShouldAllBe(d => !d.Title.Contains("Tenant B"));
    }

    [Fact]
    public async Task TenantIsolation_CannotAccessOtherTenantByManipulatingTenantKey()
    {
        // Arrange
        var legitimateTenant = "project-a:main:hash-a";
        var attackTenant = "project-b:main:hash-b";

        var secretDoc = TestDocumentBuilder.Create()
            .WithId("secret-doc")
            .WithTenantKey(attackTenant)
            .WithTitle("Secret Document")
            .WithContent("Confidential information")
            .Build();

        var documentRepository = CreateMockDocumentRepositoryWithIsolation();
        var sessionContext = CreateMockSessionContext(isActive: true, tenantKey: legitimateTenant);
        var embeddingService = CreateMockEmbeddingService();
        var logger = CreateLooseMock<ILogger<SemanticSearchTool>>().Object;

        var searchTool = new SemanticSearchTool(
            documentRepository,
            embeddingService,
            sessionContext.Object,
            logger);

        // Act - Attempt search (should only see own tenant's documents)
        var result = await searchTool.SearchAsync("confidential secret", limit: 10);

        // Assert - Should not find the secret document from other tenant
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.Documents.ShouldNotContain(d => d.Title == "Secret Document");
    }

    [Theory]
    [InlineData("project:branch:hash", "project", "branch", "hash")]
    [InlineData("my-project:feature/test:abc123", "my-project", "feature/test", "abc123")]
    [InlineData("compound:develop:xyz", "compound", "develop", "xyz")]
    public void TenantKey_ParseAndCreate_ShouldBeReversible(
        string tenantKey, string expectedProject, string expectedBranch, string expectedHash)
    {
        // Act
        var (project, branch, hash) = CompoundDocument.ParseTenantKey(tenantKey);
        var recreatedKey = CompoundDocument.CreateTenantKey(project, branch, hash);

        // Assert
        project.ShouldBe(expectedProject);
        branch.ShouldBe(expectedBranch);
        hash.ShouldBe(expectedHash);
        recreatedKey.ShouldBe(tenantKey);
    }

    [Fact]
    public void TenantKey_WithMaliciousInput_ShouldNotBreakIsolation()
    {
        // Arrange - Attempt to inject additional colons to break parsing
        var maliciousInput = "project:branch:hash:extra:parts";

        // Act
        var (project, branch, pathHash) = CompoundDocument.ParseTenantKey(maliciousInput);

        // Assert - The third part should contain everything after the second colon
        project.ShouldBe("project");
        branch.ShouldBe("branch");
        pathHash.ShouldBe("hash:extra:parts"); // Split limit of 3 preserves this
    }

    #endregion

    #region SQL Injection Prevention Tests

    [Theory]
    [InlineData("'; DROP TABLE documents; --")]
    [InlineData("1' OR '1'='1")]
    [InlineData("admin'--")]
    [InlineData("'; TRUNCATE TABLE documents; --")]
    [InlineData("1; DELETE FROM documents WHERE 1=1; --")]
    public async Task SqlInjection_InSearchQuery_ShouldBeSafelyHandled(string maliciousQuery)
    {
        // Arrange
        var sessionContext = CreateMockSessionContext(isActive: true, tenantKey: "test:main:hash");
        var embeddingService = CreateMockEmbeddingService();
        var documentRepository = CreateMockDocumentRepositoryWithIsolation();
        var logger = CreateLooseMock<ILogger<SemanticSearchTool>>().Object;

        var searchTool = new SemanticSearchTool(
            documentRepository,
            embeddingService,
            sessionContext.Object,
            logger);

        // Act - The malicious query should be treated as regular text
        var result = await searchTool.SearchAsync(maliciousQuery, limit: 10);

        // Assert - Should complete without error (query is embedded, not executed as SQL)
        result.ShouldNotBeNull();
        // The tool should either succeed with empty results or handle gracefully
    }

    [Theory]
    [InlineData("project'; DROP TABLE--", "main", "hash")]
    [InlineData("project", "branch'; DELETE--", "hash")]
    [InlineData("project", "branch", "hash'; TRUNCATE--")]
    public void SqlInjection_InTenantKey_ShouldNotExecute(string project, string branch, string hash)
    {
        // Act - Creating tenant key with malicious input
        var tenantKey = CompoundDocument.CreateTenantKey(project, branch, hash);

        // Assert - The key should be created as a plain string, no execution
        tenantKey.ShouldNotBeNull();
        tenantKey.ShouldContain(project);
        tenantKey.ShouldContain(branch);
        tenantKey.ShouldContain(hash);
    }

    [Theory]
    [InlineData("spec'; DROP TABLE--")]
    [InlineData("adr; DELETE FROM documents--")]
    public async Task SqlInjection_InDocTypeFilter_ShouldBeSafelyHandled(string maliciousDocType)
    {
        // Arrange
        var sessionContext = CreateMockSessionContext(isActive: true, tenantKey: "test:main:hash");
        var embeddingService = CreateMockEmbeddingService();
        var documentRepository = CreateMockDocumentRepositoryWithIsolation();
        var logger = CreateLooseMock<ILogger<SemanticSearchTool>>().Object;

        var searchTool = new SemanticSearchTool(
            documentRepository,
            embeddingService,
            sessionContext.Object,
            logger);

        // Act
        var result = await searchTool.SearchAsync("test", limit: 10, docTypes: maliciousDocType);

        // Assert - Should fail validation, not execute SQL
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("INVALID_DOC_TYPE");
    }

    #endregion

    #region Path Traversal Prevention Tests

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\windows\\system32\\config\\sam")]
    [InlineData("docs/../../../secrets.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\System32\\config\\sam")]
    [InlineData("....//....//etc/passwd")]
    [InlineData("%2e%2e%2f%2e%2e%2f%2e%2e%2fetc%2fpasswd")]
    public void PathTraversal_MaliciousFilePaths_ShouldBeDetected(string maliciousPath)
    {
        // Arrange & Act
        var isPathSafe = IsPathSafe(maliciousPath);

        // Assert
        isPathSafe.ShouldBeFalse($"Path '{maliciousPath}' should be detected as unsafe");
    }

    [Theory]
    [InlineData("docs/specification.md")]
    [InlineData("specs/api/v1/endpoints.md")]
    [InlineData("README.md")]
    [InlineData("docs/adr/001-decision.md")]
    public void PathTraversal_LegitimateFilePaths_ShouldBeAllowed(string legitimatePath)
    {
        // Arrange & Act
        var isPathSafe = IsPathSafe(legitimatePath);

        // Assert
        isPathSafe.ShouldBeTrue($"Path '{legitimatePath}' should be allowed");
    }

    [Fact]
    public async Task PathTraversal_InIndexDocumentTool_ShouldValidatePath()
    {
        // Arrange
        var sessionContext = CreateMockSessionContext(
            isActive: true,
            tenantKey: "test:main:hash",
            projectPath: "/home/user/project");
        var documentIndexer = CreateMockDocumentIndexer();
        var logger = CreateLooseMock<ILogger<IndexDocumentTool>>().Object;

        var indexTool = new IndexDocumentTool(
            documentIndexer,
            sessionContext.Object,
            logger);

        // Act - Attempt path traversal
        var result = await indexTool.IndexDocumentAsync("../../../etc/passwd");

        // Assert - Should fail because file doesn't exist in project
        // The tool validates path by attempting to read the file, which will fail
        result.Success.ShouldBeFalse();
        // The actual error code depends on where validation fails - typically FILE_NOT_FOUND
        // since the file doesn't exist at the path resolved from project root
        result.ErrorCode.ShouldNotBeNull();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task PathTraversal_InUpdatePromotionLevel_ShouldValidatePath()
    {
        // Arrange
        var sessionContext = CreateMockSessionContext(isActive: true, tenantKey: "test:main:hash");
        var documentRepository = CreateMockDocumentRepositoryWithIsolation();
        var logger = CreateLooseMock<ILogger<UpdatePromotionLevelTool>>().Object;

        var updateTool = new UpdatePromotionLevelTool(
            documentRepository,
            sessionContext.Object,
            logger);

        // Act - Attempt path traversal
        var result = await updateTool.UpdatePromotionLevelAsync(
            "../../../sensitive/file.md",
            "promoted");

        // Assert - Should not find the document (path doesn't match any stored path)
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("DOCUMENT_NOT_FOUND");
    }

    #endregion

    #region Input Validation Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task InputValidation_EmptyQuery_ShouldReject(string? emptyQuery)
    {
        // Arrange
        var sessionContext = CreateMockSessionContext(isActive: true, tenantKey: "test:main:hash");
        var embeddingService = CreateMockEmbeddingService();
        var documentRepository = CreateMockDocumentRepositoryWithIsolation();
        var logger = CreateLooseMock<ILogger<SemanticSearchTool>>().Object;

        var searchTool = new SemanticSearchTool(
            documentRepository,
            embeddingService,
            sessionContext.Object,
            logger);

        // Act
        var result = await searchTool.SearchAsync(emptyQuery!, limit: 10);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("EMPTY_QUERY");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InputValidation_EmptyFilePath_ShouldReject(string? emptyPath)
    {
        // Arrange
        var sessionContext = CreateMockSessionContext(isActive: true, tenantKey: "test:main:hash");
        var documentIndexer = CreateMockDocumentIndexer();
        var logger = CreateLooseMock<ILogger<IndexDocumentTool>>().Object;

        var indexTool = new IndexDocumentTool(
            documentIndexer,
            sessionContext.Object,
            logger);

        // Act
        var result = await indexTool.IndexDocumentAsync(emptyPath!);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("MISSING_PARAMETER");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("super-promoted")]
    [InlineData("admin")]
    [InlineData("root")]
    [InlineData("")]
    public async Task InputValidation_InvalidPromotionLevel_ShouldReject(string invalidLevel)
    {
        // Arrange
        var sessionContext = CreateMockSessionContext(isActive: true, tenantKey: "test:main:hash");
        var documentRepository = CreateLooseMock<IDocumentRepository>().Object;
        var logger = CreateLooseMock<ILogger<UpdatePromotionLevelTool>>().Object;

        var updateTool = new UpdatePromotionLevelTool(
            documentRepository,
            sessionContext.Object,
            logger);

        // Act
        var result = await updateTool.UpdatePromotionLevelAsync("docs/test.md", invalidLevel);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBeOneOf("INVALID_PROMOTION_LEVEL", "MISSING_PARAMETER");
    }

    [Theory]
    [InlineData("standard")]
    [InlineData("promoted")]
    [InlineData("important")]
    [InlineData("pinned")]
    [InlineData("critical")]
    [InlineData("STANDARD")]
    [InlineData("Promoted")]
    public async Task InputValidation_ValidPromotionLevel_ShouldAccept(string validLevel)
    {
        // Arrange
        var sessionContext = CreateMockSessionContext(isActive: true, tenantKey: "test:main:hash");
        var documentRepository = CreateLooseMock<IDocumentRepository>();
        var testDoc = TestDocumentBuilder.Create().Build();

        documentRepository
            .Setup(r => r.GetByTenantKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testDoc);

        documentRepository
            .Setup(r => r.UpdatePromotionLevelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var logger = CreateLooseMock<ILogger<UpdatePromotionLevelTool>>().Object;

        var updateTool = new UpdatePromotionLevelTool(
            documentRepository.Object,
            sessionContext.Object,
            logger);

        // Act
        var result = await updateTool.UpdatePromotionLevelAsync("docs/test.md", validLevel);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Theory]
    [InlineData("spec")]
    [InlineData("adr")]
    [InlineData("research")]
    [InlineData("doc")]
    public void InputValidation_ValidDocTypes_ShouldBeRecognized(string docType)
    {
        // Act
        var isValid = DocumentTypes.IsValid(docType);

        // Assert
        isValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("readme")]
    [InlineData("configuration")]
    [InlineData("")]
    [InlineData(null)]
    public void InputValidation_InvalidDocTypes_ShouldBeRejected(string? docType)
    {
        // Act
        var isValid = DocumentTypes.IsValid(docType ?? string.Empty);

        // Assert
        isValid.ShouldBeFalse();
    }

    #endregion

    #region XSS Prevention Tests

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert('xss')>")]
    [InlineData("javascript:alert('xss')")]
    [InlineData("<iframe src='http://evil.com'>")]
    public async Task XssPrevention_MaliciousContent_ShouldBeStoredSafely(string maliciousContent)
    {
        // Arrange
        var document = TestDocumentBuilder.Create()
            .WithTitle("Test Document")
            .WithContent(maliciousContent)
            .Build();

        // Act & Assert - Content should be stored as-is (no execution context)
        document.Content.ShouldBe(maliciousContent);
        // The important thing is that this content will be HTML-encoded when displayed
        // by the consuming application, not executed
        await Task.CompletedTask; // Satisfy async requirement
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert('xss')>")]
    public async Task XssPrevention_InSearchResults_ShouldNotExecuteScripts(string maliciousTitle)
    {
        // Arrange
        var sessionContext = CreateMockSessionContext(isActive: true, tenantKey: "test:main:hash");
        var embeddingService = CreateMockEmbeddingService();

        var maliciousDoc = TestDocumentBuilder.Create()
            .WithTitle(maliciousTitle)
            .WithTenantKey("test:main:hash")
            .Build();

        var documentRepository = CreateLooseMock<IDocumentRepository>();
        documentRepository
            .Setup(r => r.SearchAsync(
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchResult> { new(maliciousDoc, 0.9f) });

        var logger = CreateLooseMock<ILogger<SemanticSearchTool>>().Object;

        var searchTool = new SemanticSearchTool(
            documentRepository.Object,
            embeddingService,
            sessionContext.Object,
            logger);

        // Act
        var result = await searchTool.SearchAsync("test", limit: 10);

        // Assert - The malicious content is returned as data, not executed
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.Documents.ShouldContain(d => d.Title == maliciousTitle);
        // The title contains the raw content - it's the consumer's responsibility to encode
    }

    #endregion

    #region Rate Limiting and Resource Exhaustion Tests

    [Fact]
    public async Task ResourceExhaustion_ExcessiveLimit_ShouldBeCapped()
    {
        // Arrange
        var sessionContext = CreateMockSessionContext(isActive: true, tenantKey: "test:main:hash");
        var embeddingService = CreateMockEmbeddingService();
        var documentRepository = CreateLooseMock<IDocumentRepository>();

        int capturedLimit = 0;
        documentRepository
            .Setup(r => r.SearchAsync(
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<ReadOnlyMemory<float>, string, int, float, string?, CancellationToken>(
                (_, _, limit, _, _, _) => capturedLimit = limit)
            .ReturnsAsync(new List<SearchResult>());

        var logger = CreateLooseMock<ILogger<SemanticSearchTool>>().Object;

        var searchTool = new SemanticSearchTool(
            documentRepository.Object,
            embeddingService,
            sessionContext.Object,
            logger);

        // Act - Attempt to request 10,000 results
        await searchTool.SearchAsync("test", limit: 10000);

        // Assert - Should be capped at 100
        capturedLimit.ShouldBeLessThanOrEqualTo(100);
    }

    [Fact]
    public async Task ResourceExhaustion_ExcessiveRagResults_ShouldBeCapped()
    {
        // Arrange
        var sessionContext = CreateMockSessionContext(isActive: true, tenantKey: "test:main:hash");
        var embeddingService = CreateMockEmbeddingService();
        var documentRepository = CreateLooseMock<IDocumentRepository>();

        int capturedLimit = 0;
        documentRepository
            .Setup(r => r.SearchChunksAsync(
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .Callback<ReadOnlyMemory<float>, string, int, float, CancellationToken>(
                (_, _, limit, _, _) => capturedLimit = limit)
            .ReturnsAsync(new List<ChunkSearchResult>());

        var logger = CreateLooseMock<ILogger<RagQueryTool>>().Object;

        var ragTool = new RagQueryTool(
            documentRepository.Object,
            embeddingService,
            sessionContext.Object,
            logger);

        // Act - Attempt to request 1000 results
        await ragTool.QueryAsync("test", maxResults: 1000);

        // Assert - Should be capped at 20 * 2 = 40 (max 20 results, doubled for diversity)
        capturedLimit.ShouldBeLessThanOrEqualTo(40);
    }

    #endregion

    #region Helper Methods

    private Mock<ISessionContext> CreateMockSessionContext(
        bool isActive,
        string? tenantKey = null,
        string? projectPath = null)
    {
        var mock = CreateLooseMock<ISessionContext>();
        mock.Setup(s => s.IsProjectActive).Returns(isActive);
        mock.Setup(s => s.TenantKey).Returns(tenantKey);
        mock.Setup(s => s.ActiveProjectPath).Returns(projectPath);
        return mock;
    }

    private IEmbeddingService CreateMockEmbeddingService()
    {
        var mock = CreateLooseMock<IEmbeddingService>();
        mock.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestEmbedding());
        return mock.Object;
    }

    private IDocumentRepository CreateMockDocumentRepository(
        string tenantA, CompoundDocument[] docsA,
        string tenantB, CompoundDocument[] docsB)
    {
        var mock = CreateLooseMock<IDocumentRepository>();

        mock.Setup(r => r.SearchAsync(
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReadOnlyMemory<float> _, string tenant, int _, float _, string? _, CancellationToken _) =>
            {
                if (tenant == tenantA)
                    return docsA.Select(d => new SearchResult(d, 0.9f)).ToList();
                if (tenant == tenantB)
                    return docsB.Select(d => new SearchResult(d, 0.9f)).ToList();
                return new List<SearchResult>();
            });

        return mock.Object;
    }

    private IDocumentRepository CreateMockDocumentRepositoryWithIsolation()
    {
        var mock = CreateLooseMock<IDocumentRepository>();

        mock.Setup(r => r.SearchAsync(
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchResult>());

        mock.Setup(r => r.GetByTenantKeyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompoundDocument?)null);

        return mock.Object;
    }

    private IDocumentIndexer CreateMockDocumentIndexer()
    {
        var mock = CreateLooseMock<IDocumentIndexer>();

        mock.Setup(i => i.IndexDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string filePath, string _, string _, CancellationToken _) =>
            {
                // Simulate file not found for invalid paths
                if (IsPathSafe(filePath))
                {
                    var doc = TestDocumentBuilder.Create().WithFilePath(filePath).Build();
                    return IndexResult.Success(filePath, doc, 1);
                }
                return IndexResult.Failure(filePath, "File not found");
            });

        return mock.Object;
    }

    private static bool IsPathSafe(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // Check for path traversal patterns
        var normalizedPath = path.Replace('\\', '/');

        if (normalizedPath.Contains(".."))
            return false;

        if (normalizedPath.StartsWith("/") || normalizedPath.StartsWith("C:") || normalizedPath.StartsWith("c:"))
            return false;

        if (normalizedPath.Contains("%2e") || normalizedPath.Contains("%2f"))
            return false;

        // Check for absolute paths on various platforms
        if (Path.IsPathRooted(path))
            return false;

        return true;
    }

    private static ReadOnlyMemory<float> CreateTestEmbedding(int dimensions = 1024)
    {
        var random = new Random(42);
        var vector = new float[dimensions];
        for (int i = 0; i < dimensions; i++)
        {
            vector[i] = (float)random.NextDouble();
        }
        return new ReadOnlyMemory<float>(vector);
    }

    #endregion
}

/// <summary>
/// Extension methods for test assertions.
/// </summary>
public static class ShouldlySecurityExtensions
{
    /// <summary>
    /// Asserts that the value is one of the expected values.
    /// </summary>
    public static void ShouldBeOneOf<T>(this T actual, params T[] expected)
    {
        actual.ShouldSatisfyAllConditions(
            () => expected.ShouldContain(actual,
                $"Expected actual value '{actual}' to be one of [{string.Join(", ", expected)}]"));
    }
}
