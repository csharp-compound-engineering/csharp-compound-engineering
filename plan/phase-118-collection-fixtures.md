# Phase 118: xUnit Collection Fixtures

> **Status**: NOT_STARTED
> **Effort Estimate**: 3-4 hours
> **Category**: Testing Framework
> **Prerequisites**: Phase 115 (AspireIntegrationFixture)

---

## Spec References

This phase implements the xUnit collection fixture patterns defined in:

- **spec/testing/aspire-fixtures.md** - xUnit Collection Fixtures and shared fixture patterns
- **research/unit-testing-xunit-moq-shouldly.md** - xUnit fixture lifecycle and best practices

---

## Objectives

1. Implement the `ICollectionFixture<AspireIntegrationFixture>` pattern for shared test infrastructure
2. Create collection definition class for test organization
3. Enable fixture injection into test classes via constructor
4. Optimize fixture reuse across multiple test classes
5. Configure xUnit runner for proper collection behavior

---

## Acceptance Criteria

### Collection Definition

- [ ] `AspireCollection.cs` exists in `CompoundDocs.IntegrationTests/Collections/`
- [ ] Collection definition uses `[CollectionDefinition("Aspire")]` attribute
- [ ] Collection class implements `ICollectionFixture<AspireIntegrationFixture>`
- [ ] Collection class is a marker class with no implementation code

### Test Class Integration

- [ ] Test classes can opt into the collection via `[Collection("Aspire")]` attribute
- [ ] `AspireIntegrationFixture` is injected via constructor parameter
- [ ] Multiple test classes share the same fixture instance within a collection
- [ ] Fixture initialization occurs once per test run, not per test class

### Fixture Injection Pattern

- [ ] Test classes receive fully initialized fixture with:
  - [ ] `PostgresConnectionString` - Ready database connection
  - [ ] `OllamaEndpoint` - Active embedding service endpoint
  - [ ] `McpClient` - Configured MCP client for E2E testing
  - [ ] `App` - Reference to running Aspire application

### Fixture Reuse Optimization

- [ ] Single Aspire application instance across all tests in collection
- [ ] Container resources (PostgreSQL, Ollama) start once and stay running
- [ ] Test isolation achieved via unique collection names, not fixture recreation
- [ ] Proper disposal only when all tests in collection complete

### xUnit Configuration

- [ ] `xunit.runner.json` configured for integration test behavior
- [ ] Test collection parallelization disabled (`parallelizeTestCollections: false`)
- [ ] Long-running test threshold set appropriately (`longRunningTestSeconds: 60`)
- [ ] Assembly parallelization disabled for shared infrastructure safety

---

## Implementation Notes

### Collection Definition Class

Create `Collections/AspireCollection.cs`:

```csharp
using Xunit;

namespace CompoundDocs.IntegrationTests.Collections;

/// <summary>
/// Defines a collection of integration tests that share a single AspireIntegrationFixture instance.
/// All test classes decorated with [Collection("Aspire")] will receive the same fixture.
/// </summary>
[CollectionDefinition("Aspire")]
public class AspireCollection : ICollectionFixture<AspireIntegrationFixture>
{
    // This class has no code - it's a marker class for xUnit.
    // The ICollectionFixture<T> interface tells xUnit to:
    // 1. Create one instance of AspireIntegrationFixture for all tests in this collection
    // 2. Call InitializeAsync() before any test runs
    // 3. Call DisposeAsync() after all tests complete
}
```

### Test Class Using Collection Fixture

Create example test class pattern:

```csharp
using CompoundDocs.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace CompoundDocs.IntegrationTests.VectorStorage;

/// <summary>
/// Integration tests for vector storage operations.
/// Shares Aspire infrastructure with other tests in the "Aspire" collection.
/// </summary>
[Collection("Aspire")]
[Trait("Category", "Integration")]
public class VectorStorageIntegrationTests
{
    private readonly AspireIntegrationFixture _fixture;

    public VectorStorageIntegrationTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PostgresConnectionString_IsAvailable()
    {
        // Arrange & Act - fixture provides connection string
        var connectionString = _fixture.PostgresConnectionString;

        // Assert
        connectionString.ShouldNotBeNullOrEmpty();
        connectionString.ShouldContain("Host=");
    }

    [Fact]
    public async Task OllamaEndpoint_IsAvailable()
    {
        // Arrange & Act - fixture provides Ollama endpoint
        var endpoint = _fixture.OllamaEndpoint;

        // Assert
        endpoint.ShouldNotBeNullOrEmpty();
        endpoint.ShouldStartWith("http://");
    }

    [Fact]
    public async Task McpClient_IsInitialized()
    {
        // Arrange & Act - fixture provides MCP client
        var client = _fixture.McpClient;

        // Assert
        client.ShouldNotBeNull();
    }
}
```

