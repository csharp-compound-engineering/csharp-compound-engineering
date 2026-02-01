# Phase 100: /cdocs:worktree Utility Skill

> **Status**: NOT_STARTED
> **Effort Estimate**: 6-8 hours
> **Category**: Skills System
> **Prerequisites**: Phase 081 (/cdocs:activate Skill), Phase 094 (/cdocs:git-operations Skill)

---

## Spec References

This phase implements the `/cdocs:worktree` skill defined in:

- **spec/skills/utility-skills.md** - [/cdocs:worktree](../spec/skills/utility-skills.md#cdocsworktree) - Full skill specification
- **spec/skills/skill-patterns.md** - [Skill File Structure](../spec/skills/skill-patterns.md#skill-file-structure) - Standard skill structure
- **spec/skills/meta-skills.md** - [/cdocs:activate](../spec/skills/meta-skills.md#cdocsactivate) - Integration with activation

---

## Background

> **Origin**: Adapted from `git-worktree` skill in [compound-engineering-plugin](https://github.com/anthropics/compound-engineering-plugin)

Git worktrees enable parallel development across branches without stashing. Multiple working directories attach to the same repository, each checked out to a different branch. This allows developers to:
- Work on multiple branches simultaneously
- Run tests on one branch while developing on another
- Code review a PR while continuing their own work
- Compare behavior across multiple branches

---

## Objectives

1. Create the `/cdocs:worktree` skill SKILL.md file
2. Implement all worktree subcommands (create, list, switch, remove, status)
3. Integrate with `/cdocs:activate` for context switching
4. Provide safety checks for uncommitted changes
5. Support the standard worktree directory structure pattern
6. Enable seamless compound docs sharing across worktrees

---

## Acceptance Criteria

### Skill File Structure

- [ ] Skill directory created: `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-worktree/`
- [ ] SKILL.md file with proper YAML frontmatter
- [ ] No schema.yaml required (utility skill, not doc-creation)

### YAML Frontmatter

- [ ] `name: cdocs:worktree`
- [ ] `description` clearly explains git worktree management purpose
- [ ] `allowed-tools` includes `Bash` for git CLI commands
- [ ] `preconditions` requires project activation
- [ ] No `auto-invoke` - this is a manual-only skill

### Subcommand: Create

- [ ] Command: `/cdocs:worktree create <branch>`
- [ ] Creates worktree in sibling directory: `{project}-worktrees/{branch-sanitized}/`
- [ ] Handles branch creation if branch doesn't exist
- [ ] Validates branch doesn't already have a worktree
- [ ] Reports worktree path on success
- [ ] Offers to switch context after creation

### Subcommand: List

- [ ] Command: `/cdocs:worktree list`
- [ ] Executes `git worktree list --porcelain`
- [ ] Formats output showing path, branch, and HEAD commit
- [ ] Indicates current worktree with marker
- [ ] Shows stale worktrees if any

### Subcommand: Switch

- [ ] Command: `/cdocs:worktree switch <name>`
- [ ] Validates worktree exists
- [ ] Reports context switch instructions
- [ ] Triggers `/cdocs:activate` for new context
- [ ] Preserves any session state that can be transferred

### Subcommand: Remove

- [ ] Command: `/cdocs:worktree remove <name>`
- [ ] Checks for uncommitted changes before removal
- [ ] Warns and requires confirmation if changes exist
- [ ] Executes `git worktree remove` with proper flags
- [ ] Suggests running `/cdocs:delete` for orphaned docs (optional)

### Subcommand: Status

- [ ] Command: `/cdocs:worktree status`
- [ ] Shows all worktrees with their status
- [ ] Indicates uncommitted changes in each worktree
- [ ] Flags stale worktrees that may need cleanup
- [ ] Reports compound docs activation state per worktree

### Git CLI Only

- [ ] All operations use git CLI commands via Bash
- [ ] No MCP tool dependencies for worktree operations
- [ ] Uses only standard git worktree commands:
  - `git worktree add`
  - `git worktree list`
  - `git worktree remove`
  - `git worktree prune`
  - `git status`

### Integration with /cdocs:activate

- [ ] `/cdocs:activate` detects worktree context automatically
- [ ] Branch name derived from worktree, not git HEAD
- [ ] Path hash includes worktree path for uniqueness
- [ ] Compound docs can be branch-specific or shared

### Safety Features

- [ ] Warns before removing worktree with uncommitted changes
- [ ] Validates branch doesn't already have a worktree
- [ ] Suggests cleanup for stale worktrees
- [ ] Prevents accidental removal of main worktree

---

## Implementation Notes

### SKILL.md Content

```yaml
---
name: cdocs:worktree
description: Manage Git worktrees for parallel development across branches
allowed-tools:
  - Bash  # Git worktree commands
  - Read  # Check file states
preconditions:
  - Project activated via /cdocs:activate
  - Git repository detected
---

# Git Worktree Management Skill

## Overview

Git worktrees allow multiple working directories attached to the same repository,
each checked out to a different branch. Work on multiple branches simultaneously
without stashing.

## Commands

### Create Worktree

`/cdocs:worktree create <branch>`

Creates a new worktree for the specified branch.

### Step 1: Validate Branch

```bash
# Check if branch exists
git branch --list <branch>

# Check if worktree already exists for branch
git worktree list | grep <branch>
```

If branch doesn't exist, offer to create it:
- Create from current HEAD: `git worktree add -b <branch> ../{project}-worktrees/<branch-sanitized>`
- Create from existing remote: `git worktree add ../{project}-worktrees/<branch-sanitized> <branch>`

### Step 2: Create Worktree

```bash
# Get project name from current directory
PROJECT_NAME=$(basename $(git rev-parse --show-toplevel))

# Sanitize branch name for directory (replace / with -)
WORKTREE_DIR="../${PROJECT_NAME}-worktrees/${BRANCH_SANITIZED}"

# Create worktree
git worktree add "$WORKTREE_DIR" <branch>
```

### Step 3: Report and Offer Context Switch

```
Worktree created successfully!

Branch: <branch>
Path: <worktree-path>

To work in this worktree:
  cd <worktree-path>

Switch context now?
1. Yes - switch to new worktree
2. No - stay in current worktree
```

---

### List Worktrees

`/cdocs:worktree list`

### Step 1: Get Worktree List

```bash
git worktree list --porcelain
```

### Step 2: Format Output

```
Git Worktrees for {project}:

  * /path/to/main-project          [main]      abc1234
    /path/to/worktrees/feature-x   [feature/x] def5678
    /path/to/worktrees/bugfix-y    [bugfix/y]  ghi9012

* = current worktree
```

---

### Switch Worktree

`/cdocs:worktree switch <name>`

### Step 1: Find Worktree

```bash
git worktree list | grep <name>
```

### Step 2: Validate and Report

```
Switching to worktree: <name>

Path: <worktree-path>
Branch: <branch>

To complete the switch:
1. Change directory: cd <worktree-path>
2. Compounding docs will reactivate automatically

Note: Claude Code session will need to be restarted in the new directory.
```

### Step 3: Activate in New Context

If staying in same Claude session (user changes cwd):
- Run `/cdocs:activate` to re-initialize with new worktree context

---

### Remove Worktree

`/cdocs:worktree remove <name>`

### Step 1: Find Worktree

```bash
git worktree list | grep <name>
```

### Step 2: Safety Check

```bash
# Check for uncommitted changes
cd <worktree-path>
git status --porcelain
```

If changes exist:
```
WARNING: Worktree has uncommitted changes!

Uncommitted files:
  M  src/file1.cs
  ?? src/file2.cs

Proceed with removal? This will DISCARD all changes.
1. Yes - remove anyway (changes lost)
2. No - cancel removal
3. Stash changes first
```

### Step 3: Remove Worktree

```bash
git worktree remove <worktree-path>
# Or with force if needed:
git worktree remove --force <worktree-path>
```

### Step 4: Offer Cleanup

```
Worktree removed.

The compounding docs for branch [<branch>] may now be orphaned.
Run /cdocs:delete to clean up? (branch: <branch>)
1. Yes - delete orphaned docs
2. No - keep docs (may be useful later)
```

---

### Status

`/cdocs:worktree status`

### Step 1: Gather Information

```bash
# Get all worktrees
git worktree list --porcelain

# For each worktree, check status
for worktree in $(git worktree list --porcelain | grep worktree | cut -d' ' -f2); do
  cd "$worktree"
  git status --porcelain
done
```

### Step 2: Format Status Report

```
Worktree Status Report
======================

/path/to/main-project [main]
  Status: Clean
  Compounding Docs: Activated

/path/to/worktrees/feature-x [feature/x]
  Status: 2 uncommitted changes
    M  src/Service.cs
    ?? src/NewFile.cs
  Compounding Docs: Not activated

/path/to/worktrees/stale-branch [stale/branch]
  Status: STALE (branch deleted)
  Action: Run 'git worktree prune' to clean up
```

---

## Worktree Directory Structure

Standard structure for worktrees:

```
my-project/                        # main branch (primary worktree)
my-project-worktrees/
├── feature-auth/                  # feature/auth branch
│   └── .csharp-compounding-docs/  # Can be shared via symlink or separate
├── bugfix-login/                  # bugfix/login branch
└── release-2.0/                   # release/2.0 branch
```

## Compound Docs in Worktrees

Worktrees can handle compound docs in two ways:

1. **Shared docs** (default): All worktrees use the same compound docs via `branch_name` differentiation
   - Docs stored in main worktree's `.csharp-compounding-docs/`
   - Each worktree's `/cdocs:activate` uses its branch name
   - RAG queries filter by branch

2. **Independent docs** (advanced): Each worktree has its own `.csharp-compounding-docs/`
   - Useful for major version branches with divergent docs
   - Requires copying or symlinking config

## Safety Notes

- Never remove the main worktree (the original clone)
- Always check for uncommitted changes before removal
- Stale worktrees (pointing to deleted branches) should be pruned
- Branch deletion doesn't automatically remove worktrees
```

### Git Commands Reference

| Operation | Command |
|-----------|---------|
| Create worktree | `git worktree add <path> <branch>` |
| Create with new branch | `git worktree add -b <branch> <path>` |
| List worktrees | `git worktree list [--porcelain]` |
| Remove worktree | `git worktree remove <path>` |
| Force remove | `git worktree remove --force <path>` |
| Prune stale | `git worktree prune` |
| Lock worktree | `git worktree lock <path>` |
| Unlock worktree | `git worktree unlock <path>` |

### Branch Name Sanitization

Branch names like `feature/auth-system` become directory names `feature-auth-system`:

```bash
BRANCH_SANITIZED=$(echo "$BRANCH" | tr '/' '-')
```

### Worktree Detection in /cdocs:activate

The activate skill should detect worktree context:

```bash
# Check if current directory is a worktree
git rev-parse --git-dir
# If result contains "/worktrees/", we're in a worktree

# Get the worktree-specific git dir
WORKTREE_GIT_DIR=$(git rev-parse --git-dir)

# Get the common git dir (main repo)
COMMON_GIT_DIR=$(git rev-parse --git-common-dir)
```

### Context Switching Workflow

```
User in main-project/
      |
      v
/cdocs:worktree create feature/new-feature
      |
      v
Worktree created at ../main-project-worktrees/feature-new-feature/
      |
      v
User: "cd ../main-project-worktrees/feature-new-feature/"
      |
      v
/cdocs:activate auto-invokes
      |
      v
MCP activate_project called with:
  - config_path: /path/to/main-project/.csharp-compounding-docs/config.json
  - branch_name: feature/new-feature
  - worktree_path: /path/to/main-project-worktrees/feature-new-feature/
```

---

## Dependencies

### Depends On
- Phase 081: /cdocs:activate Skill (context switching integration)
- Phase 094: /cdocs:git-operations Skill (shared git CLI patterns)

### Blocks
- None (terminal skill)

---

## Verification Steps

After completing this phase, verify:

1. **Skill Discovery**: `/cdocs:worktree` appears in skill list
2. **Create Command**: Successfully creates worktree in expected location
3. **List Command**: Shows all worktrees with proper formatting
4. **Switch Command**: Provides correct instructions and triggers activation
5. **Remove Command**: Safety checks work, removal succeeds
6. **Status Command**: Accurately reports worktree states
7. **Integration**: `/cdocs:activate` works correctly in worktree context

### Manual Verification

```bash
# Test create
cd /path/to/project
# Invoke: /cdocs:worktree create feature/test

# Verify worktree created
ls ../project-worktrees/
# Should show: feature-test/

# Test list
git worktree list
# Should show main + feature-test

# Test switch
# Invoke: /cdocs:worktree switch feature-test
# Follow instructions to change directory

# Test activation in worktree
cd ../project-worktrees/feature-test
# /cdocs:activate should auto-invoke

# Test status
# Invoke: /cdocs:worktree status
# Should show both worktrees

# Test remove (after making uncommitted changes)
echo "test" > test.txt
# Invoke: /cdocs:worktree remove feature-test
# Should warn about uncommitted changes

# Clean removal
rm test.txt
# Invoke: /cdocs:worktree remove feature-test
# Should succeed
```

### Integration Tests

```csharp
[Fact]
public async Task WorktreeCreate_CreatesWorktreeInExpectedLocation()
{
    // Arrange
    var projectPath = CreateTestRepository();
    var skill = new WorktreeSkill(projectPath);

    // Act
    var result = await skill.CreateWorktreeAsync("feature/test");

    // Assert
    var expectedPath = Path.Combine(
        Path.GetDirectoryName(projectPath),
        $"{Path.GetFileName(projectPath)}-worktrees",
        "feature-test");
    Assert.True(Directory.Exists(expectedPath));
}

[Fact]
public async Task WorktreeRemove_WarnsOnUncommittedChanges()
{
    // Arrange
    var projectPath = CreateTestRepository();
    var skill = new WorktreeSkill(projectPath);
    await skill.CreateWorktreeAsync("feature/test");
    CreateUncommittedFile("feature/test");

    // Act
    var result = await skill.RemoveWorktreeAsync("feature/test", force: false);

    // Assert
    Assert.True(result.HasWarning);
    Assert.Contains("uncommitted changes", result.WarningMessage);
}

[Fact]
public async Task WorktreeList_IncludesAllWorktrees()
{
    // Arrange
    var projectPath = CreateTestRepository();
    var skill = new WorktreeSkill(projectPath);
    await skill.CreateWorktreeAsync("feature/a");
    await skill.CreateWorktreeAsync("feature/b");

    // Act
    var worktrees = await skill.ListWorktreesAsync();

    // Assert
    Assert.Equal(3, worktrees.Count); // main + 2 features
    Assert.Contains(worktrees, w => w.Branch == "main");
    Assert.Contains(worktrees, w => w.Branch == "feature/a");
    Assert.Contains(worktrees, w => w.Branch == "feature/b");
}
```

---

## Files to Create/Modify

### New Files

| File | Purpose |
|------|---------|
| `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-worktree/SKILL.md` | Main skill definition |

### Modified Files

| File | Changes |
|------|---------|
| `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-activate/SKILL.md` | Add worktree detection logic |

---

## Notes

- This skill uses git CLI commands only - no MCP tools required
- Worktree paths should use sanitized branch names (/ replaced with -)
- The skill provides guidance for manual directory changes (Claude can't change cwd)
- Integration with `/cdocs:activate` ensures compound docs context updates
- Safety checks are critical to prevent data loss from uncommitted changes
- Stale worktree detection helps maintain clean development environments
