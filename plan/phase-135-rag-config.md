# Phase 135: RAG Parameter Configuration

> **Category**: Configuration System
> **Prerequisites**: Phase 010 (Project Configuration System)
> **Estimated Effort**: Small
> **Priority**: Medium (required for tunable RAG behavior)

---

## Overview

This phase implements the RAG parameter configuration infrastructure that allows users to tune retrieval behavior for both RAG queries and semantic search operations. The configuration includes relevance thresholds, result limits, and link-following depth, with a clear precedence system allowing tool-level overrides.

---

## Goals

1. Implement RAG retrieval parameter models with validation
2. Implement semantic search parameter models with validation
3. Define configuration precedence rules (tool param > project config > default)
4. Create parameter resolution service for MCP tools
5. Support link depth configuration for related document following

---

## Spec References

- [spec/configuration.md](../spec/configuration.md) - RAG retrieval parameters, semantic search settings, configuration precedence
- [plan/phase-010-project-config.md](./phase-010-project-config.md) - Base configuration models

---

## Deliverables

### 1. RAG Parameter Constants

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/RagParameterDefaults.cs`

```csharp
/// <summary>
/// Built-in default values for RAG and search parameters.
/// These are the fallback values when neither tool params nor project config specify a value.
/// </summary>
public static class RagParameterDefaults
{
    // === RAG Retrieval Defaults ===

    /// <summary>
    /// Default minimum relevance score for RAG queries (0.0-1.0).
    /// High threshold ensures only highly relevant documents are used for synthesis.
    /// </summary>
    public const double RetrievalMinRelevanceScore = 0.7;

    /// <summary>
    /// Minimum allowed value for retrieval relevance score.
    /// </summary>
    public const double RetrievalMinRelevanceScoreMin = 0.0;

    /// <summary>
    /// Maximum allowed value for retrieval relevance score.
    /// </summary>
    public const double RetrievalMinRelevanceScoreMax = 1.0;

    /// <summary>
    /// Default maximum documents returned from RAG queries (excluding linked docs).
    /// Also referred to as "top_k" in RAG terminology.
    /// </summary>
    public const int RetrievalMaxResults = 3;

    /// <summary>
    /// Minimum allowed value for max results (top_k).
    /// </summary>
    public const int RetrievalMaxResultsMin = 1;

    /// <summary>
    /// Maximum allowed value for max results (top_k).
    /// Prevents excessive resource consumption.
    /// </summary>
    public const int RetrievalMaxResultsMax = 50;

    /// <summary>
    /// Default maximum linked documents to include in RAG results.
    /// </summary>
    public const int RetrievalMaxLinkedDocs = 5;

    /// <summary>
    /// Minimum allowed value for max linked docs.
    /// </summary>
    public const int RetrievalMaxLinkedDocsMin = 0;

    /// <summary>
    /// Maximum allowed value for max linked docs.
    /// </summary>
    public const int RetrievalMaxLinkedDocsMax = 20;

    // === Link Resolution Defaults ===

    /// <summary>
    /// Default depth for following document links.
    /// 0 = no link following, 1 = immediate links only, 2 = two levels deep.
    /// </summary>
    public const int LinkResolutionMaxDepth = 2;

    /// <summary>
    /// Minimum allowed link depth.
    /// </summary>
    public const int LinkResolutionMaxDepthMin = 0;

    /// <summary>
    /// Maximum allowed link depth to prevent circular reference issues.
    /// </summary>
    public const int LinkResolutionMaxDepthMax = 10;

    // === Semantic Search Defaults ===

    /// <summary>
    /// Default minimum relevance score for semantic search (0.0-1.0).
    /// Lower than RAG threshold since search is exploratory.
    /// </summary>
    public const double SemanticSearchMinRelevanceScore = 0.5;

    /// <summary>
    /// Minimum allowed value for semantic search relevance score.
    /// </summary>
    public const double SemanticSearchMinRelevanceScoreMin = 0.0;

    /// <summary>
    /// Maximum allowed value for semantic search relevance score.
    /// </summary>
    public const double SemanticSearchMinRelevanceScoreMax = 1.0;

    /// <summary>
    /// Default number of results for semantic search.
    /// </summary>
    public const int SemanticSearchDefaultLimit = 10;

    /// <summary>
    /// Minimum allowed search result limit.
    /// </summary>
    public const int SemanticSearchDefaultLimitMin = 1;

