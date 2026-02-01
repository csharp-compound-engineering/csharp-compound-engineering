# Phase 081: Skill File Structure

> **Category**: Skills System
> **Prerequisites**: Phase 009 (Plugin Directory Structure)
> **Estimated Effort**: 3-4 hours
> **Status**: Pending

---

## Objective

Define the canonical directory layout, SKILL.md template with YAML frontmatter, schema.yaml placement, and configuration patterns for all `/cdocs:` skills in the CSharp Compounding Docs plugin.

---

## Success Criteria

- [ ] Skill directory naming convention established (`skills/cdocs-{name}/`)
- [ ] SKILL.md template with complete YAML frontmatter defined
- [ ] `schema.yaml` location and format specified for built-in doc-types
- [ ] Optional `references/` directory pattern documented
- [ ] `allowed-tools` configuration patterns for each skill category
- [ ] `preconditions` specification for skill activation requirements
- [ ] Example skill structure created for reference implementation

---

## Specification References

| Document | Section | Relevance |
|----------|---------|-----------|
| [spec/skills/skill-patterns.md](../spec/skills/skill-patterns.md) | Skill File Structure | Canonical directory layout definition |
| [spec/skills/skill-patterns.md](../spec/skills/skill-patterns.md) | SKILL.md Template | Complete frontmatter and body structure |
| [research/building-claude-code-skills-research.md](../research/building-claude-code-skills-research.md) | Complete YAML Frontmatter Reference | All available frontmatter fields |
| [research/building-claude-code-skills-research.md](../research/building-claude-code-skills-research.md) | Tool Permissions | allowed-tools patterns |
| [structure/skills.md](../structure/skills.md) | Skills Summary | Overview of skill categories and patterns |

---

## Tasks

### Task 81.1: Define Skill Directory Naming Convention

Establish the naming convention for skill directories within the plugin:

**Pattern**: `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-{name}/`

**Naming Rules**:
- Directory name uses `cdocs-` prefix followed by skill name
- Skill name portion uses lowercase letters, numbers, and hyphens only
- Directory name must match the `name` field in SKILL.md frontmatter (minus the `:` separator)
- Maximum total directory name length: 64 characters

**Examples**:
| Skill Name | Directory Path |
|------------|----------------|
| `cdocs:problem` | `skills/cdocs-problem/` |
| `cdocs:query-external` | `skills/cdocs-query-external/` |
| `cdocs:capture-select` | `skills/cdocs-capture-select/` |
| `cdocs:create-type` | `skills/cdocs-create-type/` |

---

### Task 81.2: Define Complete Skill Directory Structure

Create the canonical directory structure for each skill:

```
${CLAUDE_PLUGIN_ROOT}/skills/cdocs-{name}/
├── SKILL.md              # Required - Core skill definition with YAML frontmatter
├── schema.yaml           # Required for capture skills - Schema for this doc-type
└── references/           # Optional - Reference materials and examples
    └── examples.md       # Optional - Example captured documents
```

**File Purposes**:

| File | Required | Purpose |
|------|----------|---------|
| `SKILL.md` | Always | Main skill definition with frontmatter and instructions |
| `schema.yaml` | Capture skills only | YAML schema defining required/optional fields for doc-type |
| `references/` | Optional | Directory for supplementary documentation |
| `references/examples.md` | Optional | Example documents showing expected output format |

---

### Task 81.3: Define SKILL.md YAML Frontmatter Structure

Create the complete YAML frontmatter template for cdocs skills:

```yaml
---
# Required Fields
name: cdocs:{type}
description: Brief description in third person that describes what the skill does and when to use it

# Tool Restrictions
allowed-tools:
  - Read                    # Access conversation context and files
  - Write                   # Create documentation files
  - Bash                    # Git operations if needed
  - mcp__csharp-compounding-docs__*  # All plugin MCP tools
  - mcp__sequential-thinking__*       # Complex reasoning (if needed)

# Activation Requirements
preconditions:
  - Project activated via /cdocs:activate
  - [Type-specific preconditions]

# Invocation Control (optional)
disable-model-invocation: false   # Set true for user-only invocation
user-invocable: true              # Set false for Claude-only invocation

# Auto-Invocation (optional - for capture skills)
auto-invoke:
  trigger: conversation-pattern   # or project-entry
  patterns:
    - "trigger phrase 1"
    - "trigger phrase 2"

# Metadata (optional)
version: 1.0.0
argument-hint: "[optional-arguments]"
---
```

**Field Requirements by Skill Category**:

