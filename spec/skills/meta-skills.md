# Meta Skills

> **Status**: [DRAFT]
> **Parent**: [../skills.md](../skills.md)

---

## Overview

Meta skills operate on the skill system itself rather than on documentation content. They include creating new doc-types, handling multi-trigger conflicts, and project activation.

> **Background**: Comprehensive details on SKILL.md file structure, YAML frontmatter fields, and skill invocation patterns. See [Claude Skills YAML Frontmatter Research](../../research/claude-skills-yaml-frontmatter-research.md).
>
> **Background**: Detailed guidance on building custom skills including tool permissions, triggers, hooks, and MCP integration. See [Building Claude Code Skills Research](../../research/building-claude-code-skills-research.md).

---

## `/cdocs:activate`

**Purpose**: Activate the compounding docs system for current project.

**Invocation**: Auto-invoked when entering a project with `.csharp-compounding-docs/config.json`.

> **Background**: Methods for detecting hooks and auto-invoking skills on project entry, including SessionStart hooks and CLAUDE.md integration. See [Claude Code Hooks for Skill Invocation Research](../../research/claude-code-hooks-skill-invocation.md).

**Behavior**:
1. Detect project root (git root)
2. Check for `.csharp-compounding-docs/config.json`
3. Get current git branch name via `git branch --show-current`
   > **Background**: Comprehensive guide to Git branch detection methods, edge cases (detached HEAD, bare repos), and cross-platform scripting best practices. See [Git Current Branch Detection Research](../../research/git-current-branch-detection.md).
4. Compute absolute path to config file
5. Call MCP `activate_project` tool with:
   - `config_path`: Absolute path to `.csharp-compounding-docs/config.json`
   - `branch_name`: Current git branch
6. Call MCP `list_doc_types` tool to get available doc-types
7. Report activation status to user
8. **Update CLAUDE.md**: Append/update a "Compounding Docs" section listing available doc-types and their descriptions, so future sessions know what documentation types can be captured

**SKILL.md Structure**:
```yaml
---
name: cdocs:activate
description: Activate compounding docs for current project
allowed-tools:
  - Bash  # Git root and branch detection
preconditions:
  - .csharp-compounding-docs/config.json exists
auto-invoke:
  trigger: project-entry
---
```

**Example Activation Call**:
```pseudocode
# Get branch name
git branch --show-current
# Output: feature/new-feature

# Get repo root
git rev-parse --show-toplevel
# Output: /Users/dev/my-project

# Call MCP tool
activate_project(
  config_path="/Users/dev/my-project/.csharp-compounding-docs/config.json",
  branch_name="feature/new-feature"
)
```

---

## `/cdocs:create-type`

**Purpose**: Create a new custom doc-type with dedicated skill and schema.

**MCP Integration**:
- **Sequential Thinking**: Schema design reasoning (field relationships, type selection), trigger phrase generation (semantic coverage analysis), and validation rule logic (constraint dependencies)
  > **Background**: Verification of the correct MCP package name (`@modelcontextprotocol/server-sequential-thinking`). See [Sequential Thinking MCP Verification](../../research/sequential-thinking-mcp-verification.md).

**Behavior**:
1. Interview user about new doc-type:
   - Name (kebab-case)
   - Description
   - Required fields (with types and enums)
   - Optional fields
   - **Trigger phrases** (phrases that auto-trigger capture for this type)
   - **Classification hints** (semantic signals indicating this type is appropriate)
2. Use Sequential Thinking to:
   - Analyze field relationships and recommend appropriate types/constraints
   - Generate comprehensive trigger phrases with semantic coverage analysis
   - Validate that classification hints don't overlap with existing doc-types
   - Ensure trigger phrases and classification hints work together (two-stage classification to reduce false positives)
3. Generate schema file with trigger and classification metadata
4. Generate dedicated skill for capturing this doc-type (`/cdocs:{name}`)
5. Update config with new doc-type registration
6. Report completion and explain how to use the new skill

**Generated Artifacts**:

| Artifact | Location | Purpose |
|----------|----------|---------|
| Schema file | `./csharp-compounding-docs/schemas/{name}.schema.yaml` | Field definitions, validation rules, classification metadata |
| Skill file | `.claude/skills/cdocs-{name}/SKILL.md` | Dedicated capture skill for this doc-type (project scope) |
| Config update | `./csharp-compounding-docs/config.json` | Register new doc-type |

**Important**: Custom skills are placed in `.claude/skills/` (project scope), NOT in the plugin folder. This ensures:
- Custom doc-types are project-specific (different projects can have different custom types)
- Custom skills are version-controlled with the project
- No modification of the installed plugin files

**Schema File Structure**:

