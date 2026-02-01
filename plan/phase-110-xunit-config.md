# Phase 110: xUnit Test Framework Configuration

> **Status**: NOT_STARTED
> **Effort Estimate**: 2-3 hours
> **Category**: Testing Framework
> **Prerequisites**: Phase 109 (Test Project Structure)

---

## Spec References

This phase implements the xUnit test framework configuration defined in:

- **spec/testing.md** - [Technology Stack](../spec/testing.md#technology-stack) and [xUnit Configuration](../spec/testing.md#xunit-configuration)
- **research/unit-testing-xunit-moq-shouldly.md** - xUnit setup patterns, Moq integration, and Shouldly assertions

---

## Objectives

1. Add xUnit 2.9.3 (stable) package reference to all test projects
2. Add Moq 4.20.72+ package reference for mocking
3. Add Shouldly 4.2.1+ package reference for fluent assertions
4. Add Microsoft.NET.Test.Sdk 17.13.0+ for test host integration
5. Add xunit.runner.visualstudio for IDE integration
6. Create `xunit.runner.json` configuration files for Integration and E2E test projects
7. Configure test parallelization settings per project type
8. Configure global usings for test assemblies

---

## Acceptance Criteria

- [ ] NuGet package references added to `tests/Directory.Build.props`:
  - [ ] `Microsoft.NET.Test.Sdk` version 17.13.0+
  - [ ] `xunit` version 2.9.3
  - [ ] `xunit.runner.visualstudio` version 3.1.1+
  - [ ] `Moq` version 4.20.72+
  - [ ] `Shouldly` version 4.2.1+
- [ ] Package versions centralized in `Directory.Packages.props`
- [ ] `xunit.runner.json` created in `tests/CompoundDocs.IntegrationTests/`:
  - [ ] `parallelizeAssembly: false`
  - [ ] `parallelizeTestCollections: false`
  - [ ] `maxParallelThreads: 1`
  - [ ] `diagnosticMessages: true`
  - [ ] `longRunningTestSeconds: 60`
- [ ] `xunit.runner.json` created in `tests/CompoundDocs.E2ETests/`:
  - [ ] `parallelizeAssembly: false`
  - [ ] `parallelizeTestCollections: false`
  - [ ] `maxParallelThreads: 1`
  - [ ] `diagnosticMessages: true`
  - [ ] `longRunningTestSeconds: 60`
- [ ] Unit test project (`CompoundDocs.Tests`) allows parallel execution (default)
- [ ] Global usings configured for test projects:
  - [ ] `Xunit`
  - [ ] `Moq`
  - [ ] `Shouldly`
- [ ] All test projects build successfully with `dotnet build`
- [ ] `dotnet test` runs without configuration errors

---

## Implementation Notes

### Package Version Management

Add to `Directory.Packages.props` at repository root:

```xml
<ItemGroup>
  <!-- Testing Framework -->
  <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
  <PackageVersion Include="xunit" Version="2.9.3" />
  <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.1" />

  <!-- Mocking and Assertions -->
  <PackageVersion Include="Moq" Version="4.20.72" />
  <PackageVersion Include="Shouldly" Version="4.2.1" />
</ItemGroup>
```

### Update tests/Directory.Build.props

Extend the existing `tests/Directory.Build.props` to include package references and global usings:

```xml
<Project>
  <!-- Existing coverage configuration from spec -->
  <PropertyGroup>
    <CollectCoverage>true</CollectCoverage>
    <CoverletOutput>$(MSBuildThisFileDirectory)../coverage/</CoverletOutput>
    <CoverletOutputFormat>cobertura</CoverletOutputFormat>
    <Threshold>100</Threshold>
    <ThresholdType>line,branch,method</ThresholdType>
    <ThresholdStat>total</ThresholdStat>
    <ExcludeByAttribute>
      Obsolete,
      GeneratedCodeAttribute,
      CompilerGeneratedAttribute,
      ExcludeFromCodeCoverage
    </ExcludeByAttribute>
    <SkipAutoProps>true</SkipAutoProps>
    <Include>[CompoundDocs.*]*</Include>
    <Exclude>[*.Tests]*,[*.IntegrationTests]*,[*.E2ETests]*</Exclude>
  </PropertyGroup>

  <!-- Common test properties -->
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <!-- Testing framework packages (all test projects) -->
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

  <!-- Global usings for test projects -->
  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="Moq" />
    <Using Include="Shouldly" />
  </ItemGroup>
</Project>
```

### xunit.runner.json for Integration Tests

Create `tests/CompoundDocs.IntegrationTests/xunit.runner.json`:

```json
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "parallelizeAssembly": false,
  "parallelizeTestCollections": false,
  "maxParallelThreads": 1,
  "diagnosticMessages": true,
  "longRunningTestSeconds": 60,
  "methodDisplay": "classAndMethod",
  "methodDisplayOptions": "replacePeriodWithComma"
}
```

**Rationale**: Integration tests share database state via Aspire fixture; parallel execution would cause race conditions.

### xunit.runner.json for E2E Tests

Create `tests/CompoundDocs.E2ETests/xunit.runner.json`:

```json
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "parallelizeAssembly": false,
  "parallelizeTestCollections": false,
  "maxParallelThreads": 1,
  "diagnosticMessages": true,
  "longRunningTestSeconds": 60,
  "methodDisplay": "classAndMethod",
  "methodDisplayOptions": "replacePeriodWithComma"
}
```

**Rationale**: E2E tests require shared MCP server and database infrastructure; sequential execution ensures test isolation.

### Ensure xunit.runner.json is Copied to Output

Add to both `CompoundDocs.IntegrationTests.csproj` and `CompoundDocs.E2ETests.csproj`:

```xml
<ItemGroup>
  <Content Include="xunit.runner.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

### Unit Test Project Configuration

The `CompoundDocs.Tests` project does NOT need an `xunit.runner.json` file. By default, xUnit allows parallel test collection execution, which is appropriate for unit tests since they:
- Have no shared state
- Create fresh mocks per test
- Execute quickly (< 100ms each)

Default parallel settings for unit tests:
- `parallelizeAssembly`: true (default)
- `parallelizeTestCollections`: true (default)
- `maxParallelThreads`: CPU core count (default)

### Sample Unit Test Project File

Create/verify `tests/CompoundDocs.Tests/CompoundDocs.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\CompoundDocs.Core\CompoundDocs.Core.csproj" />
    <!-- Add other project references as needed -->
  </ItemGroup>

</Project>
```

**Note**: Package references inherited from `tests/Directory.Build.props`.

---

## Dependencies

### Depends On
- **Phase 109**: Test Project Structure (test project directories must exist)
- **Phase 001**: Solution Structure (solution file and Directory.Packages.props must exist)

### Blocks
- **Phase 111+**: Unit test implementation phases
- **Phase 112+**: Integration test implementation phases
- **Phase 113+**: E2E test implementation phases
- All testing phases require this configuration

---

## Verification Steps

After completing this phase, verify:

1. **Package restore succeeds**:
   ```bash
   dotnet restore
   ```

2. **All test projects build**:
   ```bash
   dotnet build tests/CompoundDocs.Tests/
   dotnet build tests/CompoundDocs.IntegrationTests/
   dotnet build tests/CompoundDocs.E2ETests/
   ```

3. **xunit.runner.json files exist and are valid**:
   ```bash
   cat tests/CompoundDocs.IntegrationTests/xunit.runner.json
   cat tests/CompoundDocs.E2ETests/xunit.runner.json
   ```

4. **Test discovery works** (once sample tests exist):
   ```bash
   dotnet test --list-tests
   ```

5. **Global usings available**: Create a simple test file and verify `[Fact]`, `Mock<T>`, and `ShouldBe()` resolve without explicit using statements.

---

## Key Technical Decisions

### xUnit Version Selection

| Version | Status | Decision |
|---------|--------|----------|
| xUnit 2.9.3 | Stable | **Selected** - Production-ready, well-documented |
| xUnit 3.x | Preview | Deferred - Will evaluate for future migration |

**Rationale**: v3 is still in preview; stable v2.9.3 preferred for production codebase per spec.

### Assertion Library Selection

| Library | License | Decision |
|---------|---------|----------|
| Shouldly 4.2.1+ | MIT (Free) | **Selected** |
| FluentAssertions | Paid ($129.95/dev for v8+) | Rejected |

**Rationale**: Shouldly provides fluent assertions under MIT license; FluentAssertions now requires paid license for commercial use.

### Test Parallelization Strategy

| Project Type | Parallelization | Rationale |
|--------------|-----------------|-----------|
| Unit Tests | Enabled (default) | No shared state, fast execution |
| Integration Tests | Disabled | Shared database via Aspire fixture |
| E2E Tests | Disabled | Shared MCP server infrastructure |

### xunit.runner.json Configuration Options

| Option | Unit | Integration/E2E | Purpose |
|--------|------|-----------------|---------|
| `parallelizeAssembly` | true | false | Assembly-level parallelization |
| `parallelizeTestCollections` | true | false | Collection-level parallelization |
| `maxParallelThreads` | default | 1 | Thread pool size |
| `diagnosticMessages` | false | true | Verbose xUnit diagnostics |
| `longRunningTestSeconds` | 5 | 60 | Warning threshold for slow tests |

---

## Notes

- Moq version 4.20.72+ is specified to avoid the SponsorLink controversy from earlier versions
- The `PrivateAssets` and `IncludeAssets` settings on `xunit.runner.visualstudio` and `coverlet.msbuild` prevent these from being published as dependencies
- If NSubstitute is preferred over Moq in the future, it can be swapped with minimal code changes (similar API surface)
- The global usings reduce boilerplate but explicit imports can still be used when needed
- Consider adding `xunit.analyzers` package in future phases for additional static analysis rules
