# Phase 111: Test Independence Patterns

> **Status**: NOT_STARTED
> **Effort Estimate**: 3-4 hours
> **Category**: Testing Framework
> **Prerequisites**: Phase 110 (Test Method Structure)

---

## Spec References

This phase implements test independence requirements defined in:

- **spec/testing/test-independence.md** - Complete test independence specification including anti-patterns, correct patterns, and enforcement guidelines

---

## Objectives

1. Establish no shared mocks rule enforcement
2. Implement no test ordering dependencies pattern
3. Define shared data isolation using GUIDs
4. Eliminate static state in test classes
5. Create fresh mocks per test pattern
6. Implement IAsyncLifetime for proper setup/cleanup

---

## Acceptance Criteria

### No Shared Mocks Rule

- [ ] No `Mock<T>` fields declared at class level in test classes
- [ ] Each `[Fact]` or `[Theory]` creates its own mock instances
- [ ] Helper methods return NEW mock instances each call (not shared)
- [ ] Code review checklist includes shared mock detection
- [ ] Static analyzers configured to warn on class-level mock fields

### No Test Ordering Dependencies

- [ ] Tests pass regardless of execution order
- [ ] No test assumes another test ran first
- [ ] No `static` fields that hold state between tests
- [ ] Each test is self-contained with own setup and assertions
- [ ] Tests can run in isolation: `dotnet test --filter "FullyQualifiedName~TestName"`

### No Shared Data (Use GUIDs)

- [ ] Each test creates its own test data with unique identifiers
- [ ] Collection names use GUID pattern: `$"test_{Guid.NewGuid():N}"`
- [ ] File paths use GUID pattern: `$"doc-{Guid.NewGuid()}.md"`
- [ ] Database isolation achieved through unique collection names per test
- [ ] No test data pollutes other tests' assertions

### No Static State

- [ ] No `static` fields that persist state between tests
- [ ] If static fields unavoidable, reset in test setup
- [ ] Singleton patterns avoided in test classes
- [ ] Shared fixtures use xUnit collection fixtures properly (not static state)

### Fresh Mocks Per Test Pattern

- [ ] Standard pattern documented for creating mocks in Arrange section
- [ ] Helper methods pattern documented for reducing boilerplate:
  ```csharp
  private static (Mock<IService> service, Mock<IRepository> repo) CreateMocks()
  ```
- [ ] Factory method pattern documented for SUT creation with mock configuration:
  ```csharp
  private static MyClass CreateSut(Action<Mock<IService>>? configureService = null)
  ```
- [ ] All examples in test documentation follow fresh mocks pattern

### IAsyncLifetime for Setup/Cleanup

- [ ] Integration tests implement `IAsyncLifetime` interface
- [ ] `InitializeAsync()` used for test-specific setup (not shared state)
- [ ] `DisposeAsync()` cleans up ALL test-specific resources:
  - [ ] Temporary files deleted
  - [ ] Test collections deleted from database
  - [ ] Any external resources released
- [ ] Cleanup failures logged but do not fail tests (best-effort cleanup)
- [ ] Pattern documented in test template

---

## Implementation Notes

### Anti-Patterns to Forbid

From spec, these patterns are FORBIDDEN:

**Shared Mock at Class Level**
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
    }
}
```

**Shared System Under Test**
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

**Test Method Dependencies**
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

### Correct Patterns to Enforce

**Fresh Mocks Per Test**
```csharp
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
        mockService.Setup(x => x.DoWork()).Returns(false);

        var sut = new MyClass(mockService.Object, mockRepo.Object);

        // Act & Assert
        sut.Execute().ShouldBeFalse();
    }
}
```

**Helper Methods for Mock Setup**
```csharp
public class GoodTestsWithHelpers
{
    // Helper method returns NEW mocks each call - acceptable
    private static (Mock<IService> service, Mock<IRepository> repo) CreateMocks()
    {
        return (new Mock<IService>(), new Mock<IRepository>());
    }

    // Builder/factory method pattern - acceptable
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
}
```

**IAsyncLifetime for Integration Tests**
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

### Database Isolation Strategies

| Strategy | Use Case | Isolation Level |
|----------|----------|-----------------|
| Unique collection names | Vector storage tests | Per-test |
| Unique GUIDs in data | General data isolation | Per-test |
| TRUNCATE in setup | Shared schema tests | Per-test-class |
| Schema per test class | Complete isolation needed | Per-test-class |

**Recommended Default**: Unique collection names
```csharp
private readonly string _collection = $"test_{Guid.NewGuid():N}";
```

### Code Review Checklist

From spec, reviewers must verify:

- [ ] No `Mock<T>` fields at class level
- [ ] No `static` fields that hold test state
- [ ] Each `[Fact]` or `[Theory]` creates its own dependencies
- [ ] Unique identifiers used for test data (GUIDs, timestamps)
- [ ] `DisposeAsync` cleans up test-specific resources
- [ ] Test passes when run in isolation

### CI Verification Command

```bash
# Verify a test is truly independent by running it alone
dotnet test --filter "FullyQualifiedName~MyTestClass.MyTestMethod"
```

---

## Dependencies

### Depends On
- Phase 110: Test Method Structure (AAA pattern, naming conventions)

### Blocks
- Phase 112+ (subsequent testing framework phases that rely on independence patterns)

---

## Verification Steps

After completing this phase, verify:

1. **Documentation**: Test independence patterns documented in project test guidelines
2. **Templates**: Test class templates follow fresh mocks pattern
3. **Integration Tests**: All use IAsyncLifetime with proper cleanup
4. **GUID Usage**: Test data uses unique identifiers
5. **Isolation Verification**: Sample tests pass when run in isolation

### Verification Commands

```bash
# Run single test in isolation to verify independence
dotnet test --filter "FullyQualifiedName~SampleTests.Sample_Test"

# Run all tests to verify no ordering dependencies
dotnet test

# Run tests multiple times to catch flaky ordering issues
for i in {1..5}; do dotnet test; done
```

### Test Scenarios

| Scenario | Expected Behavior |
|----------|-------------------|
| Run test in isolation | Test passes with same result as full suite |
| Run tests in random order | All tests pass regardless of order |
| Run tests in parallel | No race conditions or shared state conflicts |
| Check cleanup after test failure | Resources still cleaned up properly |

---

## Files to Create/Modify

### New Files

| File | Purpose |
|------|---------|
| `tests/README.md` | Test independence guidelines and patterns |
| `tests/Templates/UnitTestTemplate.cs` | Template showing fresh mocks pattern |
| `tests/Templates/IntegrationTestTemplate.cs` | Template showing IAsyncLifetime pattern |

### Modified Files

| File | Changes |
|------|---------|
| Existing test files | Refactor any violations of independence patterns |
| `.editorconfig` | Add analyzer rules for detecting shared mock fields |

---

## Notes

- Test independence enables reliable parallel execution
- Fresh mocks per test eliminates subtle state pollution bugs
- GUIDs for test data prevent cross-test interference
- IAsyncLifetime ensures cleanup even when tests fail
- CI runs with `parallelizeTestCollections: false` for Aspire tests due to shared infrastructure, but data is still isolated
- These patterns apply to ALL test levels: unit, integration, and E2E
