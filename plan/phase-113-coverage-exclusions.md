# Phase 113: Coverage Exclusion Patterns

> **Status**: NOT_STARTED
> **Effort Estimate**: 3-4 hours
> **Category**: Testing Framework
> **Prerequisites**: Phase 112 (Coverage Thresholds)

---

## Spec References

This phase implements the coverage exclusion patterns defined in:

- **spec/testing.md** - [Coverage Configuration](../spec/testing.md#coverage-configuration) - Exclusion patterns and `ExcludeFromCodeCoverage` attribute usage
- **research/csharp-code-coverage-exclusions.md** - Comprehensive guide to exclusion mechanisms, valid use cases, anti-patterns, and justification best practices

---

## Objectives

1. Configure `ExcludeFromCodeCoverage` attribute recognition in Coverlet
2. Define file path exclusion patterns for generated code
3. Establish namespace and assembly exclusion rules
4. Implement method-level exclusion patterns
5. Create exclusion documentation requirements
6. Establish exclusion review process for code reviews

---

## Acceptance Criteria

### ExcludeFromCodeCoverage Attribute Configuration

- [ ] Coverlet configured to recognize `ExcludeFromCodeCoverage` attribute
- [ ] Attribute recognition via `ExcludeByAttribute` in Directory.Build.props:
  ```xml
  <ExcludeByAttribute>
    Obsolete,
    GeneratedCodeAttribute,
    CompilerGeneratedAttribute,
    ExcludeFromCodeCoverage
  </ExcludeByAttribute>
  ```
- [ ] Attribute can be applied at class, method, property, and constructor levels
- [ ] All exclusions MUST include `Justification` property (per spec requirement)

### Justification Requirement Enforcement

- [ ] All `[ExcludeFromCodeCoverage]` usages include `Justification` parameter:
  ```csharp
  [ExcludeFromCodeCoverage(Justification = "Infrastructure startup")]
  public class Program { }
  ```
- [ ] Justifications must be specific (not "hard to test" or "will add later")
- [ ] Valid justification categories documented:
  - Infrastructure startup code
  - DTO/record with no business logic
  - External API adapter (tested via integration tests)
  - Generated code
  - Debug-only code
  - Obsolete code pending removal

### File Pattern Exclusions

- [ ] Generated file patterns excluded via `ExcludeByFile`:
  ```xml
  <ExcludeByFile>
    **/Migrations/*.cs,
    **/obj/**/*.cs,
    **/*.g.cs,
    **/*.generated.cs,
    **/*.Designer.cs,
    **/GlobalUsings.cs
  </ExcludeByFile>
  ```
- [ ] Program.cs with top-level statements uses partial class workaround:
  ```csharp
  // At end of Program.cs
  [ExcludeFromCodeCoverage(Justification = "Entry point - infrastructure only")]
  public partial class Program { }
  ```
- [ ] EF Core migrations excluded automatically
- [ ] Source generator output (*.g.cs) excluded automatically

### Namespace Exclusions

- [ ] Test project namespaces excluded via assembly filter:
  ```xml
  <Exclude>[*.Tests]*,[*.IntegrationTests]*,[*.E2ETests]*</Exclude>
  ```
- [ ] Only production assemblies included:
  ```xml
  <Include>[CompoundDocs.*]*</Include>
  ```
- [ ] Test helper/utility namespaces excluded from coverage reporting

### Method-Level Exclusions

- [ ] Individual methods can be excluded when class is not excluded:
  ```csharp
  public class MyService
  {
      [ExcludeFromCodeCoverage(Justification = "Native interop - requires hardware")]
      public void HardwareSpecificMethod() { }

      public void TestedMethod() { } // Still covered
  }
  ```
- [ ] Constructor-only exclusions supported:
  ```csharp
  public class MyClass
  {
      [ExcludeFromCodeCoverage(Justification = "Parameterless ctor for DI")]
      public MyClass() { }

      public MyClass(IDependency dep) { } // Tested
  }
  ```
- [ ] Property-level exclusions supported for edge cases

### Auto-Property Handling

- [ ] `SkipAutoProps=true` configured to exclude trivial property accessors
- [ ] DTOs with only auto-properties automatically handled
- [ ] Properties with logic (computed properties, validation) NOT excluded automatically
- [ ] Document which property patterns are auto-excluded vs. require explicit coverage

### Exclusion Documentation Requirements

- [ ] Create `docs/coverage-exclusions.md` with:
  - List of all globally excluded patterns
  - When to use `[ExcludeFromCodeCoverage]`
  - Required justification format
  - Examples of good vs. bad exclusions
- [ ] In-code documentation via justification strings
- [ ] README section linking to exclusion documentation

### Exclusion Review Process

- [ ] PR review checklist includes coverage exclusion review:
  - Every new `[ExcludeFromCodeCoverage]` requires review
  - Justification must be meaningful and specific
  - Reviewer confirms exclusion is appropriate
- [ ] No exclusions without justification pass code review
- [ ] Periodic exclusion audit documented (quarterly review of all exclusions)

---

## Implementation Notes

### Directory.Build.props Configuration

Complete exclusion configuration in `tests/Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <!-- Coverage collection -->
    <CollectCoverage>true</CollectCoverage>

    <!-- Output configuration -->
    <CoverletOutput>$(MSBuildThisFileDirectory)../coverage/</CoverletOutput>
    <CoverletOutputFormat>cobertura</CoverletOutputFormat>

    <!-- 100% threshold enforcement (from Phase 112) -->
    <Threshold>100</Threshold>
    <ThresholdType>line,branch,method</ThresholdType>
    <ThresholdStat>total</ThresholdStat>

    <!-- Exclusion by attribute -->
    <ExcludeByAttribute>
      Obsolete,
      GeneratedCodeAttribute,
      CompilerGeneratedAttribute,
      ExcludeFromCodeCoverage
    </ExcludeByAttribute>

    <!-- Skip auto-implemented properties -->
    <SkipAutoProps>true</SkipAutoProps>

    <!-- Assembly/namespace filters -->
    <Include>[CompoundDocs.*]*</Include>
    <Exclude>[*.Tests]*,[*.IntegrationTests]*,[*.E2ETests]*</Exclude>

    <!-- File pattern exclusions -->
    <ExcludeByFile>
      **/Migrations/*.cs,
      **/obj/**/*.cs,
      **/*.g.cs,
      **/*.generated.cs,
      **/*.Designer.cs,
      **/GlobalUsings.cs
    </ExcludeByFile>
  </PropertyGroup>
</Project>
```

### Valid Exclusion Examples

```csharp
using System.Diagnostics.CodeAnalysis;

// Infrastructure startup
[ExcludeFromCodeCoverage(Justification = "Entry point - validated via integration tests")]
public partial class Program { }

// DTO with no logic
[ExcludeFromCodeCoverage(Justification = "DTO - no business logic")]
public record DocumentDto(string Path, string Content, string Hash);

// External adapter
[ExcludeFromCodeCoverage(Justification = "Ollama HTTP client - tested via integration tests")]
public class OllamaHttpClient : IOllamaClient
{
    // Direct HTTP calls
}

// Debug-only code
[ExcludeFromCodeCoverage(Justification = "Debug diagnostics only")]
[Conditional("DEBUG")]
public void DumpState() { }

// Obsolete code
[Obsolete("Use NewMethod instead - removal in v2.0")]
[ExcludeFromCodeCoverage(Justification = "Deprecated - scheduled for removal")]
public void OldMethod() { }
```

### Invalid Exclusion Anti-Patterns

```csharp
// BAD: No justification
[ExcludeFromCodeCoverage]  // REJECTED - always requires justification
public class MyService { }

// BAD: Vague justification
[ExcludeFromCodeCoverage(Justification = "Hard to test")]  // REJECTED
public void ComplexLogic() { }

// BAD: Excluding business logic
[ExcludeFromCodeCoverage(Justification = "Complex calculations")]  // REJECTED
public decimal CalculateDiscount(Order order) { }

// BAD: Excluding to meet coverage targets
[ExcludeFromCodeCoverage(Justification = "Need to hit 100%")]  // REJECTED
public void UncoveredMethod() { }

// BAD: Excluding error handling
[ExcludeFromCodeCoverage(Justification = "Exception handling")]  // REJECTED
public void HandleError(Exception ex) { }  // This SHOULD be tested
```

### Coverage Exclusion Documentation Template

Create `docs/coverage-exclusions.md`:

```markdown
# Coverage Exclusion Patterns

## Global Exclusions (Configuration-Based)

### By Attribute
| Attribute | Auto-Excluded | Notes |
|-----------|---------------|-------|
| `[Obsolete]` | Yes | Deprecated code pending removal |
| `[GeneratedCodeAttribute]` | Yes | Source generators, T4 templates |
| `[CompilerGeneratedAttribute]` | Yes | Records, lambdas, async state machines |
| `[ExcludeFromCodeCoverage]` | Yes | Explicit exclusions (requires justification) |

### By File Pattern
| Pattern | Purpose |
|---------|---------|
| `**/Migrations/*.cs` | EF Core database migrations |
| `**/*.g.cs` | Source generator output |
| `**/*.generated.cs` | T4 and other generated code |
| `**/*.Designer.cs` | WinForms/WPF designers |
| `**/GlobalUsings.cs` | Global using directives |

### By Assembly
| Pattern | Excluded |
|---------|----------|
| `[*.Tests]*` | Unit test assemblies |
| `[*.IntegrationTests]*` | Integration test assemblies |
| `[*.E2ETests]*` | End-to-end test assemblies |

## Manual Exclusions (Attribute-Based)

### When to Use `[ExcludeFromCodeCoverage]`

Valid use cases:
1. **Infrastructure/Startup**: Entry points, DI configuration
2. **DTOs/Records**: Data containers with no business logic
3. **External Adapters**: HTTP clients wrapping external APIs
4. **Debug-Only**: Code conditional on DEBUG compilation
5. **Obsolete Code**: Scheduled for removal, not worth testing

### Justification Requirements

Every `[ExcludeFromCodeCoverage]` MUST include a `Justification`:

```csharp
// GOOD
[ExcludeFromCodeCoverage(Justification = "Entry point - validated via integration tests")]

// BAD - will fail code review
[ExcludeFromCodeCoverage]
```

### Exclusion Review Checklist

For PRs adding new exclusions:
- [ ] Justification is specific and meaningful
- [ ] Code genuinely cannot be unit tested
- [ ] Alternative testing strategy exists (integration, E2E)
- [ ] Not excluding business logic
- [ ] Not gaming coverage metrics
```

### PR Review Checklist Addition

Add to `.github/PULL_REQUEST_TEMPLATE.md`:

```markdown
## Coverage Exclusions

- [ ] No new `[ExcludeFromCodeCoverage]` attributes added
  - OR -
- [ ] New exclusions reviewed and approved:
  - [ ] Each has a meaningful `Justification` property
  - [ ] Code genuinely cannot be unit tested
  - [ ] Alternative testing strategy documented
```

---

## Dependencies

### Depends On
- Phase 112: Coverage Thresholds (100% coverage enforcement baseline)

### Blocks
- Phase 114: xUnit Configuration (runner settings depend on coverage config)
- Phase 115: Test Project Setup (uses exclusion patterns)

---

## Verification Steps

After completing this phase, verify:

1. **Attribute Recognition**: `[ExcludeFromCodeCoverage]` excludes marked code
2. **File Patterns**: Generated files (*.g.cs, Migrations) excluded automatically
3. **SkipAutoProps**: Auto-properties in DTOs don't require explicit exclusion
4. **Namespace Filters**: Test assemblies excluded from coverage
5. **Justification Enforcement**: Code review process catches unjustified exclusions
6. **Documentation**: Exclusion guide exists and is linked from README

### Manual Verification

```bash
# Create a test class with exclusion
echo '[ExcludeFromCodeCoverage(Justification = "Test")] public class ExcludedClass { }' > src/CompoundDocs.Core/ExcludedClass.cs

# Run coverage
dotnet test tests/CompoundDocs.Tests --collect:"XPlat Code Coverage"

# Verify ExcludedClass not in coverage report
# Check coverage.cobertura.xml for absence of ExcludedClass
```

### Negative Tests

| Scenario | Expected Behavior |
|----------|-------------------|
| Exclusion without justification | Code review rejection |
| Excluding business logic | Code review rejection |
| Excluding error handling | Code review rejection |
| Legitimate infrastructure exclusion | Accepted with justification |

---

## Files to Create/Modify

### New Files

| File | Purpose |
|------|---------|
| `docs/coverage-exclusions.md` | Exclusion patterns documentation |

### Modified Files

| File | Changes |
|------|---------|
| `tests/Directory.Build.props` | Add complete exclusion configuration |
| `src/CompoundDocs.Server/Program.cs` | Add partial class with exclusion |
| `.github/PULL_REQUEST_TEMPLATE.md` | Add exclusion review checklist |

---

## Notes

- The 100% coverage target (Phase 112) is achievable specifically because proper exclusions are available
- Exclusions should be rare and well-documented; they are not a way to avoid testing
- The `Justification` property is available in .NET 5+ and MUST be used
- Consider future tooling: SonarQube rule S6513 flags exclusions without justification
- Periodic audit (quarterly) ensures exclusions remain valid as code evolves
- SkipAutoProps handles most DTO scenarios without explicit attributes