    /// <summary>
    /// Maximum allowed search result limit.
    /// </summary>
    public const int SemanticSearchDefaultLimitMax = 100;
}
```

---

### 2. Effective RAG Parameters Model

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/EffectiveRagParameters.cs`

```csharp
/// <summary>
/// Resolved RAG parameters after applying configuration precedence.
/// This is the final parameter set used by RAG operations.
/// </summary>
public sealed record EffectiveRagParameters
{
    /// <summary>
    /// Minimum similarity score for including documents (0.0-1.0).
    /// Documents below this threshold are excluded from results.
    /// </summary>
    public required double MinRelevanceScore { get; init; }

    /// <summary>
    /// Maximum number of documents to return (top_k).
    /// Does not include linked documents which are added separately.
    /// </summary>
    public required int MaxResults { get; init; }

    /// <summary>
    /// Maximum linked documents to include in results.
    /// </summary>
    public required int MaxLinkedDocs { get; init; }

    /// <summary>
    /// How many levels deep to follow document links.
    /// </summary>
    public required int LinkDepth { get; init; }

    /// <summary>
    /// Source of each parameter for debugging/logging.
    /// </summary>
    public required ParameterSources Sources { get; init; }
}

/// <summary>
/// Tracks where each parameter value came from for debugging.
/// </summary>
public sealed record ParameterSources
{
    public ParameterSource MinRelevanceScore { get; init; }
    public ParameterSource MaxResults { get; init; }
    public ParameterSource MaxLinkedDocs { get; init; }
    public ParameterSource LinkDepth { get; init; }
}

/// <summary>
/// Indicates the source of a resolved parameter value.
/// </summary>
public enum ParameterSource
{
    /// <summary>Value came from built-in default.</summary>
    Default,

    /// <summary>Value came from project configuration.</summary>
    ProjectConfig,

    /// <summary>Value came from tool parameter (highest precedence).</summary>
    ToolParameter
}
```

---

### 3. Effective Semantic Search Parameters Model

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/EffectiveSearchParameters.cs`

```csharp
/// <summary>
/// Resolved semantic search parameters after applying configuration precedence.
/// </summary>
public sealed record EffectiveSearchParameters
{
    /// <summary>
    /// Minimum similarity score for including documents (0.0-1.0).
    /// </summary>
    public required double MinRelevanceScore { get; init; }

    /// <summary>
    /// Maximum number of results to return.
    /// </summary>
    public required int Limit { get; init; }

    /// <summary>
    /// Source of each parameter for debugging/logging.
    /// </summary>
    public required SearchParameterSources Sources { get; init; }
}

/// <summary>
/// Tracks where each search parameter value came from.
/// </summary>
public sealed record SearchParameterSources
{
    public ParameterSource MinRelevanceScore { get; init; }
    public ParameterSource Limit { get; init; }
}
```

---

### 4. Tool Parameter Input Models

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/ToolParameterInputs.cs`

```csharp
/// <summary>
/// Optional RAG parameters that can be provided by MCP tool calls.
/// Null values indicate "use configured default".
/// </summary>
public sealed record RagToolParameters
{
    /// <summary>
    /// Optional minimum relevance score override.
    /// </summary>
    public double? MinRelevanceScore { get; init; }

    /// <summary>
    /// Optional max results (top_k) override.
    /// </summary>
    public int? MaxResults { get; init; }

    /// <summary>
    /// Optional max linked docs override.
    /// </summary>
    public int? MaxLinkedDocs { get; init; }

    /// <summary>
    /// Optional link depth override.
    /// </summary>
    public int? LinkDepth { get; init; }

    /// <summary>
    /// Creates an empty parameter set (all defaults).
    /// </summary>
    public static RagToolParameters Empty => new();
}

/// <summary>
/// Optional semantic search parameters that can be provided by MCP tool calls.
/// </summary>
public sealed record SearchToolParameters
{
    /// <summary>
    /// Optional minimum relevance score override.
    /// </summary>
    public double? MinRelevanceScore { get; init; }

    /// <summary>
    /// Optional result limit override.
    /// </summary>
    public int? Limit { get; init; }

    /// <summary>
    /// Creates an empty parameter set (all defaults).
    /// </summary>
    public static SearchToolParameters Empty => new();
}
```

---

