# Utility Skills

> **Status**: [DRAFT]
> **Parent**: [../skills.md](../skills.md)

---

## Overview

Utility skills provide management and maintenance capabilities for compounding documentation. They handle deletion, promotion, todos, worktree management, and research orchestration.

---

## `/cdocs:delete`

**Purpose**: Delete compounding docs data from the database by project, branch, or path.

**Invocation**: Manual - user wants to clean up orphaned or stale data.

**Behavior**:
1. Prompt user for deletion scope:
   - **Project only**: Delete all docs for a specific project (all branches)
   - **Project + Branch**: Delete docs for a specific project/branch combo
   - **Project + Path**: Delete docs for a specific repo path (worktree cleanup)
2. Show count of documents that will be deleted
3. Request confirmation before proceeding
4. Call MCP `delete_documents` tool with appropriate filters
5. Report deletion results

**MCP Tool**: `delete_documents`

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `project_name` | string | Yes | Project identifier |
| `branch_name` | string | No | Branch name (if omitted, deletes all branches) |
| `path_hash` | string | No | Path hash (if omitted, deletes all paths) |
| `dry_run` | boolean | No | If true, return counts without deleting (default: false) |

**Workflow**: Call with `dry_run=true` first to show counts, request user confirmation, then call with `dry_run=false` to delete.

**Response**:
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

**Use Cases**:
- Clean up after deleting a feature branch
- Remove stale data from deleted worktrees
- Reset a project's docs (rare)

**Safety**:
- Always show count before deleting
- Require explicit confirmation
- Never auto-invoke this skill

---

## `/cdocs:promote`

**Purpose**: Promote or demote a document's visibility level.

**Invocation**: Manual - user wants to increase/decrease a document's visibility.

**Behavior**:
1. If document path not provided, prompt user to search or provide path
2. Display current document metadata including current promotion level
3. Present promotion options:
   - Standard (default visibility)
   - Important (higher relevance boost)
   - Critical (required reading)
4. Confirm the change
5. Update both the file's YAML frontmatter and the database record
6. Call MCP `update_promotion_level` tool
7. Report success

**MCP Tool**: `update_promotion_level`

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `document_path` | string | Yes | Relative path to document |
| `promotion_level` | enum | Yes | `standard`, `important`, or `critical` |

**Response**:
```json
{
  "status": "updated",
  "document_path": "problems/n-plus-one-query-20250120.md",
  "previous_level": "standard",
  "new_level": "critical"
}
```

**Restrictions**:
- External docs cannot be promoted (read-only)
- Only documents in `./csharp-compounding-docs/` can be promoted

**Decision Menu After Promotion**:
```
Document promoted to [level]

File: ./csharp-compounding-docs/[path]

What's next?
1. Continue workflow
2. Promote another document
3. View document
4. Other
```

---

## `/cdocs:todo`

