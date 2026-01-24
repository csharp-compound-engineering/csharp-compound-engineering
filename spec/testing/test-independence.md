# Test Independence Specification

> **Status**: [DRAFT]
> **Last Updated**: 2025-01-23
> **Parent**: [spec/testing.md](../testing.md)

---

> **Background**: For comprehensive coverage of xUnit fixtures, Moq configuration patterns, and test organization best practices referenced throughout this spec. See [Unit Testing in C#/.NET: xUnit, Moq, and Shouldly Research Report](../../research/unit-testing-xunit-moq-shouldly.md).

> **Background**: For Aspire integration testing patterns, collection fixtures, and database isolation strategies used in integration/E2E tests. See [.NET Aspire Testing with MCP Client Research](../../research/aspire-testing-mcp-client.md).

---

## Critical Requirement

**All tests at all levels (unit, integration, E2E) MUST be completely independent.** Shared state between tests is strictly prohibited.

---

## Core Principles

### 1. No Shared Mocks

Each test method creates its own mock instances. Class-level mock fields are forbidden.

### 2. No Test Ordering Dependencies

Tests must pass regardless of execution order. Never assume another test ran first.

### 3. No Shared Data

Each test creates and cleans up its own test data. Use unique identifiers (GUIDs) for isolation.

### 4. No Static State

Avoid static fields that persist between tests. If unavoidable, reset in test setup.

---

## Why This Matters

| Benefit | Description |
|---------|-------------|
| **Parallel Execution** | Independent tests can run in parallel safely |
| **Debuggability** | Failing tests are reproducible in isolation |
| **Maintainability** | Changing one test never breaks another |
| **CI Reliability** | No flaky tests due to execution order |

---

## Anti-Patterns (Forbidden)

### Shared Mock at Class Level

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
        // May fail if Test1 ran first and configured _sharedMock differently
        // Or may pass locally but fail in CI due to different execution order
    }
}
```

### Shared System Under Test

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

### Test Method Dependencies

```csharp
// WRONG: Tests that depend on each other
public class BadTests
{
    private static string? _createdId; // FORBIDDEN: static shared state

    [Fact]
    public void Test1_CreateItem()
    {
        _createdId = CreateItem(); // Other tests depend on this
    }

    [Fact]
    public void Test2_UseItem()
    {
        UseItem(_createdId!); // Fails if Test1 didn't run first
    }
}
```

---

## Correct Patterns

### Unit Tests: Fresh Mocks Per Test

```csharp
// CORRECT: Each test creates its own mocks
public class GoodTests
{
    [Fact]
    public void Test1()
    {
        // Arrange - fresh mocks for this test only
        var mockService = new Mock<IService>();
        var mockRepo = new Mock<IRepository>();
        mockService.Setup(x => x.DoWork()).Returns(true);

        var sut = new MyClass(mockService.Object, mockRepo.Object);

        // Act & Assert
        sut.Execute().ShouldBeTrue();
    }

    [Fact]
    public void Test2()
    {
        // Arrange - completely independent mocks
        var mockService = new Mock<IService>();
        var mockRepo = new Mock<IRepository>();
        mockService.Setup(x => x.DoWork()).Returns(false); // Different setup, no conflict

        var sut = new MyClass(mockService.Object, mockRepo.Object);

        // Act & Assert
        sut.Execute().ShouldBeFalse();
    }
}
```

### Helper Methods for Mock Setup (Acceptable)

If multiple tests need similar mock configurations, use helper methods that return new instances:

```csharp
public class GoodTestsWithHelpers
{
    // Helper method returns NEW mocks each call - this is acceptable
    private static (Mock<IService> service, Mock<IRepository> repo) CreateMocks()
    {
        return (new Mock<IService>(), new Mock<IRepository>());
    }

    // Another acceptable pattern: builder/factory method
    private static MyClass CreateSut(Action<Mock<IService>>? configureService = null)
    {
        var mockService = new Mock<IService>();
        var mockRepo = new Mock<IRepository>();

        configureService?.Invoke(mockService);

        return new MyClass(mockService.Object, mockRepo.Object);
    }

