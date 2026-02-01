# Phase 099: /cdocs:todo Utility Skill

> **Status**: NOT_STARTED
> **Effort Estimate**: 6-8 hours
> **Category**: Skills System
> **Prerequisites**: Phase 081 (Skills System Foundation)

---

## Spec References

This phase implements the `/cdocs:todo` utility skill defined in:

- **spec/skills/utility-skills.md** - [/cdocs:todo](../spec/skills/utility-skills.md#cdocstodo) - Complete skill specification
- **research/compound-engineering-paradigm-research.md** - [File-Based State Management](../research/compound-engineering-paradigm-research.md#file-based-state-management) - Origin pattern from compound-engineering-plugin

---

## Overview

The `/cdocs:todo` skill provides file-based todo tracking where the **filesystem is the database**. Unlike other skills that interact with the MCP server, this skill uses only file operations, making todos:

- **Human-readable**: Plain markdown files
- **Git-trackable**: Status changes visible in diffs
- **No external dependencies**: No database, no MCP integration
- **Self-documenting**: Filename encodes state

---

## Objectives

1. Create SKILL.md with complete todo management instructions
2. Implement filename-based state encoding convention
3. Define todo file structure with YAML frontmatter
4. Support CRUD operations (create, list, update, complete, archive)
5. Integrate with compound docs via `related_docs` linking
6. Provide status transition workflows

---

## Acceptance Criteria

### Skill Definition

- [ ] SKILL.md created at `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-todo/SKILL.md`
- [ ] YAML frontmatter includes:
  - [ ] `name: cdocs:todo`
  - [ ] `description: File-based todo tracking where the filesystem is the database`
  - [ ] `allowed-tools: [Read, Write, Bash, Glob, Grep]`
  - [ ] `preconditions: ["Project has csharp-compounding-docs directory"]`
- [ ] Skill invocable via `/cdocs:todo` command

### Folder Structure

- [ ] Todos stored in `./csharp-compounding-docs/todos/`
- [ ] Archive folder at `./csharp-compounding-docs/todos/archive/`
- [ ] Folder created automatically if not exists on first todo creation

### File Naming Convention

- [ ] Pattern: `{id}-{status}-{priority}-{description}.md`
- [ ] ID: Zero-padded sequential number (001, 002, 003, ...)
- [ ] Status values: `pending`, `in-progress`, `blocked`, `complete`
- [ ] Priority values: `p1` (critical), `p2` (important), `p3` (nice-to-have)
- [ ] Description: Kebab-case summary (max 50 chars recommended)
- [ ] Example: `001-pending-p1-security-vulnerability.md`

### Todo File Structure

- [ ] YAML frontmatter with:
  - [ ] `id` - Sequential identifier (matches filename)
  - [ ] `status` - Current status (matches filename)
  - [ ] `priority` - Priority level (matches filename)
  - [ ] `created` - ISO date (YYYY-MM-DD)
  - [ ] `source` - Origin: `code-review`, `manual`, `agent-finding`, `bug-report`
  - [ ] `related_docs` - Array of relative paths to compound docs
- [ ] Markdown body with:
  - [ ] Title heading (H1)
  - [ ] Description section
  - [ ] Acceptance Criteria section (with checkboxes)
  - [ ] Notes section (optional)

### Operations

#### Create Todo
- [ ] Generate next sequential ID by scanning existing files
- [ ] Default status: `pending`
- [ ] Prompt for priority if not specified
- [ ] Prompt for source if not specified
- [ ] Generate kebab-case description from title
- [ ] Create file with template structure

#### List Todos
- [ ] Display all todos in tabular format
- [ ] Support filtering by status (`--status pending`)
- [ ] Support filtering by priority (`--priority p1`)
- [ ] Support filtering by source (`--source code-review`)
- [ ] Show count summary by status

#### Update Todo (Status Transition)
- [ ] Rename file to reflect new status
- [ ] Update frontmatter `status` field
- [ ] Valid transitions:
  - [ ] `pending` -> `in-progress`, `blocked`, `complete`
  - [ ] `in-progress` -> `pending`, `blocked`, `complete`
  - [ ] `blocked` -> `pending`, `in-progress`, `complete`
  - [ ] `complete` -> (archived only, no status change)

#### Complete Todo
- [ ] Shortcut for status -> `complete`
- [ ] Rename file with `complete` status
- [ ] Update frontmatter

#### Archive Todo
- [ ] Move completed todos to `archive/` subdirectory
- [ ] Only `complete` status todos can be archived
- [ ] Preserve original filename in archive

### Integration with Compound Docs

- [ ] `related_docs` field links to compound documentation
- [ ] Relative paths from todos folder (e.g., `../problems/null-reference-20250123.md`)
- [ ] Skill suggests linking when creating from code-review findings
- [ ] Skill offers to create compound doc when completing (if significant)

### No MCP Integration

- [ ] Skill uses only Claude Code built-in tools
- [ ] All operations via file system: Read, Write, Bash (for mv), Glob
- [ ] No database queries or API calls
- [ ] State is entirely in filenames and file contents

---

## Implementation Notes

### SKILL.md Content Structure

```markdown
---
name: cdocs:todo
description: File-based todo tracking where the filesystem is the database. Status is encoded in filenames for git-friendly tracking.
allowed-tools:
  - Read
  - Write
  - Bash
  - Glob
  - Grep
preconditions:
  - Project has csharp-compounding-docs directory
---

# /cdocs:todo - File-Based Todo Tracking

## Overview

This skill manages todos using the filesystem as the database. Each todo is a markdown file where the filename encodes the state, making status changes visible in git diffs.

## Folder Location

```
./csharp-compounding-docs/todos/
├── 001-pending-p1-security-vulnerability.md
├── 002-in-progress-p2-performance-optimization.md
├── 003-blocked-p2-api-integration.md
└── archive/
    └── 004-complete-p3-code-cleanup.md
```

## File Naming Convention

Pattern: `{id}-{status}-{priority}-{description}.md`

| Component | Values | Description |
|-----------|--------|-------------|
| `id` | 001, 002, ... | Zero-padded sequential number |
| `status` | pending, in-progress, blocked, complete | Current state |
| `priority` | p1, p2, p3 | Critical, Important, Nice-to-have |
| `description` | kebab-case | Brief summary (max 50 chars) |

## Intake Options

Present this menu when invoked:

```
/cdocs:todo - File-Based Todo Tracking

What would you like to do?

1. Create new todo
2. List todos (all)
3. List todos (pending only)
4. Update todo status
5. Complete a todo
6. Archive completed todos
7. Show todo by ID

Enter choice (1-7):
```

## Operations

### 1. Create Todo

1. Scan `./csharp-compounding-docs/todos/` for highest ID
2. Generate next ID (zero-padded to 3 digits)
3. Prompt for:
   - Title (required)
   - Priority: p1/p2/p3 (default: p2)
   - Source: code-review/manual/agent-finding/bug-report (default: manual)
   - Related docs (optional, comma-separated paths)
4. Generate kebab-case description from title
5. Create file with template

### 2. List Todos

```bash
# Scan todos directory
Glob: ./csharp-compounding-docs/todos/*.md
```

Display as table:
```
ID   Status       Priority  Description
---  -----------  --------  -----------
001  pending      p1        security-vulnerability
002  in-progress  p2        performance-optimization
003  blocked      p2        api-integration

Summary: 1 pending, 1 in-progress, 1 blocked, 0 complete
```

### 3. Update Status

1. Find todo by ID
2. Present valid transitions based on current status
3. Rename file with new status
4. Update frontmatter status field

### 4. Complete Todo

Shortcut for update -> complete status.

### 5. Archive Todos

1. Find all complete todos
2. Move to archive/ subdirectory
3. Report archived count

## Todo File Template

```yaml
---
id: {id}
status: pending
priority: {priority}
created: {YYYY-MM-DD}
source: {source}
related_docs:
  - ../problems/example-20250123.md
---

# {Title}

## Description

[Detailed description of the todo item]

## Acceptance Criteria

- [ ] Criterion 1
- [ ] Criterion 2
- [ ] Criterion 3

## Notes

[Additional context, links, references]
```

## Status Transitions

```
pending ──────┬──────> in-progress ──────┬──────> complete ──────> archive
              │              │           │
              │              v           │
              └────────> blocked ────────┘
```

Valid transitions:
- `pending` -> `in-progress`, `blocked`, `complete`
- `in-progress` -> `pending`, `blocked`, `complete`
- `blocked` -> `pending`, `in-progress`, `complete`
- `complete` -> archive (move, not status change)

## Integration with Compound Docs

When completing a significant todo, offer to capture learnings:

```
Todo completed: 001-complete-p1-security-vulnerability.md

This looks like a significant fix. Would you like to document it?

1. Create problem doc (/cdocs:problem)
2. Create insight doc (/cdocs:insight)
3. No documentation needed
```

## Key Characteristics

- **No database**: Files ARE the state
- **Git-friendly**: Status visible in diffs
- **Human-readable**: Plain markdown
- **Self-documenting**: Filename tells the story
```

### Helper Functions for Skill

The skill should implement these operations using built-in tools:

```markdown
## Implementation Guidance

### Get Next ID
1. Glob: `./csharp-compounding-docs/todos/*.md`
2. Extract IDs from filenames using regex
3. Find max ID, add 1, zero-pad to 3 digits

### Rename for Status Change
1. Parse current filename
2. Replace status component
3. Use Bash: `mv "{old_name}" "{new_name}"`

### Create Todo File
1. Build filename from components
2. Generate YAML frontmatter
3. Generate markdown body
4. Write file

### Archive Todo
1. Verify status is `complete`
2. Use Bash: `mv "todos/{filename}" "todos/archive/{filename}"`
```

---

## Dependencies

### Depends On
- Phase 081: Skills System Foundation (skill loading and invocation)
- Phase 009: Plugin Directory Structure (skill file locations)

### Blocks
- None (standalone utility skill)

---

## Verification Steps

After completing this phase, verify:

1. **Skill Discovery**: `/cdocs:todo` appears in available skills
2. **Create Operation**: Can create new todo with sequential ID
3. **List Operation**: Shows todos in tabular format with filters
4. **Update Operation**: File renamed correctly on status change
5. **Complete Operation**: Shortcut works for completing todos
6. **Archive Operation**: Completed todos move to archive folder
7. **Git Integration**: Status changes visible in `git diff`
8. **Related Docs**: Links to compound docs work correctly

### Manual Verification

```bash
# Create a todo
/cdocs:todo
> 1 (Create new todo)
> "Fix null reference in UserService"
> p1
> code-review

# Verify file created
ls ./csharp-compounding-docs/todos/
# Expected: 001-pending-p1-fix-null-reference-in-userservice.md

# List todos
/cdocs:todo
> 2 (List todos)
# Expected: Table showing the new todo

# Update status
/cdocs:todo
> 4 (Update status)
> 001
> in-progress

# Verify rename
ls ./csharp-compounding-docs/todos/
# Expected: 001-in-progress-p1-fix-null-reference-in-userservice.md

# Complete todo
/cdocs:todo
> 5 (Complete a todo)
> 001

# Archive
/cdocs:todo
> 6 (Archive completed)

# Verify archive
ls ./csharp-compounding-docs/todos/archive/
# Expected: 001-complete-p1-fix-null-reference-in-userservice.md

# Check git status
git status
# Expected: Shows file moves/renames
```

### Acceptance Test Scenarios

| Scenario | Input | Expected Output |
|----------|-------|-----------------|
| Create first todo | Title: "Test todo" | File: `001-pending-p2-test-todo.md` |
| Create second todo | Title: "Another todo" | File: `002-pending-p2-another-todo.md` |
| List with filter | `--status pending` | Only pending todos shown |
| Update to blocked | ID: 001, Status: blocked | File renamed to `001-blocked-p2-test-todo.md` |
| Complete todo | ID: 001 | File renamed to `001-complete-p2-test-todo.md` |
| Archive completed | (no input) | File moved to `archive/` |
| Invalid transition | complete -> pending | Error: Invalid transition |

---

## Files to Create

### New Files

| File | Purpose |
|------|---------|
| `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-todo/SKILL.md` | Main skill definition |
| `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-todo/references/patterns.md` | Todo patterns and examples |
| `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-todo/templates/todo.md` | Todo file template |

### Template File Content

```markdown
# templates/todo.md

---
id: {{ID}}
status: pending
priority: {{PRIORITY}}
created: {{DATE}}
source: {{SOURCE}}
related_docs: []
---

# {{TITLE}}

## Description

{{DESCRIPTION}}

## Acceptance Criteria

- [ ] TODO: Add acceptance criteria

## Notes

_No additional notes._
```

### References File Content

```markdown
# references/patterns.md

## Priority Guidelines

| Priority | Use When | SLA |
|----------|----------|-----|
| p1 (Critical) | Security issues, data loss, production outages | Same day |
| p2 (Important) | Bugs affecting users, performance issues | This sprint |
| p3 (Nice-to-have) | Tech debt, minor improvements, enhancements | Backlog |

## Source Types

| Source | Description | Example |
|--------|-------------|---------|
| `code-review` | Found during PR review | Security vulnerability in auth |
| `manual` | Manually created | Remember to update docs |
| `agent-finding` | Discovered by AI agent | N+1 query detected |
| `bug-report` | User-reported issue | Login fails on Safari |

## Naming Conventions

Good descriptions:
- `fix-null-reference-in-userservice`
- `add-rate-limiting-to-api`
- `update-deprecated-package`

Avoid:
- `fix-bug` (too vague)
- `implement-feature` (too vague)
- `todo` (not descriptive)
```

---

## Notes

- This skill intentionally avoids MCP integration for simplicity and portability
- Files can be edited directly with any text editor or IDE
- The filename convention allows quick status checks via `ls` or file explorer
- Git diffs show status transitions clearly (file renames)
- The skill works even when the MCP server is not running
- Archive is optional - completed todos can remain in main folder if preferred
- ID gaps are acceptable (if todo 002 is deleted, next is still 003)
- The skill should handle edge cases like missing folders gracefully
