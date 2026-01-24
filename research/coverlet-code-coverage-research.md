# Coverlet Code Coverage for .NET - Research Report

> **Status**: Research Complete
> **Date**: 2026-01-23
> **Purpose**: Inform testing strategy for csharp-compounding-docs MCP server

---

## Executive Summary

Coverlet is the leading open-source code coverage library for .NET. This report covers configuration for 100% coverage enforcement with threshold-based build failures, suitable for the `csharp-compounding-docs` MCP server project.

---

## 1. Package Setup

### Current Stable Version: 6.0.4

**Requirements for .NET 8+:**
- .NET SDK 8.0.112 or newer
- Microsoft.NET.Test.Sdk 17.12.0 or above

### Package Choice: `coverlet.msbuild` vs `coverlet.collector`

| Feature | coverlet.msbuild | coverlet.collector |
|---------|------------------|-------------------|
| Threshold enforcement | Yes | No |
| Console output | Yes | Limited |
| Report merging | Yes | No |
| Integration | MSBuild props | VSTest data collector |
| Command | `/p:CollectCoverage=true` | `--collect:"XPlat Code Coverage"` |

**Recommendation for MCP server project:** Use `coverlet.msbuild` for threshold enforcement and build failure capability.

**Important:** Never add both packages to the same test project.

### Test Project Package Reference

```xml
<ItemGroup>
  <PackageReference Include="coverlet.msbuild" Version="6.0.4">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
  </PackageReference>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
  <PackageReference Include="xunit" Version="2.9.2" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
</ItemGroup>
```

---

## 2. MSBuild Configuration for 100% Coverage Enforcement

### Directory.Build.props (in tests/ directory)

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
    <ExcludeByAttribute>Obsolete,GeneratedCodeAttribute,CompilerGeneratedAttribute,ExcludeFromCodeCoverage</ExcludeByAttribute>
    <SkipAutoProps>true</SkipAutoProps>

    <!-- Include only production assemblies -->
    <Include>[MyMcpServer.*]*</Include>
    <Exclude>[*.Tests]*,[*.TestUtilities]*</Exclude>
  </PropertyGroup>
</Project>
```

### Alternative: Test Project csproj Configuration

```xml
<PropertyGroup>
  <CollectCoverage>true</CollectCoverage>
  <CoverletOutputFormat>cobertura</CoverletOutputFormat>
  <Threshold>100</Threshold>
  <ThresholdType>line,branch,method</ThresholdType>
  <ThresholdStat>total</ThresholdStat>
</PropertyGroup>
```

---

## 3. Exclusion Patterns

### By Attribute (Most Common)

```xml
<ExcludeByAttribute>
  Obsolete,
  GeneratedCodeAttribute,
  CompilerGeneratedAttribute,
  ExcludeFromCodeCoverage
</ExcludeByAttribute>
```

**Usage in code:**
```csharp
using System.Diagnostics.CodeAnalysis;

[ExcludeFromCodeCoverage]
public class MyDto
{
    public string Name { get; set; }
    public int Value { get; set; }
}

[ExcludeFromCodeCoverage(Justification = "Infrastructure code")]
public class StartupConfiguration { }
```

### By File Pattern (Globbing)

```xml
<ExcludeByFile>
  **/*.g.cs,
  **/*.designer.cs,
  **/Migrations/*.cs,
  **/obj/**/*.cs
</ExcludeByFile>
```

### By Assembly/Namespace Filter

Filter syntax: `[Assembly-Filter]Type-Filter`
- `*` matches zero or more characters
- `?` matches a single optional character

```xml
<!-- Exclude all types in assemblies starting with "Generated" -->
<Exclude>[Generated.*]*</Exclude>

<!-- Exclude specific namespace -->
<Exclude>[*]MyApp.Infrastructure.Migrations.*</Exclude>

<!-- Include only specific assemblies -->
<Include>[MyMcpServer]*,[MyMcpServer.Core]*</Include>
```

### Command Line (with escaping for multiple values)

```bash
dotnet test /p:CollectCoverage=true \
  /p:Exclude=\"[*.Tests]*,[*]*.Migrations.*\" \
  /p:ExcludeByAttribute=\"GeneratedCodeAttribute,CompilerGeneratedAttribute\"
```

---

## 4. Threshold Enforcement

### Threshold Types

| ThresholdType | Description |
|---------------|-------------|
| `line` | Percentage of executable lines covered |
| `branch` | Percentage of conditional branches covered |
| `method` | Percentage of methods with at least one line covered |

### ThresholdStat Options

| ThresholdStat | Behavior |
|---------------|----------|
| `Minimum` (default) | Each module must individually meet threshold |
| `Total` | Combined coverage across all modules must meet threshold |
| `Average` | Average coverage across modules must meet threshold |

### Configuration Examples

**Strict 100% enforcement (all metrics):**
```xml
<Threshold>100</Threshold>
<ThresholdType>line,branch,method</ThresholdType>
<ThresholdStat>total</ThresholdStat>
```

**Tiered thresholds:**
```bash
dotnet test /p:CollectCoverage=true \
  /p:Threshold=\"100,90,100\" \
  /p:ThresholdType=\"line,branch,method\"
