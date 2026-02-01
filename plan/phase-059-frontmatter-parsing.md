# Phase 059: YAML Frontmatter Parsing

> **Status**: NOT_STARTED
> **Effort Estimate**: 6-8 hours
> **Category**: Document Processing
> **Prerequisites**: Phase 015 (Markdown Parser Integration)

---

## Spec References

This phase implements YAML frontmatter parsing defined in:

- **spec/doc-types.md** - [Common Fields](../spec/doc-types.md#common-fields) (lines 77-99)
- **spec/doc-types/built-in-types.md** - Complete schemas for all 5 built-in types
- **research/dotnet-markdown-parser-research.md** - YamlDotNet integration examples (lines 82-95, 350-355)

---

## Objectives

1. Implement robust YAML frontmatter extraction from markdown documents
2. Create strongly-typed frontmatter models aligned with doc-type schemas
3. Integrate YamlDotNet for deserialization with proper configuration
4. Handle common required fields (`doc_type`, `title`, `date`, `summary`, `significance`)
5. Support optional fields (`tags`, `related_docs`, `supersedes`, `promotion_level`)
6. Implement comprehensive parse error handling with meaningful messages
7. Handle documents without frontmatter gracefully

---

## Acceptance Criteria

### Core Frontmatter Extraction
- [ ] Extract YAML frontmatter delimited by `---` markers
- [ ] Preserve source location (start/end line numbers) for error reporting
- [ ] Handle documents with no frontmatter (return null/empty result)
- [ ] Handle empty frontmatter blocks (`---\n---`)
- [ ] Handle frontmatter with only whitespace

### YamlDotNet Integration
- [ ] YamlDotNet deserializer configured with `IgnoreUnmatchedProperties()`
- [ ] Support snake_case YAML keys mapping to PascalCase C# properties
- [ ] Configure naming convention to handle `doc_type` -> `DocType` mapping
- [ ] Thread-safe deserializer instance (singleton or factory pattern)

### Common Required Fields
- [ ] `doc_type` - string, identifies document type (problem, insight, codebase, tool, style)
- [ ] `title` - string, human-readable title
- [ ] `date` - DateTime, parsed from YYYY-MM-DD format
- [ ] `summary` - string, one-line summary for search results
- [ ] `significance` - enum, values: `architectural`, `behavioral`, `performance`, `correctness`, `convention`, `integration`

### Common Optional Fields
- [ ] `tags` - array of strings, searchable keywords
- [ ] `related_docs` - array of strings, relative paths to related documents
- [ ] `supersedes` - string, path to document this replaces
- [ ] `promotion_level` - enum, values: `standard`, `important`, `critical` (default: `standard`)

### Type-Specific Required Fields
- [ ] Problem: `problem_type`, `symptoms`, `root_cause`, `solution`
- [ ] Insight: `insight_type`, `impact_area`
- [ ] Codebase: `knowledge_type`, `scope`
- [ ] Tool: `tool_name`, `version`, `knowledge_type`
- [ ] Style: `style_type`, `scope`, `rationale`

### Type-Specific Optional Fields
- [ ] Problem: `component`, `severity`, `prevention`
- [ ] Insight: `confidence`, `source`
- [ ] Codebase: `files_involved`, `alternatives_considered`, `trade_offs`
- [ ] Tool: `official_docs_gap`, `related_tools`
- [ ] Style: `examples`, `exceptions`

### Error Handling
- [ ] Invalid YAML syntax errors include line/column information
- [ ] Missing required field errors list the missing field name
- [ ] Invalid enum value errors list valid options
- [ ] Date format errors specify expected format (YYYY-MM-DD)
- [ ] Type mismatch errors describe expected vs actual type
- [ ] All errors are aggregated (not fail-fast) for comprehensive feedback

### Frontmatter-less Document Handling
- [ ] Documents without frontmatter return `FrontmatterResult` with `IsPresent = false`
- [ ] Processing continues normally (frontmatter is optional for some operations)
- [ ] Warning logged when frontmatter expected but missing
- [ ] Validation can be skipped for frontmatter-less documents

### Testing
- [ ] Unit tests for each common required field
- [ ] Unit tests for each common optional field
- [ ] Unit tests for each doc-type's specific fields
- [ ] Unit tests for all enum values (significance, promotion_level, doc-type enums)
- [ ] Unit tests for malformed YAML (syntax errors)
- [ ] Unit tests for type mismatches (string where array expected, etc.)
- [ ] Unit tests for missing delimiters
- [ ] Unit tests for frontmatter-less documents
- [ ] Test coverage meets 100% line/branch/method requirement

---

## Implementation Notes

### Significance Enum Definition

```csharp
namespace CompoundDocs.Common.Models.Enums;

/// <summary>
/// Why a documented item matters to the codebase.
/// </summary>
public enum Significance
{
    /// <summary>System structure or design decisions</summary>
    Architectural,

    /// <summary>Runtime behavior or logic patterns</summary>
    Behavioral,

    /// <summary>Speed, memory, or resource usage</summary>
    Performance,

    /// <summary>Bug fixes or data integrity</summary>
    Correctness,

    /// <summary>Coding standards or team preferences</summary>
    Convention,

    /// <summary>External system or API interactions</summary>
    Integration
}
```

### Promotion Level Enum Definition

```csharp
namespace CompoundDocs.Common.Models.Enums;

/// <summary>
/// Document visibility tier affecting RAG retrieval ranking.
/// </summary>
public enum PromotionLevel
{
    /// <summary>Normal visibility (default)</summary>
    Standard,

    /// <summary>Boosted in search results</summary>
    Important,

    /// <summary>Always included in relevant queries</summary>
    Critical
}
```

### Enhanced Base Frontmatter Model

```csharp
using CompoundDocs.Common.Models.Enums;

namespace CompoundDocs.Common.Models.Frontmatter;

/// <summary>
/// Base frontmatter fields common to all doc-types.
/// </summary>
public abstract class BaseFrontmatter
{
    /// <summary>Document type identifier (problem, insight, codebase, tool, style)</summary>
    public required string DocType { get; set; }

    /// <summary>Human-readable title</summary>
    public required string Title { get; set; }

    /// <summary>Date captured (YYYY-MM-DD)</summary>
    public required DateTime Date { get; set; }

    /// <summary>One-line summary for search results</summary>
    public required string Summary { get; set; }

    /// <summary>Why this matters</summary>
    public required Significance Significance { get; set; }

    // Optional fields with defaults

    /// <summary>Searchable keywords</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Relative paths to related documents</summary>
    public List<string> RelatedDocs { get; set; } = [];

    /// <summary>Path to document this replaces</summary>
    public string? Supersedes { get; set; }

    /// <summary>Document visibility tier</summary>
    public PromotionLevel PromotionLevel { get; set; } = PromotionLevel.Standard;
}
```

### Problem-Specific Enums and Model

```csharp
namespace CompoundDocs.Common.Models.Enums;

public enum ProblemType
{
    Bug,
    Configuration,
    Integration,
    Performance,
    Security,
    Data
}

public enum Severity
{
    Low,
    Medium,
    High,
    Critical
}

namespace CompoundDocs.Common.Models.Frontmatter;

public class ProblemFrontmatter : BaseFrontmatter
{
    public required ProblemType ProblemType { get; set; }
    public required List<string> Symptoms { get; set; }
    public required string RootCause { get; set; }
    public required string Solution { get; set; }

    // Optional
    public string? Component { get; set; }
    public Severity? Severity { get; set; }
    public string? Prevention { get; set; }
}
```

### Insight-Specific Enums and Model

```csharp
namespace CompoundDocs.Common.Models.Enums;

public enum InsightType
{
    BusinessLogic,
    UserBehavior,
    DomainKnowledge,
    FeatureInteraction,
    MarketObservation
}

public enum ConfidenceLevel
{
    Verified,
    Hypothesis,
    Observation
}

namespace CompoundDocs.Common.Models.Frontmatter;

public class InsightFrontmatter : BaseFrontmatter
{
    public required InsightType InsightType { get; set; }
    public required string ImpactArea { get; set; }

    // Optional
    public ConfidenceLevel? Confidence { get; set; }
    public string? Source { get; set; }
}
```

### Codebase-Specific Enums and Model

```csharp
namespace CompoundDocs.Common.Models.Enums;

public enum CodebaseKnowledgeType
{
    ArchitectureDecision,
    CodePattern,
    ModuleInteraction,
    DataFlow,
    DependencyRationale
}

public enum CodebaseScope
{
    System,
    Module,
    Component,
    Function
}

namespace CompoundDocs.Common.Models.Frontmatter;

public class CodebaseFrontmatter : BaseFrontmatter
{
    public required CodebaseKnowledgeType KnowledgeType { get; set; }
    public required CodebaseScope Scope { get; set; }

    // Optional
    public List<string>? FilesInvolved { get; set; }
    public List<string>? AlternativesConsidered { get; set; }
    public string? TradeOffs { get; set; }
}
```

### Tool-Specific Enums and Model

```csharp
namespace CompoundDocs.Common.Models.Enums;

public enum ToolKnowledgeType
{
    Gotcha,
    Configuration,
    Integration,
    Performance,
    Workaround,
    Deprecation
}

namespace CompoundDocs.Common.Models.Frontmatter;

public class ToolFrontmatter : BaseFrontmatter
{
    public required string ToolName { get; set; }
    public required string Version { get; set; }
    public required ToolKnowledgeType KnowledgeType { get; set; }

    // Optional
    public bool? OfficialDocsGap { get; set; }
    public List<string>? RelatedTools { get; set; }
}
```

### Style-Specific Enums and Model

```csharp
namespace CompoundDocs.Common.Models.Enums;

public enum StyleType
{
    Naming,
    Architecture,
    ErrorHandling,
    Testing,
    Documentation,
    Formatting,
    AsyncPatterns
}

public enum StyleScope
{
    Project,
    Module,
    Team
}

namespace CompoundDocs.Common.Models.Frontmatter;

public class StyleFrontmatter : BaseFrontmatter
{
    public required StyleType StyleType { get; set; }
    public required StyleScope Scope { get; set; }
    public required string Rationale { get; set; }

    // Optional
    public List<string>? Examples { get; set; }
    public string? Exceptions { get; set; }
}
```

### YamlDotNet Naming Convention Configuration

```csharp
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CompoundDocs.Common.Yaml;

/// <summary>
/// Factory for creating configured YamlDotNet deserializers.
/// </summary>
public static class YamlDeserializerFactory
{
    private static readonly Lazy<IDeserializer> _instance = new(() =>
        new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build());

    /// <summary>
    /// Thread-safe singleton deserializer configured for frontmatter parsing.
    /// </summary>
    public static IDeserializer Instance => _instance.Value;
}
```

### Enhanced Frontmatter Parser Interface

```csharp
namespace CompoundDocs.Common.Markdown.Abstractions;

/// <summary>
/// Result of parsing YAML frontmatter from a markdown document.
/// </summary>
public record FrontmatterParseResult
{
    /// <summary>Whether frontmatter was present in the document</summary>
    public required bool IsPresent { get; init; }

    /// <summary>Starting line number of frontmatter (0-based)</summary>
    public int StartLine { get; init; }

    /// <summary>Ending line number of frontmatter (0-based)</summary>
    public int EndLine { get; init; }

    /// <summary>Raw YAML content (without delimiters)</summary>
    public string? RawYaml { get; init; }

    /// <summary>Parse errors if any</summary>
    public IReadOnlyList<FrontmatterError> Errors { get; init; } = [];

    /// <summary>Whether parsing was successful (no errors)</summary>
    public bool IsSuccess => Errors.Count == 0;
}

/// <summary>
/// Represents a frontmatter parsing error.
/// </summary>
public record FrontmatterError(
    FrontmatterErrorType Type,
    string Message,
    string? FieldName = null,
    int? Line = null,
    int? Column = null
);

public enum FrontmatterErrorType
{
    SyntaxError,
    MissingRequiredField,
    InvalidEnumValue,
    InvalidDateFormat,
    TypeMismatch,
    UnexpectedValue
}

/// <summary>
/// Parses YAML frontmatter from markdown documents.
/// </summary>
public interface IFrontmatterParser
{
    /// <summary>
    /// Extract raw frontmatter from markdown content.
    /// </summary>
    FrontmatterParseResult Extract(string markdownContent);

    /// <summary>
    /// Parse frontmatter to a dictionary for dynamic access.
    /// </summary>
    FrontmatterParseResult<Dictionary<string, object?>> ParseToDictionary(string markdownContent);

    /// <summary>
    /// Parse frontmatter to a strongly-typed model.
    /// </summary>
    FrontmatterParseResult<T> Parse<T>(string markdownContent) where T : BaseFrontmatter;

    /// <summary>
    /// Parse frontmatter and auto-detect doc-type.
    /// </summary>
    FrontmatterParseResult<BaseFrontmatter> ParseAuto(string markdownContent);
}

/// <summary>
/// Generic result containing parsed frontmatter data.
/// </summary>
public record FrontmatterParseResult<T> : FrontmatterParseResult
{
    /// <summary>Parsed frontmatter data (null if parsing failed)</summary>
    public T? Data { get; init; }
}
```

### Frontmatter Parser Implementation

```csharp
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using YamlDotNet.Core;

namespace CompoundDocs.Common.Markdown;

public class YamlFrontmatterParser : IFrontmatterParser
{
    private readonly IDeserializer _deserializer = YamlDeserializerFactory.Instance;
    private readonly ILogger<YamlFrontmatterParser> _logger;

    public YamlFrontmatterParser(ILogger<YamlFrontmatterParser> logger)
    {
        _logger = logger;
    }

    public FrontmatterParseResult Extract(string markdownContent)
    {
        var document = Markdown.Parse(markdownContent, MarkdownPipelines.Default);
        var yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();

        if (yamlBlock is null)
        {
            return new FrontmatterParseResult { IsPresent = false };
        }

        return new FrontmatterParseResult
        {
            IsPresent = true,
            StartLine = yamlBlock.Line,
            EndLine = yamlBlock.Line + yamlBlock.Lines.Count,
            RawYaml = yamlBlock.Lines.ToString()
        };
    }

    public FrontmatterParseResult<Dictionary<string, object?>> ParseToDictionary(string markdownContent)
    {
        var extraction = Extract(markdownContent);
        if (!extraction.IsPresent)
        {
            return new FrontmatterParseResult<Dictionary<string, object?>>
            {
                IsPresent = false
            };
        }

        try
        {
            var data = _deserializer.Deserialize<Dictionary<string, object?>>(extraction.RawYaml!);
            return new FrontmatterParseResult<Dictionary<string, object?>>
            {
                IsPresent = true,
                StartLine = extraction.StartLine,
                EndLine = extraction.EndLine,
                RawYaml = extraction.RawYaml,
                Data = data
            };
        }
        catch (YamlException ex)
        {
            return new FrontmatterParseResult<Dictionary<string, object?>>
            {
                IsPresent = true,
                StartLine = extraction.StartLine,
                EndLine = extraction.EndLine,
                RawYaml = extraction.RawYaml,
                Errors = [new FrontmatterError(
                    FrontmatterErrorType.SyntaxError,
                    ex.Message,
                    Line: ex.Start.Line,
                    Column: ex.Start.Column
                )]
            };
        }
    }

    public FrontmatterParseResult<T> Parse<T>(string markdownContent) where T : BaseFrontmatter
    {
        var extraction = Extract(markdownContent);
        if (!extraction.IsPresent)
        {
            _logger.LogWarning("Attempted to parse frontmatter but document has none");
            return new FrontmatterParseResult<T> { IsPresent = false };
        }

        var errors = new List<FrontmatterError>();

        try
        {
            var data = _deserializer.Deserialize<T>(extraction.RawYaml!);

            // Validate required fields
            errors.AddRange(ValidateRequiredFields(data));

            return new FrontmatterParseResult<T>
            {
                IsPresent = true,
                StartLine = extraction.StartLine,
                EndLine = extraction.EndLine,
                RawYaml = extraction.RawYaml,
                Data = errors.Count == 0 ? data : default,
                Errors = errors
            };
        }
        catch (YamlException ex)
        {
            return CreateErrorResult<T>(extraction, ex);
        }
    }

    public FrontmatterParseResult<BaseFrontmatter> ParseAuto(string markdownContent)
    {
        var dictResult = ParseToDictionary(markdownContent);
        if (!dictResult.IsPresent || !dictResult.IsSuccess)
        {
            return new FrontmatterParseResult<BaseFrontmatter>
            {
                IsPresent = dictResult.IsPresent,
                StartLine = dictResult.StartLine,
                EndLine = dictResult.EndLine,
                RawYaml = dictResult.RawYaml,
                Errors = dictResult.Errors
            };
        }

        if (!dictResult.Data!.TryGetValue("doc_type", out var docTypeObj) || docTypeObj is not string docType)
        {
            return new FrontmatterParseResult<BaseFrontmatter>
            {
                IsPresent = true,
                StartLine = dictResult.StartLine,
                EndLine = dictResult.EndLine,
                RawYaml = dictResult.RawYaml,
                Errors = [new FrontmatterError(
                    FrontmatterErrorType.MissingRequiredField,
                    "Required field 'doc_type' is missing or not a string",
                    FieldName: "doc_type"
                )]
            };
        }

        return docType.ToLowerInvariant() switch
        {
            "problem" => CastResult(Parse<ProblemFrontmatter>(markdownContent)),
            "insight" => CastResult(Parse<InsightFrontmatter>(markdownContent)),
            "codebase" => CastResult(Parse<CodebaseFrontmatter>(markdownContent)),
            "tool" => CastResult(Parse<ToolFrontmatter>(markdownContent)),
            "style" => CastResult(Parse<StyleFrontmatter>(markdownContent)),
            _ => new FrontmatterParseResult<BaseFrontmatter>
            {
                IsPresent = true,
                StartLine = dictResult.StartLine,
                EndLine = dictResult.EndLine,
                RawYaml = dictResult.RawYaml,
                Errors = [new FrontmatterError(
                    FrontmatterErrorType.InvalidEnumValue,
                    $"Unknown doc_type '{docType}'. Valid values: problem, insight, codebase, tool, style",
                    FieldName: "doc_type"
                )]
            }
        };
    }

    private static FrontmatterParseResult<BaseFrontmatter> CastResult<T>(FrontmatterParseResult<T> result)
        where T : BaseFrontmatter
    {
        return new FrontmatterParseResult<BaseFrontmatter>
        {
            IsPresent = result.IsPresent,
            StartLine = result.StartLine,
            EndLine = result.EndLine,
            RawYaml = result.RawYaml,
            Data = result.Data,
            Errors = result.Errors
        };
    }

    private static IEnumerable<FrontmatterError> ValidateRequiredFields<T>(T data) where T : BaseFrontmatter
    {
        if (string.IsNullOrWhiteSpace(data.DocType))
            yield return new FrontmatterError(FrontmatterErrorType.MissingRequiredField,
                "Required field 'doc_type' is missing", "doc_type");

        if (string.IsNullOrWhiteSpace(data.Title))
            yield return new FrontmatterError(FrontmatterErrorType.MissingRequiredField,
                "Required field 'title' is missing", "title");

        if (data.Date == default)
            yield return new FrontmatterError(FrontmatterErrorType.MissingRequiredField,
                "Required field 'date' is missing or invalid. Expected format: YYYY-MM-DD", "date");

        if (string.IsNullOrWhiteSpace(data.Summary))
            yield return new FrontmatterError(FrontmatterErrorType.MissingRequiredField,
                "Required field 'summary' is missing", "summary");
    }

    private static FrontmatterParseResult<T> CreateErrorResult<T>(FrontmatterParseResult extraction, YamlException ex)
        where T : BaseFrontmatter
    {
        var errorType = ex.Message switch
        {
            var m when m.Contains("not a valid") => FrontmatterErrorType.InvalidEnumValue,
            var m when m.Contains("expected") => FrontmatterErrorType.TypeMismatch,
            _ => FrontmatterErrorType.SyntaxError
        };

        return new FrontmatterParseResult<T>
        {
            IsPresent = true,
            StartLine = extraction.StartLine,
            EndLine = extraction.EndLine,
            RawYaml = extraction.RawYaml,
            Errors = [new FrontmatterError(
                errorType,
                ex.Message,
                Line: ex.Start.Line,
                Column: ex.Start.Column
            )]
        };
    }
}
```

### Frontmatter Validation Service

```csharp
namespace CompoundDocs.Common.Markdown;

/// <summary>
/// Validates frontmatter against doc-type schemas.
/// </summary>
public interface IFrontmatterValidator
{
    /// <summary>
    /// Validate frontmatter data for a specific doc-type.
    /// </summary>
    ValidationResult Validate(BaseFrontmatter frontmatter);

    /// <summary>
    /// Validate frontmatter dictionary against expected schema.
    /// </summary>
    ValidationResult Validate(Dictionary<string, object?> data, string docType);
}

public record ValidationResult(
    bool IsValid,
    IReadOnlyList<ValidationError> Errors
);

public record ValidationError(
    string FieldName,
    string Message,
    ValidationErrorSeverity Severity
);

public enum ValidationErrorSeverity
{
    Warning,
    Error
}

public class FrontmatterValidator : IFrontmatterValidator
{
    public ValidationResult Validate(BaseFrontmatter frontmatter)
    {
        var errors = new List<ValidationError>();

        // Validate common fields
        ValidateCommonFields(frontmatter, errors);

        // Validate type-specific fields
        switch (frontmatter)
        {
            case ProblemFrontmatter problem:
                ValidateProblemFields(problem, errors);
                break;
            case InsightFrontmatter insight:
                ValidateInsightFields(insight, errors);
                break;
            case CodebaseFrontmatter codebase:
                ValidateCodebaseFields(codebase, errors);
                break;
            case ToolFrontmatter tool:
                ValidateToolFields(tool, errors);
                break;
            case StyleFrontmatter style:
                ValidateStyleFields(style, errors);
                break;
        }

        return new ValidationResult(
            errors.All(e => e.Severity != ValidationErrorSeverity.Error),
            errors
        );
    }

    public ValidationResult Validate(Dictionary<string, object?> data, string docType)
    {
        // Implementation for schema-based validation
        throw new NotImplementedException();
    }

    private static void ValidateCommonFields(BaseFrontmatter fm, List<ValidationError> errors)
    {
        if (fm.Date > DateTime.UtcNow.Date)
        {
            errors.Add(new ValidationError("date", "Date cannot be in the future",
                ValidationErrorSeverity.Warning));
        }

        if (fm.Summary.Length > 200)
        {
            errors.Add(new ValidationError("summary", "Summary should be under 200 characters",
                ValidationErrorSeverity.Warning));
        }
    }

    private static void ValidateProblemFields(ProblemFrontmatter fm, List<ValidationError> errors)
    {
        if (fm.Symptoms.Count == 0)
        {
            errors.Add(new ValidationError("symptoms", "At least one symptom is required",
                ValidationErrorSeverity.Error));
        }
    }

    private static void ValidateInsightFields(InsightFrontmatter fm, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(fm.ImpactArea))
        {
            errors.Add(new ValidationError("impact_area", "Impact area is required",
                ValidationErrorSeverity.Error));
        }
    }

    private static void ValidateCodebaseFields(CodebaseFrontmatter fm, List<ValidationError> errors)
    {
        // Codebase-specific validation
    }

    private static void ValidateToolFields(ToolFrontmatter fm, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(fm.Version))
        {
            errors.Add(new ValidationError("version", "Version is required for tool documentation",
                ValidationErrorSeverity.Error));
        }
    }

    private static void ValidateStyleFields(StyleFrontmatter fm, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(fm.Rationale))
        {
            errors.Add(new ValidationError("rationale", "Rationale is required for style documentation",
                ValidationErrorSeverity.Error));
        }
    }
}
```

### Dependency Injection Registration

```csharp
namespace CompoundDocs.Common.Markdown;

public static class FrontmatterServiceCollectionExtensions
{
    public static IServiceCollection AddFrontmatterParsing(this IServiceCollection services)
    {
        services.AddSingleton<IFrontmatterParser, YamlFrontmatterParser>();
        services.AddSingleton<IFrontmatterValidator, FrontmatterValidator>();

        return services;
    }
}
```

---

## File Structure

After completion, the following files should exist:

```
src/CompoundDocs.Common/
├── Models/
│   ├── Enums/
│   │   ├── Significance.cs
│   │   ├── PromotionLevel.cs
│   │   ├── ProblemType.cs
│   │   ├── Severity.cs
│   │   ├── InsightType.cs
│   │   ├── ConfidenceLevel.cs
│   │   ├── CodebaseKnowledgeType.cs
│   │   ├── CodebaseScope.cs
│   │   ├── ToolKnowledgeType.cs
│   │   ├── StyleType.cs
│   │   └── StyleScope.cs
│   └── Frontmatter/
│       ├── BaseFrontmatter.cs
│       ├── ProblemFrontmatter.cs
│       ├── InsightFrontmatter.cs
│       ├── CodebaseFrontmatter.cs
│       ├── ToolFrontmatter.cs
│       └── StyleFrontmatter.cs
├── Yaml/
│   └── YamlDeserializerFactory.cs
├── Markdown/
│   ├── Abstractions/
│   │   ├── IFrontmatterParser.cs
│   │   ├── IFrontmatterValidator.cs
│   │   ├── FrontmatterParseResult.cs
│   │   ├── FrontmatterError.cs
│   │   └── ValidationResult.cs
│   ├── YamlFrontmatterParser.cs
│   ├── FrontmatterValidator.cs
│   └── FrontmatterServiceCollectionExtensions.cs
tests/CompoundDocs.Tests/
└── Frontmatter/
    ├── FrontmatterParserTests.cs
    ├── FrontmatterValidatorTests.cs
    ├── ProblemFrontmatterTests.cs
    ├── InsightFrontmatterTests.cs
    ├── CodebaseFrontmatterTests.cs
    ├── ToolFrontmatterTests.cs
    ├── StyleFrontmatterTests.cs
    └── FrontmatterErrorHandlingTests.cs
```

---

## Dependencies

### Depends On
- Phase 015: Markdown Parser Integration (Markdig pipeline, YamlDotNet package)

### Blocks
- Phase XXX: Document Indexing Service (requires frontmatter for metadata)
- Phase XXX: Doc-Type Classification (requires parsed frontmatter)
- Phase XXX: Search Result Ranking (requires promotion_level)
- Phase XXX: Document Validation Tool (requires frontmatter validation)

---

## Verification Steps

After completing this phase, verify:

1. **Package compatibility**: YamlDotNet works with Markdig's frontmatter extraction
2. **Build**: `dotnet build` completes without errors
3. **Tests pass**: `dotnet test` runs all frontmatter parsing tests successfully
4. **Coverage**: Code coverage report shows 100% for all frontmatter parsing code
5. **Error messages**: All error messages include actionable information

### Sample Test Documents

**Valid Problem Document:**
```markdown
---
doc_type: problem
title: Database Connection Pool Exhaustion
date: 2025-01-24
summary: Connection pool exhausted under high load due to unclosed connections
significance: performance
problem_type: performance
symptoms:
  - Timeout errors during peak traffic
  - "Cannot open connection" exceptions
root_cause: Connections not being returned to pool in error paths
solution: Added using statements and proper disposal pattern
severity: high
component: DataAccess
tags:
  - database
  - connection-pool
promotion_level: important
---

# Database Connection Pool Exhaustion

## Problem Description
...
```

**Valid Insight Document:**
```markdown
---
doc_type: insight
title: Users Prefer Keyboard Navigation
date: 2025-01-24
summary: Power users strongly prefer keyboard shortcuts over mouse interactions
significance: behavioral
insight_type: user_behavior
impact_area: UI/UX Design
confidence: verified
source: User interview sessions Q4 2024
---

# Users Prefer Keyboard Navigation

## Context
...
```

**Document Without Frontmatter:**
```markdown
# Just a Regular Markdown File

This document has no frontmatter and should be handled gracefully.
```

**Malformed Frontmatter:**
```markdown
---
doc_type: problem
title: Missing closing delimiter
date: invalid-date-format
significance: not_a_valid_enum
```

---

## Notes

- YamlDotNet's `UnderscoredNamingConvention` maps `doc_type` to `DocType` automatically
- The `IgnoreUnmatchedProperties()` setting allows forward compatibility as schemas evolve
- Enum parsing in YamlDotNet is case-insensitive by default
- Source line numbers from Markdig are 0-based; add 1 for user-facing display
- The frontmatter validation is separate from parsing to allow partial document processing
- Documents without frontmatter are valid for some operations (e.g., external reference resolution)
