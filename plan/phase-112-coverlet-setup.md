# Phase 112: Coverlet Code Coverage Setup

> **Status**: NOT_STARTED
> **Effort Estimate**: 2-3 hours
> **Category**: Testing Framework
> **Prerequisites**: Phase 110 (xUnit Test Framework Setup)

---

## Spec References

This phase implements code coverage enforcement as defined in:

- **spec/testing.md** - [Coverage Configuration](../spec/testing.md#coverage-configuration) (100% code coverage enforcement)
- **spec/testing.md** - [Technology Stack](../spec/testing.md#technology-stack) (coverlet.msbuild 6.0.4+)
- **research/coverlet-code-coverage-research.md** - Coverlet configuration and threshold enforcement patterns
- **research/csharp-code-coverage-exclusions.md** - Exclusion patterns and justification guidelines

---

## Objectives

1. Add `coverlet.msbuild` package reference to all test projects
2. Configure coverage collection via `tests/Directory.Build.props`
3. Enforce 100% threshold on line, branch, and method coverage
4. Output coverage reports in Cobertura format
5. Configure proper exclusion patterns for generated code and infrastructure
6. Ensure coverage failures block test execution (fail the build)

---

## Acceptance Criteria

- [ ] `coverlet.msbuild` package (6.0.4+) added to all test projects
- [ ] `tests/Directory.Build.props` contains complete coverage configuration
- [ ] Running `dotnet test` collects coverage and enforces thresholds
- [ ] Coverage threshold set to 100% for line, branch, and method
- [ ] Cobertura XML report generated at `coverage/` directory
- [ ] Test assemblies excluded from coverage analysis
- [ ] Generated code excluded via `ExcludeByAttribute`:
  - [ ] `Obsolete`
  - [ ] `GeneratedCodeAttribute`
  - [ ] `CompilerGeneratedAttribute`
  - [ ] `ExcludeFromCodeCoverage`
- [ ] Auto-implemented properties excluded via `SkipAutoProps`
- [ ] Coverage failure causes non-zero exit code (fails CI)
- [ ] Production assemblies correctly included via `[CompoundDocs.*]*` filter

---

## Implementation Notes

### Package Reference Configuration

Add to each test project's `.csproj` file. The package reference must use `PrivateAssets` to prevent transitive inclusion:

```xml
<ItemGroup>
  <PackageReference Include="coverlet.msbuild" Version="6.0.4">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
  </PackageReference>
</ItemGroup>
```

**Critical:** Do NOT mix `coverlet.msbuild` with `coverlet.collector`. Remove any `coverlet.collector` references if present. Only `coverlet.msbuild` supports threshold enforcement.

### tests/Directory.Build.props

Create or update the shared configuration file at `tests/Directory.Build.props`:

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

  <!-- Exclude test assemblies from coverage analysis -->
  <ItemGroup>
    <AssemblyAttribute Include="System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute" />
  </ItemGroup>
</Project>
```

### Coverage Output Directory

Create the `coverage/` directory at the repository root with a `.gitkeep` file:

```
csharp-compound-engineering/
├── coverage/
│   └── .gitkeep
└── tests/
    └── Directory.Build.props
```

Add `coverage/` contents (except `.gitkeep`) to `.gitignore`:

```gitignore
# Coverage reports
coverage/*
!coverage/.gitkeep
```

### Threshold Configuration

The threshold configuration enforces 100% coverage across three dimensions:

| Property | Value | Description |
|----------|-------|-------------|
| `Threshold` | `100` | Percentage required to pass |
| `ThresholdType` | `line,branch,method` | All three metrics enforced |
| `ThresholdStat` | `total` | Combined coverage across all assemblies |

**Threshold Types Explained:**

- **Line Coverage**: Percentage of executable lines executed
- **Branch Coverage**: Percentage of conditional branches taken (if/else, switch, ternary)
- **Method Coverage**: Percentage of methods with at least one executed line

### Exclusion Pattern Details

#### ExcludeByAttribute

Coverlet automatically excludes code marked with these attributes:

| Attribute | Purpose |
|-----------|---------|
| `Obsolete` | Deprecated code pending removal |
| `GeneratedCodeAttribute` | Source generator output |
| `CompilerGeneratedAttribute` | Compiler-synthesized code (records, async state machines) |
| `ExcludeFromCodeCoverage` | Explicit developer exclusion |

#### SkipAutoProps

Setting `<SkipAutoProps>true</SkipAutoProps>` excludes auto-implemented property accessors:

```csharp
// These trivial accessors are excluded from coverage
public string Name { get; set; }
public int Value { get; init; }
```

#### Assembly Filters

Filter syntax: `[Assembly-Filter]Type-Filter`

- `*` matches zero or more characters
- `[CompoundDocs.*]*` includes all types in CompoundDocs.* assemblies
- `[*.Tests]*` excludes all test assemblies

### Using ExcludeFromCodeCoverage in Production Code

When excluding code, always provide a justification:

```csharp
using System.Diagnostics.CodeAnalysis;

// Infrastructure startup - validated via integration tests
[ExcludeFromCodeCoverage(Justification = "Entry point infrastructure")]
public partial class Program { }

// Simple data transfer object
[ExcludeFromCodeCoverage(Justification = "DTO with no business logic")]
public record DocumentDto(string Path, string Content, DateTimeOffset IndexedAt);

// External service wrapper
[ExcludeFromCodeCoverage(Justification = "HTTP client wrapper - covered by integration tests")]
public class OllamaHttpClient { }
```

**Valid exclusion scenarios:**
- Infrastructure startup code (Program.cs, DI configuration)
- DTOs and records with no business logic
- External service adapters tested via integration tests
- Generated code not caught by attribute exclusions
- Database migrations

**Invalid exclusion scenarios (do not exclude):**
- Business logic that is "hard to test"
- Error handling paths
- Code excluded solely to meet coverage targets
- Private methods (should be covered via public API tests)

### Running Tests with Coverage

```bash
# Run all tests with coverage collection and threshold enforcement
dotnet test

# Explicit coverage parameters (for debugging)
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Run with verbose output to see coverage details
dotnet test --verbosity normal

# Run specific test category
dotnet test --filter "Category=Unit"
```

### Coverage Report Location

After running tests, the Cobertura XML report is generated at:

```
coverage/coverage.cobertura.xml
```

This format is compatible with:
- GitHub Actions coverage reporting
- Azure DevOps code coverage
- Codecov/Coveralls integration
- ReportGenerator HTML visualization

---

## Dependencies

### Depends On
- **Phase 110**: xUnit Test Framework Setup (requires test projects to exist)

### Blocks
- **Phase 113**: ReportGenerator Setup (requires coverage output)
- **Phase 114**: CI/CD Pipeline (requires coverage enforcement)
- All test phases (coverage must pass before tests are considered complete)

---

## Verification Steps

After completing this phase, verify:

1. **Package installed**: `dotnet list package` shows `coverlet.msbuild` in test projects

2. **Configuration exists**: `tests/Directory.Build.props` contains all coverage settings

3. **Coverage collected**: Run `dotnet test` and verify:
   - Coverage percentage displayed in output
   - `coverage/coverage.cobertura.xml` file generated

4. **Threshold enforced**: Temporarily comment out a test and verify:
   - `dotnet test` fails with non-zero exit code
   - Error message indicates coverage threshold violation

5. **Exclusions working**: Add `[ExcludeFromCodeCoverage]` to a class and verify:
   - Coverage percentage unchanged or improved
   - Excluded code not counted in threshold calculation

6. **Assembly filters correct**: Verify coverage report only includes:
   - `CompoundDocs.McpServer`
   - `CompoundDocs.Common`
   - `CompoundDocs.Cleanup`
   - NOT test assemblies

---

## Troubleshooting

### Coverage Shows 0%

**Cause**: Include/Exclude filters misconfigured or assembly names don't match.

**Solution**: Temporarily remove filters and verify assembly names:
```xml
<Include></Include>
<Exclude></Exclude>
```

### Threshold Not Enforced

**Cause**: Using `coverlet.collector` instead of `coverlet.msbuild`.

**Solution**: Remove `coverlet.collector` and add `coverlet.msbuild`:
```xml
<!-- Remove this -->
<PackageReference Include="coverlet.collector" ... />

<!-- Add this -->
<PackageReference Include="coverlet.msbuild" Version="6.0.4">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
```

### Generated Code Affecting Coverage

**Cause**: Source generator output included in coverage.

**Solution**: Add file exclusion pattern:
```xml
<ExcludeByFile>**/*.g.cs,**/obj/**/*.cs</ExcludeByFile>
```

### Branch Coverage Lower Than Line Coverage

**Cause**: Conditional paths not fully tested.

**Example**:
```csharp
// Line coverage: 100% if called with true
// Branch coverage: 50% if never called with false
public string GetStatus(bool isActive) => isActive ? "Active" : "Inactive";
```

**Solution**: Add tests for all conditional branches.

---

## Notes

- The 100% threshold is achievable with proper use of `[ExcludeFromCodeCoverage]` exclusions
- Exclusions should be rare and always include a `Justification` string
- Coverage enforcement happens at build time, not as a separate CI step
- Failed coverage threshold results in `dotnet test` exit code 2
- Cobertura format is industry standard and works with most CI/CD tools
- The `ThresholdStat=total` setting means coverage is calculated across all assemblies combined, not per-assembly
