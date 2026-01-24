# Testing Specification

> **Status**: [DRAFT]
> **Last Updated**: 2025-01-23
> **Parent**: [SPEC.md](../SPEC.md)

---

## Executive Summary

This specification defines the testing strategy for the `csharp-compounding-docs` plugin, covering unit, integration, and end-to-end (E2E) testing. The approach emphasizes:

- **100% code coverage** enforcement via Coverlet
- **Dual MCP testing**: Unit tests for internal components + E2E via stdio MCP client
- **.NET Aspire** for integration test infrastructure orchestration
- **GitHub Actions** CI/CD pipeline with staged test execution

---

## Sub-Topic Index

### Test Independence

Patterns for ensuring complete test isolation across unit, integration, and E2E tests. Covers mock creation per-test, data isolation with GUIDs, and anti-patterns to avoid. Load when implementing or reviewing test code. See [testing/test-independence.md](./testing/test-independence.md). **Status**: [DRAFT]

### CI/CD Pipeline

GitHub Actions workflow configuration with staged test execution (unit → integration → E2E), coverage thresholds, and multi-platform builds. Load when setting up or modifying the CI/CD pipeline. See [testing/ci-cd-pipeline.md](./testing/ci-cd-pipeline.md). **Status**: [DRAFT]

> **Background**: Draws on patterns for GitHub Actions workflows, multi-platform builds, service containers, and coverage integration. See [GitHub Actions CI/CD for .NET Applications](../research/github-actions-dotnet-cicd-research.md).

### Aspire Fixtures

.NET Aspire test fixtures using `DistributedApplicationTestingBuilder` for container orchestration, MCP client setup via stdio transport, and database isolation strategies. Load when implementing integration or E2E tests. See [testing/aspire-fixtures.md](./testing/aspire-fixtures.md). **Status**: [DRAFT]

> **Background**: Draws on `DistributedApplicationTestingBuilder` patterns, MCP client test setup, and database isolation strategies. See [.NET Aspire Testing with MCP Client Research](../research/aspire-testing-mcp-client.md).

---

## Technology Stack

