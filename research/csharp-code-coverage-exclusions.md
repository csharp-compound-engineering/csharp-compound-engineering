# C# Code Coverage Exclusions with Coverlet

A comprehensive guide to properly excluding code from coverage analysis in .NET projects using Coverlet.

## Table of Contents

1. [The ExcludeFromCodeCoverage Attribute](#the-excludefromcodecoverage-attribute)
2. [Other Exclusion Mechanisms](#other-exclusion-mechanisms)
3. [What SHOULD Be Excluded](#what-should-be-excluded)
4. [What Should NOT Be Excluded](#what-should-not-be-excluded)
5. [Documenting Exclusions with Justifications](#documenting-exclusions-with-justifications)
6. [Complete Configuration Examples](#complete-configuration-examples)

---

## The ExcludeFromCodeCoverage Attribute

### Basic Usage

The `[ExcludeFromCodeCoverage]` attribute from `System.Diagnostics.CodeAnalysis` tells coverage tools to skip the attributed code. It can be applied at multiple levels:

```csharp
using System.Diagnostics.CodeAnalysis;

// Assembly level (in AssemblyInfo.cs or via .csproj)
[assembly: ExcludeFromCodeCoverage]

// Class/struct level - excludes ALL members
[ExcludeFromCodeCoverage]
public class MyClass
{
    public void Method1() { } // Excluded
    public int Property1 { get; set; } // Excluded
}

// Method level
public class MyService
{
    [ExcludeFromCodeCoverage]
    public void InfrastructureMethod() { }

    public void TestedMethod() { } // Still covered
}

// Property level
public class MyEntity
{
    [ExcludeFromCodeCoverage]
    public string GeneratedField { get; set; }

    public string ImportantField { get; set; } // Still covered
}

// Constructor level
public class MyStartup
{
    [ExcludeFromCodeCoverage]
    public MyStartup() { }
}
```

### The Justification Property (.NET 5+)

Starting with .NET 5, the attribute includes a `Justification` property to document why code is excluded:

```csharp
[ExcludeFromCodeCoverage(Justification = "DTO with no business logic")]
public record CustomerDto(string Name, string Email);

[ExcludeFromCodeCoverage(Justification = "Startup infrastructure code - tested via integration tests")]
public class Program
{
    public static void Main(string[] args) { }
}

[ExcludeFromCodeCoverage(Justification = "External API adapter - verified via contract tests")]
public class ExternalServiceClient
{
    public async Task<Response> CallExternalApiAsync() { }
}
```

### Top-Level Statements Workaround (C# 9+)

When using top-level statements, the compiler generates an implicit `Program` class. Use a partial class to exclude it:

```csharp
// In Program.cs, add at the end:
[ExcludeFromCodeCoverage(Justification = "Entry point - infrastructure only")]
public partial class Program { }
```

This works because the compiler generates a `Program` class, and your partial declaration extends it with the attribute.

---

## Other Exclusion Mechanisms

### 1. Assembly-Level Exclusion via .csproj

Exclude an entire assembly (useful for test projects):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <AssemblyAttribute Include="System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute" />
  </ItemGroup>
</Project>
```

For test projects, place this in a `Directory.Build.props` at the tests folder root:

```xml
<Project>
  <ItemGroup>
    <AssemblyAttribute Include="System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute" />
  </ItemGroup>
</Project>
```

### 2. Coverlet Configuration via .runsettings

Create a `coverlet.runsettings` file at your solution root:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat code coverage">
        <Configuration>
          <!-- Output formats -->
          <Format>cobertura,opencover</Format>

          <!-- Assembly/Type filters: [Assembly-Filter]Type-Filter -->
          <Exclude>[*.Tests]*,[*.IntegrationTests]*</Exclude>
          <Include>[MyApp.*]*</Include>

          <!-- Exclude by attribute (short name, full name, or fully qualified) -->
          <ExcludeByAttribute>
            Obsolete,
            GeneratedCodeAttribute,
            CompilerGeneratedAttribute,
            ExcludeFromCodeCoverageAttribute
          </ExcludeByAttribute>

          <!-- Exclude by file path (glob patterns) -->
          <ExcludeByFile>
            **/Migrations/*.cs,
            **/obj/**/*.cs,
            **/*.g.cs,
            **/*.generated.cs,
            **/*.Designer.cs
          </ExcludeByFile>

          <!-- Skip auto-implemented properties -->
          <SkipAutoProps>true</SkipAutoProps>

          <!-- Don't include test assemblies in coverage -->
          <IncludeTestAssembly>false</IncludeTestAssembly>

          <!-- Exclude assemblies without source files -->
          <ExcludeAssembliesWithoutSources>MissingAll</ExcludeAssembliesWithoutSources>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

Run tests with the settings file:

```bash
dotnet test --settings coverlet.runsettings --collect:"XPlat Code Coverage"
```

### 3. MSBuild Integration (Command Line)

Pass exclusion options directly to `dotnet test`:

```bash
# Exclude by attribute (URL-encode comma as %2c)
dotnet test /p:CollectCoverage=true \
  /p:ExcludeByAttribute="Obsolete%2cGeneratedCodeAttribute%2cCompilerGeneratedAttribute"

# Exclude by file path
dotnet test /p:CollectCoverage=true \
  /p:ExcludeByFile="**/Migrations/*.cs%2c**/obj/**/*.cs"

# Exclude by assembly/type filter
dotnet test /p:CollectCoverage=true \
  /p:Exclude="[*.Tests]*%2c[*.IntegrationTests]*"

# Skip auto-properties
dotnet test /p:CollectCoverage=true /p:SkipAutoProps=true

# Combined example
dotnet test /p:CollectCoverage=true \
  /p:Exclude="[*.Tests]*" \
  /p:ExcludeByFile="**/Migrations/*.cs" \
  /p:ExcludeByAttribute="GeneratedCodeAttribute%2cCompilerGeneratedAttribute" \
  /p:SkipAutoProps=true
```

### 4. Filter Expression Syntax

Coverlet supports wildcard patterns for assembly and type filters:

| Pattern | Description |
|---------|-------------|
| `*` | Zero or more characters |
| `?` | Optional single character |
| `[*]*` | All types in all assemblies |
| `[MyAssembly]*` | All types in MyAssembly |
| `[*]MyNamespace.*` | All types in MyNamespace across all assemblies |
| `[MyAssembly]MyNamespace.MyClass` | Specific class |
| `[coverlet.*.tests?]*` | Assemblies matching pattern (tests optional 's') |

**Important:** `Exclude` takes precedence over `Include` when both are specified.

---

## What SHOULD Be Excluded

### 1. Generated Code

**Reason:** Generated code is not under developer control and inflates/deflates metrics unfairly.

```csharp
// These are typically excluded via ExcludeByFile or ExcludeByAttribute
// - *.g.cs (source generators)
// - *.Designer.cs (WinForms/WPF designers)
// - *.generated.cs (T4 templates, etc.)
// - Migrations/*.cs (EF Core migrations)
```

Runsettings configuration:

```xml
<ExcludeByFile>**/*.g.cs,**/*.Designer.cs,**/Migrations/*.cs</ExcludeByFile>
<ExcludeByAttribute>GeneratedCodeAttribute,CompilerGeneratedAttribute</ExcludeByAttribute>
```

### 2. Infrastructure/Startup Code

**Reason:** Entry points and DI configuration are better validated through integration tests.

```csharp
[ExcludeFromCodeCoverage(Justification = "Startup configuration - validated via integration tests")]
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // DI registration
    }

    public void Configure(IApplicationBuilder app)
    {
        // Middleware pipeline
    }
}

// For top-level statements:
[ExcludeFromCodeCoverage(Justification = "Entry point infrastructure")]
public partial class Program { }
```

### 3. DTOs and Records (When Appropriate)

**Reason:** Simple data containers with no logic provide no value when tested.

```csharp
[ExcludeFromCodeCoverage(Justification = "DTO - no business logic")]
public record CreateCustomerRequest(
    string Name,
    string Email,
    string PhoneNumber
);

[ExcludeFromCodeCoverage(Justification = "Response model - data container only")]
public class CustomerResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; }
    public DateTime CreatedAt { get; init; }
}
```

**Note:** Use `SkipAutoProps=true` in Coverlet to automatically exclude auto-implemented property accessors, reducing the need for per-class exclusions on simple DTOs.

### 4. External Dependency Adapters

**Reason:** Code that wraps external APIs should be tested via integration/contract tests, not unit tests.

```csharp
[ExcludeFromCodeCoverage(Justification = "External API wrapper - tested via integration tests")]
public class PaymentGatewayClient : IPaymentGateway
{
    private readonly HttpClient _httpClient;

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request)
    {
        // Direct HTTP calls to external service
    }
}
```

### 5. Exception Handlers and Defensive Code

**Reason:** Some defensive code paths are difficult to trigger in tests without excessive mocking.

```csharp
public class DataProcessor
{
    public void ProcessData(Data data)
    {
        try
        {
            // Main logic - this should be tested
        }
        catch (OutOfMemoryException)
        {
            // Exclude: Genuinely hard to trigger safely in tests
            LogCriticalError();
            throw;
        }
    }

    [ExcludeFromCodeCoverage(Justification = "Critical error handler - cannot safely trigger OOM in tests")]
    private void LogCriticalError() { }
}
```

### 6. Debug-Only Code

**Reason:** Code that only runs in debug builds should not affect production coverage metrics.

```csharp
public class DiagnosticService
{
    [ExcludeFromCodeCoverage(Justification = "Debug diagnostics only")]
    [Conditional("DEBUG")]
    public void DumpState()
    {
        // Debug output
    }
}
```

### 7. Obsolete Code Pending Removal

**Reason:** Code marked for removal should not require new test investment.

```csharp
[Obsolete("Use NewMethod instead")]
[ExcludeFromCodeCoverage(Justification = "Deprecated - scheduled for removal in v3.0")]
public void OldMethod()
{
    // Legacy implementation
}
```

---

## What Should NOT Be Excluded

### Anti-Pattern 1: Excluding Code Because It's "Hard to Test"

**Problem:** This is the most common misuse. Difficult-to-test code often indicates design problems.

```csharp
// BAD: Don't do this
[ExcludeFromCodeCoverage(Justification = "Complex business logic - hard to test")]
public decimal CalculateDiscount(Order order)
{
    // This IS business logic that NEEDS testing!
}
```

**Solution:** Refactor for testability instead:
- Extract dependencies behind interfaces
- Use dependency injection
- Break down complex methods

### Anti-Pattern 2: Excluding Code to Meet Coverage Targets

**Problem:** Gaming metrics defeats the purpose of coverage measurement.

```csharp
// BAD: Excluding to artificially inflate percentage
[ExcludeFromCodeCoverage] // No justification = red flag
public class ImportantBusinessService
{
    public void CriticalOperation() { }
}
```

**Solution:** Set realistic coverage targets and improve tests, not exclusions.

### Anti-Pattern 3: Blanket Assembly Exclusions on Production Code

**Problem:** Excluding entire production assemblies hides real coverage gaps.

```xml
<!-- BAD: Don't exclude production assemblies -->
<Exclude>[MyApp.Core]*,[MyApp.Services]*</Exclude>
```

**Solution:** Only exclude test assemblies and be surgical with production exclusions.

### Anti-Pattern 4: Excluding DTOs with Validation Logic

**Problem:** DTOs with computed properties or validation DO have testable logic.

```csharp
// BAD: This has logic that should be tested!
[ExcludeFromCodeCoverage]
public class OrderDto
{
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }

    // This IS business logic!
    public decimal Total => Subtotal + Tax;

    // This IS validation logic!
    public bool IsValid => Subtotal > 0 && Items.Any();
}
```

**Solution:** Only exclude truly logic-free data containers.

### Anti-Pattern 5: Excluding Error Handling Paths

**Problem:** Error handling is often where bugs hide and should be tested.

```csharp
// BAD: Error paths need testing
public async Task<Result> ProcessAsync()
{
    try
    {
        return await DoWorkAsync();
    }
    catch (InvalidOperationException ex)
    {
        // Don't exclude this - test that errors are handled correctly!
        return Result.Failure(ex.Message);
    }
}
```

**Solution:** Write tests that verify error handling behavior.

### Anti-Pattern 6: Excluding Private Methods

**Problem:** Private methods should be covered through public API tests.

```csharp
public class Calculator
{
    public int Calculate(int a, int b)
    {
        return AddNumbers(a, b); // Test via this public method
    }

    // BAD: Don't exclude - should be covered via Calculate()
    [ExcludeFromCodeCoverage]
    private int AddNumbers(int a, int b) => a + b;
}
```

**Solution:** If private methods aren't covered, add tests for public methods that use them.

---

## Documenting Exclusions with Justifications

### Best Practices for Justification Text

1. **Be Specific:** State exactly why the code is excluded
2. **Reference Testing Strategy:** Mention how the code IS tested (if applicable)
3. **Include Timeline:** For temporary exclusions, note when they'll be addressed
4. **Keep It Brief:** One sentence is usually sufficient

### Good Justification Examples

```csharp
// Infrastructure
[ExcludeFromCodeCoverage(Justification = "DI configuration - validated via integration test suite")]

// External dependencies
[ExcludeFromCodeCoverage(Justification = "HTTP client wrapper - covered by contract tests in ExternalApi.Tests")]

// Generated code
[ExcludeFromCodeCoverage(Justification = "Auto-generated mapping code from AutoMapper")]

// Temporary exclusion
[ExcludeFromCodeCoverage(Justification = "TODO: Add tests - tracked in JIRA-1234, Q2 2024")]

// Genuinely untestable
[ExcludeFromCodeCoverage(Justification = "Native interop - requires hardware not available in CI")]

// Simple data
[ExcludeFromCodeCoverage(Justification = "Immutable DTO with no behavior")]
```

### Bad Justification Examples (Avoid These)

```csharp
// Too vague
[ExcludeFromCodeCoverage(Justification = "Hard to test")]
[ExcludeFromCodeCoverage(Justification = "Not important")]
[ExcludeFromCodeCoverage(Justification = "Will add tests later")]

// No justification at all
[ExcludeFromCodeCoverage]  // <-- Always provide justification!

// Inappropriate reason
[ExcludeFromCodeCoverage(Justification = "Need to hit 80% coverage target")]
```

### Enforcing Justifications

Consider using static analysis rules to enforce justifications. SonarQube/SonarCloud has rule `S6513` that flags `ExcludeFromCodeCoverage` without justification.

---

## Complete Configuration Examples

### Recommended .runsettings for Typical .NET Project

```xml
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat code coverage">
        <Configuration>
          <!-- Output format for CI integration -->
          <Format>cobertura,opencover</Format>

          <!-- Exclude test assemblies -->
          <Exclude>
            [*.Tests]*,
            [*.IntegrationTests]*,
            [*.UnitTests]*,
            [TestUtilities]*
          </Exclude>

          <!-- Exclude common generated/framework attributes -->
          <ExcludeByAttribute>
            Obsolete,
            GeneratedCodeAttribute,
            CompilerGeneratedAttribute,
            ExcludeFromCodeCoverageAttribute,
            DebuggerNonUserCodeAttribute
          </ExcludeByAttribute>

          <!-- Exclude generated files by pattern -->
          <ExcludeByFile>
            **/Migrations/*.cs,
            **/obj/**/*.cs,
            **/*.g.cs,
            **/*.generated.cs,
            **/*.Designer.cs,
            **/GlobalUsings.cs
          </ExcludeByFile>

          <!-- Skip trivial auto-properties -->
          <SkipAutoProps>true</SkipAutoProps>

          <!-- Don't include test assembly code -->
          <IncludeTestAssembly>false</IncludeTestAssembly>

          <!-- Exclude third-party without sources -->
          <ExcludeAssembliesWithoutSources>MissingAll</ExcludeAssembliesWithoutSources>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

### Directory.Build.props for Test Projects

Place at `tests/Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsPublishable>false</IsPublishable>
  </PropertyGroup>

  <!-- Exclude all test assemblies from coverage -->
  <ItemGroup>
    <AssemblyAttribute Include="System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute" />
  </ItemGroup>

  <!-- Common test package references -->
  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.9.0" />
  </ItemGroup>
</Project>
```

### CI Pipeline Command

```bash
dotnet test MySolution.sln \
  --settings coverlet.runsettings \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults \
  /p:Threshold=80 \
  /p:ThresholdType=line \
  /p:ThresholdStat=total
```

---

## Summary of Key Principles

1. **Always provide justification** for exclusions (use the `Justification` property)
2. **Prefer configuration-based exclusions** (runsettings) for patterns over per-class attributes
3. **Be surgical** - exclude specific code, not entire assemblies
4. **Use `SkipAutoProps=true`** to automatically handle simple property accessors
5. **Review exclusions regularly** - temporary exclusions can become permanent accidentally
6. **Never exclude to game metrics** - this defeats the purpose of coverage
7. **Treat exclusions like suppressions** - they should be rare and well-documented

---

## References

- [Microsoft Learn: ExcludeFromCodeCoverageAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.excludefromcodecoverageattribute)
- [Coverlet Documentation: MSBuild Integration](https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/MSBuildIntegration.md)
- [Coverlet Documentation: VSTest Integration](https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/VSTestIntegration.md)
- [Gunnar Peipman: How to Exclude Code from Code Coverage](https://gunnarpeipman.com/aspnet-core-exclude-code-coverage/)
- [Datadog: Ensure Code Coverage Exclusions Are Justified](https://docs.datadoghq.com/security/code_security/static_analysis/static_analysis_rules/csharp-best-practices/coverage-justification/)
- [ExcludeFromCodeCoverage Considered Harmful](https://snape.me/2024/02/excludefromcodecoverage/)