    [Fact]
    public void Test1()
    {
        var sut = CreateSut(m => m.Setup(x => x.DoWork()).Returns(true));
        sut.Execute().ShouldBeTrue();
    }

    [Fact]
    public void Test2()
    {
        var sut = CreateSut(m => m.Setup(x => x.DoWork()).Returns(false));
        sut.Execute().ShouldBeFalse();
    }
}
```

---

## Integration/E2E Test Isolation

For tests sharing infrastructure (Aspire fixture), isolation is achieved through data partitioning, not mock isolation.

### Unique Collection Names

```csharp
[Collection("Aspire")]
public class IsolatedIntegrationTest
{
    private readonly AspireIntegrationFixture _fixture;
    private readonly string _testCollection; // Unique per test instance

    public IsolatedIntegrationTest(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
        _testCollection = $"test_{Guid.NewGuid():N}"; // Unique collection
    }

    [Fact]
    public async Task Test1()
    {
        // Uses _testCollection - isolated from all other tests
        await IndexDocumentAsync(_testCollection, "doc1.md");
    }
}
```

### Unique File Paths

```csharp
[Fact]
public async Task FileWatcher_NewDocument_IsIndexed()
{
    // Unique file path for this test
    var testFile = Path.Combine(
        Path.GetTempPath(),
        $"test-{Guid.NewGuid()}.md");

    try
    {
        await File.WriteAllTextAsync(testFile, "# Test Content");
        // ... test logic
    }
    finally
    {
        if (File.Exists(testFile)) File.Delete(testFile);
    }
}
```

### Full Isolation Pattern with IAsyncLifetime

```csharp
[Collection("Aspire")]
public class FullyIsolatedTest : IAsyncLifetime
{
    private readonly AspireIntegrationFixture _fixture;
    private readonly string _testCollection;
    private readonly string _testFile;

    public FullyIsolatedTest(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
        _testCollection = $"test_{Guid.NewGuid():N}";
        _testFile = Path.Combine(Path.GetTempPath(), $"doc-{Guid.NewGuid()}.md");
    }

    public Task InitializeAsync()
    {
        // Setup test-specific data if needed
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // ALWAYS clean up test-specific data
        if (File.Exists(_testFile))
        {
            File.Delete(_testFile);
        }

        // Clean up test collection
        try
        {
            await _fixture.McpClient!.CallToolAsync(
                "delete_collection",
                new Dictionary<string, object?> { ["collection"] = _testCollection });
        }
        catch
        {
            // Ignore cleanup failures in tests
        }
    }

    [Fact]
    public async Task MyTest()
    {
        // Test uses _testCollection and _testFile - fully isolated
    }
}
```

---

## Database Isolation Strategies

| Strategy | Use Case | Isolation Level |
|----------|----------|-----------------|
| Unique collection names | Vector storage tests | Per-test |
| Unique GUIDs in data | General data isolation | Per-test |
| TRUNCATE in setup | Shared schema tests | Per-test-class |
| Schema per test class | Complete isolation needed | Per-test-class |

### Recommended: Unique Collection Names

Most tests should use unique collection names (or equivalent namespace):

```csharp
private readonly string _collection = $"test_{Guid.NewGuid():N}";
```

### When to Use Schema Isolation

Use separate schemas only when:
- Tests modify schema structure (DDL)
- Tests require specific database state that's expensive to recreate
- Debugging requires inspecting database state after test failure

---

## Enforcement

### Code Review Checklist

- [ ] No `Mock<T>` fields at class level
- [ ] No `static` fields that hold test state
- [ ] Each `[Fact]` or `[Theory]` creates its own dependencies
- [ ] Unique identifiers used for test data (GUIDs, timestamps)
- [ ] `DisposeAsync` cleans up test-specific resources
- [ ] Test passes when run in isolation: `dotnet test --filter "FullyQualifiedName~TestName"`

### CI Verification

Integration tests run with `parallelizeTestCollections: false` not because tests share state, but because they share expensive infrastructure (Aspire). Each test's data is still isolated.

```bash
# Verify a test is truly independent by running it alone
dotnet test --filter "FullyQualifiedName~MyTestClass.MyTestMethod"
```

---

## Change Log

| Date | Changes |
|------|---------|
| 2025-01-23 | Initial draft |
