# Unit Testing in C#/.NET: xUnit, Moq, and Shouldly Research Report

> **Status**: Research Complete
> **Date**: 2026-01-23
> **Purpose**: Inform testing strategy for csharp-compounding-docs MCP server

---

## Executive Summary

This report covers the three primary libraries for unit testing in the `csharp-compounding-docs` MCP server project:
- **xUnit** (v2.9.3 stable / v3.x preview) - Test framework
- **Moq** (v4.20+) - Mocking library
- **Shouldly** (v4.2+) - Assertion library

All three are fully compatible with .NET 8+ and work well together for testing MCP servers, database integrations, and external service calls.

---

## 1. xUnit

### Latest Versions and .NET Compatibility

| Version | Status | .NET Support |
|---------|--------|--------------|
| xUnit 2.9.3 | Stable | .NET 8+, .NET Framework 4.7.2+ |
| xUnit 3.x (v3) | Preview/New | .NET 8+, modernized architecture |

### Project Setup (.NET 8)

**Recommended .csproj for xUnit 2.x (stable):**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.1" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

**For xUnit v3 (latest features):**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageReference Include="xunit.v3" Version="2.0.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.1" />
  </ItemGroup>

</Project>
```

### Test Organization Patterns

#### Facts (Simple Tests)

```csharp
public class DocumentIndexerTests
{
    [Fact]
    public void IndexDocument_WithValidContent_ReturnsSuccess()
    {
        // Arrange
        var indexer = new DocumentIndexer();

        // Act
        var result = indexer.Index("test content");

        // Assert
        Assert.True(result.IsSuccess);
    }
}
```

#### Async Test Support

```csharp
[Fact]
public async Task IndexDocumentAsync_WithValidContent_ReturnsSuccess()
{
    // Arrange
    var indexer = new DocumentIndexer();

    // Act
    var result = await indexer.IndexAsync("test content");

    // Assert
    Assert.True(result.IsSuccess);
}
```

### Parameterized Tests (Theory)

#### InlineData - For Simple Static Values

```csharp
[Theory]
[InlineData("hello.md", true)]
[InlineData("readme.txt", true)]
[InlineData("image.png", false)]
[InlineData("", false)]
public void IsIndexableFile_ReturnsExpectedResult(string filename, bool expected)
{
    var result = FileValidator.IsIndexable(filename);
    Assert.Equal(expected, result);
}
```

#### MemberData - For Calculated/Complex Data

```csharp
public class EmbeddingServiceTests
{
    [Theory]
    [MemberData(nameof(GetEmbeddingTestCases))]
    public async Task GenerateEmbedding_ReturnsCorrectDimensions(
        string input,
        int expectedDimensions)
    {
        var service = new EmbeddingService();
        var result = await service.GenerateAsync(input);
        Assert.Equal(expectedDimensions, result.Length);
    }

    public static IEnumerable<object[]> GetEmbeddingTestCases()
    {
        yield return new object[] { "short text", 1024 };
        yield return new object[] { "longer text with more content", 1024 };
        yield return new object[] { string.Empty, 0 };
    }
}
```

#### TheoryData<T> - Strongly Typed (Recommended)

```csharp
public class VectorSearchTests
{
    [Theory]
    [ClassData(typeof(SearchTestData))]
    public async Task Search_ReturnsRelevantResults(
        string query,
        int topK,
        int minExpectedResults)
    {
        // Test implementation
    }
}

public class SearchTestData : TheoryData<string, int, int>
{
    public SearchTestData()
    {
        Add("PostgreSQL connection", 5, 1);
        Add("MCP protocol", 10, 2);
        Add("nonexistent gibberish xyz", 5, 0);
    }
}
```

#### ClassData - For Reusable Test Data Classes

```csharp
public class FileWatcherTestData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        yield return new object[] { "*.md", FileChangeType.Created, true };
        yield return new object[] { "*.cs", FileChangeType.Modified, true };
        yield return new object[] { "*.tmp", FileChangeType.Created, false };
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

[Theory]
[ClassData(typeof(FileWatcherTestData))]
public void ShouldProcessFile_ReturnsExpected(
    string pattern,
    FileChangeType changeType,
    bool expected)
{
    // Test implementation
}
```

### Fixtures and Shared Context

#### Class Fixture (Shared per Test Class)

```csharp
public class PostgresFixture : IAsyncLifetime
{
    public NpgsqlConnection Connection { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Connection = new NpgsqlConnection("Host=localhost;Database=test_db");
        await Connection.OpenAsync();
        // Setup test schema, enable pgvector, etc.
    }