> **Background**: Detailed library comparisons, setup patterns, and usage examples for the core testing libraries. See [Unit Testing in C#/.NET: xUnit, Moq, and Shouldly Research](../research/unit-testing-xunit-moq-shouldly.md).

| Component | Package | Version | Purpose |
|-----------|---------|---------|---------|
| Test Framework | xUnit | 2.9.3 | Test execution, assertions, fixtures |
| Mocking | Moq | 4.20.72+ | Dependency isolation |
| Assertions | Shouldly | 4.2.1+ | Fluent assertions (free/MIT license) |
| Coverage | coverlet.msbuild | 6.0.4+ | Coverage collection + threshold enforcement |
| Test SDK | Microsoft.NET.Test.Sdk | 17.13.0+ | Test host integration |
| Aspire Testing | Aspire.Hosting.Testing | 13.1.0+ | Container orchestration for tests |
| MCP Client | ModelContextProtocol | 0.6.0-preview+ | E2E MCP protocol testing |
| Database | Npgsql | 9.0.2+ | PostgreSQL integration tests |

### Technology Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| xUnit version | 2.9.3 (stable) | v3 is preview; stable preferred for production |
| Assertion library | Shouldly | Free/MIT license (FluentAssertions requires paid license) |
| Coverage tool | coverlet.msbuild | Threshold enforcement + build failure capability |
| Report format | Cobertura | Industry standard, GitHub Actions compatible |

---

## Test Project Structure

```
tests/
├── CompoundDocs.Tests/                        # Unit tests
│   ├── CompoundDocs.Tests.csproj
│   ├── Services/
│   │   ├── EmbeddingServiceTests.cs
│   │   ├── FileWatcherServiceTests.cs
│   │   ├── DocumentProcessorTests.cs
│   │   └── LinkResolverTests.cs
│   ├── Repositories/
│   │   └── DocumentRepositoryTests.cs
│   ├── Parsing/
│   │   └── MarkdownParserTests.cs
│   └── TestData/
│       └── DocumentTestData.cs
│
├── CompoundDocs.IntegrationTests/             # Integration tests
│   ├── CompoundDocs.IntegrationTests.csproj
│   ├── Fixtures/
│   │   ├── AspireIntegrationFixture.cs
│   │   └── DatabaseFixture.cs
│   ├── Database/
│   │   ├── VectorStorageTests.cs
│   │   └── DocumentRepositoryIntegrationTests.cs
│   ├── Mcp/
│   │   ├── McpToolTests.cs
│   │   └── McpResourceTests.cs
│   └── xunit.runner.json
│
├── CompoundDocs.E2ETests/                     # End-to-end tests
│   ├── CompoundDocs.E2ETests.csproj
│   ├── Fixtures/
│   │   └── E2EFixture.cs
│   ├── Workflows/
│   │   ├── DocumentIndexingWorkflowTests.cs
│   │   ├── RagQueryWorkflowTests.cs
│   │   └── FileWatcherWorkflowTests.cs
│   └── xunit.runner.json
│
└── Directory.Build.props                      # Shared coverage configuration
```

---

## Test Categories

### Unit Tests (`CompoundDocs.Tests`)

**Scope**: Individual classes and methods in isolation

**Characteristics**:
- No external dependencies (database, file system, network)
- All dependencies mocked via Moq
- Fast execution (<100ms per test)
- High granularity

**Example Traits**:
```csharp
[Fact]
[Trait("Category", "Unit")]
[Trait("Feature", "Embedding")]
public async Task GenerateAsync_WithValidContent_ReturnsEmbedding() { }
```

### Integration Tests (`CompoundDocs.IntegrationTests`)

**Scope**: Component interactions with real infrastructure

**Characteristics**:
- Real PostgreSQL + pgvector via Aspire
- Real Ollama for embeddings
- MCP server as child process via stdio
- Database isolation per test class

**Example Traits**:
```csharp
[Fact]
[Trait("Category", "Integration")]
[Trait("Feature", "VectorSearch")]
public async Task SearchAsync_WithSimilarVector_ReturnsRelevantResults() { }
```

### E2E Tests (`CompoundDocs.E2ETests`)

**Scope**: Complete workflows through MCP protocol

**Characteristics**:
- Full system running (MCP server + PostgreSQL + Ollama)
- Tests via `McpClient` with `StdioClientTransport`
- Validates actual user scenarios
- Longer execution time (30s-2min per test, depending on Ollama embedding latency)

**Timeout Configuration**:
- Individual tests: 2-minute max via `[Fact(Timeout = 120000)]`
- E2E test step in CI: 15-minute max
- E2E job in CI: 30-minute max (includes startup/teardown)

**Example Traits**:
```csharp
[Fact(Timeout = 120000)]  // 2-minute timeout
[Trait("Category", "E2E")]
[Trait("Workflow", "DocumentIndexing")]
public async Task IndexDocument_Query_ReturnsRelevantResults() { }
```

---

## Test Independence (Critical Requirement)

**All tests at all levels (unit, integration, E2E) MUST be completely independent.** Shared state between tests is strictly prohibited.

### Core Principles

| Principle | Requirement |
|-----------|-------------|
| No Shared Mocks | Each test method creates its own mock instances |
| No Test Ordering | Tests must pass regardless of execution order |
| No Shared Data | Each test creates/cleans up its own data using GUIDs |
| No Static State | Avoid static fields; reset in setup if unavoidable |

### Quick Reference

```csharp
// CORRECT: Each test creates its own mocks
[Fact]
public void MyTest()
{
    // Arrange - fresh mocks for this test only
    var mockService = new Mock<IService>();
    var mockRepo = new Mock<IRepository>();
    var sut = new MyClass(mockService.Object, mockRepo.Object);

    // Act & Assert
    // ...
}
```

Detailed patterns, anti-patterns, and enforcement guidelines are in the Test Independence sub-topic. Load only when implementing or reviewing tests. See [spec/testing/test-independence.md](./testing/test-independence.md).

---

## Coverage Configuration

> **Background**: Detailed Coverlet configuration options, threshold types, exclusion patterns, and CI integration examples. See [Coverlet Code Coverage for .NET](../research/coverlet-code-coverage-research.md).

### 100% Coverage Enforcement

All test projects enforce 100% line, branch, and method coverage via Coverlet.

> **Note**: 100% coverage is achievable with proper use of `[ExcludeFromCodeCoverage]` exclusions. Exclusions should be used sparingly and always include a `Justification` string. Valid exclusion cases include: generated code, infrastructure startup, DTOs/records, code with complex external dependencies that can't be meaningfully mocked, and cases where testing complexity significantly outweighs the benefit. See [research/csharp-code-coverage-exclusions.md](../research/csharp-code-coverage-exclusions.md) for detailed guidance.

**`tests/Directory.Build.props`**:
```xml
<Project>
  <PropertyGroup>
    <!-- Coverage collection -->
    <CollectCoverage>true</CollectCoverage>

    <!-- Output configuration -->
    <CoverletOutput>$(MSBuildThisFileDirectory)../coverage/</CoverletOutput>
    <CoverletOutputFormat>cobertura</CoverletOutputFormat>

    <!-- 100% threshold enforcement -->
    <Threshold>100</Threshold>
    <ThresholdType>line,branch,method</ThresholdType>
    <ThresholdStat>total</ThresholdStat>

    <!-- Exclusion settings -->
    <ExcludeByAttribute>
      Obsolete,
      GeneratedCodeAttribute,
      CompilerGeneratedAttribute,
      ExcludeFromCodeCoverage
    </ExcludeByAttribute>
    <SkipAutoProps>true</SkipAutoProps>

    <!-- Include only production assemblies -->
    <Include>[CompoundDocs.*]*</Include>
    <Exclude>[*.Tests]*,[*.IntegrationTests]*,[*.E2ETests]*</Exclude>
  </PropertyGroup>
</Project>
```

### Exclusion Patterns

Code excluded from coverage requirements:

| Pattern | Rationale |
|---------|-----------|
| `[ExcludeFromCodeCoverage]` | Explicitly marked infrastructure code |
| `[GeneratedCodeAttribute]` | Source-generated code |
| `[CompilerGeneratedAttribute]` | Compiler-generated (records, etc.) |
| `**/*.g.cs` | Generated files |
| `**/Migrations/*.cs` | Database migrations |
| DTOs with only auto-properties | Data transfer objects (via SkipAutoProps) |

**Usage in code**:
```csharp
using System.Diagnostics.CodeAnalysis;

[ExcludeFromCodeCoverage(Justification = "Infrastructure startup")]
public class Program { }

[ExcludeFromCodeCoverage(Justification = "DTO")]
public record DocumentDto(string Path, string Content);
```

### Running Tests with Coverage

```bash
# Unit tests only
dotnet test tests/CompoundDocs.Tests

# All tests with coverage report
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Specific category
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
dotnet test --filter "Category=E2E"
```

---

## MCP Testing Patterns

### Unit Testing MCP Tools

Test tool handlers directly with mocked dependencies. **Each test creates its own mocks** to ensure complete independence:

```csharp
public class RagQueryToolTests
{
    [Fact]
    public async Task HandleAsync_WithValidQuery_ReturnsResults()
    {
        // Arrange - each test creates its own mocks
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
    public async Task HandleAsync_WhenNoResults_ReturnsEmptyResponse()
    {
        // Arrange - fresh mocks, completely independent from other tests
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

### E2E Testing via MCP Client

Test complete workflows through the MCP protocol:

```csharp
[Collection("Aspire")]
[Trait("Category", "E2E")]
public class RagQueryWorkflowTests
{
    private readonly AspireIntegrationFixture _fixture;
    private readonly string _testCollection;

    public RagQueryWorkflowTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
        _testCollection = $"test_{Guid.NewGuid():N}";
    }

    [Fact(Timeout = 120000)]
    public async Task CompleteWorkflow_IndexDocument_Query_ReturnsAccurateResponse()
    {
        var mcpClient = _fixture.McpClient!;

        // Index document via MCP tool
        var indexResult = await mcpClient.CallToolAsync(
            "index_document",
            new Dictionary<string, object?>
            {
                ["path"] = "/path/to/test.md",
                ["collection"] = _testCollection
            });

        // Query via RAG tool
        var queryResult = await mcpClient.CallToolAsync(
            "rag_query",
            new Dictionary<string, object?>
            {
                ["query"] = "What is the document about?",
                ["collection"] = _testCollection
            });

        // Assert
        var response = queryResult.Content.OfType<TextContentBlock>().First().Text;
        response.ShouldContain(/* expected content */);
    }
}
```

---

## Test Naming Conventions

### Class Naming

```
{ClassUnderTest}Tests           → Unit tests
{ClassUnderTest}IntegrationTests → Integration tests
{Workflow}WorkflowTests         → E2E workflow tests
```

### Method Naming

```
{Method}_{Scenario}_{ExpectedResult}
{Method}_When{Condition}_Should{Behavior}
```

**Examples**:
```csharp
GenerateAsync_WithValidInput_ReturnsEmbedding()
GenerateAsync_WhenOllamaUnavailable_ThrowsServiceException()
IndexDocument_AndQuery_ReturnsRelevantResults()
```

---

## xUnit Configuration

### `xunit.runner.json` (Integration/E2E Projects)

```json
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "parallelizeAssembly": false,
  "parallelizeTestCollections": false,
  "maxParallelThreads": 1,
  "diagnosticMessages": true,
  "longRunningTestSeconds": 60
}
```

**Rationale**: Integration and E2E tests share database state via Aspire fixture; parallel execution would cause race conditions.

---

## Coverage Visualization

### ReportGenerator

Coverage reports are generated using [ReportGenerator](https://github.com/danielpalme/ReportGenerator) and published to GitHub Pages per release.

**Installation**:
```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

