# Phase 085: /cdocs:problem Capture Skill

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-5 hours
> **Category**: Skills System
> **Prerequisites**: Phase 081 (Skills Infrastructure), Phase 082 (Schema Validation Service), Phase 083 (Skill File Writer)

---

## Spec References

This phase implements the problem capture skill defined in:

- **spec/skills/capture-skills.md** - [/cdocs:problem specification](../spec/skills/capture-skills.md#cdocsproblem) (lines 26-68)
- **spec/doc-types/built-in-types.md** - [Problems & Solutions schema](../spec/doc-types/built-in-types.md#1-problems--solutions-problem) (lines 24-84)
- **spec/skills/skill-patterns.md** - Common implementation patterns for capture skills

---

## Objectives

1. Create SKILL.md content for problem capture with auto-invoke triggers
2. Define schema.yaml for the problem doc-type with all required/optional fields
3. Implement context gathering for symptoms, root cause, and solution
4. Integrate Sequential Thinking MCP for complex root cause analysis
5. Configure output directory as `./csharp-compounding-docs/problems/`

---

## Acceptance Criteria

### SKILL.md Content
- [ ] Skill file located at `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-problem/SKILL.md`
- [ ] YAML frontmatter includes `name: cdocs:problem`
- [ ] Description clearly states purpose: "Capture solved problems with symptoms, root cause, and solution"
- [ ] `allowed-tools` includes: Read, Write, Bash (for file operations)
- [ ] Precondition requires project activation via `/cdocs:activate`
- [ ] Auto-invoke configuration with `trigger: conversation-pattern`

### Trigger Phrases
- [ ] Auto-invoke patterns include all spec-defined triggers:
  - "fixed"
  - "it's fixed"
  - "bug"
  - "the issue was"
  - "problem solved"
  - "resolved"
  - "exception"
  - "error"
  - "crash"
  - "failing"
- [ ] Classification hints configured for semantic validation:
  - "error message"
  - "stack trace"
  - "exception"
  - "null reference"
  - "debugging"
  - "root cause"
  - "symptoms"
  - "workaround"
  - "fix"

### Schema.yaml Definition
- [ ] Schema file located at `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-problem/schema.yaml`
- [ ] Schema includes `name: problem` and `description`
- [ ] Required fields defined:
  - `problem_type` (enum: bug, configuration, integration, performance, security, data)
  - `symptoms` (array) - Observable symptoms of the problem
  - `root_cause` (string) - The underlying cause
  - `solution` (string) - How it was fixed
- [ ] Optional fields defined:
  - `component` (string) - Affected component/module
  - `severity` (enum: critical, high, medium, low)
  - `prevention` (string) - How to prevent recurrence
- [ ] `trigger_phrases` and `classification_hints` arrays match spec

### Output Directory Configuration
- [ ] Documents written to `./csharp-compounding-docs/problems/`
- [ ] Filename format: `{sanitized-title}-{YYYYMMDD}.md`
- [ ] Directory created automatically if not exists (`mkdir -p`)
- [ ] Files include YAML frontmatter with all schema fields

### Sequential Thinking MCP Integration
- [ ] SKILL.md references Sequential Thinking MCP for complex analysis
- [ ] Skill invokes Sequential Thinking when multiple factors involved in root cause
- [ ] Skill assumes MCP server is available (no defensive checks per Core Principle #6)
- [ ] Sequential Thinking used to structure multi-step root cause analysis

### Context Gathering Flow
- [ ] Step 1: Extract symptoms from conversation (error messages, stack traces, observed behavior)
- [ ] Step 2: Identify root cause from discussion
- [ ] Step 3: Document the solution that fixed the problem
- [ ] Step 4: Classify problem_type based on context
- [ ] BLOCKING behavior if critical context (symptoms, root_cause, solution) missing

### Post-Capture Decision Menu
- [ ] Confirmation message shows file path
- [ ] Options presented: Continue workflow, Link related docs, View documentation, Other
- [ ] Menu follows skill-patterns.md template

---

## Implementation Notes

### SKILL.md Structure

```yaml
---
name: cdocs:problem
description: Capture solved problems with symptoms, root cause, and solution. Auto-invokes when problem-solving language detected in conversation.
allowed-tools:
  - Read
  - Write
  - Bash
preconditions:
  - Project activated via /cdocs:activate
auto-invoke:
  trigger: conversation-pattern
  patterns:
    - "fixed"
    - "it's fixed"
    - "bug"
    - "the issue was"
    - "problem solved"
    - "resolved"
    - "exception"
    - "error"
    - "crash"
    - "failing"
---

# Problem Documentation Skill

## Purpose

Capture a solved problem as documentation for future reference. This skill embodies the core "compound" step of the compound-engineering paradigm - each solved problem teaches the system.

## Intake

This skill expects:
- A conversation where a problem was discussed and solved
- Observable symptoms (error messages, unexpected behavior, crashes)
- Identified root cause
- Applied solution

## Process

### Step 1: Gather Context

Extract from conversation history:
- **Symptoms**: What was observed? (error messages, stack traces, unexpected behavior)
- **Root Cause**: What was the underlying issue?
- **Solution**: What fixed it?
- **Context**: Files, modules, versions involved

**BLOCKING**: If symptoms, root_cause, or solution cannot be determined, ask and WAIT.

### Step 2: Analyze with Sequential Thinking (Complex Cases)

For problems with multiple contributing factors:
1. Use Sequential Thinking MCP to structure root cause analysis
2. Break down the causal chain
3. Identify primary vs. contributing causes
4. Document the analysis path

### Step 3: Validate Schema

Load `schema.yaml` and validate:
- `problem_type` matches enum values
- `symptoms` is non-empty array
- `root_cause` is non-empty string
- `solution` is non-empty string
- Optional: `severity`, `component`, `prevention`

**BLOCK if validation fails** - show specific errors.

### Step 4: Write Documentation

1. Generate filename: `{sanitized-title}-{YYYYMMDD}.md`
   - Sanitize: lowercase, replace spaces with hyphens, remove special chars
2. Create directory: `mkdir -p ./csharp-compounding-docs/problems/`
3. Write file with YAML frontmatter + markdown body

### Step 5: Post-Capture Options

```
Documentation captured

File created: `./csharp-compounding-docs/problems/{filename}.md`

What's next?
1. Continue workflow
2. Link related docs
3. View documentation
4. Other
```

## Schema Reference

See `schema.yaml` in this skill directory.

## Examples

### Example Frontmatter

```yaml
---
doc_type: problem
problem_type: bug
symptoms:
  - "NullReferenceException on startup"
  - "App crashes when config file missing"
root_cause: "Configuration loader doesn't handle missing file gracefully"
solution: "Added null check and default configuration fallback"
component: "ConfigurationService"
severity: medium
prevention: "Add integration test for missing config scenario"
created: 2025-01-24
---
```

### Example Body

```markdown
# NullReferenceException on Missing Config File

## Symptoms

- Application crashes immediately on startup
- Stack trace points to `ConfigurationService.Load()`
- Only occurs in fresh installations

## Root Cause

The `ConfigurationLoader` assumed the config file always exists. When the file is missing (common in fresh installs), `File.ReadAllText()` returns null, which is then passed to `JsonSerializer.Deserialize()` causing the NullReferenceException.

## Solution

1. Added existence check before reading config file
2. Implemented default configuration fallback
3. Added logging for missing config scenario

## Prevention

- Added integration test: `ConfigurationService_MissingFile_UsesDefaults`
- Added XML documentation warning about file existence
```
```

### Schema.yaml Content

```yaml
name: problem
description: Solved problems with symptoms, root cause, and solution

trigger_phrases:
  - "fixed"
  - "it's fixed"
  - "bug"
  - "the issue was"
  - "problem solved"
  - "resolved"
  - "exception"
  - "error"
  - "crash"
  - "failing"

classification_hints:
  - "error message"
  - "stack trace"
  - "exception"
  - "null reference"
  - "debugging"
  - "root cause"
  - "symptoms"
  - "workaround"
  - "fix"

required_fields:
  - name: problem_type
    type: enum
    values: [bug, configuration, integration, performance, security, data]
  - name: symptoms
    type: array
    description: Observable symptoms of the problem
  - name: root_cause
    type: string
    description: The underlying cause
  - name: solution
    type: string
    description: How it was fixed

optional_fields:
  - name: component
    type: string
    description: Affected component/module
  - name: severity
    type: enum
    values: [critical, high, medium, low]
  - name: prevention
    type: string
    description: How to prevent recurrence
```

### Skill Directory Structure

```
${CLAUDE_PLUGIN_ROOT}/skills/cdocs-problem/
├── SKILL.md              # Main skill definition (auto-invoke enabled)
├── schema.yaml           # Problem doc-type schema
└── references/           # Optional examples
    └── examples.md       # Sample problem documents
```

---

## Dependencies

### Depends On
- Phase 081: Skills Infrastructure (skill loading, registration)
- Phase 082: Schema Validation Service (validates against schema.yaml)
- Phase 083: Skill File Writer (writes markdown with frontmatter)
- Phase 014: Schema Validation (YAML schema parsing)
- Phase 053: File Watcher Service (auto-indexes created documents)

### Blocks
- Phase 086: /cdocs:insight Capture Skill (follows same pattern)
- Phase 087: /cdocs:codebase Capture Skill
- Phase 088: /cdocs:tool Capture Skill
- Phase 089: /cdocs:style Capture Skill

---

## Verification Steps

After completing this phase, verify:

1. **Skill loads correctly**: `/cdocs:problem` recognized as valid skill
2. **Auto-invoke triggers**: Skill activates on "fixed", "bug", "error" phrases
3. **Schema validates**: Required fields enforced, enums validated
4. **Files created correctly**: Proper filename format, correct directory
5. **Sequential Thinking integration**: Complex root cause analysis works
6. **Frontmatter valid**: Created documents parse correctly

---

## Unit Test Scenarios

```csharp
[Fact]
public void ProblemSkill_LoadsFromDirectory()
{
    var skillLoader = new SkillLoader(skillsDirectory);
    var skill = skillLoader.Load("cdocs-problem");

    Assert.NotNull(skill);
    Assert.Equal("cdocs:problem", skill.Name);
    Assert.Contains("fixed", skill.AutoInvoke.Patterns);
}

[Fact]
public void ProblemSchema_ValidatesRequiredFields()
{
    var schema = SchemaLoader.Load("cdocs-problem/schema.yaml");
    var document = new Dictionary<string, object>
    {
        ["problem_type"] = "bug",
        ["symptoms"] = new[] { "Error on startup" },
        ["root_cause"] = "Missing null check",
        ["solution"] = "Added null check"
    };

    var result = schema.Validate(document);

    Assert.True(result.IsValid);
}

[Fact]
public void ProblemSchema_RejectsMissingRequiredFields()
{
    var schema = SchemaLoader.Load("cdocs-problem/schema.yaml");
    var document = new Dictionary<string, object>
    {
        ["problem_type"] = "bug"
        // Missing: symptoms, root_cause, solution
    };

    var result = schema.Validate(document);

    Assert.False(result.IsValid);
    Assert.Contains(result.Errors, e => e.Contains("symptoms"));
    Assert.Contains(result.Errors, e => e.Contains("root_cause"));
    Assert.Contains(result.Errors, e => e.Contains("solution"));
}

[Fact]
public void ProblemSchema_ValidatesEnumValues()
{
    var schema = SchemaLoader.Load("cdocs-problem/schema.yaml");
    var document = new Dictionary<string, object>
    {
        ["problem_type"] = "invalid_type", // Not in enum
        ["symptoms"] = new[] { "Error" },
        ["root_cause"] = "Cause",
        ["solution"] = "Solution"
    };

    var result = schema.Validate(document);

    Assert.False(result.IsValid);
    Assert.Contains(result.Errors, e => e.Contains("problem_type"));
}

[Fact]
public void ProblemSkill_GeneratesCorrectFilename()
{
    var skill = new ProblemCaptureSkill();
    var title = "NullReference Exception on Startup";

    var filename = skill.GenerateFilename(title, new DateTime(2025, 1, 24));

    Assert.Equal("nullreference-exception-on-startup-20250124.md", filename);
}

[Fact]
public void ProblemSkill_WritesToCorrectDirectory()
{
    var skill = new ProblemCaptureSkill();

    var outputPath = skill.GetOutputDirectory();

    Assert.Equal("./csharp-compounding-docs/problems/", outputPath);
}

[Fact]
public void ProblemSkill_TriggerPhrasesMatchSpec()
{
    var schema = SchemaLoader.Load("cdocs-problem/schema.yaml");

    var expectedTriggers = new[]
    {
        "fixed", "it's fixed", "bug", "the issue was", "problem solved",
        "resolved", "exception", "error", "crash", "failing"
    };

    Assert.All(expectedTriggers, trigger =>
        Assert.Contains(trigger, schema.TriggerPhrases));
}

[Fact]
public void ProblemSkill_AutoInvokeOnErrorPhrase()
{
    var skillMatcher = new SkillMatcher(skillsDirectory);
    var conversation = "The error was caused by a missing dependency.";

    var matches = skillMatcher.FindMatchingSkills(conversation);

    Assert.Contains(matches, m => m.SkillName == "cdocs:problem");
}

[Fact]
public void ProblemDocument_IncludesAllFrontmatterFields()
{
    var skill = new ProblemCaptureSkill();
    var document = new ProblemDocument
    {
        ProblemType = "bug",
        Symptoms = new[] { "App crashes" },
        RootCause = "Missing null check",
        Solution = "Added null check",
        Component = "ConfigService",
        Severity = "medium",
        Prevention = "Add unit test"
    };

    var markdown = skill.GenerateMarkdown(document);

    Assert.Contains("doc_type: problem", markdown);
    Assert.Contains("problem_type: bug", markdown);
    Assert.Contains("symptoms:", markdown);
    Assert.Contains("root_cause:", markdown);
    Assert.Contains("solution:", markdown);
}
```

---

## Integration with Sequential Thinking MCP

The skill leverages Sequential Thinking MCP for complex root cause analysis:

```markdown
### When to Use Sequential Thinking

Use Sequential Thinking MCP when:
- Multiple potential causes identified
- Causal chain is unclear
- Problem involves multiple system components
- Debugging path was non-linear

### Sequential Thinking Prompt Template

"Analyze the root cause of this problem:

Symptoms observed:
{symptoms}

Potential causes discussed:
{potential_causes}

Break down the causal chain step by step. Identify:
1. The trigger event
2. Intermediate failures
3. The root cause
4. Contributing factors"
```

---

## Notes

- The problem skill is the flagship capture skill embodying the "compound" paradigm
- Auto-indexing happens via file watcher - skill does not call `index_document`
- Sequential Thinking MCP availability is assumed (verified by SessionStart hook)
- Multiple skills may trigger on same conversation - handled by `/cdocs:capture-select` meta-skill
