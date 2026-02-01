---
name: cdocs:capture-select
description: Present unified selection interface when 2+ capture skills trigger
allowed-tools:
  - Read
preconditions:
  - Project activated via /cdocs:activate
  - 2+ capture skills triggered on same conversation
auto-invoke:
  trigger: multi-skill-conflict  # Auto-invokes when multiple capture skills trigger
---

# Capture Selection Skill

## Purpose

Present a unified selection interface when 2 or more capture skills trigger simultaneously on the same conversation context. Allows users to select which doc-type(s) to capture or skip all.

## Characteristics

- **User-invocable**: Can be manually invoked via `/cdocs:capture-select`
- **Auto-invoke**: Automatically triggers when 2+ capture skills detect matching trigger phrases
- **Dynamic detection**: Does not have a hardcoded list of skills - dynamically queries all registered capture skills
- **Multi-select**: User can select 0, 1, or multiple doc-types to capture from the same conversation

## Intake

**Auto-invocation scenario**: Multiple capture skills (e.g., `/cdocs:problem` and `/cdocs:tool`) detect trigger phrases in the same conversation.

**Manual invocation**: `/cdocs:capture-select` - User explicitly wants to choose which doc-type to use.

## Process

### Step 1: Query Triggered Skills

Identify which capture skills would trigger based on current conversation context.

**For each registered capture skill**:
1. Read skill's YAML frontmatter to get `auto-invoke.patterns`
2. Check if any pattern matches recent conversation (last 5-10 messages)
3. If match found, add to triggered skills list

**Include**:
- Built-in capture skills (`cdocs:problem`, `cdocs:tool`, `cdocs:codebase`, etc.)
- Custom capture skills (any skill with name pattern `cdocs:{name}`)

**BLOCKING**: If 0 or 1 skill triggered, this meta-skill should not execute.
- 0 skills: No action needed
- 1 skill: That skill should invoke directly
- 2+ skills: Continue to Step 2

### Step 2: Present Multi-Select Interface

Display selection interface to user:

```
Multiple doc types detected for this content:

[ ] problem - Solved problems with symptoms, root cause, and solution
    Triggers matched: "fixed", "bug"
    Classification hints: "error message", "stack trace"

[ ] tool - Library/package knowledge, API usage, configuration
    Triggers matched: "NuGet", "package"
    Classification hints: "dependency", "version"

[ ] [custom-type] - [Custom description]
    Triggers matched: "[phrase]"
    Classification hints: "[hint1]", "[hint2]"

[Capture Selected] [Skip All]
```

**Format**:
- Checkboxes for each triggered doc-type
- Show doc-type name and description
- Display which trigger phrases matched
- Show relevant classification hints (first 2-3)
- Action buttons: "Capture Selected" and "Skip All"

### Step 3: Wait for User Selection

**User can**:
1. Select one or more checkboxes
2. Click "Capture Selected" to proceed
3. Click "Skip All" to cancel all captures

**BLOCKING**: Wait for user input before proceeding.

### Step 4: Process Selected Doc-Types

For each selected doc-type:

1. Invoke the corresponding capture skill (`/cdocs:{name}`)
2. Each skill executes independently with the same conversation context
3. Each skill performs its own validation and file writing
4. Each skill may ask follow-up questions if needed

**Sequential execution**: Process one doc-type at a time to avoid context confusion.

**User experience**:
```
Capturing problem documentation...
✓ Problem documentation captured: ./csharp-compounding-docs/problems/nuget-reference-issue-20250125.md

Capturing tool documentation...
✓ Tool documentation captured: ./csharp-compounding-docs/tools/newtonsoft-json-20250125.md

All selected doc-types captured successfully.
```

### Step 5: Report Completion

Display summary:

```
✓ Documentation capture complete

Captured:
- problem: ./csharp-compounding-docs/problems/nuget-reference-issue-20250125.md
- tool: ./csharp-compounding-docs/tools/newtonsoft-json-20250125.md

What's next?
1. Continue workflow
2. Link related docs
3. View documentation
4. Other
```

If user clicked "Skip All":

```
ℹ No documentation captured
```

## Dynamic Skill Detection

This meta-skill does NOT have a hardcoded list of capture skills. Instead, it:

1. **Discovers skills dynamically**:
   - Reads `.csharp-compounding-docs/config.json` to get registered doc-types
   - For each doc-type, checks if corresponding skill exists
   - Includes both built-in and custom skills

2. **Evaluates triggers independently**:
   - Each skill's trigger phrases are checked against conversation
   - No centralized routing - each skill reports if it would trigger
   - Classification hints used to refine matches

3. **Scales automatically**:
   - When new custom doc-types are created with `/cdocs:create-type`
   - No modification to this meta-skill needed
   - New skills participate in conflict detection automatically

## Multi-Trigger Conflict Resolution

### Why Conflicts Occur

The distributed capture pattern (where each skill has its own triggers) creates the possibility of multiple skills matching the same conversation:

**Examples**:
- "I fixed a bug in the NuGet package"
  - Matches `/cdocs:problem` ("fixed", "bug")
  - Matches `/cdocs:tool` ("NuGet", "package")