> **Origin**: Adapted from `file-todos` skill in [compound-engineering-plugin](https://github.com/anthropics/compound-engineering-plugin)
>
> **Background**: The file-based todo pattern and naming conventions are documented in detail. See [Compound Engineering Paradigm Research](../../research/compound-engineering-paradigm-research.md#file-based-state-management).

**Purpose**: File-based todo tracking where the filesystem is the database.

**Folder**: `./csharp-compounding-docs/todos/`

**File Naming Convention**:
```
./csharp-compounding-docs/todos/
+-- 001-pending-p1-security-vulnerability.md
+-- 002-pending-p2-performance-optimization.md
+-- 003-complete-p3-code-cleanup.md
```

Pattern: `{id}-{status}-{priority}-{description}.md`

| Component | Values |
|-----------|--------|
| `id` | Zero-padded sequential number (001, 002, ...) |
| `status` | `pending`, `in-progress`, `blocked`, `complete` |
| `priority` | `p1` (critical), `p2` (important), `p3` (nice-to-have) |
| `description` | Kebab-case summary |

**Behavior**:
1. **Create**: Generate new todo file with next available ID
2. **List**: Show all todos, optionally filtered by status/priority
3. **Update**: Change status (renames file to reflect new state)
4. **Complete**: Move to `complete` status
5. **Archive**: Move completed todos to `./csharp-compounding-docs/todos/archive/`

**Todo File Structure**:
```yaml
---
id: 001
status: pending
priority: p1
created: 2025-01-23
source: code-review  # or: manual, agent-finding, bug-report
related_docs:
  - ../problems/null-reference-20250123.md
---

# Security Vulnerability in Auth Module

## Description
[Detailed description of the todo item]

## Acceptance Criteria
- [ ] Criterion 1
- [ ] Criterion 2

## Notes
[Additional context, links, etc.]
```

**Key Characteristics**:
- No database - files ARE the state
- Status visible in filename (git-friendly)
- Human-readable and git-trackable
- Integrates with compound docs via `related_docs`

---

## `/cdocs:worktree`

> **Origin**: Adapted from `git-worktree` skill in [compound-engineering-plugin](https://github.com/anthropics/compound-engineering-plugin)
>
> **Background**: Git worktrees enable parallel development across branches without stashing. See [Compound Engineering Paradigm Research](../../research/compound-engineering-paradigm-research.md#skills-14) for the original skill design.

**Purpose**: Manage Git worktrees for parallel development.

**What are Git Worktrees?**: Multiple working directories attached to the same repository, each checked out to a different branch. Work on multiple branches simultaneously without stashing.

**Behavior**:

| Command | Action |
|---------|--------|
| `/cdocs:worktree create <branch>` | Create new worktree for branch |
| `/cdocs:worktree list` | List all worktrees |
| `/cdocs:worktree switch <name>` | Switch context to worktree |
| `/cdocs:worktree remove <name>` | Remove worktree (with safety checks) |
| `/cdocs:worktree status` | Show status of all worktrees |

**Worktree Directory Structure**:
```
my-project/                        # main branch (primary worktree)
my-project-worktrees/
+-- feature-auth/                  # feature/auth branch
|   +-- .csharp-compounding-docs/  # Shared via symlink or config
+-- bugfix-login/                  # bugfix/login branch
+-- release-2.0/                   # release/2.0 branch
```

**Compound Docs Integration**:
- Worktrees share the same compound docs (via `branch_name` in activation)
- Each worktree can have branch-specific docs
- `/cdocs:activate` detects worktree context automatically

**Use Cases**:
- Work on feature while fixing urgent bug on another branch
- Run tests on one branch while developing on another
- Code review a PR while continuing your own work
- Compare behavior across multiple branches

**Safety Features**:
- Warns before removing worktree with uncommitted changes
- Validates branch doesn't already have a worktree
- Suggests cleanup for stale worktrees

**MCP Integration**: None required. This skill uses only git CLI commands (`git worktree add`, `git worktree list`, etc.) via Bash. The `/cdocs:activate` skill handles MCP activation when switching worktree contexts.

---

## `/cdocs:research`

> **Origin**: Adapted from `/workflows:review` in [compound-engineering-plugin](https://github.com/anthropics/compound-engineering-plugin)
>
> **Background**: The multi-agent research pattern with parallel agent execution is detailed in the paradigm research. See [Compound Engineering Paradigm Research](../../research/compound-engineering-paradigm-research.md#command-implementation-workflowsplan).

**Purpose**: Orchestrate research agents with knowledge-base context and capture significant findings.

**Invocation**: Manual - user wants research with compounding docs integration.

**MCP Integration**:
- **Sequential Thinking**: Research planning, cross-agent finding synthesis, and capture routing decisions

> **Note**: The Sequential Thinking MCP uses the package `@modelcontextprotocol/server-sequential-thinking` (not `@anthropics/sequential-thinking-mcp`). See [Sequential Thinking MCP Verification](../../research/sequential-thinking-mcp-verification.md) for configuration details.

**Behavior**:
1. **Research Planning** (via Sequential Thinking):
   - Analyze the research question
   - Determine which agents are most relevant
   - Plan execution order if dependencies exist

3. **Pre-Research Context Loading**:
   - Query RAG for relevant documented knowledge (patterns, past issues)
   - Load critical-level documents related to the research scope
   - Present context summary

4. **Research Agent Orchestration**:
   - Launch selected research agents to gather context
   - Agents: best-practices-researcher, framework-docs-researcher, git-history-analyzer, repo-research-analyst
   - Each agent receives knowledge-base context and augments with compound docs

5. **Finding Synthesis** (via Sequential Thinking):
   - Collect findings from all agents
   - De-duplicate overlapping findings
   - Reconcile conflicting recommendations
   - Categorize by relevance and actionability
   - Synthesize into coherent recommendations

6. **Capture Routing** (via Sequential Thinking):
   - For significant findings, determine appropriate doc-type
   - Offer to capture via `/cdocs:problem`, `/cdocs:codebase`, `/cdocs:tool`, etc.
   - Link findings to existing related docs

**Agent Selection Menu**:
```
Research-Informed Review

Select research agents to run (space to toggle, enter to confirm):

Research Agents:
[x] best-practices-researcher - External best practices with compound docs
[x] framework-docs-researcher - Framework documentation + internal context
[ ] git-history-analyzer - Code evolution analysis
[ ] repo-research-analyst - Repository conventions

Preset:
1. Quick Research (best practices + framework docs)
2. Full Research (all agents)
3. Historical Focus (git + repo analysis)
4. Custom selection
```

**Post-Review Menu**:
```
Research Complete

Findings Summary:
- Best Practices: 3 recommendations
- Framework Guidance: 2 relevant patterns
- Related Internal Docs: 4 documents

What's next?
1. View detailed findings
2. Capture findings as documentation
3. Apply recommendations
4. Run additional research
5. Done
```

---

## Related Documentation

- [Capture Skills](./capture-skills.md) - Create documentation for specific doc-types
- [Query Skills](./query-skills.md) - Search and retrieve documentation
- [Meta Skills](./meta-skills.md) - Create custom doc-types and handle conflicts
