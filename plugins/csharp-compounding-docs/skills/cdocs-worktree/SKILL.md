---
name: cdocs:worktree
description: Manage Git worktrees for parallel development across branches with compound docs integration
allowed-tools:
  - Bash
  - Read
preconditions:
  - Project is a Git repository
  - Git 2.5+ installed (worktree support)
---

# Git Worktree Management Skill

## Purpose

Manage Git worktrees for parallel development. Work on multiple branches simultaneously without stashing, with full compound docs integration per branch.

## What are Git Worktrees?

Git worktrees allow multiple working directories attached to the same repository, each checked out to a different branch. This enables:

- Work on feature branch while fixing urgent bug on another branch
- Run tests on one branch while developing on another
- Code review a PR while continuing your own work
- Compare behavior across multiple branches

## Invocation

**Manual** - User wants to manage worktrees.

**Commands**:
- `/cdocs:worktree create <branch>` - Create new worktree for branch
- `/cdocs:worktree list` - List all worktrees
- `/cdocs:worktree switch <name>` - Switch context to worktree
- `/cdocs:worktree remove <name>` - Remove worktree with safety checks
- `/cdocs:worktree status` - Show status of all worktrees

## Worktree Directory Structure

**Recommended Layout**:
```
my-project/                        # main branch (primary worktree)
├── .git/                          # Git repository
├── src/
└── .csharp-compounding-docs/      # Compound docs (shared)

my-project-worktrees/              # Worktrees directory
├── feature-auth/                  # feature/auth branch
│   ├── src/
│   └── .csharp-compounding-docs/  # Branch-specific docs
├── bugfix-login/                  # bugfix/login branch
│   ├── src/
│   └── .csharp-compounding-docs/
└── release-2.0/                   # release/2.0 branch
    ├── src/
    └── .csharp-compounding-docs/
```

## Process

### Create Worktree

**Command**: `/cdocs:worktree create <branch> [--path=<path>]`

**Steps**:

1. **Validate Prerequisites**:
   - Check git version: `git --version` (require 2.5+)
   - Verify repository: `git rev-parse --git-dir`
   - Check if branch exists: `git rev-parse --verify <branch>`

2. **Determine Worktree Path**:
   - If `--path` provided, use that path
   - Otherwise, auto-generate path:
     - Get repository name: `basename $(git rev-parse --show-toplevel)`
     - Create: `../{repo-name}-worktrees/{branch-name-sanitized}/`
   - Example: `../my-project-worktrees/feature-auth/`

3. **Check for Existing Worktree**:
   - Run: `git worktree list`
   - Verify branch not already checked out in another worktree
   - If exists, report error and show existing worktree path

4. **Create Worktree**:
   ```bash
   git worktree add <path> <branch>
   ```

   **Options**:
   - If branch doesn't exist: `git worktree add -b <branch> <path>`
   - For new branch from specific commit: `git worktree add -b <branch> <path> <commit>`

5. **Initialize Compound Docs**:
   - CD into new worktree: `cd <path>`
   - Check for existing config: `ls .csharp-compounding-docs/config.json`
   - If exists, load config and activate
   - If not exists, offer to create new config or share from main

6. **Report Success**:
   ```
   Worktree Created
   ================

   Branch: feature/auth
   Path: /Users/dev/my-project-worktrees/feature-auth
   Compound Docs: Shared from main worktree

   To switch to this worktree:
   /cdocs:worktree switch feature-auth

   Or use:
   cd /Users/dev/my-project-worktrees/feature-auth
   ```

### List Worktrees

**Command**: `/cdocs:worktree list`

**Steps**:

1. **Get Worktree List**:
   ```bash
   git worktree list --porcelain
   ```

2. **Parse Output**:
   - Extract worktree path, branch, and commit
   - Identify main worktree (contains .git directory)
   - Check for locked or prunable worktrees

