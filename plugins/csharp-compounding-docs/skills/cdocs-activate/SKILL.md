---
name: cdocs:activate
description: Activate compounding docs system for current project
allowed-tools:
  - Bash  # Git root and branch detection
  - Read
  - Write
preconditions:
  - .csharp-compounding-docs/config.json exists
auto-invoke:
  trigger: project-entry
---

# Compounding Docs Activation Skill

## Purpose

Activate the compounding docs system for the current project, establishing the tenant context for all subsequent documentation operations.

## Intake

This skill auto-invokes when Claude Code enters a project with `.csharp-compounding-docs/config.json` present.

Manual invocation: `/cdocs:activate`

## Process

### Step 1: Detect Project Root

Use Bash to find the git repository root:

```bash
git rev-parse --show-toplevel
```

**BLOCKING**: If not in a git repository, inform user and exit.

### Step 2: Verify Configuration

Check for configuration file at `{repo_root}/.csharp-compounding-docs/config.json`.

**BLOCKING**: If config file not found, inform user that compounding docs is not configured for this project and exit.

### Step 3: Detect Current Branch

Use Bash to get current git branch:

```bash
git branch --show-current
```

**Handle edge cases**:
- Detached HEAD: Use commit SHA with warning
- No commits: Use "main" as default
- Command fails: Prompt user for branch name

### Step 4: Compute Absolute Path

Construct absolute path to config file:
- Path: `{repo_root}/.csharp-compounding-docs/config.json`

### Step 5: Call MCP Activation

Call the MCP `activate_project` tool with:
- `config_path`: Absolute path to config.json
- `branch_name`: Current git branch name

**BLOCKING**: If activation fails, display error message from MCP and exit.

### Step 6: List Available Doc-Types

Call MCP `list_doc_types` tool to retrieve:
- Built-in doc-types
- Custom doc-types for this project
- Their descriptions and trigger phrases

### Step 7: Update CLAUDE.md

Read existing `CLAUDE.md` (or create if not exists).

Append or update a "Compounding Docs" section:

```markdown
## Compounding Docs

This project uses the compounding docs system for documentation capture.

### Available Doc-Types

- **problem** - Solved problems with symptoms, root cause, and solution
- **tool** - Library/package knowledge, API usage, configuration
- **codebase** - Architecture, design decisions, patterns
- **[custom-type]** - [Custom description]

### Available Skills

- `/cdocs:activate` - Activate docs system (auto-invokes on project entry)
- `/cdocs:problem` - Capture problem documentation
- `/cdocs:tool` - Capture tool/library documentation
- `/cdocs:codebase` - Capture architecture/design documentation
- `/cdocs:[custom]` - Capture custom doc-type
- `/cdocs:query` - Query documentation with RAG
- `/cdocs:search` - Search documentation semantically
- `/cdocs:create-type` - Create new custom doc-type
- `/cdocs:capture-select` - Select doc-type when multiple triggers detected
```

Use the Write tool to save the updated CLAUDE.md.

### Step 8: Report Activation Status

Display confirmation to user:

```
✓ Compounding Docs activated

Project: {project_name}
Branch: {branch_name}
Tenant Key: {tenant_key}

Available doc-types: {count}
- problem
- tool
- codebase
- [custom types...]

You can now:
- Capture documentation with /cdocs:capture-* skills
- Query documentation with /cdocs:query
- Search documentation with /cdocs:search
- Create custom doc-types with /cdocs:create-type
```

## Schema Reference

This skill does not use a schema - it operates on the MCP activation mechanism.

## Examples

### Example 1: Successful Activation

```
✓ Compounding Docs activated

Project: my-dotnet-app
Branch: feature/new-api
Tenant Key: my-dotnet-app:feature/new-api

Available doc-types: 4
- problem - Solved problems with symptoms and solutions
- tool - Library/package knowledge
- codebase - Architecture decisions
- api-contract - API design decisions (custom)

You can now:
- Capture documentation with /cdocs:problem, /cdocs:tool, etc.
- Query documentation with /cdocs:query
- Search documentation with /cdocs:search
```

### Example 2: Detached HEAD

```
⚠ Warning: Detached HEAD state detected

Using commit SHA as branch identifier: abc123def
You may want to check out a branch for better organization.

✓ Compounding Docs activated
...
```

### Example 3: Missing Config

```
✗ Compounding Docs not configured

No .csharp-compounding-docs/config.json found in this project.

To set up compounding docs:
1. Initialize the configuration
2. Define your doc-types
3. Run /cdocs:activate again
```

## Notes

- This skill must execute before any capture/query skills can function
- The MCP server uses the tenant key (project:branch) to isolate documentation between projects and branches
- CLAUDE.md updates ensure future sessions know about available doc-types
- Auto-invocation happens once per session when entering a configured project