```

**Build failure behavior:** When coverage falls below threshold, `dotnet test` exits with a non-zero code, failing CI builds.

---

## 5. Report Formats

### Format Comparison

| Format | Best For | Notes |
|--------|----------|-------|
| **cobertura** | GitHub Actions, Azure DevOps | Industry standard, wide CI support |
| **opencover** | Codecov, detailed analysis | More branch detail, not deterministic-build compatible |
| **lcov** | SonarQube, local HTML reports | Common Linux format |
| **json** | Coverlet internal, merging | Coverlet's native format |

### Recommendation for GitHub Actions

**Use Cobertura** - it's the default format and has the widest CI/CD support.

```xml
<CoverletOutputFormat>cobertura</CoverletOutputFormat>
```

**Multiple formats (if needed):**
```xml
<CoverletOutputFormat>cobertura,opencover</CoverletOutputFormat>
```

---

## 6. xUnit Integration

### Runsettings File (coverage.runsettings)

For projects using `coverlet.collector` with xUnit:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat code coverage">
        <Configuration>
          <Format>cobertura</Format>
          <ExcludeByAttribute>
            Obsolete,
            GeneratedCodeAttribute,
            CompilerGeneratedAttribute,
            ExcludeFromCodeCoverage
          </ExcludeByAttribute>
          <ExcludeByFile>**/*.g.cs,**/*.designer.cs</ExcludeByFile>
          <SkipAutoProps>true</SkipAutoProps>
          <IncludeTestAssembly>false</IncludeTestAssembly>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

**Usage:**
```bash
dotnet test --settings coverage.runsettings --collect:"XPlat Code Coverage"
```

### xUnit-Specific Considerations

- New xUnit projects (`dotnet new xunit`) include `coverlet.collector` by default
- Remove `coverlet.collector` if using `coverlet.msbuild` for threshold enforcement
- No special xUnit configuration needed - Coverlet instruments assemblies transparently

---

## 7. Branch Coverage vs Line Coverage Best Practices

### Key Insight

100% line coverage does NOT guarantee 100% branch coverage. Example:

```csharp
public string GetMessage(bool flag)
{
    if (flag)
        return "Yes";
    return "No";  // If only called with true, line coverage = 100%, branch = 50%
}
```

### Recommendations

1. **Enforce both metrics:** `<ThresholdType>line,branch</ThresholdType>`

2. **Branch coverage is stricter:** It ensures all conditional paths are tested

3. **Practical thresholds for new projects targeting 100%:**
   ```xml
   <Threshold>100</Threshold>
   <ThresholdType>line,branch,method</ThresholdType>
   ```

4. **For legacy projects being improved:**
   ```xml
   <Threshold>80,70,90</Threshold>
   <ThresholdType>line,branch,method</ThresholdType>
   ```

---

## 8. Complete Example: GitHub Actions Workflow

```yaml
name: Build and Test

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore

      - name: Test with Coverage
        run: |
          dotnet test --no-build \
            /p:CollectCoverage=true \
            /p:CoverletOutputFormat=cobertura \
            /p:CoverletOutput=./coverage/ \
            /p:Threshold=100 \
            /p:ThresholdType=line,branch,method \
            /p:ThresholdStat=total

      - name: Upload Coverage Report
        uses: actions/upload-artifact@v4
        with:
          name: coverage-report
          path: ./coverage/coverage.cobertura.xml
```

---

## 9. Troubleshooting Common Issues

### Issue: Coverage Not Collected

**Cause**: Missing `Microsoft.NET.Test.Sdk` package or incorrect version.

**Solution**: Ensure version 17.12.0+ is installed:
```xml
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
```

### Issue: Threshold Not Enforced

**Cause**: Using `coverlet.collector` instead of `coverlet.msbuild`.

**Solution**: Switch to `coverlet.msbuild`:
```xml
<PackageReference Include="coverlet.msbuild" Version="6.0.4">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
```

### Issue: Generated Code Affecting Coverage

**Cause**: Source generators or EF Core migrations included in coverage.

**Solution**: Add exclusion patterns:
```xml
<ExcludeByFile>**/*.g.cs,**/Migrations/*.cs</ExcludeByFile>
<ExcludeByAttribute>GeneratedCodeAttribute,CompilerGeneratedAttribute</ExcludeByAttribute>
```

### Issue: Coverage Report Shows 0%

**Cause**: Include/Exclude filters too restrictive.

**Solution**: Verify assembly names match your project:
```xml
<!-- Debug: Temporarily remove filters -->
<Include></Include>
<Exclude></Exclude>
```

---

## 10. Integration with Coverage Services

### Codecov

```yaml
- name: Upload to Codecov
  uses: codecov/codecov-action@v4
  with:
    files: ./coverage/coverage.cobertura.xml
    fail_ci_if_error: true
```

### Coveralls

```yaml
- name: Upload to Coveralls
  uses: coverallsapp/github-action@v2
  with:
    file: ./coverage/coverage.cobertura.xml
```

### Azure DevOps

```yaml
- task: PublishCodeCoverageResults@2
  inputs:
    codeCoverageTool: 'Cobertura'
    summaryFileLocation: '$(Build.SourcesDirectory)/coverage/coverage.cobertura.xml'
```

---

## Sources

### Official Documentation
- [Coverlet GitHub Repository](https://github.com/coverlet-coverage/coverlet)
- [Coverlet MSBuild Integration Documentation](https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/MSBuildIntegration.md)
- [Coverlet VSTest Integration Documentation](https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/VSTestIntegration.md)

### Microsoft Learn
- [Code Coverage for Unit Testing](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-code-coverage)

### Package References
- [coverlet.msbuild NuGet](https://www.nuget.org/packages/coverlet.msbuild)
- [coverlet.collector NuGet](https://www.nuget.org/packages/coverlet.collector)

### Best Practices
- [Line vs Branch Coverage - Codecov Blog](https://about.codecov.io/blog/line-or-branch-coverage-which-type-is-right-for-you/)
- [Steve Smith (Ardalis) - Line vs Branch Coverage](https://ardalis.com/which-is-more-important-line-coverage-or-branch-coverage/)
