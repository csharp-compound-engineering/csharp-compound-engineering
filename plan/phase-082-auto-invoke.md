# Phase 082: Auto-Invoke System

> **Status**: NOT_STARTED
> **Effort Estimate**: 8-12 hours
> **Category**: Skills System
> **Prerequisites**: Phase 081 (Skill Infrastructure)

---

## Spec References

This phase implements the auto-invoke system for skill activation as defined in:

- **spec/skills/skill-patterns.md** - [Auto-Invocation](../spec/skills/skill-patterns.md#auto-invocation) - Project entry detection, capture detection, auto-invoke frontmatter
- **spec/skills.md** - [Two-Stage Classification](../spec/skills.md#built-in-skills) - Trigger phrase matching + classification hint validation
- **spec/doc-types.md** - [Two-Stage Classification Model](../spec/doc-types.md#two-stage-classification-model) - How trigger phrases and classification hints work together
- **spec/doc-types/built-in-types.md** - Complete trigger phrases and classification hints for all 5 built-in doc-types
- **spec/doc-types/custom-types.md** - [Classification Metadata](../spec/doc-types/custom-types.md#classification-metadata) - How custom types configure auto-invoke
- **spec/skills/meta-skills.md** - [Multi-Trigger Conflict Resolution](../spec/skills/meta-skills.md#multi-trigger-conflict-resolution) - `/cdocs:capture-select` behavior
- **research/claude-code-hooks-skill-invocation.md** - Hooks-based approaches for skill auto-invocation

---

## Objectives

1. Implement trigger phrase matching engine for conversation pattern detection
2. Support project entry detection via `project-entry` trigger type
3. Implement two-stage classification with trigger phrases and classification hints
4. Parse and validate auto-invoke YAML frontmatter configuration in SKILL.md files
5. Support both built-in and custom doc-type auto-invoke configurations
6. Implement multi-trigger conflict detection for `/cdocs:capture-select` activation
7. Provide configuration model for auto-invoke settings

---

## Acceptance Criteria

### Auto-Invoke Configuration Model

- [ ] `AutoInvokeConfig` record defined with properties:
  - [ ] `Trigger` - enum: `ProjectEntry`, `ConversationPattern`
  - [ ] `Patterns` - array of trigger phrases (for `ConversationPattern` trigger)
- [ ] `SkillAutoInvokeMetadata` record containing:
  - [ ] `SkillName` - fully qualified skill name (e.g., `cdocs:problem`)
  - [ ] `AutoInvoke` - `AutoInvokeConfig` instance
  - [ ] `ClassificationHints` - array of semantic validation hints
  - [ ] `Preconditions` - array of precondition strings
- [ ] Configuration model supports YAML parsing from SKILL.md frontmatter
- [ ] Validation of required fields based on trigger type

### YAML Frontmatter Parsing

- [ ] `ISkillMetadataParser` interface with methods:
  - [ ] `ParseSkillMetadataAsync(string skillPath)` - Parse SKILL.md file
  - [ ] `ExtractAutoInvokeConfig(YamlNode frontmatter)` - Extract auto-invoke section
- [ ] Parser handles standard Claude Code SKILL.md frontmatter format:
  ```yaml
  ---
  name: cdocs:problem
  description: Capture solved problems
  allowed-tools:
    - Read
  preconditions:
    - Project activated via /cdocs:activate
  auto-invoke:
    trigger: conversation-pattern
    patterns:
      - "fixed"
      - "bug"
      - "problem solved"
  ---
  ```
- [ ] Parser validates `trigger` values: `project-entry`, `conversation-pattern`
- [ ] Parser requires `patterns` array when trigger is `conversation-pattern`
- [ ] Parser tolerates missing `auto-invoke` section (skill is manual-only)
- [ ] Error handling for malformed YAML with descriptive messages

### Trigger Phrase Matching Engine

- [ ] `ITriggerPhraseMatcherService` interface defined with methods:
  - [ ] `MatchTriggerPhrases(string conversationText, IEnumerable<string> phrases)` - Returns matched phrases
  - [ ] `FindTriggeredSkills(string conversationText, IEnumerable<SkillAutoInvokeMetadata> skills)` - Returns skills with matching triggers
- [ ] Case-insensitive phrase matching by default
- [ ] Word boundary matching to avoid partial matches (e.g., "fixed" shouldn't match "affixed")
- [ ] Support for multi-word phrases (e.g., "problem solved", "it's fixed")
- [ ] Performance optimized for multiple concurrent skill evaluations
- [ ] Matching results include:
  - [ ] `SkillName` - Which skill was triggered
  - [ ] `MatchedPhrases` - Array of phrases that matched
  - [ ] `MatchPositions` - Where in the text matches occurred

### Two-Stage Classification

- [ ] `IClassificationValidator` interface defined with methods:
  - [ ] `ValidateClassificationHints(string conversationText, IEnumerable<string> hints)` - Returns validation score
  - [ ] `ShouldAutoInvoke(TriggerMatchResult triggerMatch, string conversationText)` - Returns boolean
- [ ] Stage 1: Trigger phrase matching (wide net):
  - [ ] Checks if any trigger phrase exists in conversation
  - [ ] Returns immediately if no trigger phrases match
- [ ] Stage 2: Classification hint validation (semantic filter):
  - [ ] Checks conversation context against classification hints
  - [ ] Returns confidence score (0.0 - 1.0)
  - [ ] Configurable threshold for auto-invoke (default: 0.3 - at least 30% of hints should match)
- [ ] Combined validation:
  - [ ] Skill auto-invokes only when both stages pass
  - [ ] Prevents false positives from generic trigger phrases (e.g., "always" in style type)

### Classification Hints from Schema

- [ ] `ISchemaClassificationLoader` interface defined with methods:
  - [ ] `LoadClassificationMetadata(string docType)` - Returns trigger phrases and hints from schema
  - [ ] `GetBuiltInClassificationMetadata()` - Returns metadata for all 5 built-in types
- [ ] Schema files contain classification metadata:
  ```yaml
  trigger_phrases:
    - "fixed"
    - "bug"
    - "the issue was"

  classification_hints:
    - "error message"
    - "stack trace"
    - "root cause"
    - "debugging"
  ```
- [ ] Integration between schema metadata and skill auto-invoke configuration
- [ ] Support for custom doc-type schemas with their own classification metadata

### Project Entry Detection

- [ ] `IProjectEntryDetector` interface defined with methods:
  - [ ] `DetectProjectEntry(string projectRoot)` - Returns project entry info
  - [ ] `ShouldActivate(string projectRoot)` - Returns boolean based on config presence
- [ ] Project entry conditions:
  - [ ] `.csharp-compounding-docs/config.json` exists
  - [ ] Project has not been activated in current session
- [ ] Project entry triggers `/cdocs:activate` skill
- [ ] Session state tracking to prevent re-activation on same project

### Multi-Trigger Conflict Detection

- [ ] `IMultiTriggerResolver` interface defined with methods:
  - [ ] `DetectConflicts(IEnumerable<TriggerMatchResult> matches)` - Returns conflict info
  - [ ] `ShouldInvokeCaptureSelect(IEnumerable<TriggerMatchResult> matches)` - Returns boolean
- [ ] Conflict detection rules:
  - [ ] 0 skills triggered: No action
  - [ ] 1 skill triggered: Direct invocation
  - [ ] 2+ skills triggered: Invoke `/cdocs:capture-select`
- [ ] `MultiTriggerConflict` record containing:
  - [ ] `TriggeredSkills` - Array of skill names
  - [ ] `SharedTriggerPhrases` - Phrases that triggered multiple skills
  - [ ] `UniqueTriggersPerSkill` - Map of skill to unique triggers

### Capture Detection Configuration

- [ ] Built-in capture skill auto-invoke configurations:
  - [ ] `/cdocs:problem` - trigger phrases from problem schema
  - [ ] `/cdocs:insight` - trigger phrases from insight schema
  - [ ] `/cdocs:codebase` - trigger phrases from codebase schema
  - [ ] `/cdocs:tool` - trigger phrases from tool schema
  - [ ] `/cdocs:style` - trigger phrases from style schema
- [ ] Each capture skill has precondition: "Project activated via /cdocs:activate"
- [ ] Trigger phrase examples (complete lists from spec):

  **Problem**:
  ```yaml
  trigger_phrases: [fixed, "it's fixed", bug, "the issue was", "problem solved", resolved, exception, error, crash, failing]
  classification_hints: [error message, stack trace, exception, null reference, debugging, root cause, symptoms, workaround, fix]
  ```

  **Insight**:
  ```yaml
  trigger_phrases: [users want, users prefer, interesting that, makes sense because, the reason is, apparently, learned that, realized]
  classification_hints: [business context, user behavior, product, feature, customer, domain, market, requirement, stakeholder]
  ```

  **Codebase**:
  ```yaml
  trigger_phrases: [decided to, going with, settled on, our approach, the pattern is, architecture, structure]
  classification_hints: [architecture, module, component, pattern, structure, design decision, layer, dependency, separation, SOLID]
  ```

  **Tool**:
  ```yaml
  trigger_phrases: [gotcha, watch out for, careful with, heads up, workaround, dependency, library, package, NuGet]
  classification_hints: [library, package, NuGet, dependency, version, configuration, API, SDK, framework, third-party]
  ```

  **Style**:
  ```yaml
  trigger_phrases: [always, never, prefer, convention, standard, rule, don't forget, remember to]
  classification_hints: [convention, style, naming, formatting, standard, best practice, guideline, rule, preference]
  ```

---

## Implementation Notes

### Interface Definitions

Create interfaces in `CompoundDocs.Common/Skills/AutoInvoke/`:

```csharp
// AutoInvokeConfig.cs
public enum AutoInvokeTriggerType
{
    None,
    ProjectEntry,
    ConversationPattern
}

public record AutoInvokeConfig
{
    public AutoInvokeTriggerType Trigger { get; init; } = AutoInvokeTriggerType.None;
    public IReadOnlyList<string> Patterns { get; init; } = [];
}

// SkillAutoInvokeMetadata.cs
public record SkillAutoInvokeMetadata
{
    public required string SkillName { get; init; }
    public string? Description { get; init; }
    public AutoInvokeConfig AutoInvoke { get; init; } = new();
    public IReadOnlyList<string> ClassificationHints { get; init; } = [];
    public IReadOnlyList<string> Preconditions { get; init; } = [];
    public IReadOnlyList<string> AllowedTools { get; init; } = [];
}

// TriggerMatchResult.cs
public record TriggerMatchResult
{
    public required string SkillName { get; init; }
    public IReadOnlyList<string> MatchedPhrases { get; init; } = [];
    public IReadOnlyList<(int Start, int End)> MatchPositions { get; init; } = [];
    public double ClassificationScore { get; init; }
    public bool ShouldAutoInvoke { get; init; }
}

// MultiTriggerConflict.cs
public record MultiTriggerConflict
{
    public IReadOnlyList<string> TriggeredSkills { get; init; } = [];
    public IReadOnlyList<string> SharedTriggerPhrases { get; init; } = [];
    public IReadOnlyDictionary<string, IReadOnlyList<string>> UniqueTriggersPerSkill { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>();
}
```

### Trigger Phrase Matcher Implementation

```csharp
using System.Text.RegularExpressions;

public class TriggerPhraseMatcherService : ITriggerPhraseMatcherService
{
    private static readonly RegexOptions MatchOptions =
        RegexOptions.IgnoreCase | RegexOptions.Compiled;

    public IReadOnlyList<string> MatchTriggerPhrases(
        string conversationText,
        IEnumerable<string> phrases)
    {
        if (string.IsNullOrWhiteSpace(conversationText))
            return [];

        var matches = new List<string>();

        foreach (var phrase in phrases)
        {
            // Build word-boundary pattern for phrase
            // Handles multi-word phrases and special characters
            var pattern = BuildWordBoundaryPattern(phrase);

            if (Regex.IsMatch(conversationText, pattern, MatchOptions))
            {
                matches.Add(phrase);
            }
        }

        return matches;
    }

    public IReadOnlyList<TriggerMatchResult> FindTriggeredSkills(
        string conversationText,
        IEnumerable<SkillAutoInvokeMetadata> skills)
    {
        var results = new List<TriggerMatchResult>();

        foreach (var skill in skills)
        {
            if (skill.AutoInvoke.Trigger != AutoInvokeTriggerType.ConversationPattern)
                continue;

            var matchedPhrases = MatchTriggerPhrases(
                conversationText,
                skill.AutoInvoke.Patterns);

            if (matchedPhrases.Count > 0)
            {
                var positions = FindMatchPositions(conversationText, matchedPhrases);

                results.Add(new TriggerMatchResult
                {
                    SkillName = skill.SkillName,
                    MatchedPhrases = matchedPhrases,
                    MatchPositions = positions
                });
            }
        }

        return results;
    }

    private static string BuildWordBoundaryPattern(string phrase)
    {
        // Escape regex special characters
        var escaped = Regex.Escape(phrase);

        // Add word boundaries
        // Use \b for word boundaries, but handle apostrophes specially
        // "it's fixed" should match as a phrase
        return $@"\b{escaped}\b";
    }

    private static IReadOnlyList<(int Start, int End)> FindMatchPositions(
        string text,
        IReadOnlyList<string> phrases)
    {
        var positions = new List<(int Start, int End)>();

        foreach (var phrase in phrases)
        {
            var pattern = BuildWordBoundaryPattern(phrase);
            var matches = Regex.Matches(text, pattern, MatchOptions);

            foreach (Match match in matches)
            {
                positions.Add((match.Index, match.Index + match.Length));
            }
        }

        return positions.OrderBy(p => p.Start).ToList();
    }
}
```

### Two-Stage Classification Validator

```csharp
public class ClassificationValidator : IClassificationValidator
{
    private const double DefaultThreshold = 0.3; // 30% of hints should match

    private readonly ITriggerPhraseMatcherService _triggerMatcher;

    public ClassificationValidator(ITriggerPhraseMatcherService triggerMatcher)
    {
        _triggerMatcher = triggerMatcher;
    }

    public double ValidateClassificationHints(
        string conversationText,
        IEnumerable<string> hints)
    {
        var hintsList = hints.ToList();
        if (hintsList.Count == 0)
            return 1.0; // No hints means no filter

        var matchedHints = _triggerMatcher.MatchTriggerPhrases(
            conversationText,
            hintsList);

        return (double)matchedHints.Count / hintsList.Count;
    }

    public bool ShouldAutoInvoke(
        TriggerMatchResult triggerMatch,
        string conversationText,
        IReadOnlyList<string> classificationHints,
        double threshold = DefaultThreshold)
    {
        // Stage 1: Must have trigger phrase matches
        if (triggerMatch.MatchedPhrases.Count == 0)
            return false;

        // Stage 2: Classification hint validation
        if (classificationHints.Count == 0)
            return true; // No hints = no filter

        var score = ValidateClassificationHints(conversationText, classificationHints);
        return score >= threshold;
    }
}
```

### Skill Metadata Parser

```csharp
using YamlDotNet.Serialization;
using YamlDotNet.RepresentationModel;

public class SkillMetadataParser : ISkillMetadataParser
{
    private readonly IDeserializer _yamlDeserializer;

    public SkillMetadataParser()
    {
        _yamlDeserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public async Task<SkillAutoInvokeMetadata?> ParseSkillMetadataAsync(
        string skillPath,
        CancellationToken ct = default)
    {
        if (!File.Exists(skillPath))
            return null;

        var content = await File.ReadAllTextAsync(skillPath, ct);

        // Extract YAML frontmatter between --- markers
        var frontmatter = ExtractFrontmatter(content);
        if (string.IsNullOrEmpty(frontmatter))
            return null;

        return ParseFrontmatter(frontmatter);
    }

    private static string? ExtractFrontmatter(string content)
    {
        if (!content.StartsWith("---"))
            return null;

        var endIndex = content.IndexOf("---", 3);
        if (endIndex < 0)
            return null;

        return content.Substring(3, endIndex - 3).Trim();
    }

    private SkillAutoInvokeMetadata ParseFrontmatter(string frontmatter)
    {
        using var reader = new StringReader(frontmatter);
        var yaml = new YamlStream();
        yaml.Load(reader);

        var root = (YamlMappingNode)yaml.Documents[0].RootNode;

        var name = GetScalarValue(root, "name") ?? throw new InvalidOperationException("Skill name is required");
        var description = GetScalarValue(root, "description");
        var allowedTools = GetSequenceValues(root, "allowed-tools");
        var preconditions = GetSequenceValues(root, "preconditions");

        var autoInvoke = ExtractAutoInvokeConfig(root);

        return new SkillAutoInvokeMetadata
        {
            SkillName = name,
            Description = description,
            AutoInvoke = autoInvoke,
            AllowedTools = allowedTools,
            Preconditions = preconditions
        };
    }

    public AutoInvokeConfig ExtractAutoInvokeConfig(YamlMappingNode root)
    {
        if (!root.Children.TryGetValue(new YamlScalarNode("auto-invoke"), out var autoInvokeNode))
            return new AutoInvokeConfig { Trigger = AutoInvokeTriggerType.None };

        var autoInvokeMapping = (YamlMappingNode)autoInvokeNode;

        var triggerValue = GetScalarValue(autoInvokeMapping, "trigger");
        var trigger = ParseTriggerType(triggerValue);
        var patterns = GetSequenceValues(autoInvokeMapping, "patterns");

        // Validate: conversation-pattern requires patterns
        if (trigger == AutoInvokeTriggerType.ConversationPattern && patterns.Count == 0)
        {
            throw new InvalidOperationException(
                "auto-invoke with trigger 'conversation-pattern' requires 'patterns' array");
        }

        return new AutoInvokeConfig
        {
            Trigger = trigger,
            Patterns = patterns
        };
    }

    private static AutoInvokeTriggerType ParseTriggerType(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "project-entry" => AutoInvokeTriggerType.ProjectEntry,
            "conversation-pattern" => AutoInvokeTriggerType.ConversationPattern,
            null => AutoInvokeTriggerType.None,
            _ => throw new InvalidOperationException($"Unknown auto-invoke trigger type: {value}")
        };
    }

    private static string? GetScalarValue(YamlMappingNode node, string key)
    {
        if (node.Children.TryGetValue(new YamlScalarNode(key), out var value))
            return ((YamlScalarNode)value).Value;
        return null;
    }

    private static IReadOnlyList<string> GetSequenceValues(YamlMappingNode node, string key)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode(key), out var value))
            return [];

        var sequence = (YamlSequenceNode)value;
        return sequence.Children
            .OfType<YamlScalarNode>()
            .Select(n => n.Value ?? "")
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();
    }
}
```

### Schema Classification Loader

```csharp
public class SchemaClassificationLoader : ISchemaClassificationLoader
{
    private static readonly Dictionary<string, (string[] TriggerPhrases, string[] ClassificationHints)>
        BuiltInMetadata = new()
    {
        ["problem"] = (
            TriggerPhrases: ["fixed", "it's fixed", "bug", "the issue was", "problem solved",
                            "resolved", "exception", "error", "crash", "failing"],
            ClassificationHints: ["error message", "stack trace", "exception", "null reference",
                                  "debugging", "root cause", "symptoms", "workaround", "fix"]
        ),
        ["insight"] = (
            TriggerPhrases: ["users want", "users prefer", "interesting that", "makes sense because",
                            "the reason is", "apparently", "learned that", "realized"],
            ClassificationHints: ["business context", "user behavior", "product", "feature",
                                  "customer", "domain", "market", "requirement", "stakeholder"]
        ),
        ["codebase"] = (
            TriggerPhrases: ["decided to", "going with", "settled on", "our approach",
                            "the pattern is", "architecture", "structure"],
            ClassificationHints: ["architecture", "module", "component", "pattern", "structure",
                                  "design decision", "layer", "dependency", "separation", "SOLID"]
        ),
        ["tool"] = (
            TriggerPhrases: ["gotcha", "watch out for", "careful with", "heads up",
                            "workaround", "dependency", "library", "package", "NuGet"],
            ClassificationHints: ["library", "package", "NuGet", "dependency", "version",
                                  "configuration", "API", "SDK", "framework", "third-party"]
        ),
        ["style"] = (
            TriggerPhrases: ["always", "never", "prefer", "convention", "standard",
                            "rule", "don't forget", "remember to"],
            ClassificationHints: ["convention", "style", "naming", "formatting", "standard",
                                  "best practice", "guideline", "rule", "preference"]
        )
    };

    private readonly IDocTypeSchemaResolver _schemaResolver;

    public SchemaClassificationLoader(IDocTypeSchemaResolver schemaResolver)
    {
        _schemaResolver = schemaResolver;
    }

    public (IReadOnlyList<string> TriggerPhrases, IReadOnlyList<string> ClassificationHints)
        LoadClassificationMetadata(string docType)
    {
        if (BuiltInMetadata.TryGetValue(docType.ToLowerInvariant(), out var metadata))
        {
            return (metadata.TriggerPhrases, metadata.ClassificationHints);
        }

        // For custom types, would load from schema file
        // Implementation depends on schema format
        return ([], []);
    }

    public IReadOnlyDictionary<string, (IReadOnlyList<string> TriggerPhrases, IReadOnlyList<string> ClassificationHints)>
        GetBuiltInClassificationMetadata()
    {
        return BuiltInMetadata.ToDictionary(
            kv => kv.Key,
            kv => ((IReadOnlyList<string>)kv.Value.TriggerPhrases,
                   (IReadOnlyList<string>)kv.Value.ClassificationHints));
    }
}
```

### Multi-Trigger Resolver

```csharp
public class MultiTriggerResolver : IMultiTriggerResolver
{
    public MultiTriggerConflict? DetectConflicts(IEnumerable<TriggerMatchResult> matches)
    {
        var matchList = matches.Where(m => m.ShouldAutoInvoke).ToList();

        if (matchList.Count < 2)
            return null;

        // Find shared trigger phrases
        var allPhrases = matchList
            .SelectMany(m => m.MatchedPhrases)
            .ToList();

        var sharedPhrases = allPhrases
            .GroupBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        // Build unique triggers per skill
        var uniquePerSkill = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var match in matchList)
        {
            var unique = match.MatchedPhrases
                .Where(p => !sharedPhrases.Contains(p, StringComparer.OrdinalIgnoreCase))
                .ToList();
            uniquePerSkill[match.SkillName] = unique;
        }

        return new MultiTriggerConflict
        {
            TriggeredSkills = matchList.Select(m => m.SkillName).ToList(),
            SharedTriggerPhrases = sharedPhrases,
            UniqueTriggersPerSkill = uniquePerSkill
        };
    }

    public bool ShouldInvokeCaptureSelect(IEnumerable<TriggerMatchResult> matches)
    {
        return matches.Count(m => m.ShouldAutoInvoke) >= 2;
    }
}
```

### Project Entry Detector

```csharp
public class ProjectEntryDetector : IProjectEntryDetector
{
    private readonly ISessionStateService _sessionState;

    public ProjectEntryDetector(ISessionStateService sessionState)
    {
        _sessionState = sessionState;
    }

    public ProjectEntryInfo DetectProjectEntry(string projectRoot)
    {
        var configPath = Path.Combine(
            projectRoot,
            ".csharp-compounding-docs",
            "config.json");

        var configExists = File.Exists(configPath);
        var alreadyActivated = _sessionState.IsProjectActivated(projectRoot);

        return new ProjectEntryInfo
        {
            ProjectRoot = projectRoot,
            ConfigPath = configPath,
            ConfigExists = configExists,
            AlreadyActivated = alreadyActivated,
            ShouldActivate = configExists && !alreadyActivated
        };
    }

    public bool ShouldActivate(string projectRoot)
    {
        var info = DetectProjectEntry(projectRoot);
        return info.ShouldActivate;
    }
}

public record ProjectEntryInfo
{
    public required string ProjectRoot { get; init; }
    public required string ConfigPath { get; init; }
    public bool ConfigExists { get; init; }
    public bool AlreadyActivated { get; init; }
    public bool ShouldActivate { get; init; }
}
```

### Dependency Injection Registration

```csharp
using Microsoft.Extensions.DependencyInjection;

public static class AutoInvokeServiceExtensions
{
    public static IServiceCollection AddAutoInvokeServices(this IServiceCollection services)
    {
        services.AddSingleton<ITriggerPhraseMatcherService, TriggerPhraseMatcherService>();
        services.AddSingleton<IClassificationValidator, ClassificationValidator>();
        services.AddSingleton<ISkillMetadataParser, SkillMetadataParser>();
        services.AddSingleton<ISchemaClassificationLoader, SchemaClassificationLoader>();
        services.AddSingleton<IMultiTriggerResolver, MultiTriggerResolver>();
        services.AddSingleton<IProjectEntryDetector, ProjectEntryDetector>();

        return services;
    }
}
```

---

## File Structure

After implementation, the following files should exist:

```
src/CompoundDocs.Common/
├── Skills/
│   └── AutoInvoke/
│       ├── Abstractions/
│       │   ├── ITriggerPhraseMatcherService.cs
│       │   ├── IClassificationValidator.cs
│       │   ├── ISkillMetadataParser.cs
│       │   ├── ISchemaClassificationLoader.cs
│       │   ├── IMultiTriggerResolver.cs
│       │   └── IProjectEntryDetector.cs
│       ├── Models/
│       │   ├── AutoInvokeConfig.cs
│       │   ├── AutoInvokeTriggerType.cs
│       │   ├── SkillAutoInvokeMetadata.cs
│       │   ├── TriggerMatchResult.cs
│       │   ├── MultiTriggerConflict.cs
│       │   └── ProjectEntryInfo.cs
│       ├── TriggerPhraseMatcherService.cs
│       ├── ClassificationValidator.cs
│       ├── SkillMetadataParser.cs
│       ├── SchemaClassificationLoader.cs
│       ├── MultiTriggerResolver.cs
│       ├── ProjectEntryDetector.cs
│       └── AutoInvokeServiceExtensions.cs
tests/CompoundDocs.Tests/
└── Skills/
    └── AutoInvoke/
        ├── TriggerPhraseMatcherTests.cs
        ├── ClassificationValidatorTests.cs
        ├── SkillMetadataParserTests.cs
        ├── SchemaClassificationLoaderTests.cs
        ├── MultiTriggerResolverTests.cs
        ├── ProjectEntryDetectorTests.cs
        ├── TwoStageClassificationTests.cs
        └── TestData/
            ├── valid-skill-with-autoinvoke.md
            ├── valid-skill-manual-only.md
            ├── valid-skill-project-entry.md
            ├── invalid-skill-missing-patterns.md
            └── sample-conversations/
                ├── problem-context.txt
                ├── insight-context.txt
                ├── style-context-false-positive.txt
                └── multi-trigger-context.txt
```

---

## Dependencies

### Depends On
- **Phase 081**: Skill Infrastructure (skill loading, discovery, basic skill types)
- **Phase 060**: Frontmatter Validation (schema resolution, YAML parsing patterns)
- **Phase 035**: Session State Service (for project activation tracking)
- **Phase 017**: DI Container (dependency injection registration)

### Blocks
- **Phase 083**: Capture Select Meta-Skill (requires multi-trigger resolution)
- **Phase 084**: Project Activation Skill (requires project entry detection)
- Capture skill implementations (require auto-invoke configuration)
- Custom doc-type skill generation (requires classification metadata support)

---

## Verification Steps

After completing this phase, verify:

1. **Trigger Phrase Matching**
   - Single word match: "fixed" in "I fixed the bug" - expect match
   - Multi-word match: "problem solved" in "The problem solved itself" - expect match
   - No partial match: "fixed" should NOT match "affixed"
   - Case insensitivity: "FIXED" matches "fixed"
   - Multiple phrases: conversation with "bug" and "error" - expect both matched

2. **Two-Stage Classification**
   - Problem context with hints: "Found the error message in logs, debugged it" - expect high score
   - Style trigger without hints: "I always drink coffee" - expect low score (style trigger "always" but no style hints)
   - Tool context: "Watch out for this NuGet package version" - expect tool skill triggered

3. **Skill Metadata Parsing**
   - Parse valid SKILL.md with auto-invoke section
   - Parse SKILL.md without auto-invoke (manual-only)
   - Parse project-entry trigger type
   - Reject invalid trigger type
   - Reject conversation-pattern without patterns

4. **Multi-Trigger Detection**
   - Single skill triggered: returns that skill directly
   - Two skills triggered: returns conflict with both skills
   - Example: "I fixed a bug in the NuGet package" - triggers both problem and tool

5. **Project Entry Detection**
   - Directory with config.json - expect ShouldActivate = true
   - Directory without config.json - expect ShouldActivate = false
   - Already activated project - expect ShouldActivate = false

---

## Testing Notes

Create unit tests in `tests/CompoundDocs.Tests/Skills/AutoInvoke/`:

### Test Scenarios

```csharp
// TriggerPhraseMatcherTests.cs
[Fact] public void MatchTriggerPhrases_SingleWord_ReturnsMatch()
[Fact] public void MatchTriggerPhrases_MultiWord_ReturnsMatch()
[Fact] public void MatchTriggerPhrases_NoMatch_ReturnsEmpty()
[Fact] public void MatchTriggerPhrases_PartialWord_DoesNotMatch()
[Fact] public void MatchTriggerPhrases_CaseInsensitive_ReturnsMatch()
[Fact] public void MatchTriggerPhrases_Apostrophe_HandlesCorrectly()
[Fact] public void FindTriggeredSkills_MultipleSkills_ReturnsAll()

// ClassificationValidatorTests.cs
[Fact] public void ValidateClassificationHints_AllMatch_ReturnsOne()
[Fact] public void ValidateClassificationHints_PartialMatch_ReturnsRatio()
[Fact] public void ValidateClassificationHints_NoMatch_ReturnsZero()
[Fact] public void ValidateClassificationHints_EmptyHints_ReturnsOne()
[Fact] public void ShouldAutoInvoke_TriggerAndHintsMatch_ReturnsTrue()
[Fact] public void ShouldAutoInvoke_TriggerButNoHints_ReturnsFalse()
[Fact] public void ShouldAutoInvoke_NoTrigger_ReturnsFalse()

// TwoStageClassificationTests.cs
[Fact] public void StyleTrigger_WithoutStyleContext_DoesNotAutoInvoke()
[Fact] public void StyleTrigger_WithStyleContext_AutoInvokes()
[Fact] public void ProblemTrigger_WithDebuggingContext_AutoInvokes()
[Fact] public void GenericPhrase_FilteredByHints()

// SkillMetadataParserTests.cs
[Fact] public async Task ParseSkillMetadataAsync_ValidSkill_ReturnsMetadata()
[Fact] public async Task ParseSkillMetadataAsync_NoAutoInvoke_ReturnsNoneTrigger()
[Fact] public async Task ParseSkillMetadataAsync_ProjectEntry_ReturnsTrigger()
[Fact] public async Task ParseSkillMetadataAsync_MissingPatterns_ThrowsException()
[Fact] public async Task ParseSkillMetadataAsync_InvalidTrigger_ThrowsException()
[Fact] public async Task ParseSkillMetadataAsync_FileNotFound_ReturnsNull()

// MultiTriggerResolverTests.cs
[Fact] public void DetectConflicts_SingleSkill_ReturnsNull()
[Fact] public void DetectConflicts_TwoSkills_ReturnsConflict()
[Fact] public void DetectConflicts_SharedPhrases_IdentifiesCorrectly()
[Fact] public void ShouldInvokeCaptureSelect_TwoOrMore_ReturnsTrue()
[Fact] public void ShouldInvokeCaptureSelect_OneOrZero_ReturnsFalse()

// ProjectEntryDetectorTests.cs
[Fact] public void DetectProjectEntry_ConfigExists_ReturnsShouldActivate()
[Fact] public void DetectProjectEntry_ConfigMissing_ReturnsNoActivation()
[Fact] public void DetectProjectEntry_AlreadyActivated_ReturnsNoActivation()
```

### Test Data Files

Create sample SKILL.md files for testing:

```yaml
# valid-skill-with-autoinvoke.md
---
name: cdocs:problem
description: Capture solved problems with symptoms, root cause, and solution
allowed-tools:
  - Read
preconditions:
  - Project activated via /cdocs:activate
auto-invoke:
  trigger: conversation-pattern
  patterns:
    - "fixed"
    - "it's fixed"
    - "problem solved"
    - "the issue was"
---

# Problem Documentation Skill
...
```

```yaml
# valid-skill-manual-only.md
---
name: cdocs:custom
description: Manual-only custom skill
allowed-tools:
  - Read
  - Write
---

# Custom Skill
...
```

Create sample conversation texts for integration testing:

```text
# problem-context.txt
User: I've been debugging this for hours. Finally fixed it!
The issue was a null reference exception in the database connection pooler.
The error message was "Object reference not set to an instance of an object"
and it happened whenever concurrent requests exceeded 10.
The root cause was the connection pool size being too small.
```

```text
# style-context-false-positive.txt
User: I always drink coffee in the morning.
It helps me wake up and focus on my work.
I never skip it, even on weekends.
```

---

## Notes

- **Performance**: Trigger phrase matching may run frequently. Use compiled regex and consider caching patterns for repeated evaluations.
- **False Positive Mitigation**: The two-stage classification is critical for generic phrases like "always", "never", "fixed". Without classification hints, these would trigger too often.
- **Extensibility**: The schema classification loader should support loading trigger phrases and hints from custom doc-type schema files, not just built-in types.
- **Session Tracking**: Project entry detection relies on session state to prevent re-activation. Ensure session state persists across conversation turns.
- **Multi-Trigger UX**: When multiple skills trigger, the user sees a selection dialog. The conflict information helps them understand why multiple options appeared.
- **Spec Alignment**: All trigger phrases and classification hints are taken directly from spec/doc-types/built-in-types.md to ensure consistency.
