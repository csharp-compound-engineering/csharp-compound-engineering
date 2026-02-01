# Phase 089: /cdocs:style Capture Skill

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-5 hours
> **Category**: Skills System
> **Prerequisites**: Phase 081 (Skills Infrastructure), Phase 082 (Capture Skill Base), Phase 083 (Problem Skill)

---

## Spec References

This phase implements the `/cdocs:style` capture skill defined in:

- **spec/skills/capture-skills.md** - [/cdocs:style Specification](../spec/skills/capture-skills.md#cdocsstyle) (lines 194-231)
- **spec/doc-types/built-in-types.md** - [Coding Styles & Preferences Schema](../spec/doc-types/built-in-types.md#5-coding-styles--preferences-style) (lines 255-307)
- **research/claude-code-skills-research.md** - Skill auto-invoke mechanisms and trigger phrase handling

---

## Objectives

1. Create SKILL.md file for the `/cdocs:style` capture skill
2. Define trigger phrases for coding convention and preference detection
3. Implement schema.yaml for the style doc-type
4. Configure output directory (`./csharp-compounding-docs/styles/`)
5. Define fields for convention type, examples, rationale, and exceptions
6. Support team standards documentation capture

---

## Acceptance Criteria

### SKILL.md Content
- [ ] SKILL.md created at `./csharp-compounding-docs/.skills/cdocs-style/SKILL.md`
- [ ] YAML frontmatter includes skill name, description, and auto-invoke configuration
- [ ] Description optimized for Claude's semantic matching of coding conventions
- [ ] Instructions section covers style preference extraction workflow
- [ ] MCP integration section references Sequential Thinking for rationale analysis

### Trigger Phrases
- [ ] `trigger_phrases` array includes: "always", "never", "prefer", "convention", "standard", "rule", "don't forget", "remember to"
- [ ] Additional C#/.NET-specific triggers: "StyleCop", "editorconfig", "naming convention", "code style"
- [ ] Triggers balanced to catch style discussions without false positives on casual usage

### schema.yaml for Style Doc-Type
- [ ] Schema file created at `./csharp-compounding-docs/.doc-types/style/schema.yaml`
- [ ] `name: style` with description: "Coding conventions, preferences, and team standards"
- [ ] `trigger_phrases` array matches SKILL.md triggers
- [ ] `classification_hints` include: "convention", "style", "naming", "formatting", "standard", "best practice", "guideline", "rule", "preference"

### Required Fields
- [ ] `style_type` (enum): `naming`, `architecture`, `error_handling`, `testing`, `documentation`, `formatting`, `async_patterns`
- [ ] `scope` (enum): `project`, `module`, `team`
- [ ] `rationale` (string): Why this style is preferred

### Optional Fields
- [ ] `examples` (array): Good and bad code examples demonstrating the convention
- [ ] `exceptions` (string): When this rule doesn't apply
- [ ] `enforcement` (enum): `required`, `recommended`, `optional` - how strictly to apply
- [ ] `related_rules` (array): Links to related style documents
- [ ] `tooling` (string): Tools that enforce this (StyleCop, editorconfig, analyzers)

### Output Directory
- [ ] Documents written to `./csharp-compounding-docs/styles/`
- [ ] Filename format: `{style-type}-{sanitized-title}-{YYYYMMDD}.md`
- [ ] Directory created automatically if not exists

### Team Standards Documentation
- [ ] Skill captures team-agreed conventions vs personal preferences
- [ ] `scope: team` indicates agreed-upon standards
- [ ] `scope: project` indicates project-specific conventions
- [ ] Rationale field captures the "why" behind the standard

---

## Implementation Notes

### SKILL.md Structure

```markdown
---
name: cdocs:style
description: |
  Capture coding style preferences, naming conventions, and team standards.
  Triggers when discussing conventions, rules, or preferences like
  "we always use", "never do", "our convention is", or "prefer X over Y".
  Documents the rationale and identifies valid exceptions.
auto_invoke: true
trigger_phrases:
  - "always"
  - "never"
  - "prefer"
  - "convention"
  - "standard"
  - "rule"
  - "don't forget"
  - "remember to"
  - "StyleCop"
  - "editorconfig"
  - "naming convention"
  - "code style"
---

# /cdocs:style

Capture coding style preferences and team standards as documentation.

## When to Use

This skill activates when the conversation indicates a coding convention or preference:
- Team agreements on coding style ("we always use PascalCase for public methods")
- Formatting preferences ("prefer expression-bodied members for simple getters")
- Naming conventions ("never use Hungarian notation")
- Architecture patterns ("always use async/await over Task.Result")
- Error handling standards ("prefer throwing specific exceptions over generic ones")

## Behavior

1. **Detect** trigger phrase or manual invocation
2. **Extract** the style preference from conversation context
3. **Analyze** using Sequential Thinking to:
   - Document the rationale (why this convention exists)
   - Identify valid exceptions (when the rule shouldn't apply)
   - Classify the style type and scope
4. **Validate** against the style schema
5. **Generate** filename: `{style-type}-{sanitized-title}-{YYYYMMDD}.md`
6. **Write** to `./csharp-compounding-docs/styles/`
7. **Confirm** with decision menu

## MCP Integration

- **Sequential Thinking**: Use for evaluating style rationale and identifying exceptions
  - Analyze: Why does this convention improve code quality?
  - Consider: Are there legitimate cases where this doesn't apply?
  - Document: Both the rule and its boundaries

## Output Format

```yaml
---
doc_type: style
style_type: naming  # or: architecture, error_handling, testing, documentation, formatting, async_patterns
scope: team  # or: project, module
title: "Use PascalCase for Public Methods"
rationale: "Consistent with .NET Framework Design Guidelines and improves code readability"
examples:
  - good: "public void ProcessOrder()"
  - bad: "public void processOrder()"
exceptions: "Test method names may use underscores for readability: Should_ReturnNull_When_InputIsEmpty"
enforcement: recommended
tooling: "StyleCop SA1300, editorconfig dotnet_naming_rule"
created: 2025-01-15
---

# Use PascalCase for Public Methods

[Detailed explanation of the convention...]
```

## DO Capture

- Non-obvious conventions that new team members wouldn't know
- Conventions with specific rationale (not just "because we always have")
- Style decisions that have trade-offs worth documenting
- Team agreements from code review discussions

## DO NOT Capture

- Standard .NET conventions documented in Microsoft guidelines
- Personal preferences not agreed upon by the team
- Formatting rules already enforced by automated tooling
- Trivial preferences without meaningful rationale
```

### schema.yaml Structure

```yaml
name: style
description: Coding conventions, preferences, and team standards

trigger_phrases:
  - "always"
  - "never"
  - "prefer"
  - "convention"
  - "standard"
  - "rule"
  - "don't forget"
  - "remember to"
  - "StyleCop"
  - "editorconfig"
  - "naming convention"
  - "code style"

classification_hints:
  - "convention"
  - "style"
  - "naming"
  - "formatting"
  - "standard"
  - "best practice"
  - "guideline"
  - "rule"
  - "preference"
  - "PascalCase"
  - "camelCase"
  - "async"
  - "SOLID"
  - "DRY"

required_fields:
  - name: style_type
    type: enum
    values: [naming, architecture, error_handling, testing, documentation, formatting, async_patterns]
    description: Category of the coding style
  - name: scope
    type: enum
    values: [project, module, team]
    description: Applicability scope of this style
  - name: rationale
    type: string
    description: Why this style is preferred

optional_fields:
  - name: examples
    type: array
    description: Good and bad examples demonstrating the convention
  - name: exceptions
    type: string
    description: When this rule doesn't apply
  - name: enforcement
    type: enum
    values: [required, recommended, optional]
    description: How strictly this rule should be applied
  - name: related_rules
    type: array
    description: Links to related style documents
  - name: tooling
    type: string
    description: Tools that enforce this style (StyleCop, editorconfig, analyzers)
```

### Skill Implementation Class

```csharp
using CSharpCompoundDocs.Skills.Base;
using CSharpCompoundDocs.DocTypes;
using Microsoft.Extensions.Logging;

namespace CSharpCompoundDocs.Skills.Capture;

/// <summary>
/// Capture skill for coding style preferences and team standards.
/// </summary>
public class StyleCaptureSkill : CaptureSkillBase
{
    private const string DocTypeName = "style";
    private const string OutputDirectory = "styles";

    public StyleCaptureSkill(
        IDocTypeRegistry docTypeRegistry,
        IDocumentWriter documentWriter,
        ISequentialThinkingClient sequentialThinking,
        ILogger<StyleCaptureSkill> logger)
        : base(docTypeRegistry, documentWriter, sequentialThinking, logger)
    {
    }

    public override string SkillName => "cdocs:style";

    public override string Description =>
        "Capture coding style preferences, naming conventions, and team standards. " +
        "Triggers when discussing conventions, rules, or preferences.";

    public override IReadOnlyList<string> TriggerPhrases => new[]
    {
        "always",
        "never",
        "prefer",
        "convention",
        "standard",
        "rule",
        "don't forget",
        "remember to",
        "StyleCop",
        "editorconfig",
        "naming convention",
        "code style"
    };

    protected override string GetDocTypeName() => DocTypeName;

    protected override string GetOutputDirectory() => OutputDirectory;

    protected override async Task<StyleAnalysisResult> AnalyzeWithSequentialThinking(
        ConversationContext context,
        CancellationToken cancellationToken)
    {
        // Use Sequential Thinking to:
        // 1. Extract the core style preference from conversation
        // 2. Determine the style type (naming, architecture, etc.)
        // 3. Evaluate the rationale for this convention
        // 4. Identify valid exceptions where the rule shouldn't apply
        // 5. Determine enforcement level and scope

        var prompt = BuildAnalysisPrompt(context);
        var analysis = await _sequentialThinking.AnalyzeAsync(prompt, cancellationToken);

        return new StyleAnalysisResult
        {
            StyleType = analysis.GetEnum<StyleType>("style_type"),
            Scope = analysis.GetEnum<StyleScope>("scope"),
            Title = analysis.GetString("title"),
            Rationale = analysis.GetString("rationale"),
            Examples = analysis.GetArray<StyleExample>("examples"),
            Exceptions = analysis.GetString("exceptions"),
            Enforcement = analysis.GetEnum<EnforcementLevel>("enforcement"),
            Tooling = analysis.GetString("tooling")
        };
    }

    private string BuildAnalysisPrompt(ConversationContext context)
    {
        return $"""
            Analyze this conversation to extract a coding style preference or convention.

            Conversation:
            {context.RecentMessages}

            Determine:
            1. style_type: What category? (naming, architecture, error_handling, testing, documentation, formatting, async_patterns)
            2. scope: Who does this apply to? (project, module, team)
            3. title: A concise title for this convention
            4. rationale: Why is this style preferred? What problems does it solve?
            5. examples: Provide good and bad code examples if applicable
            6. exceptions: When should this rule NOT apply?
            7. enforcement: How strictly should this be followed? (required, recommended, optional)
            8. tooling: What tools can enforce this? (StyleCop rules, editorconfig settings, Roslyn analyzers)

            Focus on conventions that:
            - Are team-specific or project-specific (not standard .NET guidelines)
            - Have meaningful rationale (not just "because we always have")
            - Would benefit new team members
            """;
    }

    protected override string GenerateFilename(StyleAnalysisResult analysis)
    {
        var sanitizedTitle = SanitizeForFilename(analysis.Title);
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        return $"{analysis.StyleType.ToString().ToLowerInvariant()}-{sanitizedTitle}-{date}.md";
    }

    protected override async Task<DocumentContent> GenerateDocument(
        StyleAnalysisResult analysis,
        CancellationToken cancellationToken)
    {
        var frontmatter = new Dictionary<string, object>
        {
            ["doc_type"] = DocTypeName,
            ["style_type"] = analysis.StyleType.ToString().ToLowerInvariant(),
            ["scope"] = analysis.Scope.ToString().ToLowerInvariant(),
            ["title"] = analysis.Title,
            ["rationale"] = analysis.Rationale,
            ["created"] = DateTime.UtcNow.ToString("yyyy-MM-dd")
        };

        if (analysis.Examples?.Any() == true)
        {
            frontmatter["examples"] = analysis.Examples;
        }

        if (!string.IsNullOrEmpty(analysis.Exceptions))
        {
            frontmatter["exceptions"] = analysis.Exceptions;
        }

        if (analysis.Enforcement != EnforcementLevel.Recommended)
        {
            frontmatter["enforcement"] = analysis.Enforcement.ToString().ToLowerInvariant();
        }

        if (!string.IsNullOrEmpty(analysis.Tooling))
        {
            frontmatter["tooling"] = analysis.Tooling;
        }

        var body = GenerateDocumentBody(analysis);

        return new DocumentContent(frontmatter, body);
    }

    private string GenerateDocumentBody(StyleAnalysisResult analysis)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {analysis.Title}");
        sb.AppendLine();
        sb.AppendLine("## Rationale");
        sb.AppendLine();
        sb.AppendLine(analysis.Rationale);
        sb.AppendLine();

        if (analysis.Examples?.Any() == true)
        {
            sb.AppendLine("## Examples");
            sb.AppendLine();
            foreach (var example in analysis.Examples)
            {
                if (!string.IsNullOrEmpty(example.Good))
                {
                    sb.AppendLine("### Good");
                    sb.AppendLine("```csharp");
                    sb.AppendLine(example.Good);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
                if (!string.IsNullOrEmpty(example.Bad))
                {
                    sb.AppendLine("### Bad");
                    sb.AppendLine("```csharp");
                    sb.AppendLine(example.Bad);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
            }
        }

        if (!string.IsNullOrEmpty(analysis.Exceptions))
        {
            sb.AppendLine("## Exceptions");
            sb.AppendLine();
            sb.AppendLine(analysis.Exceptions);
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(analysis.Tooling))
        {
            sb.AppendLine("## Tooling");
            sb.AppendLine();
            sb.AppendLine($"This convention can be enforced with: {analysis.Tooling}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

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

public enum EnforcementLevel
{
    Required,
    Recommended,
    Optional
}

public record StyleExample(string? Good, string? Bad);

public record StyleAnalysisResult
{
    public StyleType StyleType { get; init; }
    public StyleScope Scope { get; init; }
    public required string Title { get; init; }
    public required string Rationale { get; init; }
    public IReadOnlyList<StyleExample>? Examples { get; init; }
    public string? Exceptions { get; init; }
    public EnforcementLevel Enforcement { get; init; } = EnforcementLevel.Recommended;
    public string? Tooling { get; init; }
}
```

### Dependency Injection Registration

```csharp
// In skill registration
services.AddScoped<StyleCaptureSkill>();
services.AddScoped<ICaptureSkill>(sp => sp.GetRequiredService<StyleCaptureSkill>());
```

---

## Dependencies

### Depends On
- Phase 081: Skills Infrastructure (skill base classes, registration)
- Phase 082: Capture Skill Base (CaptureSkillBase, common capture logic)
- Phase 083: Problem Skill (pattern for implementing capture skills)
- Phase 014: Schema Validation (YAML schema validation)
- Phase 059: Frontmatter Parsing (YAML frontmatter handling)

### Blocks
- Phase 095: Multi-Trigger Conflict Resolution (needs all capture skills)
- Team coding standards documentation workflows

---

## Verification Steps

After completing this phase, verify:

1. **SKILL.md created**: File exists at expected path with correct frontmatter
2. **schema.yaml created**: Schema file validates style documents correctly
3. **Triggers work**: Skill activates on "convention", "always", "prefer" phrases
4. **Document generation**: Style documents created with correct structure
5. **Examples captured**: Good/bad code examples included when provided
6. **Rationale documented**: The "why" is captured, not just the "what"

---

## Unit Test Scenarios

```csharp
[Fact]
public void TriggerPhrases_IncludesAllExpectedTriggers()
{
    var skill = new StyleCaptureSkill(
        Mock.Of<IDocTypeRegistry>(),
        Mock.Of<IDocumentWriter>(),
        Mock.Of<ISequentialThinkingClient>(),
        NullLogger<StyleCaptureSkill>.Instance);

    Assert.Contains("always", skill.TriggerPhrases);
    Assert.Contains("never", skill.TriggerPhrases);
    Assert.Contains("prefer", skill.TriggerPhrases);
    Assert.Contains("convention", skill.TriggerPhrases);
    Assert.Contains("StyleCop", skill.TriggerPhrases);
}

[Fact]
public void GenerateFilename_IncludesStyleTypeAndDate()
{
    var skill = CreateSkill();
    var analysis = new StyleAnalysisResult
    {
        StyleType = StyleType.Naming,
        Scope = StyleScope.Team,
        Title = "Use PascalCase for Public Methods",
        Rationale = "Consistency with .NET guidelines"
    };

    var filename = skill.GenerateFilename(analysis);

    Assert.StartsWith("naming-", filename);
    Assert.Contains("use-pascalcase", filename.ToLowerInvariant());
    Assert.Matches(@"\d{8}\.md$", filename);
}

[Fact]
public async Task GenerateDocument_IncludesRequiredFields()
{
    var skill = CreateSkill();
    var analysis = new StyleAnalysisResult
    {
        StyleType = StyleType.ErrorHandling,
        Scope = StyleScope.Project,
        Title = "Prefer Specific Exceptions",
        Rationale = "Specific exceptions are easier to catch and handle"
    };

    var document = await skill.GenerateDocument(analysis, CancellationToken.None);

    Assert.Equal("style", document.Frontmatter["doc_type"]);
    Assert.Equal("error_handling", document.Frontmatter["style_type"]);
    Assert.Equal("project", document.Frontmatter["scope"]);
    Assert.Equal("Prefer Specific Exceptions", document.Frontmatter["title"]);
    Assert.NotNull(document.Frontmatter["rationale"]);
}

[Fact]
public async Task GenerateDocument_IncludesExamplesWhenProvided()
{
    var skill = CreateSkill();
    var analysis = new StyleAnalysisResult
    {
        StyleType = StyleType.Naming,
        Scope = StyleScope.Team,
        Title = "Async Method Suffix",
        Rationale = "Clear indication of async behavior",
        Examples = new[]
        {
            new StyleExample(
                Good: "public async Task<User> GetUserAsync(int id)",
                Bad: "public async Task<User> GetUser(int id)")
        }
    };

    var document = await skill.GenerateDocument(analysis, CancellationToken.None);

    Assert.Contains("examples", document.Frontmatter.Keys);
    Assert.Contains("GetUserAsync", document.Body);
    Assert.Contains("### Good", document.Body);
    Assert.Contains("### Bad", document.Body);
}

[Fact]
public async Task GenerateDocument_IncludesExceptionsWhenProvided()
{
    var skill = CreateSkill();
    var analysis = new StyleAnalysisResult
    {
        StyleType = StyleType.Naming,
        Scope = StyleScope.Team,
        Title = "No Hungarian Notation",
        Rationale = "Modern IDEs make type prefixes redundant",
        Exceptions = "Form controls may use prefixes (btnSubmit, txtName) for legacy compatibility"
    };

    var document = await skill.GenerateDocument(analysis, CancellationToken.None);

    Assert.Contains("exceptions", document.Frontmatter.Keys);
    Assert.Contains("## Exceptions", document.Body);
    Assert.Contains("Form controls", document.Body);
}

[Fact]
public async Task GenerateDocument_IncludesToolingWhenProvided()
{
    var skill = CreateSkill();
    var analysis = new StyleAnalysisResult
    {
        StyleType = StyleType.Formatting,
        Scope = StyleScope.Project,
        Title = "Four Space Indentation",
        Rationale = "Consistent with .NET defaults",
        Tooling = "editorconfig: indent_size = 4, StyleCop SA1027"
    };

    var document = await skill.GenerateDocument(analysis, CancellationToken.None);

    Assert.Contains("tooling", document.Frontmatter.Keys);
    Assert.Contains("## Tooling", document.Body);
    Assert.Contains("editorconfig", document.Body);
}

[Fact]
public void StyleType_IncludesAsyncPatterns()
{
    // Verify the async_patterns enum value exists for C#-specific conventions
    Assert.True(Enum.IsDefined(typeof(StyleType), StyleType.AsyncPatterns));
}

[Theory]
[InlineData("We always use PascalCase for public properties")]
[InlineData("Never use var when the type isn't obvious")]
[InlineData("Our convention is to suffix async methods with Async")]
[InlineData("Remember to dispose IDisposable objects")]
public void ShouldTrigger_ReturnsTrueForStyleConversations(string message)
{
    var skill = CreateSkill();
    var context = new ConversationContext(new[] { message });

    var shouldTrigger = skill.ShouldTrigger(context);

    Assert.True(shouldTrigger);
}

[Theory]
[InlineData("The database always returns null for missing records")]  // "always" but not a style
[InlineData("I prefer pizza for lunch")]  // "prefer" but not coding related
public void ShouldTrigger_ReturnsFalseForNonStyleConversations(string message)
{
    var skill = CreateSkill();
    var context = new ConversationContext(new[] { message });

    // Note: This requires semantic analysis beyond simple phrase matching
    // The skill may need additional context analysis to avoid false positives
    // This test documents the expected behavior
}

[Fact]
public void SchemaValidation_RequiresStyleType()
{
    var schema = LoadStyleSchema();
    var document = new Dictionary<string, object>
    {
        ["scope"] = "team",
        ["rationale"] = "Test rationale"
        // Missing style_type
    };

    var result = schema.Validate(document);

    Assert.False(result.IsValid);
    Assert.Contains("style_type", result.Errors.First().PropertyName);
}

[Fact]
public void SchemaValidation_RequiresScope()
{
    var schema = LoadStyleSchema();
    var document = new Dictionary<string, object>
    {
        ["style_type"] = "naming",
        ["rationale"] = "Test rationale"
        // Missing scope
    };

    var result = schema.Validate(document);

    Assert.False(result.IsValid);
    Assert.Contains("scope", result.Errors.First().PropertyName);
}

[Fact]
public void SchemaValidation_RequiresRationale()
{
    var schema = LoadStyleSchema();
    var document = new Dictionary<string, object>
    {
        ["style_type"] = "naming",
        ["scope"] = "team"
        // Missing rationale
    };

    var result = schema.Validate(document);

    Assert.False(result.IsValid);
    Assert.Contains("rationale", result.Errors.First().PropertyName);
}

[Theory]
[InlineData("naming")]
[InlineData("architecture")]
[InlineData("error_handling")]
[InlineData("testing")]
[InlineData("documentation")]
[InlineData("formatting")]
[InlineData("async_patterns")]
public void SchemaValidation_AcceptsValidStyleTypes(string styleType)
{
    var schema = LoadStyleSchema();
    var document = new Dictionary<string, object>
    {
        ["style_type"] = styleType,
        ["scope"] = "team",
        ["rationale"] = "Test rationale"
    };

    var result = schema.Validate(document);

    Assert.True(result.IsValid);
}

[Theory]
[InlineData("project")]
[InlineData("module")]
[InlineData("team")]
public void SchemaValidation_AcceptsValidScopes(string scope)
{
    var schema = LoadStyleSchema();
    var document = new Dictionary<string, object>
    {
        ["style_type"] = "naming",
        ["scope"] = scope,
        ["rationale"] = "Test rationale"
    };

    var result = schema.Validate(document);

    Assert.True(result.IsValid);
}
```

---

## Example Captured Documents

### Example 1: Naming Convention

```yaml
---
doc_type: style
style_type: naming
scope: team
title: "Async Method Suffix Convention"
rationale: "The Async suffix clearly communicates that a method returns a Task and should be awaited. This prevents common bugs where developers forget to await async calls."
examples:
  - good: "public async Task<User> GetUserAsync(int id)"
  - bad: "public async Task<User> GetUser(int id)"
exceptions: "Entry points like Main, event handlers, and interface implementations where the signature is fixed may omit the suffix."
enforcement: required
tooling: "Roslyn analyzer VSTHRD200"
created: 2025-01-15
---

# Async Method Suffix Convention

## Rationale

The Async suffix clearly communicates that a method returns a Task and should be awaited. This prevents common bugs where developers forget to await async calls.

## Examples

### Good
```csharp
public async Task<User> GetUserAsync(int id)
```

### Bad
```csharp
public async Task<User> GetUser(int id)
```

## Exceptions

Entry points like Main, event handlers, and interface implementations where the signature is fixed may omit the suffix.

## Tooling

This convention can be enforced with: Roslyn analyzer VSTHRD200
```

### Example 2: Error Handling Style

```yaml
---
doc_type: style
style_type: error_handling
scope: project
title: "Prefer Result Types Over Exceptions for Expected Failures"
rationale: "Using Result<T, E> for expected failure cases makes error handling explicit in the type system, prevents exception-driven control flow, and improves performance for hot paths."
examples:
  - good: "public Result<User, ValidationError> CreateUser(CreateUserRequest request)"
  - bad: "public User CreateUser(CreateUserRequest request) // throws ValidationException"
exceptions: "Infrastructure failures (network, database) should still throw exceptions as they represent unexpected system states."
enforcement: recommended
created: 2025-01-15
---

# Prefer Result Types Over Exceptions for Expected Failures

## Rationale

Using Result<T, E> for expected failure cases makes error handling explicit in the type system, prevents exception-driven control flow, and improves performance for hot paths.

## Examples

### Good
```csharp
public Result<User, ValidationError> CreateUser(CreateUserRequest request)
```

### Bad
```csharp
public User CreateUser(CreateUserRequest request) // throws ValidationException
```

## Exceptions

Infrastructure failures (network, database) should still throw exceptions as they represent unexpected system states.
```

---

## Notes

- The style skill has a high false-positive risk due to common trigger words ("always", "never", "prefer")
- Semantic analysis should consider context to distinguish coding conventions from casual usage
- The `async_patterns` style type is C#-specific, reflecting the project's focus
- Consider integration with .editorconfig and StyleCop for automated enforcement
- Team scope documents represent agreed-upon standards; project scope may be experimental