3. **Display Worktrees**:
   ```
   Git Worktrees
   =============

   Main Worktree:
   - Branch: main
   - Path: /Users/dev/my-project
   - Status: Clean
   - Docs: Active (branch: main)

   Additional Worktrees:
   ---------------------
   1. feature/auth
      Path: /Users/dev/my-project-worktrees/feature-auth
      Status: Modified (3 files)
      Docs: Active (branch: feature/auth)

   2. bugfix/login
      Path: /Users/dev/my-project-worktrees/bugfix-login
      Status: Clean
      Docs: Shared from main

   3. release/2.0
      Path: /Users/dev/my-project-worktrees/release-2.0
      Status: Clean
      Docs: Branch-specific (branch: release-2.0)

   Total: 3 worktrees (+ 1 main)
   ```

### Switch Worktree

**Command**: `/cdocs:worktree switch <name>`

**Steps**:

1. **Find Worktree**:
   - Match by branch name or path
   - Use `git worktree list` to find exact path

2. **Change Directory**:
   - Not directly possible in skill
   - Provide instructions to user

3. **Activate Compound Docs**:
   - Detect worktree's compound docs config
   - Call `/cdocs:activate` with branch context
   - Load worktree-specific documentation

4. **Display Context**:
   ```
   Switched to Worktree
   ====================

   Branch: feature/auth
   Path: /Users/dev/my-project-worktrees/feature-auth

   Compound Docs Status:
   - Project: my-project
   - Branch: feature/auth
   - Documents: 23 (5 branch-specific)

   To navigate in terminal:
   cd /Users/dev/my-project-worktrees/feature-auth
   ```

**Note**: Actual directory change requires user action. Skill updates compound docs context.

### Remove Worktree

**Command**: `/cdocs:worktree remove <name>`

**Steps**:

1. **Find Worktree**:
   - Locate by branch name or path
   - Verify it's not the current worktree

2. **Check Status**:
   ```bash
   cd <worktree-path>
   git status --porcelain
   ```

3. **Safety Checks**:
   - If uncommitted changes: Warn user
   - If unpushed commits: Warn user
   - Require confirmation before proceeding

4. **Display Warning**:
   ```
   Remove Worktree
   ===============

   Branch: feature/auth
   Path: /Users/dev/my-project-worktrees/feature-auth

   ⚠️ WARNING:
   - Uncommitted changes: 3 files
   - Unpushed commits: 2 commits ahead of origin

   This will permanently delete this worktree directory.
   The branch will remain in the repository.

   Continue? (y/N)
   ```

5. **Remove Worktree**:
   ```bash
   git worktree remove <path>
   ```

   **Options**:
   - Force removal: `git worktree remove --force <path>` (if user confirms)

6. **Cleanup Compound Docs**:
   - Optionally offer to delete worktree's docs from database
   - Call `/cdocs:delete` with path-specific filter

7. **Report Success**:
   ```
   Worktree Removed
   ================

   Branch: feature/auth
   Path: /Users/dev/my-project-worktrees/feature-auth

   ✓ Worktree directory deleted
   ✓ Git worktree removed

   Branch 'feature/auth' still exists in repository.
   To delete the branch: git branch -d feature/auth
   ```

### Status

**Command**: `/cdocs:worktree status`

**Steps**:

1. **Gather Status for All Worktrees**:
   - List worktrees: `git worktree list`
   - For each worktree:
     - Run `git status --porcelain --branch`
     - Count modified, staged, untracked files
     - Check for unpushed commits
     - Check compound docs status

2. **Display Comprehensive Status**:
   ```
   Worktree Status Summary
   =======================

   Main (main)
   -----------
   Path: /Users/dev/my-project
   Files: 2 modified, 0 staged, 0 untracked
   Commits: Up to date with origin/main
   Docs: 45 documents (5 critical, 12 important)

   feature/auth
   ------------
   Path: /Users/dev/my-project-worktrees/feature-auth
   Files: 5 modified, 3 staged, 1 untracked
   Commits: 2 ahead of origin/feature/auth
   Docs: 23 documents (branch-specific)

   bugfix/login
   ------------
   Path: /Users/dev/my-project-worktrees/bugfix-login
   Files: Clean working tree
   Commits: 1 ahead, 3 behind origin/bugfix/login (merge needed)
   Docs: Shared from main

   Stale Worktrees:
   ----------------
   release/1.9
   - Path: /Users/dev/my-project-worktrees/release-1.9
   - Last commit: 45 days ago
   - Suggestion: Consider removing (git worktree remove)
   ```

