# Phase 145: Unit Test Suite

> **Status**: NOT_STARTED
> **Effort Estimate**: 12-16 hours
> **Category**: Testing Framework
> **Prerequisites**: Phase 119 (Unit Test Patterns for MCP Tools)

---

## Spec References

This phase implements the comprehensive unit test suite defined in:

- **spec/testing.md** - [Test Categories - Unit Tests](../spec/testing.md#unit-tests-compounddocstests) (lines 114-130)
- **spec/testing.md** - [MCP Testing Patterns](../spec/testing.md#mcp-testing-patterns) (lines 292-349)
- **spec/testing.md** - [Test Independence](../spec/testing.md#test-independence-critical-requirement) (lines 175-205)
- **spec/testing/test-independence.md** - Complete test isolation patterns
- **research/unit-testing-xunit-moq-shouldly.md** - xUnit, Moq, and Shouldly patterns

---

## Objectives

1. Implement comprehensive unit tests for all MCP tool handlers
2. Implement unit tests for all service layer components
3. Implement unit tests for all repository implementations
4. Implement unit tests for utility classes and helpers
5. Establish reusable mock setup patterns and test data factories
6. Achieve 100% code coverage for all unit-testable code paths
7. Ensure complete test independence with no shared state

---

## Acceptance Criteria

### Tool Handler Unit Tests

- [ ] `RagQueryToolTests` - Full coverage of query handling:
  - [ ] Valid query returns results
  - [ ] Empty results handled correctly
  - [ ] Null/empty query throws `ArgumentException`
  - [ ] Invalid topK values throw `ArgumentOutOfRangeException`
  - [ ] Embedding service failure wrapped in `ToolExecutionException`
  - [ ] Repository failure wrapped in `ToolExecutionException`
  - [ ] Cancellation properly propagated
  - [ ] Timeout handling verified

- [ ] `IndexDocumentToolTests` - Full coverage of document indexing:
  - [ ] Valid document indexed successfully
  - [ ] Invalid path throws appropriate exception
  - [ ] File not found handled gracefully
  - [ ] Embedding generation failure handled
  - [ ] Repository save failure handled
  - [ ] Duplicate document detection verified
  - [ ] Content hash calculation tested

- [ ] `SemanticSearchToolTests` - Full coverage of semantic search:
  - [ ] Valid search returns ranked results
  - [ ] Empty query handling
  - [ ] Filter parameters applied correctly
  - [ ] Collection/namespace isolation verified
  - [ ] Similarity threshold filtering tested

- [ ] `DeleteDocumentsToolTests` - Full coverage of deletion:
  - [ ] Single document deletion
  - [ ] Batch deletion by pattern
  - [ ] Collection deletion
  - [ ] Non-existent document handling
  - [ ] Cascade deletion of chunks verified

- [ ] `ListDocTypesToolTests` - Full coverage of listing:
  - [ ] Returns all document types
  - [ ] Empty collection handled
  - [ ] Type metadata included

- [ ] `ActivateProjectToolTests` - Full coverage of project activation:
  - [ ] Valid project activates successfully
  - [ ] Invalid project path handling
  - [ ] Configuration loading verified
  - [ ] Tenant context established

- [ ] `UpdatePromotionToolTests` - Full coverage of promotion updates:
  - [ ] Promotion boost updated successfully
  - [ ] Invalid document ID handling
  - [ ] Out-of-range boost values rejected

### Service Layer Unit Tests

- [ ] `EmbeddingServiceTests` - Full coverage:
  - [ ] `GenerateAsync` with valid content returns embedding
  - [ ] Empty content handling
  - [ ] Ollama unavailable throws `HttpRequestException`
  - [ ] Retry policy behavior verified
  - [ ] Dimension validation (1024 for nomic-embed-text)
  - [ ] Batch embedding generation tested
  - [ ] Rate limiting behavior verified
  - [ ] Cancellation token respected

- [ ] `FileWatcherServiceTests` - Full coverage:
  - [ ] File creation events detected
  - [ ] File modification events detected
  - [ ] File deletion events detected
  - [ ] Debouncing logic verified
  - [ ] Filter patterns applied
  - [ ] Excluded paths respected
  - [ ] Error recovery behavior tested

- [ ] `DocumentProcessorTests` - Full coverage:
  - [ ] Markdown parsing extracts content
  - [ ] Frontmatter extraction and validation
  - [ ] Chunking strategy applied correctly
  - [ ] Link extraction from content
  - [ ] Content hash calculation
  - [ ] Unicode content handled

- [ ] `LinkResolverTests` - Full coverage:
  - [ ] Relative link resolution
  - [ ] Absolute link handling
  - [ ] Circular reference detection
  - [ ] Broken link detection
  - [ ] External link identification

- [ ] `ChunkingServiceTests` - Full coverage:
  - [ ] Content split by size limits
  - [ ] Semantic boundaries respected
  - [ ] Overlap configuration applied
  - [ ] Metadata preserved per chunk
  - [ ] Empty content handling

- [ ] `IndexingServiceTests` - Full coverage:
  - [ ] Full document indexing workflow
  - [ ] Incremental update detection
  - [ ] Orphan chunk cleanup
  - [ ] Deferred processing queue

- [ ] `ConfigurationServiceTests` - Full coverage:
  - [ ] Global config loading
  - [ ] Project config loading
  - [ ] Environment variable overrides
  - [ ] Config validation errors

### Repository Unit Tests

- [ ] `DocumentRepositoryTests` - Full coverage:
  - [ ] `SaveAsync` stores document
  - [ ] `GetByIdAsync` retrieves document
  - [ ] `GetByPathAsync` retrieves by path
  - [ ] `DeleteAsync` removes document
  - [ ] `SearchAsync` performs vector search
  - [ ] `ExistsAsync` checks existence
  - [ ] `GetAllAsync` with pagination
  - [ ] Transaction handling verified

- [ ] `ChunkRepositoryTests` - Full coverage:
  - [ ] `SaveChunksAsync` stores multiple chunks
  - [ ] `GetByDocumentIdAsync` retrieves related chunks
  - [ ] `DeleteByDocumentIdAsync` cascade delete
  - [ ] Vector storage operations mocked

- [ ] `ExternalDocumentRepositoryTests` - Full coverage:
  - [ ] External source CRUD operations
  - [ ] Source type filtering
  - [ ] Sync status tracking

### Utility Class Unit Tests

- [ ] `MarkdownParserTests` - Full coverage:
  - [ ] Heading extraction
  - [ ] Code block handling
  - [ ] Link parsing
  - [ ] Image reference extraction
  - [ ] Table parsing
  - [ ] Frontmatter separation

- [ ] `ContentHasherTests` - Full coverage:
  - [ ] SHA-256 hash generation
  - [ ] Consistent hashing for same content
  - [ ] Different hashes for different content
  - [ ] Unicode content handling

- [ ] `PathUtilitiesTests` - Full coverage:
  - [ ] Path normalization
  - [ ] Relative path calculation
  - [ ] Extension extraction
  - [ ] Git root detection

- [ ] `VectorMathTests` - Full coverage:
  - [ ] Cosine similarity calculation
  - [ ] Euclidean distance calculation
  - [ ] Vector normalization
  - [ ] Dimension mismatch handling

- [ ] `SchemaValidatorTests` - Full coverage:
  - [ ] Valid YAML passes
  - [ ] Invalid YAML fails with errors
  - [ ] Required field validation
  - [ ] Type coercion handling

### Mock Setup Patterns

- [ ] `MockFactories.cs` created with:
  - [ ] `CreateEmbeddingServiceMock()` - returns configured mock
  - [ ] `CreateDocumentRepositoryMock()` - returns configured mock
  - [ ] `CreateFileSystemMock()` - returns configured mock
  - [ ] `CreateConfigurationMock()` - returns configured mock
  - [ ] `CreateHttpClientMock()` - returns configured mock for Ollama

- [ ] `TestDataBuilders.cs` created with:
  - [ ] `DocumentBuilder` - fluent builder for test documents
  - [ ] `ChunkBuilder` - fluent builder for test chunks
  - [ ] `EmbeddingBuilder` - creates test embeddings
  - [ ] `RequestBuilder` - builds tool request objects

### Test Independence Enforcement

- [ ] No class-level mock fields in any test class
- [ ] Each test creates fresh mock instances
- [ ] Helper methods return new instances (not shared)
- [ ] All tests pass when run in isolation
- [ ] All tests pass regardless of execution order
- [ ] Static analyzers configured to detect shared state

---

## Implementation Notes

### Test File Organization

```
tests/CompoundDocs.Tests/
├── Tools/
│   ├── RagQueryToolTests.cs
│   ├── IndexDocumentToolTests.cs
│   ├── SemanticSearchToolTests.cs
│   ├── DeleteDocumentsToolTests.cs
│   ├── ListDocTypesToolTests.cs
│   ├── ActivateProjectToolTests.cs
│   └── UpdatePromotionToolTests.cs
├── Services/
│   ├── EmbeddingServiceTests.cs
│   ├── FileWatcherServiceTests.cs
│   ├── DocumentProcessorTests.cs
│   ├── LinkResolverTests.cs
│   ├── ChunkingServiceTests.cs
│   ├── IndexingServiceTests.cs
│   └── ConfigurationServiceTests.cs
├── Repositories/
│   ├── DocumentRepositoryTests.cs
│   ├── ChunkRepositoryTests.cs
│   └── ExternalDocumentRepositoryTests.cs
├── Parsing/
│   └── MarkdownParserTests.cs
├── Utilities/
│   ├── ContentHasherTests.cs
│   ├── PathUtilitiesTests.cs
│   ├── VectorMathTests.cs
│   └── SchemaValidatorTests.cs
├── TestData/
│   ├── MockFactories.cs
│   ├── TestDataBuilders.cs
│   ├── SampleDocuments.cs
│   └── ExpectedResults.cs
└── GlobalUsings.cs
```

### Standard Test Class Structure

Each test class follows the pattern established in Phase 119:

```csharp
namespace CompoundDocs.Tests.Services;

public class EmbeddingServiceTests
{
    // NO class-level fields for mocks or SUT

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Embedding")]
    public async Task GenerateAsync_WithValidContent_ReturnsEmbedding()
    {
        // Arrange - fresh mocks for this test only
        var mockHttpClient = MockFactories.CreateHttpClientMock(response: new EmbeddingResponse
        {
            Embedding = new float[1024]
        });
        var mockLogger = new Mock<ILogger<EmbeddingService>>();

        var sut = new EmbeddingService(mockHttpClient, mockLogger.Object);

        // Act
        var result = await sut.GenerateAsync("Test content", CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Length.ShouldBe(1024);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Embedding")]
    public async Task GenerateAsync_WhenOllamaUnavailable_ThrowsServiceException()
    {
        // Arrange - completely independent from other tests
        var mockHttpClient = MockFactories.CreateHttpClientMock(
            throwException: new HttpRequestException("Connection refused"));
        var mockLogger = new Mock<ILogger<EmbeddingService>>();

        var sut = new EmbeddingService(mockHttpClient, mockLogger.Object);

        // Act & Assert
        Func<Task> action = () => sut.GenerateAsync("Test content", CancellationToken.None);

        var exception = await action.ShouldThrowAsync<ServiceException>();
        exception.Message.ShouldContain("Ollama");
        exception.InnerException.ShouldBeOfType<HttpRequestException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "Unit")]
    [Trait("Feature", "Embedding")]
    public async Task GenerateAsync_WithInvalidContent_ThrowsArgumentException(string? invalidContent)
    {
        // Arrange
        var mockHttpClient = MockFactories.CreateHttpClientMock();
        var mockLogger = new Mock<ILogger<EmbeddingService>>();

        var sut = new EmbeddingService(mockHttpClient, mockLogger.Object);

        // Act & Assert
        Func<Task> action = () => sut.GenerateAsync(invalidContent!, CancellationToken.None);

        var exception = await action.ShouldThrowAsync<ArgumentException>();
        exception.ParamName.ShouldBe("content");
    }
}
```

### Mock Factory Pattern

```csharp
namespace CompoundDocs.Tests.TestData;

public static class MockFactories
{
    /// <summary>
    /// Creates a new HTTP client mock configured for Ollama API.
    /// Each call returns a NEW mock instance - never shared.
    /// </summary>
    public static HttpClient CreateHttpClientMock(
        EmbeddingResponse? response = null,
        Exception? throwException = null)
    {
        var mockHandler = new Mock<HttpMessageHandler>();

        if (throwException != null)
        {
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(throwException);
        }
        else
        {
            var responseContent = response ?? new EmbeddingResponse { Embedding = new float[1024] };
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(responseContent))
                });
        }

        return new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
    }

    /// <summary>
    /// Creates a new document repository mock.
    /// Each call returns a NEW mock instance - never shared.
    /// </summary>
    public static Mock<IDocumentRepository> CreateDocumentRepositoryMock(
        List<Document>? searchResults = null,
        bool saveSucceeds = true)
    {
        var mock = new Mock<IDocumentRepository>();

        mock.Setup(r => r.SearchAsync(
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults ?? new List<Document>());

        mock.Setup(r => r.SaveAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(saveSucceeds);

        return mock;
    }

    /// <summary>
    /// Creates a new embedding service mock.
    /// Each call returns a NEW mock instance - never shared.
    /// </summary>
    public static Mock<IEmbeddingService> CreateEmbeddingServiceMock(
        float[]? embedding = null,
        Exception? throwException = null)
    {
        var mock = new Mock<IEmbeddingService>();

        if (throwException != null)
        {
            mock.Setup(e => e.GenerateAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(throwException);
        }
        else
        {
            mock.Setup(e => e.GenerateAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(embedding ?? new float[1024]);
        }

        return mock;
    }

    /// <summary>
    /// Creates a new file system mock.
    /// Each call returns a NEW mock instance - never shared.
    /// </summary>
    public static Mock<IFileSystem> CreateFileSystemMock(
        Dictionary<string, string>? files = null,
        bool fileExists = true)
    {
        var mock = new Mock<IFileSystem>();
        var fileContents = files ?? new Dictionary<string, string>();

        mock.Setup(fs => fs.Exists(It.IsAny<string>()))
            .Returns<string>(path => fileExists && fileContents.ContainsKey(path));

        mock.Setup(fs => fs.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((path, ct) =>
            {
                if (fileContents.TryGetValue(path, out var content))
                    return Task.FromResult(content);
                throw new FileNotFoundException($"File not found: {path}");
            });

        return mock;
    }
}
```

### Test Data Builder Pattern

```csharp
namespace CompoundDocs.Tests.TestData;

public class DocumentBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _path = "/test/document.md";
    private string _content = "# Test Document\n\nTest content.";
    private string _contentHash = "abc123";
    private DateTime _indexedAt = DateTime.UtcNow;
    private float _promotionBoost = 1.0f;
    private string _collection = "default";

    public DocumentBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public DocumentBuilder WithPath(string path)
    {
        _path = path;
        return this;
    }

    public DocumentBuilder WithContent(string content)
    {
        _content = content;
        _contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        return this;
    }

    public DocumentBuilder WithCollection(string collection)
    {
        _collection = collection;
        return this;
    }

    public DocumentBuilder WithPromotionBoost(float boost)
    {
        _promotionBoost = boost;
        return this;
    }

    public Document Build()
    {
        return new Document
        {
            Id = _id,
            Path = _path,
            Content = _content,
            ContentHash = _contentHash,
            IndexedAt = _indexedAt,
            PromotionBoost = _promotionBoost,
            Collection = _collection
        };
    }
}

public class ChunkBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _documentId = Guid.NewGuid();
    private string _content = "Chunk content";
    private int _chunkIndex = 0;
    private float[] _embedding = new float[1024];

    public ChunkBuilder ForDocument(Guid documentId)
    {
        _documentId = documentId;
        return this;
    }

    public ChunkBuilder WithContent(string content)
    {
        _content = content;
        return this;
    }

    public ChunkBuilder WithIndex(int index)
    {
        _chunkIndex = index;
        return this;
    }

    public ChunkBuilder WithEmbedding(float[] embedding)
    {
        _embedding = embedding;
        return this;
    }

    public DocumentChunk Build()
    {
        return new DocumentChunk
        {
            Id = _id,
            DocumentId = _documentId,
            Content = _content,
            ChunkIndex = _chunkIndex,
            Embedding = _embedding
        };
    }
}
```

### Shouldly Assertion Patterns

All tests use Shouldly for assertions per spec:

```csharp
// Object assertions
result.ShouldNotBeNull();
result.Id.ShouldBe(expectedId);
result.ShouldBeOfType<Document>();

// Collection assertions
documents.ShouldNotBeEmpty();
documents.Count.ShouldBe(3);
documents.ShouldContain(d => d.Path == "/test.md");
documents.ShouldAllBe(d => d.IsIndexed);

// Exception assertions (async)
Func<Task> action = () => service.ProcessAsync(null!);
await action.ShouldThrowAsync<ArgumentNullException>();

// String assertions
content.ShouldContain("expected");
content.ShouldStartWith("# ");
content.ShouldNotBeNullOrWhiteSpace();

// Numeric tolerance for similarity scores
similarity.ShouldBe(0.95, tolerance: 0.01);
```

### Running Unit Tests

```bash
# Run all unit tests
dotnet test tests/CompoundDocs.Tests/

# Run specific feature tests
dotnet test --filter "Feature=Embedding"

# Run with coverage
dotnet test tests/CompoundDocs.Tests/ /p:CollectCoverage=true

# Run single test in isolation (verify independence)
dotnet test --filter "FullyQualifiedName~EmbeddingServiceTests.GenerateAsync_WithValidContent_ReturnsEmbedding"
```

---

## Dependencies

### Depends On
- **Phase 119**: Unit Test Patterns for MCP Tools (establishes testing patterns and conventions)
- **Phase 110**: xUnit Test Framework Configuration (framework setup)
- **Phase 109**: Test Project Structure (project files and directory structure)

### Blocks
- **Phase 146**: Integration Test Suite (requires unit tests as foundation)
- **Phase 147**: E2E Test Suite (requires unit and integration tests)
- **Phase 148**: Coverage Reporting (requires tests to measure)
- All CI/CD pipeline phases that run tests

---

## Verification Steps

After completing this phase, verify:

1. **All unit tests pass**:
   ```bash
   dotnet test tests/CompoundDocs.Tests/ --verbosity normal
   ```

2. **100% code coverage achieved**:
   ```bash
   dotnet test tests/CompoundDocs.Tests/ /p:CollectCoverage=true /p:Threshold=100
   ```

3. **Tests are independent** (run random subset):
   ```bash
   dotnet test --filter "Category=Unit" -- xunit.methodDisplay=method
   ```

4. **No shared state detected** (code review):
   - No `Mock<T>` fields at class level
   - No `static` fields holding test state
   - All helper methods return new instances

5. **Test naming conventions followed**:
   - Classes: `{ClassUnderTest}Tests`
   - Methods: `{Method}_{Scenario}_{ExpectedResult}`

6. **Traits applied consistently**:
   ```bash
   dotnet test --filter "Category=Unit" --list-tests
   ```

7. **Individual test isolation verified**:
   ```bash
   # Pick any test and run it alone
   dotnet test --filter "FullyQualifiedName~DocumentRepositoryTests.SaveAsync_WithValidDocument_ReturnsTrue"
   ```

---

## Coverage Targets

| Component | Target | Notes |
|-----------|--------|-------|
| Tool Handlers | 100% | All public methods and error paths |
| Service Layer | 100% | All business logic branches |
| Repositories | 100% | All CRUD operations |
| Utilities | 100% | All helper methods |
| Overall | 100% | Enforced via Coverlet threshold |

### Exclusions (via `[ExcludeFromCodeCoverage]`)

Only the following may be excluded:
- Infrastructure startup code (`Program.cs`)
- Generated code (source generators, migrations)
- DTOs with only auto-properties
- Code documented as untestable with justification

---

## Notes

- All tests must be completely independent per spec/testing/test-independence.md
- Mock factories always return NEW instances - never share mocks
- Use Shouldly for all assertions (MIT license, no commercial license required)
- Test data builders create unique data per test (GUIDs for IDs)
- Moq 4.20.72+ is used (post-SponsorLink versions)
- xUnit 2.9.3 (stable) is used, not v3 preview
- E2E timeout handling is NOT applicable to unit tests (unit tests must be fast, <100ms each)
- Coverage failures break the build per spec