| Field | Capture Skills | Query Skills | Meta Skills | Utility Skills |
|-------|----------------|--------------|-------------|----------------|
| `name` | Required | Required | Required | Required |
| `description` | Required | Required | Required | Required |
| `allowed-tools` | Required | Required | Required | Required |
| `preconditions` | Required | Required | Required | Varies |
| `auto-invoke` | Recommended | Not Used | Special Cases | Not Used |
| `disable-model-invocation` | Optional | Optional | Varies | Often True |

---

### Task 81.4: Define allowed-tools Configuration by Category

Specify the tool permissions for each skill category:

**Capture Skills** (`cdocs:problem`, `cdocs:insight`, `cdocs:codebase`, `cdocs:tool`, `cdocs:style`):
```yaml
allowed-tools:
  - Read                    # Read conversation context, existing docs
  - Write                   # Create new documentation files
  - Glob                    # Find existing documents
  - mcp__csharp-compounding-docs__semantic_search  # Check for duplicates
  - mcp__sequential-thinking__sequentialthinking   # Complex analysis
```

**Query Skills** (`cdocs:query`, `cdocs:search`, `cdocs:query-external`, `cdocs:search-external`):
```yaml
allowed-tools:
  - Read                    # Read retrieved documents
  - mcp__csharp-compounding-docs__rag_query        # For query skills
  - mcp__csharp-compounding-docs__semantic_search  # For search skills
  - mcp__csharp-compounding-docs__rag_query_external        # For external query
  - mcp__csharp-compounding-docs__search_external_docs      # For external search
```

**Meta Skills** (`cdocs:activate`, `cdocs:create-type`, `cdocs:capture-select`):
```yaml
allowed-tools:
  - Read                    # Read configuration files
  - Write                   # Update configuration files
  - Edit                    # Modify existing files
  - Bash                    # Git operations for cdocs:activate
  - Glob                    # Find configuration files
  - mcp__csharp-compounding-docs__activate         # For cdocs:activate
  - mcp__sequential-thinking__sequentialthinking   # For cdocs:create-type
```

**Utility Skills** (`cdocs:delete`, `cdocs:promote`, `cdocs:todo`, `cdocs:worktree`, `cdocs:research`):
```yaml
# cdocs:delete
allowed-tools:
  - Read                    # Read documents before deletion
  - mcp__csharp-compounding-docs__delete_documents

# cdocs:promote
allowed-tools:
  - Read                    # Read document to promote
  - Edit                    # Update YAML frontmatter
  - mcp__csharp-compounding-docs__update_promotion

# cdocs:todo
allowed-tools:
  - Read                    # Read todo files
  - Write                   # Create todo files
  - Bash                    # File operations (mv for status changes)
  - Glob                    # List todos

# cdocs:worktree
allowed-tools:
  - Bash                    # Git worktree commands only
  - Read                    # Read git configuration

# cdocs:research
allowed-tools:
  - Read                    # Read research context
  - Write                   # Write research output
  - WebSearch               # External research
  - WebFetch                # Fetch documentation
  - mcp__sequential-thinking__sequentialthinking
  - mcp__context7__*        # Framework documentation
  - mcp__microsoft-learn__* # .NET documentation
```

---

### Task 81.5: Define preconditions Specification

Document the preconditions system for skill activation:

**Precondition Types**:

1. **Project Activation**:
   ```yaml
   preconditions:
     - Project activated via /cdocs:activate
   ```
   Requires that `/cdocs:activate` has been run in the current session.

2. **Configuration Existence**:
   ```yaml
   preconditions:
     - ./csharp-compounding-docs/config.json exists
   ```
   Requires specific configuration files to be present.

3. **MCP Server Availability**:
   ```yaml
   preconditions:
     - csharp-compounding-docs MCP server available
   ```
   Requires the plugin's MCP server to be running.

4. **External Docs Configured** (for external query/search skills):
   ```yaml
   preconditions:
     - Project activated via /cdocs:activate
     - external_docs configured in config.json
   ```

5. **Skill-Specific Requirements**:
   ```yaml
   # For cdocs:create-type
   preconditions:
     - Project activated via /cdocs:activate
     - User has confirmed custom type creation intent
   ```

**Precondition Evaluation**:
- Preconditions are evaluated when the skill is invoked
- If preconditions are not met, the skill should display an actionable error message
- Skills should guide users to satisfy preconditions (e.g., "Run /cdocs:activate first")

---

### Task 81.6: Define schema.yaml Location and Format

Specify the schema file structure for capture skills:

**Location**: `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-{name}/schema.yaml`