    public async Task DisposeAsync()
    {
        await Connection.CloseAsync();
    }
}

public class VectorStorageTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public VectorStorageTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task StoreVector_PersistsCorrectly()
    {
        // Use _fixture.Connection
    }
}
```

#### Collection Fixture (Shared Across Multiple Test Classes)

```csharp
[CollectionDefinition("Database collection")]
public class DatabaseCollection : ICollectionFixture<PostgresFixture>
{
    // This class has no code - it's just a marker
}

[Collection("Database collection")]
public class DocumentRepositoryTests
{
    private readonly PostgresFixture _fixture;

    public DocumentRepositoryTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }
}

[Collection("Database collection")]
public class EmbeddingRepositoryTests
{
    // Same fixture instance shared
}
```

#### Assembly Fixture (xUnit v3 - Shared Across Entire Assembly)

```csharp
[assembly: AssemblyFixture(typeof(OllamaFixture))]

public class OllamaFixture : IAsyncLifetime
{
    public HttpClient OllamaClient { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        OllamaClient = new HttpClient { BaseAddress = new Uri("http://localhost:11434") };
        // Verify Ollama is running
    }

    public async Task DisposeAsync()
    {
        OllamaClient.Dispose();
    }
}
```

### Traits for Test Organization

```csharp
[Fact]
[Trait("Category", "Integration")]
[Trait("Feature", "VectorSearch")]
public async Task VectorSearch_Integration_ReturnsResults()
{
    // Integration test
}

[Fact]
[Trait("Category", "Unit")]
[Trait("Feature", "FileWatcher")]
public void FileWatcher_DetectsChanges()
{
    // Unit test
}
```

Run specific traits:
```bash
dotnet test --filter "Category=Unit"
dotnet test --filter "Feature=VectorSearch"
```

---

## 2. Moq

### Latest Version and Compatibility

| Package | Version | Notes |
|---------|---------|-------|
| Moq | 4.20.72+ | Stable, widely used |

**Note on Moq Controversy:** In 2023, Moq added a controversial SponsorLink feature that raised privacy concerns. While this was later addressed, some teams have migrated to [NSubstitute](https://nsubstitute.github.io/) as an alternative. For this project, Moq remains a solid choice.

### Basic Setup and Patterns

```csharp
using Moq;

public class DocumentIndexerTests
{
    [Fact]
    public async Task IndexDocument_CallsEmbeddingService()
    {
        // Arrange
        var mockEmbeddingService = new Mock<IEmbeddingService>();
        mockEmbeddingService
            .Setup(x => x.GenerateAsync(It.IsAny<string>()))
            .ReturnsAsync(new float[1024]);

        var indexer = new DocumentIndexer(mockEmbeddingService.Object);

        // Act
        await indexer.IndexAsync("test content");

        // Assert
        mockEmbeddingService.Verify(
            x => x.GenerateAsync("test content"),
            Times.Once());
    }
}
```

### Argument Matchers

```csharp
// Any value of type
mock.Setup(x => x.Process(It.IsAny<string>())).Returns(true);

// Specific condition
mock.Setup(x => x.Process(It.Is<string>(s => s.Length > 5))).Returns(true);

// Range
mock.Setup(x => x.GetByIndex(It.IsInRange(0, 10, Moq.Range.Inclusive))).Returns(item);