### 5. Parameter Resolution Service

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/Services/IRagParameterResolver.cs`

```csharp
/// <summary>
/// Resolves effective RAG and search parameters by applying precedence rules.
/// Precedence: Tool parameter > Project config > Built-in default
/// </summary>
public interface IRagParameterResolver
{
    /// <summary>
    /// Resolves RAG parameters for the current project context.
    /// </summary>
    /// <param name="toolParams">Optional tool-level parameter overrides</param>
    /// <returns>Effective parameters with source tracking</returns>
    EffectiveRagParameters ResolveRagParameters(RagToolParameters? toolParams = null);

    /// <summary>
    /// Resolves semantic search parameters for the current project context.
    /// </summary>
    /// <param name="toolParams">Optional tool-level parameter overrides</param>
    /// <returns>Effective parameters with source tracking</returns>
    EffectiveSearchParameters ResolveSearchParameters(SearchToolParameters? toolParams = null);

    /// <summary>
    /// Validates that a parameter value is within allowed bounds.
    /// </summary>
    /// <typeparam name="T">Parameter type</typeparam>
    /// <param name="value">Value to validate</param>
    /// <param name="min">Minimum allowed value</param>
    /// <param name="max">Maximum allowed value</param>
    /// <param name="parameterName">Name for error messages</param>
    /// <returns>Validated value</returns>
    /// <exception cref="ArgumentOutOfRangeException">If value is out of bounds</exception>
    T ValidateRange<T>(T value, T min, T max, string parameterName) where T : IComparable<T>;
}
```

---

### 6. Parameter Resolution Service Implementation

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/Services/RagParameterResolver.cs`