**Report Generation**:
```bash
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coveragereport" \
  -reporttypes:"Html;Badges;MarkdownSummaryGithub"
```

**Report Types Used**:
| Type | Purpose |
|------|---------|
| `Html` | Full navigable HTML report |
| `Badges` | SVG badges for README |
| `MarkdownSummaryGithub` | PR comments and job summary |

**GitHub Pages Publishing**: Coverage HTML is published per release to `https://{org}.github.io/{repo}/coverage/{version}/`. A `latest` symlink always points to the most recent release.

See [research/reportgenerator-coverage-visualization.md](../research/reportgenerator-coverage-visualization.md) for implementation details and complete GitHub Actions workflow.

---

## Deferred for Post-MVP

The following testing capabilities are **not required for MVP** and may be considered in future iterations:

| Capability | Tool | Rationale for Deferral |
|------------|------|------------------------|
| Snapshot testing | Verify | MCP responses are validated via assertions; snapshots add maintenance burden |
| Contract testing | Pact | MCP protocol is well-defined; integration tests provide sufficient coverage |
| Performance benchmarks | BenchmarkDotNet | Embedding performance is Ollama-dependent; not a differentiator for MVP |

---

## Change Log

| Date | Changes |
|------|---------|
| 2025-01-23 | Added Test Independence section; deferred snapshot/contract/perf testing to post-MVP |
| 2025-01-23 | Initial draft; split CI/CD and fixtures to sub-topics |
