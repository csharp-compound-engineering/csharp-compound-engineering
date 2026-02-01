# Phase 097: /cdocs:delete Utility Skill

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Skills System
> **Prerequisites**: Phase 081 (Utility Skills Foundation), Phase 077 (MCP Tool Integration)

---

## Spec References

This phase implements the `/cdocs:delete` skill defined in:

- **spec/skills/utility-skills.md** - [/cdocs:delete section](../spec/skills/utility-skills.md#cdocsdelete) - Skill behavior, scope options, safety requirements
- **spec/mcp-server/tools.md** - [7. Delete Documents Tool](../spec/mcp-server/tools.md#7-delete-documents-tool) - `delete_documents` MCP tool parameters and response format
- **spec/skills/skill-patterns.md** - SKILL.md template structure and common patterns

---

## Objectives

1. Create the `/cdocs:delete` SKILL.md file with complete workflow definition
2. Implement scope selection (project-only, project+branch, project+path)
3. Define dry-run workflow for safe deletion preview
4. Implement user confirmation requirement before destructive operations
5. Integrate with `delete_documents` MCP tool
6. Provide clear deletion feedback with counts

---

## Acceptance Criteria

### SKILL.md Structure

- [ ] SKILL.md file created at `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-delete/SKILL.md`
- [ ] YAML frontmatter with correct metadata:
  - [ ] `name: cdocs:delete`
  - [ ] `description` - Clear description of deletion capability
  - [ ] `allowed-tools` - Includes `Read`, `Bash` (git operations)
  - [ ] `preconditions` - Project activated via /cdocs:activate
  - [ ] No auto-invoke triggers (manual-only skill)
- [ ] Skill marked as manual-only (no `auto-invoke` section)

### Scope Selection Workflow

- [ ] Step 1: Scope Selection prompts user for deletion scope:
  - [ ] **Project only**: Delete all docs for the active project (all branches)
  - [ ] **Project + Branch**: Delete docs for a specific project/branch combination
  - [ ] **Project + Path**: Delete docs for a specific worktree path
- [ ] Clear explanation of what each scope means
- [ ] Default to most conservative option (project + branch)

### Dry-Run Safety Workflow

- [ ] Step 2: Preview Deletion calls `delete_documents` with `dry_run=true`
- [ ] Display deletion preview showing:
  - [ ] Number of documents that will be deleted
  - [ ] Number of chunks that will be deleted
  - [ ] Scope parameters (project_name, branch_name, path_hash)
- [ ] Clear warning that this is a PREVIEW only
- [ ] No data modified during preview step

### User Confirmation Requirement

- [ ] Step 3: Explicit Confirmation required before proceeding
- [ ] Confirmation prompt includes:
  - [ ] Summary of what will be deleted
  - [ ] Explicit yes/no question
  - [ ] Option to abort the operation
- [ ] BLOCKING: Must receive explicit "yes" or confirmation before proceeding
- [ ] Any response other than explicit confirmation aborts the operation

### MCP Tool Invocation

- [ ] Step 4: Execute Deletion calls `delete_documents` with `dry_run=false`
- [ ] Parameters passed correctly:
  - [ ] `project_name` - Required, from active project context
  - [ ] `branch_name` - Optional, based on scope selection
  - [ ] `path_hash` - Optional, based on scope selection
  - [ ] `dry_run` - `false` for actual deletion
- [ ] Handle MCP tool errors gracefully

### Deletion Feedback

- [ ] Step 5: Report Results displays deletion summary:
  - [ ] `status` - Confirms deletion completed
  - [ ] `deleted_count` - Number of documents deleted
  - [ ] `deleted_chunks` - Number of chunks deleted
  - [ ] Scope parameters that were used
- [ ] Decision menu after deletion:
  - [ ] Continue workflow
  - [ ] Delete more data (different scope)
  - [ ] Other

### Safety Requirements

- [ ] Skill NEVER auto-invokes (manual-only)
- [ ] Always show count before deleting (dry_run=true first)
- [ ] Always require explicit confirmation
- [ ] Clear warning language about destructive operation
- [ ] Document that deleted data cannot be recovered (no soft-delete)

---

## Implementation Notes

### SKILL.md Content

```yaml
---
name: cdocs:delete
description: Delete compounding docs data from the database by project, branch, or path. Always performs dry-run preview before actual deletion.
allowed-tools:
  - Read
  - Bash
preconditions:
  - Project activated via /cdocs:activate
# NO auto-invoke section - this is a manual-only skill
---

# Delete Documentation Skill

## Purpose

Remove compounding documentation data from the vector database. Use this skill to clean up:
- Orphaned data from deleted feature branches
- Stale data from removed worktrees
- Complete project reset (rare)

**WARNING**: This operation is destructive and cannot be undone. Always verify the preview before confirming deletion.

## Process

### Step 1: Select Deletion Scope

Determine what data to delete:

| Scope | Description | Use Case |
|-------|-------------|----------|
| **Project Only** | All docs for this project (all branches) | Complete project reset |
| **Project + Branch** | Docs for a specific branch | Clean up merged/deleted branch |
| **Project + Path** | Docs for a specific worktree path | Worktree cleanup |

Ask the user which scope they want:

```
What would you like to delete?

1. All documentation for this project (all branches)
2. Documentation for a specific branch
3. Documentation for a specific path (worktree)

Select an option (1-3):
```

If option 2: Ask for the branch name
If option 3: Ask for the path or path_hash

**BLOCKING**: Wait for user's scope selection before proceeding.

### Step 2: Preview Deletion (Dry Run)

Call the `delete_documents` MCP tool with `dry_run=true`:

```json
{
  "project_name": "<active_project_name>",
  "branch_name": "<selected_branch_or_null>",
  "path_hash": "<selected_path_hash_or_null>",
  "dry_run": true
}
```

Display the preview:

```
DELETION PREVIEW

This operation will delete:
- Documents: {deleted_count}
- Chunks: {deleted_chunks}

Scope:
- Project: {project_name}
- Branch: {branch_name or "ALL BRANCHES"}
- Path: {path_hash or "ALL PATHS"}

⚠️  This is a PREVIEW. No data has been modified yet.
```

If `deleted_count` is 0, inform the user and ask if they want to try a different scope.

### Step 3: Confirm Deletion

**CRITICAL**: Require explicit confirmation before proceeding.

```
⚠️  WARNING: This operation cannot be undone!

You are about to permanently delete:
- {deleted_count} documents
- {deleted_chunks} chunks

Type "DELETE" to confirm, or anything else to abort:
```

**BLOCKING**:
- If user types exactly "DELETE" (case-insensitive), proceed to Step 4
- Any other response aborts the operation
- Show abort message: "Deletion aborted. No data was modified."

### Step 4: Execute Deletion

Call the `delete_documents` MCP tool with `dry_run=false`:

```json
{
  "project_name": "<active_project_name>",
  "branch_name": "<selected_branch_or_null>",
  "path_hash": "<selected_path_hash_or_null>",
  "dry_run": false
}
```

### Step 5: Report Results

Display the deletion summary:

```
✓ Deletion Complete

Deleted:
- Documents: {deleted_count}
- Chunks: {deleted_chunks}

Scope:
- Project: {project_name}
- Branch: {branch_name or "all branches"}
- Path: {path_hash or "all paths"}
```

### Post-Deletion Options

```
What's next?
1. Continue workflow
2. Delete more data (different scope)
3. Other
```

## Error Handling

| Error | Response |
|-------|----------|
| `PROJECT_NOT_ACTIVATED` | Instruct user to run `/cdocs:activate` first |
| `DATABASE_ERROR` | Show error message, suggest retry |
| Zero documents found | Suggest different scope options |

## Safety Reminders

1. **Always** preview before deleting (dry_run=true first)
2. **Always** require explicit "DELETE" confirmation
3. **Never** auto-invoke this skill
4. Deleted data **cannot** be recovered
```

### MCP Tool Parameters Reference

From spec/mcp-server/tools.md:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `project_name` | string | Yes | Project identifier |
| `branch_name` | string | No | Branch name (if omitted, deletes all branches) |
| `path_hash` | string | No | Path hash (if omitted, deletes all paths) |
| `dry_run` | boolean | No | If true, return counts without deleting (default: false) |

### Response Format

Dry-run response (preview):
```json
{
  "status": "preview",
  "deleted_count": 34,
  "deleted_chunks": 128,
  "project_name": "my-project",
  "branch_name": "feature/old-branch",
  "path_hash": null,
  "dry_run": true
}
```

Actual deletion response:
```json
{
  "status": "deleted",
  "deleted_count": 34,
  "deleted_chunks": 128,
  "project_name": "my-project",
  "branch_name": "feature/old-branch",
  "path_hash": null,
  "dry_run": false
}
```

---

## Dependencies

### Depends On
- Phase 081: Utility Skills Foundation (skill infrastructure, common patterns)
- Phase 077: MCP Tool Integration (tool invocation from skills)
- Phase 013: Cleanup Deletion (underlying deletion logic)
- Phase 038: Tenant Context (project activation state)

### Blocks
- None (terminal skill, no dependencies on it)

---

## Verification Steps

After completing this phase, verify:

1. **SKILL.md Location**: File exists at `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-delete/SKILL.md`
2. **No Auto-Invoke**: Skill has no `auto-invoke` section in frontmatter
3. **Scope Options**: All three scope options are clearly documented
4. **Dry-Run First**: Workflow requires dry_run=true before any deletion
5. **Explicit Confirmation**: Confirmation step requires typing "DELETE"
6. **MCP Integration**: `delete_documents` tool is correctly invoked
7. **Feedback**: Deletion results clearly display counts and scope

### Manual Verification

1. Invoke `/cdocs:delete` manually
2. Select "Project + Branch" scope
3. Verify preview shows correct counts
4. Type something other than "DELETE" - verify abort
5. Re-run, type "DELETE" - verify deletion occurs
6. Verify deleted_count matches preview

### Test Scenarios

| Scenario | Expected Behavior |
|----------|-------------------|
| No project activated | Returns PROJECT_NOT_ACTIVATED error |
| Zero documents in scope | Shows "0 documents found", suggests different scope |
| User aborts at confirmation | "Deletion aborted" message, no data modified |
| User confirms deletion | Documents deleted, counts shown |
| Database error during deletion | Error message, data state preserved |

---

## Files to Create/Modify

### New Files

| File | Purpose |
|------|---------|
| `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-delete/SKILL.md` | Main skill definition |

### Modified Files

None - this is a standalone skill that uses existing MCP tool infrastructure.

---

## Notes

- This skill is intentionally verbose with safety warnings due to destructive nature
- The "DELETE" confirmation requirement prevents accidental data loss
- Dry-run is always performed first, never skipped
- Unlike other skills, this one has NO auto-invoke capability
- Consider future enhancement: soft-delete with TTL recovery window (noted in spec)
- The skill assumes the `delete_documents` MCP tool handles the actual database operations
- Path deletion scope is useful for worktree cleanup scenarios
