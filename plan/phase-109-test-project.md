# Phase 109: Test Project Structure

> **Status**: NOT_STARTED
> **Effort Estimate**: 3-5 hours
> **Category**: Testing Framework
> **Prerequisites**: Phase 001 (Solution Structure)

---

## Spec References

This phase implements the test project structure defined in:

- **spec/testing.md** - [Test Project Structure](../spec/testing.md#test-project-structure) (lines 66-108)
- **spec/testing.md** - [Technology Stack](../spec/testing.md#technology-stack) (lines 44-63)
- **spec/testing.md** - [Test Categories](../spec/testing.md#test-categories) (lines 112-171)
- **spec/testing.md** - [Test Naming Conventions](../spec/testing.md#test-naming-conventions) (lines 401-425)
- **structure/testing.md** - Testing structure summary

---

## Objectives

1. Create the `tests/` directory structure with three test projects
2. Create `CompoundDocs.Tests` project for unit tests
3. Create `CompoundDocs.IntegrationTests` project for integration tests
4. Create `CompoundDocs.E2ETests` project for end-to-end tests
5. Configure shared test properties via `tests/Directory.Build.props`
6. Set up project references and NuGet package dependencies
7. Configure xUnit runner settings for integration and E2E projects
8. Add test projects to the solution file

---

## Acceptance Criteria

- [ ] `tests/` directory exists with proper structure
- [ ] `tests/Directory.Build.props` configures coverage collection and thresholds
- [ ] Unit test project created:
  - [ ] `tests/CompoundDocs.Tests/CompoundDocs.Tests.csproj` exists
  - [ ] Project references `CompoundDocs.McpServer` (when created)
  - [ ] Contains xUnit, Moq, Shouldly packages
  - [ ] Directory structure: `Services/`, `Repositories/`, `Parsing/`, `TestData/`
- [ ] Integration test project created:
  - [ ] `tests/CompoundDocs.IntegrationTests/CompoundDocs.IntegrationTests.csproj` exists
  - [ ] Project references `CompoundDocs.McpServer` (when created)
  - [ ] Contains Aspire.Hosting.Testing, ModelContextProtocol, Npgsql packages
  - [ ] Directory structure: `Fixtures/`, `Database/`, `Mcp/`
  - [ ] `xunit.runner.json` disables parallel execution
- [ ] E2E test project created:
  - [ ] `tests/CompoundDocs.E2ETests/CompoundDocs.E2ETests.csproj` exists
  - [ ] Project references `CompoundDocs.McpServer` (when created)
  - [ ] Contains ModelContextProtocol, Aspire.Hosting.Testing packages
  - [ ] Directory structure: `Fixtures/`, `Workflows/`
  - [ ] `xunit.runner.json` disables parallel execution
- [ ] All test projects added to `csharp-compounding-docs.sln`
- [ ] `dotnet build` succeeds for all test projects
- [ ] `dotnet test` runs (with no tests initially)

---

## Implementation Notes

### Directory Structure

Create the following directory tree per spec/testing.md:

```
tests/
├── CompoundDocs.Tests/                        # Unit tests
│   ├── CompoundDocs.Tests.csproj
│   ├── Services/
│   │   └── .gitkeep
│   ├── Repositories/
│   │   └── .gitkeep
│   ├── Parsing/
│   │   └── .gitkeep
│   └── TestData/
│       └── .gitkeep
│
├── CompoundDocs.IntegrationTests/             # Integration tests
│   ├── CompoundDocs.IntegrationTests.csproj
│   ├── Fixtures/
│   │   └── .gitkeep
│   ├── Database/
│   │   └── .gitkeep
│   ├── Mcp/
│   │   └── .gitkeep
│   └── xunit.runner.json
│
├── CompoundDocs.E2ETests/                     # End-to-end tests
│   ├── CompoundDocs.E2ETests.csproj
│   ├── Fixtures/
│   │   └── .gitkeep
│   ├── Workflows/
│   │   └── .gitkeep
│   └── xunit.runner.json
│
└── Directory.Build.props                      # Shared coverage configuration
```

### tests/Directory.Build.props

Create shared test configuration with 100% coverage enforcement:

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

### CompoundDocs.Tests.csproj (Unit Tests)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Moq" />
    <PackageReference Include="Shouldly" />
    <PackageReference Include="coverlet.msbuild">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <!-- Project reference added when CompoundDocs.McpServer exists -->
  <!-- <ItemGroup>
    <ProjectReference Include="..\..\src\CompoundDocs.McpServer\CompoundDocs.McpServer.csproj" />
  </ItemGroup> -->

</Project>
```

### CompoundDocs.IntegrationTests.csproj (Integration Tests)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Moq" />
    <PackageReference Include="Shouldly" />
    <PackageReference Include="coverlet.msbuild">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Aspire.Hosting.Testing" />
    <PackageReference Include="ModelContextProtocol" />
    <PackageReference Include="Npgsql" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="xunit.runner.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

  <!-- Project reference added when CompoundDocs.McpServer exists -->
  <!-- <ItemGroup>
    <ProjectReference Include="..\..\src\CompoundDocs.McpServer\CompoundDocs.McpServer.csproj" />
  </ItemGroup> -->

</Project>
```

### CompoundDocs.E2ETests.csproj (E2E Tests)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Shouldly" />
    <PackageReference Include="coverlet.msbuild">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Aspire.Hosting.Testing" />
    <PackageReference Include="ModelContextProtocol" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="xunit.runner.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

  <!-- Project reference added when CompoundDocs.McpServer exists -->
  <!-- <ItemGroup>
    <ProjectReference Include="..\..\src\CompoundDocs.McpServer\CompoundDocs.McpServer.csproj" />
  </ItemGroup> -->

</Project>
```

### xunit.runner.json (Integration/E2E Projects)

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

### Directory.Packages.props Updates

Add testing package versions to the root `Directory.Packages.props`:

```xml
<ItemGroup Label="Testing">
  <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
  <PackageVersion Include="xunit" Version="2.9.3" />
  <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
  <PackageVersion Include="Moq" Version="4.20.72" />
  <PackageVersion Include="Shouldly" Version="4.2.1" />
  <PackageVersion Include="coverlet.msbuild" Version="6.0.4" />
  <PackageVersion Include="Aspire.Hosting.Testing" Version="9.1.0" />
  <PackageVersion Include="ModelContextProtocol" Version="0.6.0-preview.1" />
  <PackageVersion Include="Npgsql" Version="9.0.2" />
</ItemGroup>
```

### Adding Projects to Solution

```bash
dotnet sln add tests/CompoundDocs.Tests/CompoundDocs.Tests.csproj
dotnet sln add tests/CompoundDocs.IntegrationTests/CompoundDocs.IntegrationTests.csproj
dotnet sln add tests/CompoundDocs.E2ETests/CompoundDocs.E2ETests.csproj
```

### Test Naming Conventions

Per spec/testing.md, follow these naming conventions:

**Class Naming:**
```
{ClassUnderTest}Tests           → Unit tests (e.g., EmbeddingServiceTests)
{ClassUnderTest}IntegrationTests → Integration tests (e.g., DocumentRepositoryIntegrationTests)
{Workflow}WorkflowTests         → E2E workflow tests (e.g., RagQueryWorkflowTests)
```

**Method Naming:**
```
{Method}_{Scenario}_{ExpectedResult}
{Method}_When{Condition}_Should{Behavior}
```

**Examples:**
```csharp
GenerateAsync_WithValidInput_ReturnsEmbedding()
GenerateAsync_WhenOllamaUnavailable_ThrowsServiceException()
IndexDocument_AndQuery_ReturnsRelevantResults()
```

### Test Traits

Use xUnit traits for categorization:

```csharp
// Unit tests
[Fact]
[Trait("Category", "Unit")]
[Trait("Feature", "Embedding")]
public async Task GenerateAsync_WithValidContent_ReturnsEmbedding() { }

// Integration tests
[Fact]
[Trait("Category", "Integration")]
[Trait("Feature", "VectorSearch")]
public async Task SearchAsync_WithSimilarVector_ReturnsRelevantResults() { }

// E2E tests
[Fact(Timeout = 120000)]  // 2-minute timeout
[Trait("Category", "E2E")]
[Trait("Workflow", "DocumentIndexing")]
public async Task IndexDocument_Query_ReturnsRelevantResults() { }
```

---

## Dependencies

### Depends On
- **Phase 001**: Solution Structure (solution file and directory structure)

### Blocks
- **Phase 110**: Unit Test Fixtures and Helpers
- **Phase 111**: Integration Test Aspire Fixtures
- **Phase 112**: E2E Test Fixtures
- All test implementation phases

---

## Verification Steps

After completing this phase, verify:

1. **Directory structure exists**:
   ```bash
   ls -la tests/
   ls -la tests/CompoundDocs.Tests/
   ls -la tests/CompoundDocs.IntegrationTests/
   ls -la tests/CompoundDocs.E2ETests/
   ```

2. **Projects build successfully**:
   ```bash
   dotnet build tests/CompoundDocs.Tests/
   dotnet build tests/CompoundDocs.IntegrationTests/
   dotnet build tests/CompoundDocs.E2ETests/
   ```

3. **Solution includes test projects**:
   ```bash
   dotnet sln list
   ```

4. **Test runner executes** (no tests yet, but runner should work):
   ```bash
   dotnet test --list-tests
   ```

5. **Coverage configuration works**:
   ```bash
   dotnet test /p:CollectCoverage=true
   ```

---

## Notes

- Project references to `CompoundDocs.McpServer` are commented out initially; uncomment when that project is created in Phase 021
- The `Directory.Build.props` in `tests/` augments the root `Directory.Build.props`, not replaces it
- xUnit 2.9.3 (stable) is used rather than v3 preview per technology decisions in spec
- Shouldly is used for assertions instead of FluentAssertions (license requirement)
- Integration and E2E tests disable parallel execution to prevent race conditions with shared Aspire resources
- E2E tests use explicit timeouts (2 minutes per test) to handle Ollama embedding latency
