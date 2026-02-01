# Phase 083: Common Skill Workflow Pattern

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-5 hours
> **Category**: Skills System
> **Prerequisites**: Phase 081 (Skills System Foundation)

---

## Spec References

This phase implements the common skill workflow patterns as defined in:

- **spec/skills/skill-patterns.md** - [Common Skill Patterns](../spec/skills/skill-patterns.md#common-skill-patterns) - Four-step workflow (Context Gathering, Schema Validation, File Writing, Decision Menu)
- **spec/skills/skill-patterns.md** - [Context Gathering](../spec/skills/skill-patterns.md#context-gathering) - BLOCKING behavior on missing context
- **spec/skills/skill-patterns.md** - [Schema Validation](../spec/skills/skill-patterns.md#schema-validation) - Validation before writing
- **spec/skills/skill-patterns.md** - [File Writing](../spec/skills/skill-patterns.md#file-writing) - Filename generation pattern
- **spec/skills/skill-patterns.md** - [Decision Menu](../spec/skills/skill-patterns.md#decision-menu) - Post-capture options

---

## Objectives

1. Define the standard four-step workflow for all doc-creation skills
2. Implement the blocking context-gathering pattern
3. Establish schema validation integration for skill execution
4. Standardize filename generation pattern (`{sanitized-title}-{YYYYMMDD}.md`)
5. Create consistent post-capture decision menu across all skills
6. Provide reusable workflow components for skill implementations

---

## Acceptance Criteria

### Four-Step Workflow Definition

- [ ] Common workflow steps documented and standardized:
  - [ ] Step 1: Context Gathering
  - [ ] Step 2: Schema Validation
  - [ ] Step 3: File Writing
  - [ ] Step 4: Decision Menu
- [ ] Workflow steps defined in SKILL.md template format
- [ ] Clear guidance on when each step is considered complete
- [ ] Error handling documented for each step

### Context Gathering Pattern

- [ ] Context extraction requirements defined:
  - [ ] Core insight identification
  - [ ] Supporting details extraction (evidence, code examples, error messages)
  - [ ] Related context capture (files, modules, versions)
- [ ] **BLOCKING** behavior implemented:
  - [ ] If critical context missing, skill MUST ask and WAIT
  - [ ] Do NOT proceed with partial information
  - [ ] Clear prompts for missing information
- [ ] Context sources specified:
  - [ ] Conversation history
  - [ ] File references
  - [ ] User input
- [ ] Minimum required context documented per doc-type

### Schema Validation Before Writing

- [ ] Integration with frontmatter validation service (Phase 060)
- [ ] Schema loading pattern for skill execution:
  - [ ] Load `schema.yaml` for target doc-type
  - [ ] Validate all required fields present
  - [ ] Validate enum values match schema
- [ ] **BLOCK if validation fails**:
  - [ ] Show specific validation errors
  - [ ] Prompt user to provide missing/invalid values
  - [ ] Re-validate after correction
- [ ] Pre-flight validation checklist:
  - [ ] All required fields populated
  - [ ] Field types match schema expectations
  - [ ] Enum values are valid
  - [ ] Date format is correct (YYYY-MM-DD)

### Filename Generation Pattern

- [ ] `IFilenameGenerator` interface defined:
  ```csharp
  string GenerateFilename(string title, DateTime date);
  string SanitizeTitle(string title);
  ```
- [ ] Title sanitization rules:
  - [ ] Convert to lowercase
  - [ ] Replace spaces with hyphens
  - [ ] Remove special characters (keep alphanumeric and hyphens)
  - [ ] Collapse multiple hyphens into single hyphen
  - [ ] Trim leading/trailing hyphens
  - [ ] Truncate to reasonable length (50-80 characters)
- [ ] Date format: `YYYYMMDD` (no separators)
- [ ] Final pattern: `{sanitized-title}-{YYYYMMDD}.md`
- [ ] Examples:
  - [ ] "Database Connection Timeout" + 2025-01-24 = `database-connection-timeout-20250124.md`
  - [ ] "Fix: NULL Reference!!!" + 2025-01-24 = `fix-null-reference-20250124.md`
  - [ ] "API Rate Limiting (429 errors)" + 2025-01-24 = `api-rate-limiting-429-errors-20250124.md`

### Post-Capture Decision Menu

- [ ] Standard menu format defined:
  ```
  ✓ Documentation captured

  File created: `./csharp-compounding-docs/{doc-type}/{filename}.md`

  What's next?
  1. Continue workflow
  2. Link related docs
  3. View documentation
  4. Other
  ```
- [ ] Menu options handling:
  - [ ] **Continue workflow**: Return to previous task/conversation
  - [ ] **Link related docs**: Prompt for related document paths, add to frontmatter
  - [ ] **View documentation**: Display the created document content
  - [ ] **Other**: Free-form user input
- [ ] Menu presentation format:
  - [ ] Clear visual indicator of success (✓)
  - [ ] Full path to created file
  - [ ] Numbered options for easy selection
- [ ] Menu extensibility for doc-type-specific options

### Directory Creation

- [ ] Automatic directory creation before file write
- [ ] Pattern: `mkdir -p ./csharp-compounding-docs/{doc-type}/`
- [ ] Handle both built-in and custom doc-types
- [ ] Respect project root configuration
- [ ] Error handling for permission issues

---

## Implementation Notes

### Filename Generator Implementation

Create reusable filename generation in `CompoundDocs.Common/Utilities/`:

```csharp
// IFilenameGenerator.cs
public interface IFilenameGenerator
{
    /// <summary>
    /// Generates a filename from a title and date.
    /// </summary>
    /// <param name="title">The document title</param>
    /// <param name="date">The document date</param>
    /// <returns>Filename in format: {sanitized-title}-{YYYYMMDD}.md</returns>
    string GenerateFilename(string title, DateTime date);

    /// <summary>
    /// Sanitizes a title for use in a filename.
    /// </summary>
    /// <param name="title">The raw title</param>
    /// <returns>URL/filename-safe string</returns>
    string SanitizeTitle(string title);
}

// FilenameGenerator.cs
public partial class FilenameGenerator : IFilenameGenerator
{
    private const int MaxTitleLength = 60;

    [GeneratedRegex(@"[^a-z0-9\s-]", RegexOptions.Compiled)]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"-{2,}", RegexOptions.Compiled)]
    private static partial Regex MultipleHyphensRegex();

    public string GenerateFilename(string title, DateTime date)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var sanitized = SanitizeTitle(title);
        var dateStr = date.ToString("yyyyMMdd");

        return $"{sanitized}-{dateStr}.md";
    }

    public string SanitizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty", nameof(title));

        // Convert to lowercase
        var result = title.ToLowerInvariant();

        // Remove special characters (keep alphanumeric, spaces, hyphens)
        result = NonAlphanumericRegex().Replace(result, "");

        // Replace whitespace with hyphens
        result = WhitespaceRegex().Replace(result, "-");

        // Collapse multiple hyphens
        result = MultipleHyphensRegex().Replace(result, "-");

        // Trim leading/trailing hyphens
        result = result.Trim('-');

        // Truncate to max length (break at word boundary if possible)
        if (result.Length > MaxTitleLength)
        {
            result = result[..MaxTitleLength];
            var lastHyphen = result.LastIndexOf('-');
            if (lastHyphen > MaxTitleLength / 2)
            {
                result = result[..lastHyphen];
            }
            result = result.TrimEnd('-');
        }

        return result;
    }
}
```

### Workflow Context Model

```csharp
// WorkflowContext.cs
public class SkillWorkflowContext
{
    public required string DocType { get; init; }
    public required string ProjectRoot { get; init; }
    public Dictionary<string, object> ExtractedContext { get; init; } = new();
    public Dictionary<string, object> Frontmatter { get; init; } = new();
    public List<string> MissingRequiredFields { get; init; } = [];
    public List<FrontmatterValidationError> ValidationErrors { get; init; } = [];
    public WorkflowStep CurrentStep { get; set; } = WorkflowStep.ContextGathering;
    public bool IsBlocked { get; set; }
    public string? BlockReason { get; set; }
    public string? GeneratedFilename { get; set; }
    public string? GeneratedFilePath { get; set; }
}

public enum WorkflowStep
{
    ContextGathering,
    SchemaValidation,
    FileWriting,
    DecisionMenu,
    Complete
}
```

### Blocking Context Pattern Helper

```csharp
// ContextGatheringHelper.cs
public class ContextGatheringHelper
{
    private readonly Dictionary<string, string[]> _requiredContextByDocType = new()
    {
        ["problem"] = ["symptoms", "root_cause", "solution"],
        ["insight"] = ["insight_type", "observation", "implication"],
        ["codebase"] = ["component", "behavior"],
        ["tool"] = ["tool_name", "purpose", "usage"],
        ["style"] = ["applies_to", "convention"]
    };

    public ContextGatheringResult GatherContext(
        string docType,
        Dictionary<string, object> conversationContext)
    {
        var result = new ContextGatheringResult
        {
            DocType = docType,
            ExtractedFields = new Dictionary<string, object>()
        };

        // Extract common fields
        ExtractCommonFields(conversationContext, result);

        // Extract doc-type specific fields
        if (_requiredContextByDocType.TryGetValue(docType, out var requiredFields))
        {
            foreach (var field in requiredFields)
            {
                if (conversationContext.TryGetValue(field, out var value) && value is not null)
                {
                    result.ExtractedFields[field] = value;
                }
                else
                {
                    result.MissingFields.Add(field);
                }
            }
        }

        result.IsBlocked = result.MissingFields.Count > 0;
        if (result.IsBlocked)
        {
            result.BlockingPrompt = GenerateBlockingPrompt(docType, result.MissingFields);
        }

        return result;
    }

    private void ExtractCommonFields(
        Dictionary<string, object> context,
        ContextGatheringResult result)
    {
        var commonFields = new[] { "title", "summary", "significance", "tags", "related_docs" };

        foreach (var field in commonFields)
        {
            if (context.TryGetValue(field, out var value) && value is not null)
            {
                result.ExtractedFields[field] = value;
            }
            else if (field is "title" or "summary" or "significance")
            {
                // These are always required
                result.MissingFields.Add(field);
            }
        }
    }

    private string GenerateBlockingPrompt(string docType, List<string> missingFields)
    {
        var fieldDescriptions = missingFields.Select(f => GetFieldDescription(docType, f));
        return $"""
            **BLOCKING**: Cannot proceed with {docType} documentation.

            Missing required information:
            {string.Join("\n", fieldDescriptions.Select(d => $"- {d}"))}

            Please provide the missing information to continue.
            """;
    }

    private string GetFieldDescription(string docType, string fieldName)
    {
        // Field descriptions for user-friendly prompts
        return (docType, fieldName) switch
        {
            (_, "title") => "**title**: A concise title for this document",
            (_, "summary") => "**summary**: A one-line summary for search results",
            (_, "significance") => "**significance**: Impact level (architectural, behavioral, performance, correctness, convention, integration)",
            ("problem", "symptoms") => "**symptoms**: Observable behaviors indicating the problem",
            ("problem", "root_cause") => "**root_cause**: The underlying cause of the problem",
            ("problem", "solution") => "**solution**: How the problem was resolved",
            ("insight", "insight_type") => "**insight_type**: Category (pattern, anti-pattern, edge-case, performance, compatibility, idiom)",
            ("insight", "observation") => "**observation**: What was observed or discovered",
            ("insight", "implication") => "**implication**: Why this matters and what to do about it",
            ("codebase", "component") => "**component**: Which component or module this documents",
            ("codebase", "behavior") => "**behavior**: How the component behaves",
            ("tool", "tool_name") => "**tool_name**: Name of the tool being documented",
            ("tool", "purpose") => "**purpose**: What the tool is used for",
            ("tool", "usage") => "**usage**: How to use the tool",
            ("style", "applies_to") => "**applies_to**: What this style convention applies to",
            ("style", "convention") => "**convention**: The style rule or convention",
            _ => $"**{fieldName}**: Required field for {docType} documentation"
        };
    }
}

public class ContextGatheringResult
{
    public required string DocType { get; init; }
    public Dictionary<string, object> ExtractedFields { get; init; } = new();
    public List<string> MissingFields { get; init; } = [];
    public bool IsBlocked { get; set; }
    public string? BlockingPrompt { get; set; }
}
```

### Decision Menu Builder

```csharp
// DecisionMenuBuilder.cs
public class DecisionMenuBuilder
{
    public string BuildStandardMenu(string docType, string filePath)
    {
        return $"""
            ✓ Documentation captured

            File created: `{filePath}`

            What's next?
            1. Continue workflow
            2. Link related docs
            3. View documentation
            4. Other
            """;
    }

    public string BuildExtendedMenu(string docType, string filePath, IEnumerable<string> additionalOptions)
    {
        var optionNumber = 5;
        var additionalOptionsText = string.Join("\n",
            additionalOptions.Select(opt => $"{optionNumber++}. {opt}"));

        return $"""
            ✓ Documentation captured

            File created: `{filePath}`

            What's next?
            1. Continue workflow
            2. Link related docs
            3. View documentation
            4. Other
            {additionalOptionsText}
            """;
    }

    public DecisionMenuChoice ParseChoice(string input)
    {
        return input.Trim().ToLowerInvariant() switch
        {
            "1" or "continue" or "continue workflow" => DecisionMenuChoice.ContinueWorkflow,
            "2" or "link" or "link related" or "link related docs" => DecisionMenuChoice.LinkRelatedDocs,
            "3" or "view" or "view documentation" => DecisionMenuChoice.ViewDocumentation,
            "4" or "other" => DecisionMenuChoice.Other,
            _ when int.TryParse(input, out var num) && num > 4 => DecisionMenuChoice.CustomOption,
            _ => DecisionMenuChoice.Other
        };
    }
}

public enum DecisionMenuChoice
{
    ContinueWorkflow,
    LinkRelatedDocs,
    ViewDocumentation,
    Other,
    CustomOption
}
```

### SKILL.md Workflow Template

Document the standard workflow template that all doc-creation skills should follow:

```markdown
# {Doc-Type} Documentation Skill

## Intake

[Describe expected input for this doc-type]

## Process

### Step 1: Gather Context

Extract from conversation history:
- **Core insight**: What is the key takeaway?
- **Supporting details**: Evidence, code examples, error messages
- **Related context**: Files, modules, versions involved

**BLOCKING**: If critical context missing, ask and WAIT.
Do NOT proceed with partial information.

Required fields for {doc-type}:
- [List required fields specific to this doc-type]

### Step 2: Validate Schema

Load `schema.yaml` for {doc-type}.
Validate all required fields present and match enum values.

Pre-write validation checklist:
- [ ] doc_type = "{doc-type}"
- [ ] title is non-empty
- [ ] date is YYYY-MM-DD format
- [ ] summary is non-empty
- [ ] significance is valid enum
- [ ] [doc-type specific required fields]

**BLOCK if validation fails** - show specific errors.

### Step 3: Write Documentation

1. Generate filename: `{sanitized-title}-{YYYYMMDD}.md`
2. Create directories: `mkdir -p ./csharp-compounding-docs/{doc-type}/`
3. Write file with YAML frontmatter + markdown body

### Step 4: Post-Capture Options

✓ Documentation captured

File created: `./csharp-compounding-docs/{doc-type}/{filename}.md`

What's next?
1. Continue workflow
2. Link related docs
3. View documentation
4. Other

## Schema Reference

[Link to or inline the schema for this doc-type]

## Examples

[Example captured documents]
```

---

## File Structure

After implementation, the following files should exist:

```
src/CompoundDocs.Common/
├── Utilities/
│   ├── IFilenameGenerator.cs
│   └── FilenameGenerator.cs
├── Skills/
│   ├── Workflow/
│   │   ├── SkillWorkflowContext.cs
│   │   ├── WorkflowStep.cs
│   │   ├── ContextGatheringHelper.cs
│   │   ├── ContextGatheringResult.cs
│   │   ├── DecisionMenuBuilder.cs
│   │   └── DecisionMenuChoice.cs
│   └── Templates/
│       └── skill-workflow-template.md
tests/CompoundDocs.Tests/
├── Utilities/
│   └── FilenameGeneratorTests.cs
└── Skills/
    └── Workflow/
        ├── ContextGatheringHelperTests.cs
        └── DecisionMenuBuilderTests.cs
```

---

## Dependencies

### Depends On
- **Phase 081**: Skills System Foundation (skill infrastructure, SKILL.md parsing)
- **Phase 060**: Frontmatter Schema Validation (validation service for Step 2)
- **Phase 001**: Solution & Project Structure (solution file, project structure)

### Blocks
- **Phase 084**: Doc-Creation Skills - Problem (uses common workflow)
- **Phase 085**: Doc-Creation Skills - Insight (uses common workflow)
- **Phase 086**: Doc-Creation Skills - Codebase (uses common workflow)
- **Phase 087**: Doc-Creation Skills - Tool (uses common workflow)
- **Phase 088**: Doc-Creation Skills - Style (uses common workflow)
- Custom doc-type skill creation (follows established workflow pattern)

---

## Verification Steps

After completing this phase, verify:

1. **Filename Generation**
   - Generate filename from "Database Connection Timeout" + 2025-01-24 = `database-connection-timeout-20250124.md`
   - Generate filename from "Fix: NULL Reference!!!" + 2025-01-24 = `fix-null-reference-20250124.md`
   - Generate filename with very long title (>60 chars) - properly truncated
   - Generate filename with unicode characters - properly sanitized
   - Empty title throws `ArgumentException`

2. **Title Sanitization**
   - Spaces converted to hyphens
   - Special characters removed
   - Multiple hyphens collapsed
   - Leading/trailing hyphens trimmed
   - Case converted to lowercase

3. **Context Gathering - Blocking Behavior**
   - Missing required field triggers BLOCKED state
   - Blocking prompt generated with user-friendly descriptions
   - All missing fields listed in prompt
   - Complete context results in non-blocked state

4. **Decision Menu**
   - Standard menu displays correctly
   - Menu shows correct file path
   - Choice parsing works for numeric and text input
   - Extended menu with custom options displays correctly

5. **Workflow Context**
   - Workflow context tracks current step
   - Context tracks blocking state and reason
   - Context stores extracted frontmatter fields
   - Context stores validation errors

---

## Testing Notes

Create unit tests in `tests/CompoundDocs.Tests/`:

### Test Scenarios

```csharp
// FilenameGeneratorTests.cs
[Theory]
[InlineData("Database Connection Timeout", "2025-01-24", "database-connection-timeout-20250124.md")]
[InlineData("Fix: NULL Reference!!!", "2025-01-24", "fix-null-reference-20250124.md")]
[InlineData("API Rate Limiting (429 errors)", "2025-01-24", "api-rate-limiting-429-errors-20250124.md")]
[InlineData("  Spaces  Around  ", "2025-01-24", "spaces-around-20250124.md")]
public void GenerateFilename_VariousTitles_ReturnsExpected(string title, string dateStr, string expected)

[Fact] public void GenerateFilename_EmptyTitle_ThrowsArgumentException()
[Fact] public void GenerateFilename_NullTitle_ThrowsArgumentException()
[Fact] public void GenerateFilename_VeryLongTitle_TruncatesAppropriately()
[Fact] public void SanitizeTitle_UnicodeCharacters_Removes()
[Fact] public void SanitizeTitle_MultipleHyphens_Collapses()
[Fact] public void SanitizeTitle_LeadingTrailingHyphens_Trims()

// ContextGatheringHelperTests.cs
[Fact] public void GatherContext_AllFieldsPresent_NotBlocked()
[Fact] public void GatherContext_MissingRequiredField_IsBlocked()
[Fact] public void GatherContext_MissingMultipleFields_ListsAll()
[Fact] public void GatherContext_BlockingPrompt_ContainsFieldDescriptions()
[Theory, MemberData(nameof(DocTypes))]
public void GatherContext_AllDocTypes_HasRequiredFields(string docType)

// DecisionMenuBuilderTests.cs
[Fact] public void BuildStandardMenu_ContainsFilePath()
[Fact] public void BuildStandardMenu_ContainsAllFourOptions()
[Fact] public void ParseChoice_NumericInput_ReturnsCorrectChoice()
[Fact] public void ParseChoice_TextInput_ReturnsCorrectChoice()
[Fact] public void ParseChoice_UnknownInput_ReturnsOther()
[Fact] public void BuildExtendedMenu_IncludesAdditionalOptions()
```

---

## Notes

- **BLOCKING is Critical**: The context gathering step MUST block and wait for user input if required fields are missing. Skills should NEVER generate documents with placeholder or incomplete information.
- **Validation First**: Schema validation in Step 2 prevents invalid documents from being written. This catches issues early before file I/O occurs.
- **Consistent Filenames**: The filename pattern ensures documents are uniquely identifiable and sortable by date. The sanitization rules handle edge cases gracefully.
- **User Experience**: The decision menu provides a consistent experience across all doc-creation skills. Users always know what options are available after capturing documentation.
- **Extensibility**: The workflow is designed to be extended by individual doc-type skills. Custom skills can add doc-type-specific context requirements and menu options while following the standard four-step structure.