```csharp
/// <summary>
/// Implements parameter resolution with three-tier precedence:
/// 1. Tool parameter (highest) - explicitly provided in tool call
/// 2. Project config - from .csharp-compounding-docs/config.json
/// 3. Built-in default (lowest) - hardcoded fallback
/// </summary>
public sealed class RagParameterResolver : IRagParameterResolver
{
    private readonly SwitchableProjectConfigurationProvider _configProvider;
    private readonly ILogger<RagParameterResolver> _logger;

    public RagParameterResolver(
        SwitchableProjectConfigurationProvider configProvider,
        ILogger<RagParameterResolver> logger)
    {
        _configProvider = configProvider;
        _logger = logger;
    }

    public EffectiveRagParameters ResolveRagParameters(RagToolParameters? toolParams = null)
    {
        toolParams ??= RagToolParameters.Empty;
        var projectConfig = _configProvider.CurrentConfig;

        var (minRelevanceScore, minRelevanceSource) = ResolveParameter(
            toolParams.MinRelevanceScore,
            projectConfig?.Retrieval?.MinRelevanceScore,
            RagParameterDefaults.RetrievalMinRelevanceScore,
            RagParameterDefaults.RetrievalMinRelevanceScoreMin,
            RagParameterDefaults.RetrievalMinRelevanceScoreMax,
            "min_relevance_score");

        var (maxResults, maxResultsSource) = ResolveParameter(
            toolParams.MaxResults,
            projectConfig?.Retrieval?.MaxResults,
            RagParameterDefaults.RetrievalMaxResults,
            RagParameterDefaults.RetrievalMaxResultsMin,
            RagParameterDefaults.RetrievalMaxResultsMax,
            "max_results");

        var (maxLinkedDocs, maxLinkedDocsSource) = ResolveParameter(
            toolParams.MaxLinkedDocs,
            projectConfig?.Retrieval?.MaxLinkedDocs,
            RagParameterDefaults.RetrievalMaxLinkedDocs,
            RagParameterDefaults.RetrievalMaxLinkedDocsMin,
            RagParameterDefaults.RetrievalMaxLinkedDocsMax,
            "max_linked_docs");

        var (linkDepth, linkDepthSource) = ResolveParameter(
            toolParams.LinkDepth,
            projectConfig?.LinkResolution?.MaxDepth,
            RagParameterDefaults.LinkResolutionMaxDepth,
            RagParameterDefaults.LinkResolutionMaxDepthMin,
            RagParameterDefaults.LinkResolutionMaxDepthMax,
            "link_depth");

        var result = new EffectiveRagParameters
        {
            MinRelevanceScore = minRelevanceScore,
            MaxResults = maxResults,
            MaxLinkedDocs = maxLinkedDocs,
            LinkDepth = linkDepth,
            Sources = new ParameterSources
            {
                MinRelevanceScore = minRelevanceSource,
                MaxResults = maxResultsSource,
                MaxLinkedDocs = maxLinkedDocsSource,
                LinkDepth = linkDepthSource
            }
        };

        _logger.LogDebug(
            "Resolved RAG parameters: MinRelevance={MinRelevance} ({MinSource}), " +
            "MaxResults={MaxResults} ({MaxSource}), MaxLinked={MaxLinked} ({LinkedSource}), " +
            "LinkDepth={LinkDepth} ({DepthSource})",
            result.MinRelevanceScore, minRelevanceSource,
            result.MaxResults, maxResultsSource,
            result.MaxLinkedDocs, maxLinkedDocsSource,
            result.LinkDepth, linkDepthSource);

        return result;
    }

    public EffectiveSearchParameters ResolveSearchParameters(SearchToolParameters? toolParams = null)
    {
        toolParams ??= SearchToolParameters.Empty;
        var projectConfig = _configProvider.CurrentConfig;

        var (minRelevanceScore, minRelevanceSource) = ResolveParameter(
            toolParams.MinRelevanceScore,
            projectConfig?.SemanticSearch?.MinRelevanceScore,
            RagParameterDefaults.SemanticSearchMinRelevanceScore,
            RagParameterDefaults.SemanticSearchMinRelevanceScoreMin,
            RagParameterDefaults.SemanticSearchMinRelevanceScoreMax,
            "min_relevance_score");

        var (limit, limitSource) = ResolveParameter(
            toolParams.Limit,
            projectConfig?.SemanticSearch?.DefaultLimit,
            RagParameterDefaults.SemanticSearchDefaultLimit,
            RagParameterDefaults.SemanticSearchDefaultLimitMin,
            RagParameterDefaults.SemanticSearchDefaultLimitMax,
            "limit");

        var result = new EffectiveSearchParameters
        {
            MinRelevanceScore = minRelevanceScore,
            Limit = limit,
            Sources = new SearchParameterSources
            {
                MinRelevanceScore = minRelevanceSource,
                Limit = limitSource
            }
        };

        _logger.LogDebug(
            "Resolved search parameters: MinRelevance={MinRelevance} ({MinSource}), " +
            "Limit={Limit} ({LimitSource})",
            result.MinRelevanceScore, minRelevanceSource,
            result.Limit, limitSource);

        return result;
    }

    public T ValidateRange<T>(T value, T min, T max, string parameterName)
        where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Value must be between {min} and {max}");
        }
        return value;
    }

    private (T Value, ParameterSource Source) ResolveParameter<T>(
        T? toolParam,
        T? configParam,
        T defaultValue,
        T min,
        T max,
        string parameterName)
        where T : struct, IComparable<T>
    {
        if (toolParam.HasValue)
        {
            return (ValidateRange(toolParam.Value, min, max, parameterName), ParameterSource.ToolParameter);
        }

        if (configParam.HasValue)
        {
            return (ValidateRange(configParam.Value, min, max, parameterName), ParameterSource.ProjectConfig);
        }

        return (defaultValue, ParameterSource.Default);
    }

    // Overload for nullable reference config properties
    private (double Value, ParameterSource Source) ResolveParameter(
        double? toolParam,
        double configParam,
        double defaultValue,
        double min,
        double max,
        string parameterName)
    {
        if (toolParam.HasValue)
        {
            return (ValidateRange(toolParam.Value, min, max, parameterName), ParameterSource.ToolParameter);
        }

        // Config models use non-nullable with defaults, check if different from class default
        if (Math.Abs(configParam - defaultValue) > 0.0001)
        {
            return (ValidateRange(configParam, min, max, parameterName), ParameterSource.ProjectConfig);
        }

        return (defaultValue, ParameterSource.Default);
    }

    private (int Value, ParameterSource Source) ResolveParameter(
        int? toolParam,
        int configParam,
        int defaultValue,
        int min,
        int max,
        string parameterName)
    {
        if (toolParam.HasValue)
        {
            return (ValidateRange(toolParam.Value, min, max, parameterName), ParameterSource.ToolParameter);
        }

        // Config models use non-nullable with defaults, check if different from class default
        if (configParam != defaultValue)
        {
            return (ValidateRange(configParam, min, max, parameterName), ParameterSource.ProjectConfig);
        }

        return (defaultValue, ParameterSource.Default);
    }
}
```

---

### 7. Dependency Injection Registration

**Location**: Update `src/CSharpCompoundingDocs.Core/Configuration/ServiceCollectionExtensions.cs`

