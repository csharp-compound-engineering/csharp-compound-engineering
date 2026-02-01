# Phase 119: Unit Test Patterns for MCP Tools

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Testing Framework
> **Prerequisites**: Phase 110 (Test Project Structure), Phase 111 (Test Dependencies)

---

## Spec References

This phase implements the unit testing patterns defined in:

- **spec/testing.md** - [MCP Testing Patterns (unit)](../spec/testing.md#mcp-testing-patterns)
- **spec/testing.md** - [Test Categories - Unit Tests](../spec/testing.md#unit-tests-compounddocstests)
- **spec/testing.md** - [Test Naming Conventions](../spec/testing.md#test-naming-conventions)
- **spec/testing/test-independence.md** - [Core Principles](../spec/testing/test-independence.md)
- **research/unit-testing-xunit-moq-shouldly.md** - Complete testing library patterns

---

## Objectives

1. Establish unit testing patterns for MCP tool handlers with mocked dependencies
2. Define service dependency mocking patterns using Moq
3. Create parameter validation testing patterns for tool inputs
4. Define error handling testing patterns for service failures
5. Establish Shouldly assertion patterns specific to the project
6. Document and enforce test naming conventions

---

## Acceptance Criteria

### Tool Handler Unit Testing
- [ ] Base test class pattern defined for MCP tool tests
- [ ] Example test class for `RagQueryTool` with full coverage patterns
- [ ] Example test class for `IndexDocumentTool` with dependency mocking
- [ ] Tests create fresh mocks per test method (no class-level shared mocks)
- [ ] Helper methods for common mock setups return new instances

### Service Dependency Mocking
- [ ] `IEmbeddingService` mocking patterns documented with examples
- [ ] `IDocumentRepository` mocking patterns documented with examples
- [ ] `IVectorStorage` mocking patterns documented with examples
- [ ] HTTP client mocking pattern for Ollama API
- [ ] File system abstraction mocking pattern
- [ ] CancellationToken handling in mocked async methods

### Parameter Validation Testing
- [ ] Null parameter validation test pattern
- [ ] Empty/whitespace string validation test pattern
- [ ] Out-of-range numeric parameter test pattern
- [ ] `[Theory]` with `[InlineData]` pattern for boundary conditions
- [ ] Custom validation attribute testing pattern

### Error Handling Testing
- [ ] Service exception propagation test pattern
- [ ] Exception wrapping verification pattern
- [ ] Partial failure handling test pattern
- [ ] Timeout exception test pattern
- [ ] Retry exhaustion test pattern

### Shouldly Assertion Patterns
- [ ] Object equality assertions documented
- [ ] Collection assertions documented
- [ ] Exception assertions (sync and async) documented
- [ ] String assertions documented
- [ ] Custom message patterns documented

### Test Naming Conventions
- [ ] Class naming convention enforced: `{ClassUnderTest}Tests`
- [ ] Method naming convention enforced: `{Method}_{Scenario}_{ExpectedResult}`
- [ ] Alternative method naming: `{Method}_When{Condition}_Should{Behavior}`
- [ ] Trait conventions documented: `[Trait("Category", "Unit")]`

---

## Implementation Notes

### Tool Handler Unit Test Base Pattern

Create a test class structure that ensures test independence:

```csharp
public class RagQueryToolTests
{
    // NO class-level mock fields - each test creates its own

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "RagQuery")]
    public async Task HandleAsync_WithValidQuery_ReturnsResults()
    {
        // Arrange - fresh mocks for this test only
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockRepo = new Mock<IDocumentRepository>();

        mockEmbedding
            .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1024]);
        mockRepo
            .Setup(x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document> { new() { Content = "Test" } });

        var sut = new RagQueryTool(mockEmbedding.Object, mockRepo.Object);

        // Act
        var result = await sut.HandleAsync(new RagQueryRequest { Query = "test" });

        // Assert
        result.ShouldNotBeNull();
        result.Sources.ShouldNotBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Feature", "RagQuery")]
    public async Task HandleAsync_WhenNoResults_ReturnsEmptyResponse()
    {
        // Arrange - completely independent from other tests
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockRepo = new Mock<IDocumentRepository>();

        mockEmbedding
            .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1024]);
        mockRepo
            .Setup(x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>()); // Empty results

        var sut = new RagQueryTool(mockEmbedding.Object, mockRepo.Object);

        // Act
        var result = await sut.HandleAsync(new RagQueryRequest { Query = "obscure query" });

        // Assert
        result.ShouldNotBeNull();
        result.Sources.ShouldBeEmpty();
    }
}
```

### Helper Methods Pattern (Acceptable)

When multiple tests need similar mock configurations, use helper methods that return new instances:

```csharp
public class IndexDocumentToolTests
{
    // Helper method returns NEW mocks each call - this is acceptable
    private static (Mock<IEmbeddingService> embedding, Mock<IDocumentRepository> repo) CreateMocks()
    {
        return (new Mock<IEmbeddingService>(), new Mock<IDocumentRepository>());
    }

    // Factory method for SUT with configurable mocks
    private static IndexDocumentTool CreateSut(
        Action<Mock<IEmbeddingService>>? configureEmbedding = null,
        Action<Mock<IDocumentRepository>>? configureRepo = null)
    {
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockRepo = new Mock<IDocumentRepository>();

        configureEmbedding?.Invoke(mockEmbedding);
        configureRepo?.Invoke(mockRepo);

        return new IndexDocumentTool(mockEmbedding.Object, mockRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_WithValidDocument_ReturnsSuccess()
    {
        var sut = CreateSut(
            configureEmbedding: m => m
                .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new float[1024]),
            configureRepo: m => m
                .Setup(x => x.SaveAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true));

        var result = await sut.HandleAsync(new IndexDocumentRequest { Path = "/test.md", Content = "Test" });

        result.IsSuccess.ShouldBeTrue();
    }
}
```

### Service Dependency Mocking Patterns

#### IEmbeddingService Mocking

```csharp
// Success case
mockEmbedding
    .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(new float[1024]);

// Failure case - service unavailable
mockEmbedding
    .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .ThrowsAsync(new HttpRequestException("Ollama unavailable"));

// Cancellation support
mockEmbedding
    .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .Returns<string, CancellationToken>((text, ct) =>
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new float[1024]);
    });
```

#### IDocumentRepository Mocking

```csharp
// Return specific documents
mockRepo
    .Setup(r => r.SearchAsync(
        It.IsAny<float[]>(),
        It.IsAny<int>(),
        It.IsAny<CancellationToken>()))
    .ReturnsAsync(new List<Document>
    {
        new Document { Id = Guid.NewGuid(), Path = "/doc1.md", Content = "Test content" },
        new Document { Id = Guid.NewGuid(), Path = "/doc2.md", Content = "More content" }
    });

// Verify specific arguments
mockRepo.Verify(r => r.SearchAsync(
    It.Is<float[]>(v => v.Length == 1024),
    5,  // expected topK
    It.IsAny<CancellationToken>()),
    Times.Once());
```

#### HTTP Client Mocking for Ollama API

```csharp
var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
mockHttpMessageHandler
    .Protected()
    .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
    .ReturnsAsync(new HttpResponseMessage
    {
        StatusCode = HttpStatusCode.OK,
        Content = new StringContent(
            JsonSerializer.Serialize(new { embedding = new float[1024] }))
    });

var httpClient = new HttpClient(mockHttpMessageHandler.Object)
{
    BaseAddress = new Uri("http://localhost:11434")
};
```

#### File System Abstraction Mocking

```csharp
var mockFileSystem = new Mock<IFileSystem>();
mockFileSystem
    .Setup(fs => fs.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync("# Test Document\nContent here");

mockFileSystem
    .Setup(fs => fs.Exists(It.IsAny<string>()))
    .Returns(true);
```

### Parameter Validation Testing Patterns

```csharp
public class RagQueryToolValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "Unit")]
    public async Task HandleAsync_WithInvalidQuery_ThrowsArgumentException(string? invalidQuery)
    {
        // Arrange
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockRepo = new Mock<IDocumentRepository>();
        var sut = new RagQueryTool(mockEmbedding.Object, mockRepo.Object);

        // Act & Assert
        Func<Task> action = () => sut.HandleAsync(new RagQueryRequest { Query = invalidQuery! });

        var exception = await action.ShouldThrowAsync<ArgumentException>();
        exception.ParamName.ShouldBe("query");

        // Verify embedding service was never called
        mockEmbedding.Verify(
            x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [Trait("Category", "Unit")]
    public async Task HandleAsync_WithInvalidTopK_ThrowsArgumentOutOfRangeException(int invalidTopK)
    {
        // Arrange
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockRepo = new Mock<IDocumentRepository>();
        var sut = new RagQueryTool(mockEmbedding.Object, mockRepo.Object);

        // Act & Assert
        Func<Task> action = () => sut.HandleAsync(new RagQueryRequest
        {
            Query = "test",
            TopK = invalidTopK
        });

        var exception = await action.ShouldThrowAsync<ArgumentOutOfRangeException>();
        exception.ParamName.ShouldBe("topK");
        exception.ActualValue.ShouldBe(invalidTopK);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    [Trait("Category", "Unit")]
    public async Task HandleAsync_WithValidTopK_AcceptsValue(int validTopK)
    {
        // Arrange
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockRepo = new Mock<IDocumentRepository>();

        mockEmbedding
            .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1024]);
        mockRepo
            .Setup(x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        var sut = new RagQueryTool(mockEmbedding.Object, mockRepo.Object);

        // Act & Assert - should not throw
        Func<Task> action = () => sut.HandleAsync(new RagQueryRequest
        {
            Query = "test",
            TopK = validTopK
        });

        await action.ShouldNotThrowAsync();
    }
}
```

### Error Handling Testing Patterns

```csharp
public class RagQueryToolErrorHandlingTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task HandleAsync_WhenEmbeddingServiceFails_ThrowsToolExecutionException()
    {
        // Arrange
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockRepo = new Mock<IDocumentRepository>();

        mockEmbedding
            .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Ollama unavailable"));

        var sut = new RagQueryTool(mockEmbedding.Object, mockRepo.Object);

        // Act & Assert
        Func<Task> action = () => sut.HandleAsync(new RagQueryRequest { Query = "test" });

        var exception = await action.ShouldThrowAsync<ToolExecutionException>();
        exception.Message.ShouldContain("Failed to generate embedding");
        exception.InnerException.ShouldBeOfType<HttpRequestException>();

        // Verify repository was never called
        mockRepo.Verify(
            x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task HandleAsync_WhenRepositoryFails_ThrowsToolExecutionException()
    {
        // Arrange
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockRepo = new Mock<IDocumentRepository>();

        mockEmbedding
            .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1024]);
        mockRepo
            .Setup(x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NpgsqlException("Database connection failed"));

        var sut = new RagQueryTool(mockEmbedding.Object, mockRepo.Object);

        // Act & Assert
        Func<Task> action = () => sut.HandleAsync(new RagQueryRequest { Query = "test" });

        var exception = await action.ShouldThrowAsync<ToolExecutionException>();
        exception.Message.ShouldContain("Failed to search documents");
        exception.InnerException.ShouldBeOfType<NpgsqlException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task HandleAsync_WhenCancelled_ThrowsOperationCancelledException()
    {
        // Arrange
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockRepo = new Mock<IDocumentRepository>();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockEmbedding
            .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var sut = new RagQueryTool(mockEmbedding.Object, mockRepo.Object);

        // Act & Assert
        Func<Task> action = () => sut.HandleAsync(
            new RagQueryRequest { Query = "test" },
            cts.Token);

        await action.ShouldThrowAsync<OperationCanceledException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task HandleAsync_WhenTimeout_ThrowsTimeoutException()
    {
        // Arrange
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockRepo = new Mock<IDocumentRepository>();

        mockEmbedding
            .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Ollama request timed out"));

        var sut = new RagQueryTool(mockEmbedding.Object, mockRepo.Object);

        // Act & Assert
        Func<Task> action = () => sut.HandleAsync(new RagQueryRequest { Query = "test" });

        var exception = await action.ShouldThrowAsync<ToolExecutionException>();
        exception.InnerException.ShouldBeOfType<TimeoutException>();
    }
}
```

### Shouldly Assertion Patterns

#### Object Equality Assertions

```csharp
// Basic equality
result.ShouldBe(expected);
result.ShouldNotBe(unexpected);

// Null checks
result.ShouldBeNull();
result.ShouldNotBeNull();

// Type checking
result.ShouldBeOfType<Document>();
result.ShouldBeAssignableTo<IDocument>();

// Boolean
result.IsSuccess.ShouldBeTrue();
result.HasErrors.ShouldBeFalse();

// Comparison
count.ShouldBeGreaterThan(0);
count.ShouldBeLessThan(100);
count.ShouldBeInRange(1, 10);
```

#### Collection Assertions

```csharp
// Emptiness
documents.ShouldNotBeEmpty();
emptyList.ShouldBeEmpty();

// Contains
documents.ShouldContain(doc1);
documents.ShouldNotContain(doc4);

// Predicate-based
documents.ShouldContain(d => d.Path == "/readme.md");
documents.ShouldAllBe(d => d.IsIndexed);

// Count
documents.ShouldHaveSingleItem();
documents.Count().ShouldBe(3);

// Ordering
sortedIds.ShouldBeInOrder();
```

#### Exception Assertions

```csharp
// Synchronous
Action action = () => service.Process(null!);
action.ShouldThrow<ArgumentNullException>();

// With message/property checking
var exception = action.ShouldThrow<ArgumentNullException>();
exception.ParamName.ShouldBe("input");

// Async exceptions
Func<Task> asyncAction = () => service.ProcessAsync(null!);
await asyncAction.ShouldThrowAsync<ArgumentNullException>();

// Should NOT throw
Func<Task> safeAction = () => service.ProcessAsync("valid");
await safeAction.ShouldNotThrowAsync();
```

#### String Assertions

```csharp
content.ShouldStartWith("# ");
content.ShouldEndWith("\n");
content.ShouldContain("important");
content.ShouldNotContain("error");
content.ShouldMatch(@"^\d{4}-\d{2}-\d{2}$");
content.ShouldNotBeNullOrWhiteSpace();
```

#### Floating Point Tolerance

```csharp
// For similarity scores
similarity.ShouldBe(0.95, tolerance: 0.01);
```

### Test Naming Convention Enforcement

| Component | Convention | Example |
|-----------|------------|---------|
| Test Class | `{ClassUnderTest}Tests` | `RagQueryToolTests` |
| Test Method (result-focused) | `{Method}_{Scenario}_{ExpectedResult}` | `HandleAsync_WithValidQuery_ReturnsResults` |
| Test Method (behavior-focused) | `{Method}_When{Condition}_Should{Behavior}` | `HandleAsync_WhenEmbeddingFails_ShouldThrowException` |
| Validation Tests | `{Method}_With{InvalidInput}_Throws{Exception}` | `HandleAsync_WithNullQuery_ThrowsArgumentException` |

### Traits for Test Organization

```csharp
[Fact]
[Trait("Category", "Unit")]
[Trait("Feature", "RagQuery")]
public async Task HandleAsync_WithValidQuery_ReturnsResults() { }

[Fact]
[Trait("Category", "Unit")]
[Trait("Feature", "Indexing")]
public async Task IndexAsync_WithValidDocument_ReturnsSuccess() { }
```

Run tests by category:
```bash
dotnet test --filter "Category=Unit"
dotnet test --filter "Feature=RagQuery"
```

---

## Dependencies

### Depends On
- Phase 110: Test Project Structure (provides `CompoundDocs.Tests` project)
- Phase 111: Test Dependencies (provides xUnit, Moq, Shouldly packages)

### Blocks
- Phase 120: Integration Test Patterns (builds on unit test foundation)
- Phase 121: E2E Test Patterns (depends on testing conventions)
- All subsequent MCP tool implementation phases (require test patterns)

---

## Verification Steps

After completing this phase, verify:

1. **Test Pattern Documentation**: Example test files exist in `tests/CompoundDocs.Tests/` demonstrating all patterns
2. **Test Independence**: Running any single test in isolation passes: `dotnet test --filter "FullyQualifiedName~TestName"`
3. **No Shared Mocks**: Code review confirms no class-level mock fields
4. **Naming Conventions**: All test classes and methods follow naming conventions
5. **Traits Applied**: All unit tests have `[Trait("Category", "Unit")]`
6. **Coverage Ready**: Tests can be run with coverage collection: `dotnet test /p:CollectCoverage=true`

---

## Anti-Patterns to Avoid

### Forbidden: Shared Mocks at Class Level

```csharp
// WRONG: Shared mock at class level
public class BadTests
{
    private readonly Mock<IService> _sharedMock = new(); // FORBIDDEN

    [Fact]
    public void Test1()
    {
        _sharedMock.Setup(x => x.DoWork()).Returns(true); // Pollutes state
    }

    [Fact]
    public void Test2()
    {
        // May fail due to Test1's setup
    }
}
```

### Forbidden: Shared SUT with State

```csharp
// WRONG: Shared SUT with accumulated state
public class BadTests
{
    private readonly MyService _sut = new(); // FORBIDDEN if SUT has state

    [Fact]
    public void Test1()
    {
        _sut.Add("item1"); // Modifies shared state
    }

    [Fact]
    public void Test2()
    {
        _sut.Count.ShouldBe(0); // Fails if Test1 ran first
    }
}
```

### Forbidden: Test Method Dependencies

```csharp
// WRONG: Tests that depend on each other
public class BadTests
{
    private static string? _createdId; // FORBIDDEN: static shared state

    [Fact]
    public void Test1_CreateItem()
    {
        _createdId = CreateItem();
    }

    [Fact]
    public void Test2_UseItem()
    {
        UseItem(_createdId!); // Fails if Test1 didn't run first
    }
}
```

---

## Notes

- All patterns align with spec/testing.md and spec/testing/test-independence.md
- Shouldly is chosen over FluentAssertions due to MIT license (free, no commercial license required)
- Moq 4.20+ is used; be aware of the SponsorLink history but it remains a solid choice
- xUnit 2.9.3 (stable) is preferred over v3 (preview) for production use
- Helper methods that return new mock instances are acceptable and encouraged for reducing boilerplate
