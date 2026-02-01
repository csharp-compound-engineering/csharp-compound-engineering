# Phase 088: /cdocs:tool Capture Skill

> **Status**: PLANNED
> **Category**: Skills System
> **Estimated Effort**: M
> **Prerequisites**: Phase 081 (Skills Base Infrastructure), Phase 082 (Skill Frontmatter), Phase 083 (Auto-Invoke Triggers)

---

## Spec References

- [spec/skills/capture-skills.md - /cdocs:tool](../spec/skills/capture-skills.md#cdocstool)
- [spec/doc-types/built-in-types.md - Tools & Libraries](../spec/doc-types/built-in-types.md#4-tools--libraries-tool)
- [spec/skills/skill-patterns.md](../spec/skills/skill-patterns.md)

---

## Objectives

1. Create SKILL.md for `/cdocs:tool` skill to capture tool/library knowledge
2. Define trigger phrases for dependency gotchas and library warnings
3. Create schema.yaml for tool doc-type with version-specific knowledge fields
4. Configure output directory: `./csharp-compounding-docs/tools/`
5. Implement version-specific knowledge capture workflow
6. Integrate Sequential Thinking MCP for determining version specificity

---

## Acceptance Criteria

- [ ] SKILL.md created at `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-tool/SKILL.md`
- [ ] Schema.yaml created at `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-tool/schema.yaml`
- [ ] Auto-invoke triggers include all spec-defined phrases (gotcha, watch out for, careful with, etc.)
- [ ] Sequential Thinking integration for determining if issue is version-specific or configuration-related
- [ ] Required fields: `tool_name`, `version`, `knowledge_type`
- [ ] Optional fields: `official_docs_gap`, `related_tools`
- [ ] Knowledge types: `gotcha`, `configuration`, `integration`, `performance`, `workaround`, `deprecation`
- [ ] File naming convention: `{tool-name}-{sanitized-title}-{YYYYMMDD}.md`
- [ ] Output directory: `./csharp-compounding-docs/tools/`
- [ ] Post-capture decision menu with standard options
- [ ] Unit tests for skill file validation
- [ ] Integration tests for capture workflow

---

## Implementation Notes

### 1. Skill Directory Structure

```
${CLAUDE_PLUGIN_ROOT}/skills/cdocs-tool/
├── SKILL.md              # Main skill definition
├── schema.yaml           # Tool doc-type schema
└── references/
    └── examples.md       # Example captured tool docs
```

### 2. SKILL.md Content

```yaml
---
name: cdocs:tool
description: Captures tool, library, and dependency knowledge including gotchas, workarounds, and version-specific behavior that supplements or fills gaps in official documentation.
allowed-tools:
  - Read
  - Write
  - Bash
  - mcp__sequential-thinking__sequentialthinking
preconditions:
  - Project activated via /cdocs:activate
  - ./csharp-compounding-docs/ directory exists
auto-invoke:
  trigger: conversation-pattern
  patterns:
    - "gotcha"
    - "watch out for"
    - "careful with"
    - "heads up"
    - "workaround"
    - "dependency"
    - "library"
    - "package"
    - "NuGet"
---

# Tool/Library Knowledge Capture Skill

## Purpose

Capture institutional knowledge about tools, libraries, and dependencies that:
- Supplements official documentation
- Documents gotchas and non-obvious behavior
- Records version-specific quirks
- Preserves workarounds and integration patterns

## Intake

This skill expects context from the conversation about:
- Tool or library name (NuGet package, npm module, SDK, etc.)
- Version where behavior was observed
- The specific issue, gotcha, or knowledge to capture
- Whether this information is missing from official docs

## Process

### Step 1: Gather Context

Extract from conversation history:

**REQUIRED:**
- **Tool Name**: Exact name of the tool/library/package
- **Version**: Specific version where behavior observed (or version range)
- **Knowledge Content**: The actual insight, gotcha, or workaround

**CONTEXTUAL:**
- **Related tools**: Other tools/libraries this interacts with
- **Configuration**: Relevant config settings or environment
- **Code examples**: Sample code demonstrating the issue/solution

**BLOCKING**: If tool name OR version cannot be determined, ask and WAIT:
"I'd like to capture this tool knowledge. Can you confirm:
1. Which tool/library is this about?
2. What version(s) are affected?"

### Step 2: Classify Knowledge Type

Use Sequential Thinking MCP to determine:

```
<mcp_tool>
<tool_name>mcp__sequential-thinking__sequentialthinking</tool_name>
<parameters>
{
  "thought": "Analyzing tool knowledge to classify type...",
  "thoughtNumber": 1,
  "totalThoughts": 3,
  "nextThoughtNeeded": true
}
</parameters>
</mcp_tool>
```

Classify into one of:
- `gotcha` - Non-obvious behavior that causes problems
- `configuration` - Setup, config file, or environment nuances
- `integration` - Issues when combining with other tools/systems
- `performance` - Performance characteristics or optimization tips
- `workaround` - Temporary fix for a known issue
- `deprecation` - Upcoming changes or deprecated features

### Step 3: Determine Version Specificity

Use Sequential Thinking to analyze:
1. Is this behavior version-specific or applies to all versions?
2. Is it likely to be fixed in future versions?
3. Does it affect specific platforms (Windows, macOS, Linux)?

Document findings in the `version_notes` field.

### Step 4: Check Official Documentation Gap

Determine if this knowledge:
- Is documented but hard to find
- Is undocumented entirely
- Contradicts official documentation
- Provides practical examples missing from docs

Set `official_docs_gap: true` if this fills a documentation gap.

### Step 5: Validate Schema

Load `schema.yaml` and validate:

**Required Fields:**
- `tool_name` (string): Name of tool/library
- `version` (string): Version where behavior observed
- `knowledge_type` (enum): One of [gotcha, configuration, integration, performance, workaround, deprecation]

**Optional Fields:**
- `official_docs_gap` (boolean): Missing from official docs?
- `related_tools` (array): Related tools/libraries

**BLOCK if validation fails** - show specific errors to user.

### Step 6: Write Documentation

1. Generate filename: `{tool-name}-{sanitized-title}-{YYYYMMDD}.md`
   - Lowercase tool name, hyphens for spaces
   - Sanitize title: lowercase, hyphens, max 50 chars
   - Example: `entityframeworkcore-lazy-loading-gotcha-20250124.md`

2. Create directory if needed:
   ```bash
   mkdir -p ./csharp-compounding-docs/tools/
   ```

3. Write file with YAML frontmatter + markdown body:

```markdown
---
type: tool
tool_name: "{tool_name}"
version: "{version}"
knowledge_type: {knowledge_type}
official_docs_gap: {true|false}
related_tools:
  - "{related_tool_1}"
tags:
  - {tool_name}
  - {knowledge_type}
created: {YYYY-MM-DD}
status: captured
---

# {Title}

## Context

{Brief description of when/why this knowledge is relevant}

## The Issue

{Detailed description of the gotcha, configuration nuance, etc.}

## Solution/Workaround

{How to handle this, code examples if applicable}

## Version Notes

{Version-specific information, when fixed, platform differences}

## References

- [Official Docs]({link_if_relevant})
- [Related Issue]({link_if_relevant})
```

### Step 7: Post-Capture Options

```markdown
---
**Documentation captured successfully**

File created: `./csharp-compounding-docs/tools/{filename}.md`

What's next?
1. Continue working
2. Link to related tool docs
3. View the captured documentation
4. Add more version-specific details
5. Other
```

## Schema Reference

See `schema.yaml` in this skill directory.

## Examples

### Example 1: EF Core Lazy Loading Gotcha

```markdown
---
type: tool
tool_name: "Microsoft.EntityFrameworkCore"
version: "7.0.0+"
knowledge_type: gotcha
official_docs_gap: true
related_tools:
  - "Microsoft.EntityFrameworkCore.Proxies"
tags:
  - entityframeworkcore
  - gotcha
  - lazy-loading
created: 2025-01-24
status: captured
---

# EF Core Lazy Loading Requires Virtual Properties

## Context

When enabling lazy loading in EF Core, navigation properties must be virtual.

## The Issue

Lazy loading silently fails if navigation properties aren't marked `virtual`.
No error is thrown - the property just returns null.

## Solution/Workaround

Always mark navigation properties as `virtual`:

```csharp
public class Blog
{
    public int Id { get; set; }
    public virtual ICollection<Post> Posts { get; set; } // Must be virtual!
}
```

## Version Notes

Applies to EF Core 2.1+ when using lazy loading proxies.
```

### Example 2: Polly Retry Configuration

```markdown
---
type: tool
tool_name: "Polly"
version: "8.0.0"
knowledge_type: configuration
official_docs_gap: false
related_tools:
  - "Microsoft.Extensions.Http.Polly"
tags:
  - polly
  - configuration
  - resilience
created: 2025-01-24
status: captured
---

# Polly V8 Breaking Change: No More Policy.Handle

## Context

Migrating from Polly 7.x to 8.x requires rewriting retry policies.

## The Issue

The `Policy.Handle<T>()` fluent API was removed in favor of resilience pipelines.

## Solution/Workaround

Use the new pipeline builder:

```csharp
// Old (Polly 7.x)
var policy = Policy
    .Handle<HttpRequestException>()
    .WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(i));

// New (Polly 8.x)
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential
    })
    .Build();
```

## Version Notes

Breaking change introduced in Polly 8.0.0.
```
```

### 3. Schema.yaml Content

```yaml
# schema.yaml - Tool/Library Doc-Type Schema
name: tool
description: Library gotchas, configuration nuances, and dependency knowledge

trigger_phrases:
  - "gotcha"
  - "watch out for"
  - "careful with"
  - "heads up"
  - "workaround"
  - "dependency"
  - "library"
  - "package"
  - "NuGet"

classification_hints:
  - "library"
  - "package"
  - "NuGet"
  - "dependency"
  - "version"
  - "configuration"
  - "API"
  - "SDK"
  - "framework"
  - "third-party"

required_fields:
  - name: tool_name
    type: string
    description: Name of tool/library (e.g., "Microsoft.EntityFrameworkCore", "Newtonsoft.Json")
    validation:
      min_length: 1
      max_length: 200

  - name: version
    type: string
    description: Version where behavior observed (e.g., "7.0.0", "8.0+", "2.x-3.x")
    validation:
      pattern: "^[0-9a-zA-Z.+\\-*x~<>=, ]+$"

  - name: knowledge_type
    type: enum
    values:
      - gotcha
      - configuration
      - integration
      - performance
      - workaround
      - deprecation
    description: Category of tool knowledge

optional_fields:
  - name: official_docs_gap
    type: boolean
    default: false
    description: Is this information missing from official documentation?

  - name: related_tools
    type: array
    item_type: string
    description: Related tools/libraries that interact with this one

  - name: platforms
    type: array
    item_type: string
    values: [windows, macos, linux, all]
    description: Platform-specific applicability

  - name: breaking_change
    type: boolean
    default: false
    description: Does this document a breaking change between versions?

  - name: fixed_in_version
    type: string
    description: Version where this issue was fixed (if applicable)

# Validation rules
validation:
  # If knowledge_type is "deprecation", recommend setting breaking_change
  conditional:
    - when: knowledge_type == "deprecation"
      recommend: breaking_change
    # If fixed_in_version is set, validate it's newer than version
    - when: fixed_in_version != null
      validate: "fixed_in_version > version"
```

### 4. Service Integration

```csharp
// src/CompoundDocs.McpServer/Skills/ToolSkillValidator.cs
namespace CompoundDocs.McpServer.Skills;

/// <summary>
/// Validates tool skill documents against schema.
/// </summary>
public sealed class ToolSkillValidator : ISkillValidator
{
    private readonly ISchemaValidator _schemaValidator;
    private readonly ILogger<ToolSkillValidator> _logger;

    private static readonly string[] ValidKnowledgeTypes =
    [
        "gotcha",
        "configuration",
        "integration",
        "performance",
        "workaround",
        "deprecation"
    ];

    public ToolSkillValidator(
        ISchemaValidator schemaValidator,
        ILogger<ToolSkillValidator> logger)
    {
        _schemaValidator = schemaValidator;
        _logger = logger;
    }

    public string SkillName => "cdocs:tool";
    public string DocType => "tool";

    public ValidationResult Validate(DocumentFrontmatter frontmatter)
    {
        var errors = new List<string>();

        // Validate tool_name
        if (string.IsNullOrWhiteSpace(frontmatter.GetString("tool_name")))
        {
            errors.Add("Required field 'tool_name' is missing or empty");
        }
        else if (frontmatter.GetString("tool_name")!.Length > 200)
        {
            errors.Add("Field 'tool_name' exceeds maximum length of 200 characters");
        }

        // Validate version
        var version = frontmatter.GetString("version");
        if (string.IsNullOrWhiteSpace(version))
        {
            errors.Add("Required field 'version' is missing or empty");
        }
        else if (!IsValidVersionFormat(version))
        {
            errors.Add($"Field 'version' has invalid format: {version}");
        }

        // Validate knowledge_type
        var knowledgeType = frontmatter.GetString("knowledge_type");
        if (string.IsNullOrWhiteSpace(knowledgeType))
        {
            errors.Add("Required field 'knowledge_type' is missing or empty");
        }
        else if (!ValidKnowledgeTypes.Contains(knowledgeType, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"Field 'knowledge_type' must be one of: {string.Join(", ", ValidKnowledgeTypes)}");
        }

        // Validate related_tools if present
        var relatedTools = frontmatter.GetArray("related_tools");
        if (relatedTools is not null)
        {
            foreach (var tool in relatedTools)
            {
                if (string.IsNullOrWhiteSpace(tool))
                {
                    errors.Add("Array 'related_tools' contains empty entries");
                    break;
                }
            }
        }

        return errors.Count == 0
            ? ValidationResult.Success
            : new ValidationResult { IsValid = false, Errors = errors };
    }

    private static bool IsValidVersionFormat(string version)
    {
        // Allows: 7.0.0, 8.0+, 2.x-3.x, >=1.0, etc.
        return System.Text.RegularExpressions.Regex.IsMatch(
            version,
            @"^[0-9a-zA-Z.\-+*x~<>=, ]+$");
    }
}
```

### 5. Filename Generator

```csharp
// src/CompoundDocs.McpServer/Skills/ToolFilenameGenerator.cs
namespace CompoundDocs.McpServer.Skills;

/// <summary>
/// Generates filenames for tool documentation.
/// </summary>
public sealed class ToolFilenameGenerator : IFilenameGenerator
{
    public string DocType => "tool";

    public string Generate(DocumentFrontmatter frontmatter, string title)
    {
        var toolName = SanitizeForFilename(
            frontmatter.GetString("tool_name") ?? "unknown");
        var sanitizedTitle = SanitizeForFilename(title);
        var date = DateTime.UtcNow.ToString("yyyyMMdd");

        // Format: {tool-name}-{sanitized-title}-{YYYYMMDD}.md
        // Example: entityframeworkcore-lazy-loading-gotcha-20250124.md
        return $"{toolName}-{sanitizedTitle}-{date}.md";
    }

    private static string SanitizeForFilename(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "untitled";

        // Lowercase, replace spaces and dots with hyphens
        var sanitized = input
            .ToLowerInvariant()
            .Replace(" ", "-")
            .Replace(".", "-")
            .Replace("_", "-");

        // Remove invalid characters
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            @"[^a-z0-9\-]",
            "");

        // Collapse multiple hyphens
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            @"-+",
            "-");

        // Trim hyphens from ends
        sanitized = sanitized.Trim('-');

        // Max 50 characters
        if (sanitized.Length > 50)
        {
            sanitized = sanitized[..50].TrimEnd('-');
        }

        return string.IsNullOrEmpty(sanitized) ? "untitled" : sanitized;
    }
}
```

### 6. Output Directory Configuration

```csharp
// In SkillDirectoryConfiguration.cs
public static class ToolSkillConfiguration
{
    public const string OutputDirectory = "tools";
    public const string FullOutputPath = "./csharp-compounding-docs/tools/";
}
```

---

## Dependencies

### Depends On

- **Phase 081**: Skills Base Infrastructure - Skill file loading, registration
- **Phase 082**: Skill Frontmatter - YAML frontmatter parsing for skills
- **Phase 083**: Auto-Invoke Triggers - Pattern matching for auto-invocation
- **Phase 014**: Schema Validation - Document schema validation
- **Phase 059**: Frontmatter Parsing - YAML frontmatter extraction

### Blocks

- **Phase 089+**: Other capture skills that may reference tool patterns
- **Phase 095+**: Multi-trigger conflict resolution that includes tool skill

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Skills/ToolSkillValidatorTests.cs
public class ToolSkillValidatorTests
{
    [Fact]
    public void Validate_AllRequiredFields_ReturnsSuccess()
    {
        // Arrange
        var validator = new ToolSkillValidator(
            Mock.Of<ISchemaValidator>(),
            Mock.Of<ILogger<ToolSkillValidator>>());

        var frontmatter = new DocumentFrontmatter
        {
            ["tool_name"] = "Microsoft.EntityFrameworkCore",
            ["version"] = "7.0.0",
            ["knowledge_type"] = "gotcha"
        };

        // Act
        var result = validator.Validate(frontmatter);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_MissingToolName_ReturnsError()
    {
        // Arrange
        var validator = new ToolSkillValidator(
            Mock.Of<ISchemaValidator>(),
            Mock.Of<ILogger<ToolSkillValidator>>());

        var frontmatter = new DocumentFrontmatter
        {
            ["version"] = "7.0.0",
            ["knowledge_type"] = "gotcha"
        };

        // Act
        var result = validator.Validate(frontmatter);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("tool_name"));
    }

    [Fact]
    public void Validate_InvalidKnowledgeType_ReturnsError()
    {
        // Arrange
        var validator = new ToolSkillValidator(
            Mock.Of<ISchemaValidator>(),
            Mock.Of<ILogger<ToolSkillValidator>>());

        var frontmatter = new DocumentFrontmatter
        {
            ["tool_name"] = "SomeLibrary",
            ["version"] = "1.0.0",
            ["knowledge_type"] = "invalid_type"
        };

        // Act
        var result = validator.Validate(frontmatter);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("knowledge_type"));
    }

    [Theory]
    [InlineData("7.0.0")]
    [InlineData("8.0+")]
    [InlineData("2.x-3.x")]
    [InlineData(">=1.0, <2.0")]
    [InlineData("*")]
    public void Validate_ValidVersionFormats_ReturnsSuccess(string version)
    {
        // Arrange
        var validator = new ToolSkillValidator(
            Mock.Of<ISchemaValidator>(),
            Mock.Of<ILogger<ToolSkillValidator>>());

        var frontmatter = new DocumentFrontmatter
        {
            ["tool_name"] = "SomeLibrary",
            ["version"] = version,
            ["knowledge_type"] = "gotcha"
        };

        // Act
        var result = validator.Validate(frontmatter);

        // Assert
        Assert.True(result.IsValid);
    }
}

// tests/CompoundDocs.Tests/Skills/ToolFilenameGeneratorTests.cs
public class ToolFilenameGeneratorTests
{
    [Fact]
    public void Generate_ValidInputs_ReturnsCorrectFormat()
    {
        // Arrange
        var generator = new ToolFilenameGenerator();
        var frontmatter = new DocumentFrontmatter
        {
            ["tool_name"] = "Microsoft.EntityFrameworkCore"
        };

        // Act
        var filename = generator.Generate(frontmatter, "Lazy Loading Gotcha");

        // Assert
        Assert.Matches(@"^microsoft-entityframeworkcore-lazy-loading-gotcha-\d{8}\.md$", filename);
    }

    [Fact]
    public void Generate_LongTitle_TruncatesTo50Chars()
    {
        // Arrange
        var generator = new ToolFilenameGenerator();
        var frontmatter = new DocumentFrontmatter
        {
            ["tool_name"] = "Lib"
        };
        var longTitle = new string('a', 100);

        // Act
        var filename = generator.Generate(frontmatter, longTitle);

        // Assert
        // lib- (4) + title (50 max) + - (1) + date (8) + .md (3) = 66 max
        var titlePart = filename.Split('-')[1]; // Get title part after tool name
        Assert.True(filename.Length <= 70);
    }

    [Fact]
    public void Generate_SpecialCharacters_SanitizesCorrectly()
    {
        // Arrange
        var generator = new ToolFilenameGenerator();
        var frontmatter = new DocumentFrontmatter
        {
            ["tool_name"] = "Some.Complex_Package!@#"
        };

        // Act
        var filename = generator.Generate(frontmatter, "Test");

        // Assert
        Assert.DoesNotContain(".", filename.Replace(".md", ""));
        Assert.DoesNotContain("_", filename);
        Assert.DoesNotContain("!", filename);
    }
}
```

### Integration Tests

```csharp
// tests/CompoundDocs.IntegrationTests/Skills/ToolSkillIntegrationTests.cs
[Trait("Category", "Integration")]
public class ToolSkillIntegrationTests : IClassFixture<SkillFixture>
{
    private readonly SkillFixture _fixture;

    public ToolSkillIntegrationTests(SkillFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ToolSkill_FullCaptureWorkflow_CreatesValidDocument()
    {
        // Arrange
        var skillRunner = _fixture.GetSkillRunner();
        var outputPath = Path.Combine(_fixture.TempDirectory, "csharp-compounding-docs", "tools");

        // Act
        await skillRunner.ExecuteSkillAsync("cdocs:tool", new SkillContext
        {
            Inputs = new Dictionary<string, object>
            {
                ["tool_name"] = "Polly",
                ["version"] = "8.0.0",
                ["knowledge_type"] = "configuration",
                ["title"] = "Retry Policy Breaking Change",
                ["content"] = "Policy.Handle was removed in V8..."
            }
        });

        // Assert
        var files = Directory.GetFiles(outputPath, "polly-*.md");
        Assert.Single(files);

        var content = await File.ReadAllTextAsync(files[0]);
        Assert.Contains("tool_name: \"Polly\"", content);
        Assert.Contains("version: \"8.0.0\"", content);
        Assert.Contains("knowledge_type: configuration", content);
    }

    [Fact]
    public async Task ToolSkill_AutoInvokeTrigger_DetectsGotchaPhrase()
    {
        // Arrange
        var triggerDetector = _fixture.GetTriggerDetector();

        // Act
        var triggered = triggerDetector.ShouldTrigger(
            "cdocs:tool",
            "Watch out for this gotcha in Entity Framework...");

        // Assert
        Assert.True(triggered);
    }
}
```

### Skill File Validation Tests

```csharp
// tests/CompoundDocs.Tests/Skills/ToolSkillFileTests.cs
public class ToolSkillFileTests
{
    [Fact]
    public void SkillMd_HasValidFrontmatter()
    {
        // Arrange
        var skillPath = GetSkillPath("cdocs-tool/SKILL.md");
        var content = File.ReadAllText(skillPath);

        // Act
        var frontmatter = YamlFrontmatterParser.Parse(content);

        // Assert
        Assert.Equal("cdocs:tool", frontmatter["name"]);
        Assert.NotEmpty((string)frontmatter["description"]);
        Assert.Contains("Read", (List<string>)frontmatter["allowed-tools"]);
        Assert.Contains("Write", (List<string>)frontmatter["allowed-tools"]);
    }

    [Fact]
    public void SchemaYaml_HasAllRequiredFields()
    {
        // Arrange
        var schemaPath = GetSkillPath("cdocs-tool/schema.yaml");
        var content = File.ReadAllText(schemaPath);

        // Act
        var schema = YamlParser.Parse(content);
        var requiredFields = schema["required_fields"] as List<object>;

        // Assert
        Assert.NotNull(requiredFields);
        var fieldNames = requiredFields
            .Cast<Dictionary<object, object>>()
            .Select(f => f["name"].ToString())
            .ToList();

        Assert.Contains("tool_name", fieldNames);
        Assert.Contains("version", fieldNames);
        Assert.Contains("knowledge_type", fieldNames);
    }

    [Fact]
    public void SchemaYaml_HasAllTriggerPhrases()
    {
        // Arrange
        var schemaPath = GetSkillPath("cdocs-tool/schema.yaml");
        var content = File.ReadAllText(schemaPath);

        // Act
        var schema = YamlParser.Parse(content);
        var triggers = schema["trigger_phrases"] as List<object>;

        // Assert
        Assert.NotNull(triggers);
        var triggerStrings = triggers.Cast<string>().ToList();

        Assert.Contains("gotcha", triggerStrings);
        Assert.Contains("watch out for", triggerStrings);
        Assert.Contains("workaround", triggerStrings);
        Assert.Contains("NuGet", triggerStrings);
    }
}
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-tool/SKILL.md` | Create | Main skill definition |
| `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-tool/schema.yaml` | Create | Tool doc-type schema |
| `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-tool/references/examples.md` | Create | Example captured docs |
| `src/CompoundDocs.McpServer/Skills/ToolSkillValidator.cs` | Create | Schema validator |
| `src/CompoundDocs.McpServer/Skills/ToolFilenameGenerator.cs` | Create | Filename generator |
| `src/CompoundDocs.McpServer/Skills/SkillDirectoryConfiguration.cs` | Modify | Add tool output path |
| `tests/CompoundDocs.Tests/Skills/ToolSkillValidatorTests.cs` | Create | Validator unit tests |
| `tests/CompoundDocs.Tests/Skills/ToolFilenameGeneratorTests.cs` | Create | Filename generator tests |
| `tests/CompoundDocs.Tests/Skills/ToolSkillFileTests.cs` | Create | Skill file validation |
| `tests/CompoundDocs.IntegrationTests/Skills/ToolSkillIntegrationTests.cs` | Create | Integration tests |

---

## Trigger Phrase Reference

| Phrase | Expected Detection |
|--------|-------------------|
| "gotcha" | Direct match |
| "watch out for" | Direct match |
| "careful with" | Direct match |
| "heads up" | Direct match |
| "workaround" | Direct match |
| "dependency" | Contextual - needs tool context |
| "library" | Contextual - needs tool context |
| "package" | Contextual - needs tool context |
| "NuGet" | Strong indicator - .NET specific |

---

## Knowledge Type Reference

| Type | When to Use | Example |
|------|-------------|---------|
| `gotcha` | Non-obvious behavior causing problems | "Lazy loading silently fails without virtual" |
| `configuration` | Setup, config, environment nuances | "Connection string format changed in v8" |
| `integration` | Combining with other tools/systems | "Polly + HttpClientFactory ordering matters" |
| `performance` | Performance characteristics, optimization | "Avoid ToList() in EF queries with large sets" |
| `workaround` | Temporary fix for known issue | "Use reflection to access internal method until fixed" |
| `deprecation` | Upcoming changes, deprecated features | "Policy.Handle removed in Polly 8.0" |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Version format too strict | Flexible regex allows common patterns |
| Tool name variations | Sanitization normalizes variations |
| Missing version info | BLOCKING prompt asks user to clarify |
| Duplicate knowledge | Search existing docs before capture (Phase 095+) |
| Stale information | `fixed_in_version` field tracks resolution |