// Regex match
mock.Setup(x => x.Process(It.IsRegex(@"^\d+$"))).Returns(true);
```

### Mocking Patterns for This Project

#### Database Repository Mocking

```csharp
public class McpServerTests
{
    [Fact]
    public async Task HandleQuery_ReturnsDocumentsFromRepository()
    {
        // Arrange
        var mockRepo = new Mock<IDocumentRepository>();
        mockRepo
            .Setup(r => r.SearchAsync(
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>
            {
                new Document { Id = 1, Content = "Test doc" }
            });

        var server = new McpServer(mockRepo.Object);

        // Act
        var results = await server.QueryAsync("test query");

        // Assert
        Assert.Single(results);
        mockRepo.Verify(r => r.SearchAsync(
            It.IsAny<float[]>(),
            5,  // default topK
            It.IsAny<CancellationToken>()),
            Times.Once());
    }
}
```

#### HTTP Client Mocking (for Ollama API)

```csharp
public class OllamaServiceTests
{
    [Fact]
    public async Task GenerateEmbedding_CallsOllamaApi()
    {
        // Arrange
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

        var service = new OllamaEmbeddingService(httpClient);

        // Act
        var result = await service.GenerateAsync("test");

        // Assert
        Assert.Equal(1024, result.Length);
    }
}
```

#### File System Mocking

```csharp
public class FileWatcherServiceTests
{
    [Fact]
    public void ProcessFile_ReadsFileContent()
    {
        // Arrange
        var mockFileSystem = new Mock<IFileSystem>();
        mockFileSystem
            .Setup(fs => fs.ReadAllTextAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("# Test Document\nContent here");

        mockFileSystem
            .Setup(fs => fs.Exists(It.IsAny<string>()))
            .Returns(true);

        var service = new FileWatcherService(mockFileSystem.Object);

        // Act
        var content = service.ProcessFile("/path/to/file.md");

        // Assert
        Assert.Contains("Test Document", content);
    }
}
```

### Callback and Return Value Configuration

```csharp
// Track call arguments
var capturedArgs = new List<string>();
mock.Setup(x => x.Process(It.IsAny<string>()))
    .Callback<string>(arg => capturedArgs.Add(arg))
    .Returns(true);

// Return different values on sequential calls
mock.SetupSequence(x => x.GetNext())
    .Returns("first")
    .Returns("second")
    .Throws<InvalidOperationException>();

// Async returns
mock.Setup(x => x.GetDataAsync())
    .ReturnsAsync(new Data());

// Moq 4.16+ simplified async
mock.Setup(x => x.GetDataAsync().Result)
    .Returns(new Data());
```

### Strict vs Loose Mocking

```csharp
// Loose (default) - returns default values for unconfigured members
var looseMock = new Mock<IService>();

// Strict - throws for any unconfigured call
var strictMock = new Mock<IService>(MockBehavior.Strict);
strictMock.Setup(x => x.DoWork()).Returns(true);  // Must setup everything used
```

### Verification Patterns

```csharp
// Verify specific call count
mock.Verify(x => x.Save(It.IsAny<Document>()), Times.Once());
mock.Verify(x => x.Save(It.IsAny<Document>()), Times.Exactly(3));
mock.Verify(x => x.Save(It.IsAny<Document>()), Times.Never());
mock.Verify(x => x.Save(It.IsAny<Document>()), Times.AtLeastOnce());

// Verify with specific arguments
mock.Verify(x => x.Save(It.Is<Document>(d => d.Id == 1)), Times.Once());

// Verify all setups were called
mock.VerifyAll();

// Verify no other calls were made
mock.VerifyNoOtherCalls();
```

### Mock Sequence (Call Order Verification)

```csharp
[Fact]
public async Task IndexDocument_CallsServicesInOrder()
{
    var mockValidator = new Mock<IValidator>(MockBehavior.Strict);
    var mockEmbedding = new Mock<IEmbeddingService>(MockBehavior.Strict);
    var mockStorage = new Mock<IVectorStorage>(MockBehavior.Strict);

    var sequence = new MockSequence();

    mockValidator.InSequence(sequence)
        .Setup(x => x.ValidateAsync(It.IsAny<Document>()))
        .ReturnsAsync(true);

    mockEmbedding.InSequence(sequence)
        .Setup(x => x.GenerateAsync(It.IsAny<string>()))
        .ReturnsAsync(new float[1024]);

    mockStorage.InSequence(sequence)
        .Setup(x => x.StoreAsync(It.IsAny<float[]>()))
        .Returns(Task.CompletedTask);

    // Test will fail if services aren't called in this order
}
```

---

## 3. Shouldly

### Latest Version

| Package | Version | License |
|---------|---------|---------|
| Shouldly | 4.2.1+ | Free/MIT |

**Important Note:** Unlike FluentAssertions (which now requires a paid license for commercial use at $129.95), Shouldly remains completely free and open source.

### Installation

```xml
<PackageReference Include="Shouldly" Version="4.2.1" />
```

### Basic Assertion Syntax

```csharp
using Shouldly;

// Equality
result.ShouldBe(expected);
result.ShouldNotBe(unexpected);

// Null checks
result.ShouldBeNull();
result.ShouldNotBeNull();

// Boolean
result.ShouldBeTrue();
result.ShouldBeFalse();

// Comparison
count.ShouldBeGreaterThan(0);
count.ShouldBeLessThan(100);
count.ShouldBeInRange(1, 10);

// Type checking
result.ShouldBeOfType<Document>();
result.ShouldBeAssignableTo<IDocument>();
```

### Collection Assertions

```csharp
var documents = new List<Document> { doc1, doc2, doc3 };

// Emptiness
documents.ShouldNotBeEmpty();
emptyList.ShouldBeEmpty();

// Contains
documents.ShouldContain(doc1);
documents.ShouldNotContain(doc4);

// Predicate-based
documents.ShouldContain(d => d.Title == "README");
documents.ShouldAllBe(d => d.IsIndexed);

// Count
documents.ShouldHaveSingleItem();
documents.Count().ShouldBe(3);

// Ordering
sortedIds.ShouldBeInOrder();
sortedDates.ShouldBeInOrder(SortDirection.Descending);
```

### String Assertions

```csharp
content.ShouldStartWith("# ");
content.ShouldEndWith("\n");
content.ShouldContain("important");
content.ShouldNotContain("error");
content.ShouldMatch(@"^\d{4}-\d{2}-\d{2}$");
content.ShouldBeNullOrEmpty();
content.ShouldNotBeNullOrWhiteSpace();
```

### Exception Assertions

```csharp
// Synchronous
var action = () => service.Process(null!);
action.ShouldThrow<ArgumentNullException>();

// With message checking
var exception = action.ShouldThrow<ArgumentNullException>();
exception.ParamName.ShouldBe("input");

// Async exceptions
Func<Task> asyncAction = () => service.ProcessAsync(null!);
await asyncAction.ShouldThrowAsync<ArgumentNullException>();

// Alternative static syntax
await Should.ThrowAsync<InvalidOperationException>(
    () => service.ProcessAsync("invalid"));

// Should NOT throw
var safeAction = () => service.Process("valid");
safeAction.ShouldNotThrow();
```

### Custom Messages

```csharp
result.ShouldBe(expected, "Custom failure message");
result.ShouldBe(expected, () => $"Expected {expected} but got {result}");

// Contextual messages automatically include variable names
contestant.Points.ShouldBe(1337);
// Output: contestant.Points should be 1337 but was 0
```

### Tolerance for Floating Point and TimeSpan

```csharp
// Floating point tolerance
actualScore.ShouldBe(0.95, tolerance: 0.01);

// TimeSpan tolerance
actualDuration.ShouldBe(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(100));
```

### Shouldly vs FluentAssertions Quick Comparison

| Feature | Shouldly | FluentAssertions |
|---------|----------|------------------|
| License | Free (MIT) | Paid ($129.95/dev) for v8+ |
| Syntax | `value.ShouldBe(x)` | `value.Should().Be(x)` |
| Object comparison | `ShouldBeEquivalentTo` (basic) | `BeEquivalentTo` (more powerful) |
| Property exclusion | Not available | `options.Excluding(x => x.Id)` |
| Assertion scope | Not available | `AssertionScope` for grouped assertions |
| Learning curve | Lower | Higher |

**Recommendation:** Shouldly is the better choice for this project given its free license, simpler syntax, and sufficient feature set for most unit testing needs.

---

## 4. Combined Usage Patterns

### Recommended Project Structure

```
src/
  CsharpCompoundingDocs/
    CsharpCompoundingDocs.csproj
    Services/
      EmbeddingService.cs
      FileWatcherService.cs
      VectorStorageService.cs
    Repositories/
      DocumentRepository.cs
    McpServer.cs

tests/
  CsharpCompoundingDocs.Tests/
    CsharpCompoundingDocs.Tests.csproj
    Unit/
      Services/
        EmbeddingServiceTests.cs
        FileWatcherServiceTests.cs
      Repositories/
        DocumentRepositoryTests.cs
    Integration/
      McpServerIntegrationTests.cs
      VectorSearchIntegrationTests.cs
    Fixtures/
      PostgresFixture.cs
      OllamaFixture.cs
    TestData/
      DocumentTestData.cs
      EmbeddingTestData.cs
```

### Test Project Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.1" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="Shouldly" Version="4.2.1" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\CsharpCompoundingDocs\CsharpCompoundingDocs.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="Moq" />
    <Using Include="Shouldly" />
  </ItemGroup>

</Project>
```

### Naming Conventions

```
Test class:     {ClassUnderTest}Tests
Test method:    {MethodName}_{Scenario}_{ExpectedResult}
                {MethodName}_When{Condition}_Should{Behavior}

Examples:
- EmbeddingServiceTests
- GenerateAsync_WithValidInput_ReturnsEmbedding
- GenerateAsync_WhenOllamaUnavailable_ShouldThrowException
```

### Complete Example: AAA Pattern with All Three Libraries

```csharp
using Moq;
using Shouldly;
using Xunit;

public class DocumentIndexerTests
{
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<IVectorStorage> _mockVectorStorage;
    private readonly Mock<IDocumentRepository> _mockDocumentRepo;
    private readonly DocumentIndexer _sut; // System Under Test

    public DocumentIndexerTests()
    {
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockVectorStorage = new Mock<IVectorStorage>();
        _mockDocumentRepo = new Mock<IDocumentRepository>();

        _sut = new DocumentIndexer(
            _mockEmbeddingService.Object,
            _mockVectorStorage.Object,
            _mockDocumentRepo.Object);
    }

    [Fact]
    public async Task IndexAsync_WithValidDocument_StoresEmbeddingAndDocument()
    {
        // Arrange
        var document = new Document
        {
            Path = "/docs/readme.md",
            Content = "# README\nThis is a test."
        };
        var expectedEmbedding = new float[1024];

        _mockEmbeddingService
            .Setup(x => x.GenerateAsync(document.Content, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEmbedding);

        _mockVectorStorage
            .Setup(x => x.StoreAsync(It.IsAny<Guid>(), expectedEmbedding, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockDocumentRepo
            .Setup(x => x.SaveAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.IndexAsync(document);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.DocumentId.ShouldNotBe(Guid.Empty);

        _mockEmbeddingService.Verify(
            x => x.GenerateAsync(document.Content, It.IsAny<CancellationToken>()),
            Times.Once());

        _mockVectorStorage.Verify(
            x => x.StoreAsync(
                It.IsAny<Guid>(),
                It.Is<float[]>(e => e.Length == 1024),
                It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task IndexAsync_WhenEmbeddingServiceFails_ThrowsIndexingException()
    {
        // Arrange
        var document = new Document { Path = "/docs/test.md", Content = "Test" };

        _mockEmbeddingService
            .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Ollama unavailable"));

        // Act & Assert
        var action = () => _sut.IndexAsync(document);

        var exception = await action.ShouldThrowAsync<IndexingException>();
        exception.Message.ShouldContain("Failed to generate embedding");
        exception.InnerException.ShouldBeOfType<HttpRequestException>();

        _mockVectorStorage.Verify(
            x => x.StoreAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task IndexAsync_WithInvalidContent_ReturnsFailure(string? invalidContent)
    {
        // Arrange
        var document = new Document { Path = "/docs/empty.md", Content = invalidContent! };

        // Act
        var result = await _sut.IndexAsync(document);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("content");

        _mockEmbeddingService.Verify(
            x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Theory]
    [ClassData(typeof(DocumentTestData))]
    public async Task IndexAsync_WithVariousDocumentTypes_ProcessesCorrectly(
        Document document,
        bool expectedSuccess)
    {
        // Arrange
        if (expectedSuccess)
        {
            _mockEmbeddingService
                .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new float[1024]);

            _mockVectorStorage
                .Setup(x => x.StoreAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _mockDocumentRepo
                .Setup(x => x.SaveAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        // Act
        var result = await _sut.IndexAsync(document);

        // Assert
        result.IsSuccess.ShouldBe(expectedSuccess);
    }
}

public class DocumentTestData : TheoryData<Document, bool>
{
    public DocumentTestData()
    {
        Add(new Document { Path = "/readme.md", Content = "# Valid" }, true);
        Add(new Document { Path = "/code.cs", Content = "public class Test {}" }, true);
        Add(new Document { Path = "/empty.md", Content = "" }, false);
        Add(new Document { Path = "/binary.exe", Content = "\x00\x01\x02" }, false);
    }
}
```

### Integration Test Example with Fixtures

```csharp
[Collection("Database collection")]
[Trait("Category", "Integration")]
public class VectorSearchIntegrationTests : IAsyncLifetime
{
    private readonly PostgresFixture _dbFixture;
    private readonly IVectorStorage _vectorStorage;

    public VectorSearchIntegrationTests(PostgresFixture dbFixture)
    {
        _dbFixture = dbFixture;
        _vectorStorage = new PgVectorStorage(_dbFixture.Connection);
    }

    public async Task InitializeAsync()
    {
        // Seed test data
        await SeedTestVectorsAsync();
    }

    public async Task DisposeAsync()
    {
        // Clean up test data
        await CleanupTestDataAsync();
    }

    [Fact]
    public async Task Search_WithSimilarVector_ReturnsRelevantResults()
    {
        // Arrange
        var queryVector = new float[1024]; // Similar to seeded data

        // Act
        var results = await _vectorStorage.SearchAsync(queryVector, topK: 5);

        // Assert
        results.ShouldNotBeEmpty();
        results.Count.ShouldBeLessThanOrEqualTo(5);
        results.First().Similarity.ShouldBeGreaterThan(0.7f);
    }

    private async Task SeedTestVectorsAsync() { /* ... */ }
    private async Task CleanupTestDataAsync() { /* ... */ }
}
```

---

## Best Practices and Gotchas (2024-2025)

### xUnit

1. **Prefer `async Task` over `async void`** for async tests
2. **Use `IAsyncLifetime`** instead of constructor/`IDisposable` for async setup/teardown
3. **Avoid test interdependencies** - each test should be independent
4. **Use `TheoryData<T>`** over `IEnumerable<object[]>` for type safety
5. **Consider xUnit v3** for new projects - it has improved async support and Microsoft Testing Platform integration

### Moq

1. **Be aware of the SponsorLink history** - evaluate if NSubstitute is a better fit for your team
2. **Use `MockBehavior.Strict` sparingly** - it can make tests brittle
3. **Prefer `It.IsAny<T>()` with verification** over strict argument matching in setup
4. **Use `VerifyNoOtherCalls()`** to ensure no unexpected interactions
5. **For `HttpClient` mocking**, consider using `HttpMessageHandler` or libraries like [MockHttp](https://github.com/richardszalay/mockhttp)

### Shouldly

1. **Use Shouldly's natural syntax** - it provides better failure messages
2. **Leverage the automatic variable naming** in failure messages
3. **Use `ShouldThrow<T>()` for cleaner exception testing**
4. **For complex object comparison**, consider if `ShouldBeEquivalentTo` meets your needs

### General

1. **Follow AAA pattern** (Arrange-Act-Assert) consistently
2. **Keep tests focused** - one logical assertion per test
3. **Name tests descriptively** - they serve as documentation
4. **Use fixtures wisely** - balance between test isolation and performance
5. **Separate unit and integration tests** - use traits or separate projects

---

## Sources

### Official Documentation
- [xUnit.net Documentation](https://xunit.net/)
- [Moq GitHub Repository](https://github.com/devlooped/moq)
- [Shouldly Documentation](https://docs.shouldly.org/)

### Tutorials and Guides
- [Creating parameterised tests in xUnit - Andrew Lock](https://andrewlock.net/creating-parameterised-tests-in-xunit-with-inlinedata-classdata-and-memberdata/)
- [Data-Driven Testing with xUnit in .NET 8.0 - C# Corner](https://www.c-sharpcorner.com/blogs/datadriven-testing-with-xunit-in-net-80)
- [Creating Data-Driven Tests With xUnit - Milan Jovanovic](https://www.milanjovanovic.tech/blog/creating-data-driven-tests-with-xunit)
- [xUnit Theory with TheoryData - Hamid Mosalla](https://hamidmosalla.com/2020/04/05/xunit-part-8-using-theorydata-instead-of-memberdata-and-classdata/)

### Comparisons
- [Moq vs NSubstitute - DEV Community](https://dev.to/cloudx/moq-vs-nsubstitute-who-is-the-winner-40gi)
- [Moq vs NSubstitute Code Comparisons - NimblePros](https://blog.nimblepros.com/blogs/moq-vs-nsubstitute-code-comparisons/)
- [Should You Pay for FluentAssertions? Comparison with Shouldly - Cosmin Vladutu](https://cosmin-vladutu.medium.com/should-you-pay-for-fluentassertions-a-comparison-with-shouldly-ed59e142e850)
- [Replacing FluentAssertions with Shouldly - Olariu Florin](https://medium.com/@olariu/replacing-fluentassertions-with-shouldly-in-net-projects-d69083f6be16)
