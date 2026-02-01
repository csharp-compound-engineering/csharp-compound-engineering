---
name: cdocs:todo
description: File-based todo tracking where the filesystem is the database, with status and priority encoded in filenames
allowed-tools:
  - Read
  - Write
  - Bash
  - Glob
preconditions:
  - Project activated via /cdocs:activate
---

# Todo Management Skill

## Purpose

File-based todo tracking where the filesystem is the database. Status and priority are encoded in filenames, making them git-trackable and human-readable.

## Invocation

**Manual** - User wants to manage todos for the project.

**Commands**:
- `/cdocs:todo create` - Create new todo
- `/cdocs:todo list` - List todos (optionally filtered)
- `/cdocs:todo update <id>` - Update todo status or priority
- `/cdocs:todo complete <id>` - Mark todo as complete
- `/cdocs:todo archive` - Archive completed todos

## File Structure

**Todo Directory**: `./csharp-compounding-docs/todos/`

**Filename Pattern**: `{id}-{status}-{priority}-{description}.md`

**Example**:
```
./csharp-compounding-docs/todos/
├── 001-pending-p1-security-vulnerability.md
├── 002-in-progress-p2-performance-optimization.md
├── 003-blocked-p1-database-migration.md
├── 004-complete-p3-code-cleanup.md
└── archive/
    └── 003-complete-p2-refactor-auth-module.md
```

**Filename Components**:

| Component | Values | Description |
|-----------|--------|-------------|
| `id` | 001, 002, ... | Zero-padded sequential number |
| `status` | `pending`, `in-progress`, `blocked`, `complete` | Current state |
| `priority` | `p1`, `p2`, `p3` | Priority level |
| `description` | kebab-case | Short summary |

**Priority Levels**:
- `p1` - Critical (urgent, blocking)
- `p2` - Important (high priority, not blocking)
- `p3` - Nice-to-have (low priority)

## Todo File Format

```yaml
---
id: 001
status: pending
priority: p1
created: 2025-01-23
updated: 2025-01-23
source: code-review
assignee: null
related_docs:
  - ../problems/null-reference-20250123.md
tags:
  - security
  - authentication
---

# Security Vulnerability in Auth Module

## Description

Discovered potential SQL injection vulnerability in authentication module.
User input is not properly sanitized before database queries.

## Acceptance Criteria

- [ ] Review all SQL queries in AuthService
- [ ] Implement parameterized queries
- [ ] Add input validation tests
- [ ] Security audit approval

## Context

Found during code review of PR #123. Related to recent authentication
refactoring work.

## Notes

- Blocking production release
- Coordinate with security team
- Reference: OWASP SQL Injection Guide
```

## Process

### Create Todo

**Command**: `/cdocs:todo create`

**Steps**:

1. **Prompt for Details**:
   - Title/description (required)
   - Priority (p1/p2/p3, default: p2)
   - Status (default: pending)
   - Source (code-review, manual, agent-finding, bug-report)
   - Related docs (optional)
   - Tags (optional)

2. **Generate Next ID**:
   - Scan todos directory for existing files
   - Find highest ID number
   - Increment by 1, zero-pad to 3 digits

3. **Create Filename**:
   - Pattern: `{id}-{status}-{priority}-{description}.md`
   - Description: Sanitize title to kebab-case, max 50 chars
   - Example: `005-pending-p1-fix-memory-leak.md`

4. **Write Todo File**:
   - Create `./csharp-compounding-docs/todos/` if not exists
   - Write file with YAML frontmatter + markdown body
   - Include acceptance criteria template

5. **Confirm Creation**:
   ```
   Todo Created
   ============

   ID: 005
   File: ./csharp-compounding-docs/todos/005-pending-p1-fix-memory-leak.md
   Status: pending
   Priority: p1 (critical)

   Next steps:
   1. Continue working
   2. Update status
   3. View all todos
   ```

### List Todos

**Command**: `/cdocs:todo list [--status=<status>] [--priority=<priority>]`

**Steps**:

1. **Scan Todos Directory**:
   - Use Glob to find all `*.md` files in `./csharp-compounding-docs/todos/`
   - Parse filenames to extract id, status, priority, description