### Multiple Test Classes Sharing Fixture

```csharp
// File: McpToolIntegrationTests.cs
[Collection("Aspire")]
[Trait("Category", "Integration")]
public class McpToolIntegrationTests
{
    private readonly AspireIntegrationFixture _fixture;

    public McpToolIntegrationTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
        // Same fixture instance as VectorStorageIntegrationTests
    }

    [Fact]
    public async Task ListTools_ReturnsAvailableTools()
    {
        // Uses the shared MCP client
        var tools = await _fixture.McpClient!.ListToolsAsync();
        tools.ShouldNotBeEmpty();
    }
}

// File: RagQueryIntegrationTests.cs
[Collection("Aspire")]
[Trait("Category", "Integration")]
public class RagQueryIntegrationTests
{
    private readonly AspireIntegrationFixture _fixture;

    public RagQueryIntegrationTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
        // Same fixture instance as other tests in collection
    }

    [Fact]
    public async Task RagQuery_WithValidQuestion_ReturnsResponse()
    {
        // Uses shared fixture for RAG testing
        var result = await _fixture.McpClient!.CallToolAsync(
            "rag_query",
            new Dictionary<string, object?> { ["query"] = "test question" });

        result.ShouldNotBeNull();
    }
}
```

### Test Isolation Within Shared Fixture

Each test should create unique identifiers for its data:

```csharp
[Collection("Aspire")]
public class DocumentIndexingTests : IAsyncLifetime
{
    private readonly AspireIntegrationFixture _fixture;
    private readonly string _testCollection;

    public DocumentIndexingTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
        // Unique collection name per test class instance
        _testCollection = $"test_{Guid.NewGuid():N}";
    }

    public async Task InitializeAsync()
    {
        // Seed test-specific data using unique collection
        await SeedTestDataAsync(_testCollection);
    }

    public async Task DisposeAsync()
    {
        // Clean up test-specific data
        await CleanupTestDataAsync(_testCollection);
    }

    [Fact]
    public async Task IndexDocument_CreatesVectorEmbedding()
    {
        // Test uses _testCollection for isolation
    }

    private async Task SeedTestDataAsync(string collection)
    {
        // Use _fixture.PostgresConnectionString to seed data
    }

    private async Task CleanupTestDataAsync(string collection)
    {
        // Delete test data from _testCollection
        await _fixture.McpClient!.CallToolAsync(
            "delete_collection",
            new Dictionary<string, object?> { ["collection"] = collection });
    }
}
```

### xUnit Runner Configuration

Create `xunit.runner.json` in the test project root:

```json
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "parallelizeAssembly": false,
  "parallelizeTestCollections": false,
  "maxParallelThreads": 1,
  "diagnosticMessages": true,
  "longRunningTestSeconds": 60,
  "methodDisplay": "classAndMethod",
  "methodDisplayOptions": "all"
}
```

Update `.csproj` to copy configuration:

```xml
<ItemGroup>
  <Content Include="xunit.runner.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

### Fixture Lifecycle Sequence

Understanding the lifecycle helps optimize test performance:

```
Test Run Start
    |
    v
[CollectionDefinition("Aspire")] found
    |
    v
AspireIntegrationFixture.InitializeAsync()
    - DistributedApplication created
    - PostgreSQL started and healthy
    - Ollama started and healthy
    - McpClient initialized
    |
    v
[For each test class in collection]
    |
    +-> TestClass1 constructor (fixture injected)
    |   +-> Test1 runs
    |   +-> Test2 runs
    |   +-> TestClass1 disposed (if IDisposable)
    |
    +-> TestClass2 constructor (same fixture injected)
    |   +-> Test1 runs
    |   +-> Test2 runs
    |   +-> TestClass2 disposed (if IDisposable)
    |
    v