## Compound Docs Integration

### Branch-Specific Documentation

**How It Works**:
- Each worktree can have its own `.csharp-compounding-docs/` directory
- Compound docs are stored with branch context in database
- `/cdocs:activate` automatically detects worktree branch
- Documentation is scoped to project + branch + path

### Sharing Strategies

**Option 1: Shared Docs** (Default)
- All worktrees share same compound docs
- Good for: Short-lived feature branches
- Implementation: Symlink `.csharp-compounding-docs/` to main worktree

**Option 2: Branch-Specific Docs**
- Each worktree has independent compound docs
- Good for: Long-lived release branches
- Implementation: Separate `.csharp-compounding-docs/` in each worktree

**Option 3: Hybrid**
- Core docs shared, branch-specific docs separate
- Good for: Most use cases
- Implementation: Share config, separate doc directories

### Activation Flow

When switching worktrees:
1. Detect current worktree branch: `git rev-parse --abbrev-ref HEAD`
2. Detect worktree path: `git rev-parse --show-toplevel`
3. Call `/cdocs:activate` with branch and path context
4. MCP server loads docs filtered by project + branch + path
5. Branch-specific docs available in queries

## Safety Features

**Validation**:
- Check git version before operations
- Verify worktree doesn't already exist for branch
- Validate path is not inside another worktree

**Warnings**:
- Uncommitted changes before removal
- Unpushed commits before removal
- Stale worktrees (no activity in 30+ days)

**Protection**:
- Never auto-remove worktrees
- Always require confirmation for destructive operations
- Preserve branch when removing worktree

## Git Commands Reference

**Core Commands**:
```bash
# Create worktree
git worktree add <path> <branch>

# Create worktree for new branch
git worktree add -b <new-branch> <path> [<commit>]

# List worktrees
git worktree list
git worktree list --porcelain

# Remove worktree
git worktree remove <path>
git worktree remove --force <path>

# Prune stale worktrees
git worktree prune

# Lock worktree (prevent automatic pruning)
git worktree lock <path>

# Unlock worktree
git worktree unlock <path>
```

## Use Case Examples

### Example 1: Urgent Bug Fix

```
Scenario: Working on feature, urgent bug needs fixing

User: /cdocs:worktree create hotfix/critical-bug
```

Assistant creates worktree, user fixes bug in parallel, then returns to feature work.

### Example 2: Code Review

```
Scenario: Review PR while continuing development

User: /cdocs:worktree create pr-review-123
```

Assistant creates worktree from PR branch, user reviews code without affecting current work.

### Example 3: Release Maintenance

```
Scenario: Maintain multiple release versions

User: /cdocs:worktree create release/2.0
User: /cdocs:worktree create release/1.9
```

Assistant creates worktrees for each release, each with branch-specific compound docs.

## Error Handling

**Common Errors**:

1. **Branch already checked out**:
   ```
   Error: Branch 'feature/auth' is already checked out in worktree:
   /Users/dev/my-project-worktrees/feature-auth

   Use: /cdocs:worktree switch feature-auth
   Or:  /cdocs:worktree remove feature-auth
   ```

2. **Git version too old**:
   ```
   Error: Git worktrees require Git 2.5 or higher
   Current version: 2.3.1

   Please upgrade Git to use worktree features.
   ```

3. **Not a git repository**:
   ```
   Error: Current directory is not a Git repository

   Worktree management is only available in Git repositories.
   ```

## Related Skills

- **