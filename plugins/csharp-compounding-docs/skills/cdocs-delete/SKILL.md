---
name: cdocs:delete
description: Delete compounding docs data from database by project, branch, or path with dry-run safety workflow
allowed-tools:
  - Bash
preconditions:
  - Project activated via /cdocs:activate
---

# Delete Documentation Skill

## Purpose

Delete compounding docs data from the database by project, branch, or path. This skill provides a safe dry-run workflow to preview deletions before committing changes.

## Invocation

**Manual only** - User wants to clean up orphaned or stale documentation data.

**Use Cases**:
- Clean up after deleting a feature branch
- Remove stale data from deleted worktrees
- Reset a project's docs (rare)

## Process

### Step 1: Determine Deletion Scope

Prompt user for deletion scope:

**Scope Options**:
1. **Project only**: Delete all docs for a specific project (all branches)
2. **Project + Branch**: Delete docs for a specific project/branch combo
3. **Project + Path**: Delete docs for a specific repo path (worktree cleanup)

**Required Information**:
- Project name (required)
- Branch name (optional - if omitted, deletes all branches)
- Path hash (optional - if omitted, deletes all paths)

### Step 2: Dry-Run Preview

Call MCP `delete_documents` tool with `dry_run=true` to show counts without deleting.

**Parameters**:
- `project_name`: string (required) - Project identifier
- `branch_name`: string (optional) - Branch name
- `path_hash`: string (optional) - Path hash
- `dry_run`: boolean - Set to `true` for preview

**Preview Response**:
```json
{
  "status": "dry_run",
  "documents_to_delete": 34,
  "chunks_to_delete": 128,
  "project_name": "my-project",
  "branch_name": "feature/old-branch",
  "path_hash": null
}
```

Display:
```
Deletion Preview
================

This will delete:
- Documents: 34
- Chunks: 128

Scope:
- Project: my-project
- Branch: feature/old-branch
- Path: all paths

Continue with deletion? (y/N)
```

### Step 3: Confirm Deletion

**BLOCKING**: Wait for explicit user confirmation before proceeding.

- If user confirms (y/yes), proceed to Step 4
- If user cancels (n/no/any other input), abort and report cancellation
- **NEVER auto-confirm** - always require explicit user action

### Step 4: Execute Deletion

Call MCP `delete_documents` tool with `dry_run=false` to perform actual deletion.

**Deletion Response**:
```json
{
  "status": "deleted",
  "deleted_count": 34,
  "deleted_chunks": 128,
  "project_name": "my-project",
  "branch_name": "feature/old-branch",
  "path_hash": null
}
```

### Step 5: Report Results

Display deletion results:
```
Deletion Complete
=================

Successfully deleted:
- Documents: 34
- Chunks: 128

Scope:
- Project: my-project
- Branch: feature/old-branch
```

## Safety Features

**Critical Requirements**:
- Always show count before deleting
- Require explicit confirmation
- Never auto-invoke this skill
- Use dry-run workflow for all deletions

**Warning Messages**:
- If deleting entire project: "⚠️ WARNING: This will delete ALL documentation for this project across all branches."
- If deleting all paths: "⚠️ WARNING: This will delete documentation from all worktrees."

## MCP Tool Reference

**Tool**: `delete_documents`

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `project_name` | string | Yes | Project identifier |
| `branch_name` | string | No | Branch name (if omitted, deletes all branches) |
| `path_hash` | string | No | Path hash (if omitted, deletes all paths) |
| `dry_run` | boolean | No | If true, return counts without deleting (default: false) |

## Example Workflow

```
User: /cdocs:delete