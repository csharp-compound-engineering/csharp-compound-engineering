# Phase 094: /cdocs:activate Meta Skill

> **Status**: NOT_STARTED
> **Effort Estimate**: 6-8 hours
> **Category**: Skills System
> **Prerequisites**: Phase 081 (Skills System Base Infrastructure), Phase 079 (MCP Server Core - activate_project tool)

---

## Spec References

This phase implements the `/cdocs:activate` meta skill defined in:

- **spec/skills/meta-skills.md** - [/cdocs:activate](../spec/skills/meta-skills.md#cdocsactivate) - Skill specification, YAML frontmatter, and behavior
- **spec/mcp-server/tools.md** - [9. Activate Project Tool](../spec/mcp-server/tools.md#9-activate-project-tool) - MCP tool parameters and response
- **research/git-current-branch-detection.md** - Git branch detection methods and edge cases
- **research/claude-code-hooks-skill-invocation.md** - Auto-invoke mechanisms for skills

---

## Objectives

1. Create the `SKILL.md` file for `/cdocs:activate` with proper YAML frontmatter
2. Implement auto-invoke behavior triggered on project entry (config.json detection)
3. Detect Git repository root using `git rev-parse --show-toplevel`
4. Detect current Git branch using `git branch --show-current` with fallback
5. Invoke MCP `activate_project` tool with computed paths and branch name
6. Call `list_doc_types` to retrieve available documentation types
7. Update project's `CLAUDE.md` with available doc-types section
8. Report activation status and available skills to user

---

## Acceptance Criteria

### SKILL.md File Structure

- [ ] Skill file created at `plugins/csharp-compounding-docs/skills/cdocs-activate/SKILL.md`
- [ ] YAML frontmatter includes:
  - [ ] `name: cdocs:activate`
  - [ ] `description: Activate compounding docs for current project`
  - [ ] `allowed-tools: [Bash]` (for Git detection commands)
  - [ ] `preconditions: [.csharp-compounding-docs/config.json exists]`
  - [ ] `auto-invoke.trigger: project-entry`
- [ ] Skill body contains clear, step-by-step instructions for Claude

### Auto-Invoke Behavior

- [ ] Skill auto-invokes when Claude enters a project directory with `.csharp-compounding-docs/config.json`
- [ ] Auto-invoke detection uses project-entry trigger pattern
- [ ] Skill does NOT auto-invoke if config.json is absent
- [ ] Manual invocation via `/cdocs:activate` always works

### Git Detection

- [ ] Repository root detected via `git rev-parse --show-toplevel`
- [ ] Branch name detected via `git branch --show-current` (Git 2.22+)
- [ ] Fallback to `git symbolic-ref -q --short HEAD` for older Git versions
- [ ] Detached HEAD state handled gracefully (log warning, use commit hash as identifier)
- [ ] Non-Git directories produce clear error message
- [ ] Paths with spaces and special characters handled correctly

### MCP Tool Invocation

- [ ] `activate_project` MCP tool called with:
  - [ ] `config_path`: Absolute path to `.csharp-compounding-docs/config.json`
  - [ ] `branch_name`: Current Git branch name
- [ ] Tool response parsed for status verification
- [ ] Activation errors reported clearly to user
- [ ] `list_doc_types` called after successful activation
- [ ] Doc-type list extracted from response

### CLAUDE.md Update

- [ ] Project's `CLAUDE.md` file located (create if missing with warning)
- [ ] "Compounding Docs" section appended/updated in CLAUDE.md
- [ ] Section includes:
  - [ ] Project activation status
  - [ ] Table of available doc-types with descriptions
  - [ ] Instructions for capturing documentation
  - [ ] Instructions for querying documentation
- [ ] Existing "Compounding Docs" section replaced (not duplicated)
- [ ] File write uses proper line endings for platform

### User Feedback

- [ ] Activation success message displayed with:
  - [ ] Project name
  - [ ] Branch name
  - [ ] Number of existing documents
  - [ ] List of available doc-types
- [ ] Activation failure displays actionable error message
- [ ] Skill completion message indicates CLAUDE.md was updated

---

## Implementation Notes

### SKILL.md Content

Create `plugins/csharp-compounding-docs/skills/cdocs-activate/SKILL.md`:

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

### Skill Instructions Body

The skill body (after YAML frontmatter) should contain:

```markdown
# /cdocs:activate - Activate Compounding Docs

Activate the CSharp Compounding Docs system for the current project.

## Activation Steps

### 1. Detect Git Repository Information

Use Bash to get the repository root and current branch:

```bash
# Get repository root (absolute path)
git rev-parse --show-toplevel
```

```bash
# Get current branch name
git branch --show-current
```

**Fallback for older Git versions:**
```bash
git symbolic-ref -q --short HEAD
```

**Handle detached HEAD:** If branch detection returns empty, log a warning and use the commit hash:
```bash
git rev-parse --short HEAD
```

### 2. Compute Configuration Path

Construct the absolute path to the config file:
- `config_path` = `{repo_root}/.csharp-compounding-docs/config.json`

Verify the config file exists before proceeding.

### 3. Call MCP activate_project Tool

Invoke the `activate_project` MCP tool with:

```json
{
  "config_path": "/absolute/path/to/.csharp-compounding-docs/config.json",
  "branch_name": "current-branch-name"
}
```

Parse the response to verify activation succeeded.

### 4. List Available Doc-Types

Call the `list_doc_types` MCP tool (no parameters required).

Extract the list of available doc-types from the response.

### 5. Update Project CLAUDE.md

Read the project's `CLAUDE.md` file (at repo root). If it doesn't exist, create it with a warning.

Find and replace any existing "## Compounding Docs" section, or append a new section:

```markdown
## Compounding Docs

This project uses CSharp Compounding Docs for knowledge management.

### Available Doc-Types

| Type | Description | Command |
|------|-------------|---------|
| problem | Problems and solutions | `/cdocs:problem` |
| insight | Product/project insights | `/cdocs:insight` |
| codebase | Architecture decisions | `/cdocs:codebase` |
| tool | Library/tool knowledge | `/cdocs:tool` |
| style | Coding conventions | `/cdocs:style` |

### Capture Documentation

To capture new documentation, use one of the capture skills:
- `/cdocs:problem` - Document a solved problem
- `/cdocs:insight` - Capture a product insight
- `/cdocs:codebase` - Record architecture decisions
- `/cdocs:tool` - Document tool/library knowledge
- `/cdocs:style` - Capture coding conventions

### Query Documentation

To search and retrieve documentation:
- `/cdocs:query <question>` - RAG-powered Q&A
- `/cdocs:search <query>` - Semantic search
```

### 6. Report Activation Status

Display a summary to the user:

```
CSharp Compounding Docs activated!

Project: {project_name}
Branch: {branch_name}
Documents: {total_docs}

Available doc-types:
- problem (12 docs) - Problems and solutions
- insight (5 docs) - Product/project insights
- codebase (8 docs) - Architecture decisions
- tool (3 docs) - Library/tool knowledge
- style (2 docs) - Coding conventions

CLAUDE.md updated with available commands.
```

## Error Handling

| Error | Response |
|-------|----------|
| Not a Git repository | "Error: Not a Git repository. Compounding Docs requires a Git repository." |
| Config file missing | "Error: .csharp-compounding-docs/config.json not found. Run project initialization first." |
| MCP tool unavailable | "Error: MCP server not responding. Check that the compounding docs server is running." |
| Activation failed | Display error message from MCP response |
| CLAUDE.md write failed | "Warning: Could not update CLAUDE.md. Available commands displayed above." |

## Edge Cases

- **Detached HEAD**: Use commit hash, warn user that branch-specific docs may not work correctly
- **Git worktree**: Works correctly, each worktree can activate independently
- **Paths with spaces**: All paths properly quoted in Bash commands
- **Multiple activations**: Re-activation updates state, no error
```

### Directory Structure

```
plugins/csharp-compounding-docs/
└── skills/
    └── cdocs-activate/
        └── SKILL.md
```

### Git Detection Commands Reference

| Operation | Command | Fallback | Notes |
|-----------|---------|----------|-------|
| Repository root | `git rev-parse --show-toplevel` | None | Returns absolute path |
| Current branch | `git branch --show-current` | `git symbolic-ref -q --short HEAD` | Empty on detached HEAD |
| Commit hash | `git rev-parse --short HEAD` | None | 7-char hash for detached HEAD |
| Is Git repo | `git rev-parse --git-dir 2>/dev/null` | None | Exit code indicates success |

### CLAUDE.md Section Template

The "Compounding Docs" section to be inserted/updated:

```markdown
## Compounding Docs

This project uses [CSharp Compounding Docs](https://github.com/username/csharp-compound-engineering) for knowledge management.

**Status**: Activated on branch `{branch_name}`

### Available Doc-Types

| Type | Count | Description | Capture Command |
|------|-------|-------------|-----------------|
{doc_type_rows}

### Quick Reference

**Capture documentation:**
- `/cdocs:problem` - Document solved problems
- `/cdocs:insight` - Capture product insights
- `/cdocs:codebase` - Record architecture decisions
- `/cdocs:tool` - Document library/tool knowledge
- `/cdocs:style` - Capture coding conventions

**Query documentation:**
- `/cdocs:query <question>` - Get RAG-synthesized answers
- `/cdocs:search <query>` - Semantic search for documents

**Manage documentation:**
- `/cdocs:promote <path> <level>` - Change document visibility
- `/cdocs:delete` - Remove documents by criteria
```

### Section Update Logic

When updating CLAUDE.md:

1. Read entire file content
2. Search for `## Compounding Docs` header
3. If found:
   - Find end of section (next `## ` header or EOF)
   - Replace entire section with new content
4. If not found:
   - Append section at end of file
5. Write updated content back to file

**Regex pattern for section detection:**
```
^## Compounding Docs\n[\s\S]*?(?=^## |\Z)
```

### Activation Response Parsing

Expected MCP `activate_project` response:

```json
{
  "status": "activated",
  "project_name": "my-project",
  "branch_name": "feature/new-feature",
  "path_hash": "a1b2c3d4",
  "doc_types": [
    { "name": "problem", "doc_count": 12 },
    { "name": "insight", "doc_count": 5 },
    { "name": "codebase", "doc_count": 8 },
    { "name": "tool", "doc_count": 3 },
    { "name": "style", "doc_count": 2 }
  ],
  "total_docs": 30
}
```

### Error Response Handling

Expected MCP error response:

```json
{
  "error": true,
  "code": "CONFIG_NOT_FOUND",
  "message": "Configuration file not found at specified path",
  "details": { "path": "/path/to/config.json" }
}
```

---

## Dependencies

### Depends On

- **Phase 081**: Skills System Base Infrastructure (skill loading, YAML parsing)
- **Phase 079**: MCP Server Core (`activate_project` tool implementation)
- **Phase 074**: `list_doc_types` MCP Tool
- **Phase 039**: Git Branch Detection Service (Git detection patterns)
- **Phase 009**: Plugin Directory Structure (skill file location)

### Blocks

- **Phase 095+**: All capture skills require project to be activated first
- **Phase 096+**: All query skills require project to be activated first
- Custom doc-type creation requires activation

---

## Verification Steps

After completing this phase, verify:

1. **Skill File Structure**: `SKILL.md` is valid YAML frontmatter + markdown body
2. **Auto-Invoke**: Entering a project with config.json triggers activation
3. **Manual Invoke**: `/cdocs:activate` works in any project with config
4. **Git Detection**: Correct repo root and branch extracted
5. **MCP Integration**: `activate_project` called with correct parameters
6. **Doc-Types Listed**: `list_doc_types` response parsed correctly
7. **CLAUDE.md Updated**: Section added/updated without duplication

### Manual Verification

```bash
# 1. Navigate to a test project with config
cd /path/to/test-project

# 2. Verify config exists
ls -la .csharp-compounding-docs/config.json

# 3. Start Claude Code session (should auto-invoke activation)
claude

# 4. Verify activation message displayed

# 5. Check CLAUDE.md was updated
cat CLAUDE.md | grep -A 20 "## Compounding Docs"

# 6. Test manual re-activation
/cdocs:activate

# 7. Test in project without config (should show error)
cd /tmp
/cdocs:activate
# Expected: Error about missing config
```

### Integration Test Scenarios

| Scenario | Expected Behavior |
|----------|-------------------|
| Fresh project with config | Activation succeeds, CLAUDE.md created/updated |
| Re-activation | Updates state, no errors |
| Project without config | Clear error message |
| Non-Git directory | Error about Git requirement |
| Detached HEAD | Warning, uses commit hash |
| Branch switch then activate | New branch context established |
| Existing CLAUDE.md | Section updated, not duplicated |
| New CLAUDE.md | File created with section |

---

## Files to Create/Modify

### New Files

| File | Purpose |
|------|---------|
| `plugins/csharp-compounding-docs/skills/cdocs-activate/SKILL.md` | Skill definition with YAML frontmatter and instructions |

### Modified Files

| File | Changes |
|------|---------|
| `plugins/csharp-compounding-docs/.claude-plugin/plugin.json` | Verify `cdocs:activate` listed in components.skills |
| `plugins/csharp-compounding-docs/CLAUDE.md` | Document the activate skill |

---

## Notes

- The skill uses Bash tool for Git commands rather than a dedicated Git MCP tool to minimize dependencies
- Auto-invoke on project-entry means the skill runs when Claude first opens a session in a directory with config
- The CLAUDE.md update ensures future sessions have context about available doc-types
- Branch detection uses modern `git branch --show-current` with fallback for compatibility
- Detached HEAD is supported but with degraded functionality (branch-specific features may not work)
- The skill is idempotent - running multiple times produces consistent state
- Error messages are designed to be actionable with clear next steps
- The skill does not install or start the MCP server - that's handled by plugin hooks