All tests in collection complete
    |
    v
AspireIntegrationFixture.DisposeAsync()
    - McpClient disposed
    - DistributedApplication disposed
    - Containers stopped
```

### Collection Fixture vs Class Fixture Comparison

| Aspect | IClassFixture<T> | ICollectionFixture<T> |
|--------|------------------|----------------------|
| Scope | Single test class | Multiple test classes |
| Lifetime | Created/disposed per test class | Created once per collection |
| Use Case | Class-specific setup | Shared expensive resources |
| Declaration | Test class implements interface | Separate collection definition class |
| Injection | Constructor parameter | Constructor parameter |

### Best Practices for Collection Fixtures

1. **Expensive Resources**: Use collection fixtures for resources that are expensive to create (Aspire apps, databases, containers)

2. **Test Isolation**: Never rely on test execution order; use unique identifiers per test

3. **No Shared Mutable State**: Tests should not modify fixture state that affects other tests

4. **Explicit Cleanup**: Use `IAsyncLifetime` in test classes for per-test setup/teardown

5. **Single Collection**: Avoid multiple collections sharing the same fixture type

---

## Dependencies

### Depends On
- Phase 115: AspireIntegrationFixture (the fixture class to share)
- Phase 001: Solution & Project Structure

### Blocks
- Phase 119+: Integration test implementations that use the collection
- E2E test implementations
- CI/CD pipeline integration tests

---

## Verification Steps

After completing this phase, verify:

1. **Collection definition compiles**: No missing references
2. **Fixture sharing**: Multiple test classes receive the same fixture instance
3. **Single initialization**: Fixture's `InitializeAsync` called once per test run
4. **Proper disposal**: Containers stop after all tests complete
5. **Test isolation**: Tests don't interfere with each other's data

### Manual Verification

```bash
# Run all integration tests - fixture should initialize once
dotnet test tests/CompoundDocs.IntegrationTests/ \
    --filter "Category=Integration" \
    --logger "console;verbosity=detailed"

# Verify in logs:
# - Single "Aspire application started" message
# - Multiple test classes running
# - Single "Aspire application stopped" message at end
```

### Verification Test

```csharp
[Collection("Aspire")]
public class CollectionFixtureVerificationTests
{
    private static AspireIntegrationFixture? _previousFixture;
    private readonly AspireIntegrationFixture _fixture;

    public CollectionFixtureVerificationTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Fixture_IsSameInstanceAcrossTests()
    {
        // First test sets the static reference
        if (_previousFixture == null)
        {
            _previousFixture = _fixture;
        }

        // All subsequent tests verify same instance
        _fixture.ShouldBeSameAs(_previousFixture);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Fixture_HasInitializedResources()
    {
        _fixture.PostgresConnectionString.ShouldNotBeNullOrEmpty();
        _fixture.OllamaEndpoint.ShouldNotBeNullOrEmpty();
        _fixture.McpClient.ShouldNotBeNull();
        _fixture.App.ShouldNotBeNull();
    }
}
```

---

## File Structure

After completing this phase, the test project should have:

```
tests/
  CompoundDocs.IntegrationTests/
    Collections/
      AspireCollection.cs          # Collection definition
    Fixtures/
      AspireIntegrationFixture.cs  # From Phase 115
    VectorStorage/
      VectorStorageIntegrationTests.cs
    McpTools/
      McpToolIntegrationTests.cs
    Rag/
      RagQueryIntegrationTests.cs
    xunit.runner.json              # Runner configuration
    CompoundDocs.IntegrationTests.csproj
```

---

## Notes

- Collection fixtures are ideal for integration tests with shared infrastructure like Aspire applications, databases, and containerized services
- The `[Collection("Aspire")]` attribute must be applied to every test class that wants to share the fixture
- Test classes not in a collection get their own fixture instance if they implement `IClassFixture<T>`
- Collection names are strings and must match exactly (case-sensitive)
- Consider using constants for collection names to avoid typos: `public const string AspireCollection = "Aspire";`
- Collection fixtures support `IAsyncLifetime` for async initialization/cleanup, which is essential for Aspire applications