```csharp
public static class ConfigurationServiceCollectionExtensions
{
    public static IServiceCollection AddProjectConfiguration(
        this IServiceCollection services)
    {
        // Existing registrations...
        services.AddSingleton<ISchemaValidationService, SchemaValidationService>();
        services.AddSingleton<IProjectConfigurationService, ProjectConfigurationService>();
        services.AddSingleton<IProjectInitializationService, ProjectInitializationService>();
        services.AddSingleton<SwitchableProjectConfigurationProvider>();
        services.AddSingleton<ExternalDocsPathValidator>();

        // RAG parameter resolution (new)
        services.AddSingleton<IRagParameterResolver, RagParameterResolver>();

        return services;
    }
}
```

---

## Configuration Precedence Rules

The parameter resolution follows a strict three-tier precedence:

| Priority | Source | Example |
|----------|--------|---------|
| 1 (Highest) | Tool Parameter | `rag_query(min_relevance_score: 0.6)` |
| 2 | Project Config | `config.json` with `retrieval.min_relevance_score: 0.8` |
| 3 (Lowest) | Built-in Default | `RagParameterDefaults.RetrievalMinRelevanceScore = 0.7` |

### Example Scenarios

**Scenario 1: No overrides**
```
Tool param: null
Project config: default (0.7)
Result: 0.7 (from Default)
```

**Scenario 2: Project config override**
```
Tool param: null
Project config: 0.8
Result: 0.8 (from ProjectConfig)
```

**Scenario 3: Tool param override**
```
Tool param: 0.6
Project config: 0.8
Result: 0.6 (from ToolParameter)
```

---

## Parameter Summary Table

| Parameter | Default | Min | Max | Description |
|-----------|---------|-----|-----|-------------|
| `retrieval.min_relevance_score` | 0.7 | 0.0 | 1.0 | RAG query similarity threshold |
| `retrieval.max_results` (top_k) | 3 | 1 | 50 | Max documents returned |
| `retrieval.max_linked_docs` | 5 | 0 | 20 | Max linked docs to follow |
| `link_resolution.max_depth` | 2 | 0 | 10 | Link following depth |
| `semantic_search.min_relevance_score` | 0.5 | 0.0 | 1.0 | Search similarity threshold |
| `semantic_search.default_limit` | 10 | 1 | 100 | Default search result count |

---

## Testing Requirements

### Unit Tests

1. **RagParameterDefaultsTests**
   - Verify all default values match spec
   - Verify min/max bounds are sensible

2. **RagParameterResolverTests**
   - Resolve with no project config (uses defaults)
   - Resolve with project config only
   - Resolve with tool params only
   - Resolve with both (tool wins)
   - Track parameter sources correctly
   - Validate out-of-range tool params throw
   - Validate out-of-range config values throw

3. **EffectiveRagParametersTests**
   - Record equality semantics
   - Source tracking accuracy

4. **EffectiveSearchParametersTests**
   - Record equality semantics
   - Source tracking accuracy

### Integration Tests

1. **Parameter precedence end-to-end**: Configure project, call tool with overrides, verify correct values used
2. **Config reload propagation**: Change config.json, verify resolver picks up new values

---

## Acceptance Criteria

- [ ] RAG parameters have defaults matching spec (0.7 relevance, 3 max results, 5 linked docs, depth 2)
- [ ] Semantic search parameters have defaults matching spec (0.5 relevance, 10 limit)
- [ ] Tool parameters override project config
- [ ] Project config overrides built-in defaults
- [ ] Out-of-range values throw `ArgumentOutOfRangeException`
- [ ] Parameter source tracking works for debugging
- [ ] Service integrates with existing `SwitchableProjectConfigurationProvider`
- [ ] Unit test coverage > 90% for resolver logic

---

## Dependencies

### Internal Dependencies

- Phase 010: `ProjectConfig`, `RetrievalSettings`, `SemanticSearchSettings`, `LinkResolutionSettings`
- Phase 010: `SwitchableProjectConfigurationProvider`

### NuGet Packages

No additional packages required (uses existing configuration infrastructure).

---

## Notes

- The semantic search threshold (0.5) is intentionally lower than RAG threshold (0.7) because search is exploratory while RAG needs high-relevance documents for synthesis
- Parameter source tracking is valuable for debugging when users report unexpected retrieval behavior
- The resolver is designed to be called per-request, not cached, to support dynamic config changes
- Max bounds prevent resource exhaustion (e.g., max_results capped at 50 to prevent huge vector searches)