2. **Apply Filters**:
   - If `--status` provided, filter by status
   - If `--priority` provided, filter by priority
   - Default: Show all except archived

3. **Group and Sort**:
   - Group by status (pending, in-progress, blocked, complete)
   - Within groups, sort by priority (p1 → p2 → p3)
   - Within priority, sort by ID

4. **Display List**:
   ```
   Todos
   =====

   Pending (2)
   -----------
   [001] [p1] Security vulnerability in auth module
   [002] [p2] Performance optimization for API endpoints

   In Progress (1)
   ---------------
   [003] [p1] Database migration to PostgreSQL

   Blocked (1)
   -----------
   [004] [p2] Refactor payment processing (blocked on API keys)

   Complete (3)
   ------------
   [005] [p3] Code cleanup in legacy modules

   Total: 7 todos (4 active, 3 complete)
   ```

### Update Todo

**Command**: `/cdocs:todo update <id>`

**Steps**:

1. **Find Todo File**:
   - Locate file matching ID pattern: `{id}-*-*-*.md`
   - If not found, report error

2. **Display Current State**:
   - Parse filename and frontmatter
   - Show current status, priority, description

3. **Prompt for Changes**:
   - Status: pending / in-progress / blocked / complete
   - Priority: p1 / p2 / p3
   - Description: Update title if needed

4. **Update File**:
   - If status or priority changed: Rename file
   - Update frontmatter `updated` field
   - Update frontmatter `status` and `priority` fields

5. **Rename File**:
   - Old: `003-pending-p2-database-migration.md`
   - New: `003-in-progress-p1-database-migration.md`
   - Use Bash `mv` command

6. **Confirm Update**:
   ```
   Todo Updated
   ============

   ID: 003
   Changes:
   - Status: pending → in-progress
   - Priority: p2 → p1

   File: ./csharp-compounding-docs/todos/003-in-progress-p1-database-migration.md
   ```

### Complete Todo

**Command**: `/cdocs:todo complete <id>`

**Shortcut for**: `/cdocs:todo update <id> --status=complete`

**Steps**:

1. Find todo file by ID
2. Update status to `complete`
3. Update frontmatter `updated` field
4. Rename file to reflect `complete` status
5. Optionally prompt to archive

### Archive Todos

**Command**: `/cdocs:todo archive`

**Steps**:

1. **Find Completed Todos**:
   - Scan for files matching `*-complete-*-*.md`

2. **Display Candidates**:
   ```
   Archivable Todos
   ================

   [005] [p3] Code cleanup in legacy modules (completed 2025-01-20)
   [007] [p2] Update documentation (completed 2025-01-19)
   [009] [p1] Fix security issue (completed 2025-01-18)

   Archive these todos? (y/N)
   ```

3. **Confirm Archive**:
   - BLOCKING: Wait for user confirmation

4. **Move to Archive**:
   - Create `./csharp-compounding-docs/todos/archive/` if not exists
   - Move completed todos to archive directory
   - Preserve filenames

5. **Report Results**:
   ```
   Archive Complete
   ================

   Archived 3 todos to ./csharp-compounding-docs/todos/archive/

   Remaining todos: 4 (2 pending, 1 in-progress, 1 blocked)
   ```

## Integration with Compound Docs

**Related Docs Links**:
- Todos can reference related documentation
- Use relative markdown links in frontmatter
- Example: `../problems/database-timeout-20250120.md`

**Creating Todos from Documentation**:
- When capturing problems, offer to create follow-up todo
- Link todo to source documentation

**Searching Todos**:
- Todos are indexed like other compound docs
- Search with `/cdocs:search` or `/cdocs:query`

## File-Based State Advantages

**Benefits**:
- Git-trackable: See todo history in git log
- Human-readable: Status visible in filename
- No database: Filesystem IS the database
- Portable: Works offline, no server required
- Diffable: Git diffs show todo changes
- Mergeable: Standard git merge resolution

**Example Git Log**:
```
git log --oneline todos/
a1b2c3d Completed security vulnerability fix
d4e5f6g Updated todo 003: in-progress → blocked
g7h8i9j Created new todo: Performance optimization
```

## Examples

### Create Critical Todo

```
User: /cdocs:todo create