- "We decided to use the repository pattern"
  - Matches `/cdocs:codebase` ("decided to", "pattern")
  - Matches `/cdocs:style` ("pattern")

- "The API endpoint broke after the dependency update"
  - Matches `/cdocs:problem` ("broke")
  - Matches `/cdocs:tool` ("dependency")
  - Matches custom `/cdocs:api-contract` ("API endpoint")

### Resolution Strategy

1. **Single trigger**: Skill proceeds directly (no conflict)
2. **Multiple triggers**: This meta-skill auto-invokes
3. **User control**: User decides which doc-type(s) to capture
4. **Multi-capture**: Same content can be captured as multiple doc-types if appropriate

### Two-Stage Classification

Each triggered skill uses:

1. **Trigger phrases** (broad matching): Initial detection
2. **Classification hints** (semantic signals): Refinement

When presenting options, show both to help user decide:
- Which trigger phrase matched (why this skill triggered)
- Which classification hints are present (is this the right type?)

## Schema Reference

This skill does not use a schema - it operates on skill metadata and configuration.

## Examples

### Example 1: Problem + Tool Conflict

**Conversation**:
```
User: "I fixed the NuGet package reference issue by updating to version 13.0.3"
```

**Triggered Skills**:
- `/cdocs:problem` - matched "fixed", "issue"
- `/cdocs:tool` - matched "NuGet package", "version"

**Selection Interface**:
```
Multiple doc types detected for this content:

[ ] problem - Solved problems with symptoms, root cause, and solution
    Triggers matched: "fixed", "issue"
    Classification hints: None explicitly present

[ ] tool - Library/package knowledge, API usage, configuration
    Triggers matched: "NuGet package", "version"
    Classification hints: "reference", "dependency"

[Capture Selected] [Skip All]
```

**User Action**: Selects both (this is both a problem fix AND tool knowledge)

**Result**: Two documents created
- `./csharp-compounding-docs/problems/nuget-reference-issue-20250125.md`
- `./csharp-compounding-docs/tools/newtonsoft-json-13-0-3-20250125.md`

### Example 2: Codebase + Style Conflict

**Conversation**:
```
User: "We decided to use the repository pattern for all data access to maintain consistency"
```

**Triggered Skills**:
- `/cdocs:codebase` - matched "decided to", "pattern", "data access"
- `/cdocs:style` - matched "pattern", "consistency"

**Selection Interface**:
```
Multiple doc types detected for this content:

[ ] codebase - Architecture, design decisions, patterns, conventions
    Triggers matched: "decided to", "pattern", "data access"
    Classification hints: "repository", "architecture"

[ ] style - Code style, naming conventions, formatting standards
    Triggers matched: "pattern", "consistency"
    Classification hints: "consistency", "standard"

[Capture Selected] [Skip All]
```

**User Action**: Selects only "codebase" (this is an architecture decision, not a style rule)

**Result**: One document created
- `./csharp-compounding-docs/codebase/repository-pattern-adoption-20250125.md`

### Example 3: Triple Conflict with Custom Type

**Conversation**:
```
User: "The API endpoint /users broke after updating the authentication library"
```

**Triggered Skills**:
- `/cdocs:problem` - matched "broke"
- `/cdocs:tool` - matched "library"
- `/cdocs:api-contract` (custom) - matched "API endpoint"

**Selection Interface**:
```
Multiple doc types detected for this content:

[ ] problem - Solved problems with symptoms, root cause, and solution
    Triggers matched: "broke"
    Classification hints: "error", "issue"

[ ] tool - Library/package knowledge, API usage, configuration
    Triggers matched: "library"
    Classification hints: "authentication", "dependency"

[ ] api-contract - API design decisions and contract specifications
    Triggers matched: "API endpoint"
    Classification hints: "endpoint", "users"

[Capture Selected] [Skip All]
```

**User Action**: Selects "problem" and "api-contract" (not tool - the library is context, not the focus)

**Result**: Two documents created
- `./csharp-compounding-docs/problems/api-auth-endpoint-broken-20250125.md`
- `./csharp-compounding-docs/api-contracts/users-endpoint-issue-20250125.md`

### Example 4: Manual Invocation

**User**: `/cdocs:capture-select`

**Response**:
```
Which doc-type would you like to capture?

[ ] problem - Solved problems with symptoms, root cause, and solution
[ ] tool - Library/package knowledge, API usage, configuration
[ ] codebase - Architecture, design decisions, patterns, conventions
[ ] style - Code style, naming conventions, formatting standards
[ ] [custom-type] - [Custom description]

[Capture Selected] [Skip All]

Or describe what you want to capture and I'll suggest the best match.
```

## Notes

- This skill ensures users maintain control over documentation capture
- Multi-select allows same content to be captured as multiple types when appropriate
- Dynamic detection means new custom doc-types participate automatically
- No hardcoded skill list - scales with project configuration
- Classification hints help users make informed decisions
- Sequential processing prevents context confusion during multi-capture