**Schema Format**:
```yaml
# schema.yaml for cdocs:{type}
type: {type-name}
version: "1.0"
description: Brief description of what this doc-type captures

fields:
  required:
    - name: title
      type: string
      description: Document title
      validation:
        min_length: 5
        max_length: 100

    - name: category
      type: enum
      description: Document category
      values:
        - value1
        - value2
        - value3

    - name: content
      type: markdown
      description: Main content body

  optional:
    - name: tags
      type: array
      items: string
      description: Searchable tags

    - name: related_docs
      type: array
      items: path
      description: Paths to related documents

    - name: promotion
      type: enum
      values:
        - standard
        - important
        - critical
      default: standard
      description: Document visibility level

frontmatter_order:
  - title
  - type
  - category
  - date
  - tags
  - promotion
  - related_docs
```

**Schema Files by Skill**:

| Skill | Schema File | Purpose |
|-------|-------------|---------|
| `cdocs:problem` | `cdocs-problem/schema.yaml` | Problem/solution doc structure |
| `cdocs:insight` | `cdocs-insight/schema.yaml` | Product insight doc structure |
| `cdocs:codebase` | `cdocs-codebase/schema.yaml` | Codebase knowledge doc structure |
| `cdocs:tool` | `cdocs-tool/schema.yaml` | Tool/library doc structure |
| `cdocs:style` | `cdocs-style/schema.yaml` | Coding style doc structure |

---

### Task 81.7: Define Optional references/ Directory Pattern

Document the structure for optional reference materials:

**Directory Structure**:
```
${CLAUDE_PLUGIN_ROOT}/skills/cdocs-{name}/references/
├── examples.md           # Example captured documents
├── templates.md          # Template variations
└── guidelines.md         # Additional authoring guidelines
```