> **Background**: Comprehensive guide to YAML schema formats, JSON Schema validation patterns, and best practices for schema design. See [YAML Schema Formats Research](../../research/yaml-schema-formats.md).

```yaml
# ./csharp-compounding-docs/schemas/api-contract.schema.yaml
name: api-contract
description: API design decisions and contract specifications
folder: api-contracts

# Classification metadata (trigger phrases + hints work together for two-stage classification)
trigger_phrases:
  - "API endpoint"
  - "contract change"
  - "breaking change"
  - "API design"
  - "endpoint behavior"
classification_hints:
  - "HTTP method"
  - "request/response"
  - "REST"
  - "GraphQL"
  - "versioning"

required_fields:
  - name: endpoint
    type: string
    description: API endpoint path
  - name: change_type
    type: enum
    values: [new, breaking, non_breaking, deprecated]

optional_fields:
  - name: affected_consumers
    type: array
    description: List of known consumers affected
```

**Generated Skill Structure** (`/cdocs:{name}`):

> **Background**: Auto-invoke mechanisms, description-based triggering, and invocation control patterns for skills. See [Claude Code Skills Research](../../research/claude-code-skills-research.md).

```yaml
---
name: cdocs:api-contract
description: Capture API contract documentation
allowed-tools:
  - Read
  - Write
preconditions:
  - Project activated via /cdocs:activate
auto-invoke:
  trigger: conversation-pattern
  patterns:
    - "API endpoint"
    - "contract change"
    - "breaking change"
    - "API design"
    - "endpoint behavior"
---

[Skill instructions for capturing api-contract documents...]
```

**Key Feature**: Each custom doc-type gets its own **independent** skill (`/cdocs:{name}`) that:
- Has its own auto-invoke triggers based on `trigger_phrases` (each skill monitors independently)
- Has its own classification hints for semantic matching
- Validates against the custom schema
- Writes to the correct folder (`./csharp-compounding-docs/{folder}/`)

**Independence**: Custom type skills operate the same way as built-in skills. When a custom type's trigger phrase is detected, its skill activates directly - there is no centralized routing.

---

## `/cdocs:capture-select`

**Purpose**: Present a unified selection interface when 2+ capture skills trigger simultaneously.

**Characteristics**:
- **User-invocable and auto-invoke**: Can be invoked manually via `/cdocs:capture-select` or auto-invokes when 2+ capture skills trigger
- **Dynamic detection**: Does not have a hardcoded list of conflicting skills
- **Multi-select**: User can select 0, 1, or multiple doc-types to capture

**User Experience**:

```
Multiple doc types detected for this content:

[ ] Problem - Bug fix documentation
    Triggers matched: "fixed", "bug"

[ ] Tool - Library/package knowledge
    Triggers matched: "NuGet", "package"

[Capture Selected] [Skip All]
```

**Flow**:

```
Conversation with trigger phrases
         |
         v
+-------------------------+
| Check triggered skills  |
| (parallel evaluation)   |
+-------------------------+
         |
         +-- 0 skills -> No action
         |
         +-- 1 skill -> Direct invocation
         |
         +-- 2+ skills -> /cdocs:capture-select
                              |
                              v
                    +-----------------+
                    | Multi-select UI |
                    +-----------------+
                              |
                              v
              For each selected doc-type:
                    Invoke capture skill
```

**Implementation Notes**:
- The meta-skill queries all registered capture skills to check which would trigger
- Skills report their trigger match without executing capture
- User selection drives subsequent skill invocations
- Each selected skill captures independently (not merged)

---

## Multi-Trigger Conflict Resolution

When multiple capture skills (e.g., `/cdocs:problem` and `/cdocs:tool`) trigger on the same conversation, the `/cdocs:capture-select` meta-skill handles conflict resolution.

### Behavior

1. **Single trigger**: Normal flow - the triggered skill proceeds directly
2. **Multiple triggers**: Meta-skill auto-invokes with multi-select dialog

### Why This Matters

The distributed capture pattern (where each skill has its own triggers) creates the possibility of multiple skills matching the same conversation. For example:

- "I fixed a bug in the NuGet package" matches both `/cdocs:problem` ("fixed", "bug") and `/cdocs:tool` ("NuGet", "package")
- "We decided to use the repository pattern" matches both `/cdocs:codebase` ("decided to", "pattern") and `/cdocs:style` ("pattern")

The `/cdocs:capture-select` meta-skill ensures users maintain control over what gets captured when ambiguous situations arise.

---

## Related Documentation

- [Capture Skills](./capture-skills.md) - Create documentation for specific doc-types
- [Query Skills](./query-skills.md) - Search and retrieve documentation
- [Utility Skills](./utility-skills.md) - Delete, promote, and manage documentation
- [Doc Types Specification](../doc-types.md) - Schema definitions and two-stage classification