**When to Use references/**:
- Complex skills that benefit from concrete examples
- Skills with multiple output variations
- Skills requiring detailed formatting guidelines

**Referencing from SKILL.md**:
```markdown
## Examples

For complete examples of captured documents, see `references/examples.md`.

## Templates

Available template variations are documented in `references/templates.md`.
```

---

### Task 81.8: Create Reference Implementation Skill Structure

Create an example skill directory structure for `cdocs:problem`:

**Directory**: `plugins/csharp-compounding-docs/skills/cdocs-problem/`

**File: `SKILL.md`**:
```markdown
---
name: cdocs:problem
description: Captures solved problems with symptoms, root cause analysis, and verified solution. Use when a bug has been fixed, an error resolved, or a technical challenge overcome.
allowed-tools:
  - Read
  - Write
  - Glob
  - mcp__csharp-compounding-docs__semantic_search
  - mcp__sequential-thinking__sequentialthinking
preconditions:
  - Project activated via /cdocs:activate
auto-invoke:
  trigger: conversation-pattern
  patterns:
    - "fixed"
    - "it's fixed"
    - "problem solved"
    - "the issue was"
    - "root cause"
    - "turns out the problem"
version: 1.0.0
---

# Problem Documentation Skill

## Intake

This skill captures solved technical problems including:
- Bug fixes with reproduction steps
- Error resolutions with stack traces
- Configuration issues with correct settings
- Performance problems with optimizations

## Process

### Step 1: Gather Context

Extract from conversation history:
- **Problem statement**: What was the original issue?
- **Symptoms**: Error messages, unexpected behavior, stack traces
- **Root cause**: What was actually wrong?
- **Solution**: What fixed it?
- **Verification**: How was the fix confirmed?

**BLOCKING**: If root cause or solution is unclear, ask and WAIT.

### Step 2: Validate Schema

Load `schema.yaml` for problem doc-type.
Validate all required fields present:
- title
- symptoms
- root_cause
- solution
- verification

**BLOCK if validation fails** - show specific errors.

### Step 3: Write Documentation

1. Generate filename: `{sanitized-title}-{YYYYMMDD}.md`
2. Create directories: `mkdir -p ./csharp-compounding-docs/problems/`
3. Write file with YAML frontmatter + markdown body

### Step 4: Post-Capture Options

Confirm documentation captured:

```
Documentation captured

File created: ./csharp-compounding-docs/problems/{filename}.md

What's next?
1. Continue workflow
2. Link related docs
3. View documentation
4. Other
```

## Schema Reference

See `schema.yaml` in this skill directory.

## Examples

See `references/examples.md` for example problem documents.
```

**File: `schema.yaml`**:
```yaml
type: problem
version: "1.0"
description: Captures solved problems with symptoms, root cause, and solution

fields:
  required:
    - name: title
      type: string
      description: Brief problem description
      validation:
        min_length: 10
        max_length: 100

    - name: symptoms
      type: markdown
      description: Observable symptoms, error messages, or unexpected behavior

    - name: root_cause
      type: markdown
      description: The actual underlying cause of the problem

    - name: solution
      type: markdown
      description: The fix that resolved the problem

    - name: verification
      type: markdown
      description: How the fix was verified to work

  optional:
    - name: tags
      type: array
      items: string
      description: Searchable tags (e.g., dotnet, ef-core, async)

    - name: affected_files
      type: array
      items: path
      description: Files that were modified in the fix

    - name: related_docs
      type: array
      items: path
      description: Related problem or insight documents

    - name: promotion
      type: enum
      values:
        - standard
        - important
        - critical
      default: standard

    - name: environment
      type: object
      properties:
        runtime: string
        framework: string
        os: string

frontmatter_order:
  - title
  - type
  - date
  - tags
  - promotion
  - environment
  - affected_files
  - related_docs
```

**File: `references/examples.md`**:
```markdown
# Problem Documentation Examples

## Example 1: EF Core Connection Pool Exhaustion

```yaml
---
title: EF Core connection pool exhaustion under load
type: problem
date: 2025-01-15
tags:
  - ef-core
  - postgresql
  - connection-pooling
promotion: important
environment:
  runtime: .NET 10
  framework: EF Core 10.0
  os: Linux (Docker)
affected_files:
  - src/Data/AppDbContext.cs
  - src/Startup.cs
---

## Symptoms

Under load testing with 100 concurrent users, the application threw:
- `Npgsql.NpgsqlException: The connection pool has been exhausted`
- Response times degraded from 50ms to 30+ seconds
- Database connections climbed to 100 and stayed maxed

## Root Cause

DbContext instances were being injected as singleton instead of scoped,
causing connection reuse issues. Each request held a connection for the
entire singleton lifetime instead of releasing it after the request.

## Solution

Changed DbContext registration from:
```csharp
services.AddSingleton<AppDbContext>();
```

To:
```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString,
        npgsql => npgsql.EnableRetryOnFailure()));
```

## Verification

- Load test completed with 500 concurrent users
- Connection count stable at ~20
- Response times consistent at 45-60ms
```

## Example 2: Async Deadlock in ASP.NET

[Additional examples...]
```

---

### Task 81.9: Document Skill File Discovery

Specify how Claude Code discovers and loads skills:

**Discovery Process**:
1. On session start, Claude Code scans `${CLAUDE_PLUGIN_ROOT}/skills/`
2. Each directory with a `SKILL.md` file is registered as a skill
3. Only `name` and `description` from frontmatter are loaded initially (~30-50 tokens per skill)
4. Full SKILL.md content is loaded only when the skill is invoked
5. `schema.yaml` and `references/` are accessed on-demand

**Directory Requirements**:
- Directory must contain `SKILL.md` file
- `SKILL.md` must have valid YAML frontmatter with `name` and `description`
- Directory name should match skill name pattern (convention, not enforced)

**Load Priority**:
1. Plugin-level skills (`${CLAUDE_PLUGIN_ROOT}/skills/`)
2. Project-level overrides (`.claude/skills/` in project root)
3. User-level overrides (`~/.claude/skills/`)

---

## Verification Checklist

After completing all tasks, verify:

1. **Directory Structure**:
   ```bash
   tree plugins/csharp-compounding-docs/skills/cdocs-problem/
   ```
   Expected output:
   ```
   plugins/csharp-compounding-docs/skills/cdocs-problem/
   ├── SKILL.md
   ├── schema.yaml
   └── references/
       └── examples.md
   ```

2. **YAML Validation**:
   ```bash
   # Validate SKILL.md frontmatter
   head -50 plugins/csharp-compounding-docs/skills/cdocs-problem/SKILL.md

   # Validate schema.yaml
   yq . plugins/csharp-compounding-docs/skills/cdocs-problem/schema.yaml
   ```

3. **Frontmatter Field Completeness**:
   - [ ] `name` field present and matches `cdocs:{type}` pattern
   - [ ] `description` field present and under 1024 characters
   - [ ] `allowed-tools` includes all required tools for skill category
   - [ ] `preconditions` includes project activation requirement
   - [ ] `auto-invoke` configured for capture skills

4. **Schema Completeness**:
   - [ ] All required fields defined with types
   - [ ] Enum values specified where applicable
   - [ ] Validation rules defined
   - [ ] `frontmatter_order` specified

---

## Dependencies

| Phase | Dependency Type | Description |
|-------|-----------------|-------------|
| Phase 009 | Hard | Plugin directory structure must exist |
| Phase 082-090 | Provides | Individual skill implementations use this structure |
| Phase 014 | Related | Schema validation service validates against schema.yaml |

---

## Notes

- The `cdocs-` prefix in directory names avoids conflicts with other Claude Code skills
- SKILL.md uses third-person descriptions per Claude Code conventions
- Schema files enable validation before document creation, reducing errors
- The `references/` directory supports progressive disclosure of examples
- Auto-invoke patterns should be specific enough to avoid false positives

---

## Change Log

| Date | Changes |
|------|---------|
| 2025-01-24 | Initial phase creation |
