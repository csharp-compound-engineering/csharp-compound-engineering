# Git Current Branch Detection: Comprehensive Research

This document covers all methods for determining the current Git branch name, including edge cases, scripting best practices, and programmatic access.

## Table of Contents

1. [Common Methods](#common-methods)
2. [Differences Between Methods](#differences-between-methods)
3. [Edge Cases](#edge-cases)
4. [Scripting Best Practices](#scripting-best-practices)
5. [Programmatic Access](#programmatic-access)
6. [Related Information](#related-information)
7. [Quick Reference Table](#quick-reference-table)

---

## Common Methods

### 1. `git branch --show-current` (Git 2.22+)

**Introduced:** Git 2.22.0 (June 7, 2019)

```bash
git branch --show-current
```

**Example Output:**
```
main
```

**Characteristics:**
- Cleanest, most straightforward approach
- Returns empty string (no output) in detached HEAD state
- Works correctly on new/orphan branches with no commits
- Recommended for modern scripts where Git 2.22+ is guaranteed

**Use Case:** Best choice for human-readable output and modern environments.

---

### 2. `git rev-parse --abbrev-ref HEAD`

```bash
git rev-parse --abbrev-ref HEAD
```

**Example Output:**
```
main
```

**Characteristics:**
- Available in older Git versions (since Git 1.7)
- Returns literal `HEAD` in detached HEAD state
- May produce fatal error on new repositories with no commits: `fatal: ambiguous argument 'HEAD': unknown revision or path not in the working tree`
- Legacy approach, but widely compatible

**Use Case:** Best for maximum backward compatibility with older Git installations.

---

### 3. `git symbolic-ref --short HEAD`

```bash
git symbolic-ref --short HEAD
```

**Example Output:**
```
main
```

**Characteristics:**
- Reads the symbolic reference that HEAD points to
- Fails with error in detached HEAD state: `fatal: ref HEAD is not a symbolic ref`
- Use `-q` (quiet) flag to suppress error and exit silently with non-zero status
- Recommended for machine-readable output in scripts

**Quiet Mode Example:**
```bash
git symbolic-ref -q --short HEAD
# Returns empty and exits with non-zero status if detached
```

**Use Case:** Preferred for scripts that need to detect detached HEAD state via exit code.

---

### 4. `git name-rev --name-only HEAD`

```bash
git name-rev --name-only HEAD
```

**Example Output:**
```
main
```

**Characteristics:**
- Finds symbolic names suitable for human consumption
- May return branch name with additional context (e.g., `main~2`)
- Not reliable for exact branch name detection
- Works in detached HEAD state but may return unexpected results

**Use Case:** Useful for human-readable descriptions, not recommended for scripts requiring exact branch names.

---

### 5. Parsing `git branch` Output

```bash
git branch | grep '^\*' | cut -d' ' -f2
```

Or using `sed`:
```bash
git branch | sed -n 's/^\* //p'
```

**Example Output:**
```
main
```

In detached HEAD state:
```
(HEAD detached at abc1234)
```

**Characteristics:**
- "Porcelain" command designed for human consumption
- Output format may vary between Git versions
- May include color codes that break parsing
- Current branch marked with asterisk (`*`)
- Not recommended for scripts due to fragility

**Use Case:** Interactive use only; avoid in scripts.

---

## Differences Between Methods

### Behavior Comparison Table

| Scenario | `--show-current` | `rev-parse --abbrev-ref` | `symbolic-ref --short` | `name-rev` |
|----------|------------------|--------------------------|------------------------|------------|
| Normal branch | Branch name | Branch name | Branch name | Branch name |
| Detached HEAD | Empty string | `HEAD` | Error (or empty w/ `-q`) | Commit description |
| New repo (no commits) | Branch name | Fatal error | Branch name | Fatal error |
| Git version required | 2.22+ | 1.7+ | Early versions | Early versions |
| Exit code on detached | 0 | 0 | Non-zero | 0 |

### Which Methods Work in Detached HEAD State

| Method | Works? | Output |
|--------|--------|--------|
| `git branch --show-current` | Yes | Empty string |
| `git rev-parse --abbrev-ref HEAD` | Yes | Literal `HEAD` |
| `git symbolic-ref --short HEAD` | No | Error message |
| `git symbolic-ref -q --short HEAD` | Partial | Empty, non-zero exit |
| `git name-rev --name-only HEAD` | Yes | Commit-based description |

### Performance Differences

All methods are extremely fast (sub-millisecond) since they only read local references. There are no significant performance differences for practical purposes. However:

- `git branch --show-current` is a single, purpose-built operation
- `git rev-parse` is a general-purpose reference resolver
- `git symbolic-ref` directly reads the `.git/HEAD` file

For most use cases, performance differences are negligible.

### Error Handling Comparison

| Method | Silent Failure | Error Output | Exit Code |
|--------|----------------|--------------|-----------|
| `git branch --show-current` | Yes (empty output) | None | 0 |
| `git rev-parse --abbrev-ref HEAD` | Partial (returns `HEAD`) | None on detached, error on new repo | 0 or non-zero |
| `git symbolic-ref --short HEAD` | No | stderr error | Non-zero |
| `git symbolic-ref -q --short HEAD` | Yes | None | Non-zero |

---

## Edge Cases

### 1. Detached HEAD State

A detached HEAD occurs when you check out:
- A specific commit: `git checkout abc1234`
- A tag: `git checkout v1.0.0`
- A remote branch directly: `git checkout origin/main`
- During interactive rebase: `git rebase -i`

**Detection Example:**
```bash
#!/bin/bash
branch=$(git symbolic-ref -q --short HEAD)
if [ -z "$branch" ]; then
    echo "Detached HEAD state - no branch checked out"
    # Get the commit hash instead
    commit=$(git rev-parse --short HEAD)
    echo "Currently at commit: $commit"
else
    echo "On branch: $branch"
fi
```

### 2. Inside a Bare Repository

Bare repositories have no working directory and no checked-out branch in the traditional sense.

```bash
# In a bare repository
git branch --show-current
# Output: (empty)

git symbolic-ref HEAD
# Output: refs/heads/main (the default branch)
```

**Detection:**
```bash
if git rev-parse --is-bare-repository | grep -q true; then
    echo "This is a bare repository"
    # Get the default branch
    default_branch=$(git symbolic-ref HEAD | sed 's|refs/heads/||')
    echo "Default branch: $default_branch"
fi
```

### 3. During Merge/Rebase Conflicts

During a merge conflict:
```bash
git branch --show-current
# Still returns the current branch name (e.g., "main")
```

During a rebase conflict:
- HEAD is detached on the commit being rebased
- `REBASE_HEAD` pseudo-reference points to the conflicting commit
- Original branch name is stored in `.git/rebase-merge/head-name`

**Detection Example:**
```bash
#!/bin/bash
if [ -d .git/rebase-merge ] || [ -d .git/rebase-apply ]; then
    echo "Rebase in progress"
    if [ -f .git/rebase-merge/head-name ]; then
        original_branch=$(cat .git/rebase-merge/head-name | sed 's|refs/heads/||')
        echo "Rebasing branch: $original_branch"
    fi
elif [ -f .git/MERGE_HEAD ]; then
    echo "Merge in progress"
    current_branch=$(git branch --show-current)
    echo "Merging into: $current_branch"
fi
```

### 4. Shallow Clones

Shallow clones (`git clone --depth=1`) have truncated history but branch detection works normally:

```bash
git clone --depth=1 https://github.com/user/repo.git
cd repo
git branch --show-current
# Works normally: main
```

**Limitations:**
- Implicit `--single-branch` means only one branch is fetched
- Switching to other branches requires fetching them first
- `git fetch --unshallow` converts to full clone

### 5. Git Worktrees

Each worktree has its own HEAD and can have a different branch checked out:

```bash
# List worktrees with their branches
git worktree list
# Output:
# /path/to/main         abc1234 [main]
# /path/to/feature      def5678 [feature-branch]
# /path/to/detached     789abc0 (detached HEAD)

# Get current branch in a worktree
cd /path/to/feature
git branch --show-current
# Output: feature-branch
```

### 6. New Repository (No Commits)

```bash
git init new-repo
cd new-repo
git branch --show-current
# Output: (empty in some versions, "main" or "master" in others)

git symbolic-ref --short HEAD
# Output: main (or master, depending on configuration)

git rev-parse --abbrev-ref HEAD
# Error: fatal: ambiguous argument 'HEAD'
```

---

## Scripting Best Practices

### Recommended Approach for Shell Scripts

```bash
#!/bin/bash

# Function to get current branch name safely
get_current_branch() {
    local branch

    # Try the modern approach first (Git 2.22+)
    branch=$(git branch --show-current 2>/dev/null)

    # Fallback for older Git versions
    if [ -z "$branch" ]; then
        branch=$(git symbolic-ref -q --short HEAD 2>/dev/null)
    fi

    # Return empty string if detached HEAD
    echo "$branch"
}

# Usage
current_branch=$(get_current_branch)
if [ -n "$current_branch" ]; then
    echo "On branch: $current_branch"
else
    echo "Not on any branch (detached HEAD or bare repo)"
fi
```

### CI/CD Pipeline Considerations

Different CI systems provide branch information through environment variables:

```bash
#!/bin/bash

# Cross-platform CI branch detection
get_ci_branch() {
    # GitHub Actions
    if [ -n "$GITHUB_HEAD_REF" ]; then
        echo "$GITHUB_HEAD_REF"  # For pull requests
    elif [ -n "$GITHUB_REF_NAME" ]; then
        echo "$GITHUB_REF_NAME"  # For pushes
    # GitLab CI
    elif [ -n "$CI_COMMIT_BRANCH" ]; then
        echo "$CI_COMMIT_BRANCH"
    elif [ -n "$CI_MERGE_REQUEST_SOURCE_BRANCH_NAME" ]; then
        echo "$CI_MERGE_REQUEST_SOURCE_BRANCH_NAME"
    # Jenkins
    elif [ -n "$GIT_BRANCH" ]; then
        echo "${GIT_BRANCH#origin/}"  # Remove origin/ prefix
    # Azure DevOps
    elif [ -n "$BUILD_SOURCEBRANCHNAME" ]; then
        echo "$BUILD_SOURCEBRANCHNAME"
    # CircleCI
    elif [ -n "$CIRCLE_BRANCH" ]; then
        echo "$CIRCLE_BRANCH"
    # Bitbucket Pipelines
    elif [ -n "$BITBUCKET_BRANCH" ]; then
        echo "$BITBUCKET_BRANCH"
    # Travis CI
    elif [ -n "$TRAVIS_BRANCH" ]; then
        echo "$TRAVIS_BRANCH"
    # Fallback to git command
    else
        git branch --show-current 2>/dev/null || git rev-parse --abbrev-ref HEAD 2>/dev/null
    fi
}
```

### Error Handling Best Practices

```bash
#!/bin/bash
set -e  # Exit on error

get_branch_with_validation() {
    # Ensure we're in a git repository
    if ! git rev-parse --git-dir >/dev/null 2>&1; then
        echo "Error: Not a git repository" >&2
        return 1
    fi

    # Check if it's a bare repository
    if [ "$(git rev-parse --is-bare-repository)" = "true" ]; then
        echo "Error: Bare repository has no working branch" >&2
        return 1
    fi

    # Get the branch name
    local branch
    branch=$(git branch --show-current 2>/dev/null)

    # Handle detached HEAD
    if [ -z "$branch" ]; then
        echo "Warning: Detached HEAD state" >&2
        return 1
    fi

    echo "$branch"
}
```

### Cross-Platform Considerations (Windows, Linux, macOS)

**PowerShell (Windows):**
```powershell
# PowerShell approach
function Get-GitBranch {
    $branch = git branch --show-current 2>$null
    if (-not $branch) {
        $branch = git symbolic-ref --short HEAD 2>$null
    }
    return $branch
}

$currentBranch = Get-GitBranch
if ($currentBranch) {
    Write-Host "On branch: $currentBranch"
} else {
    Write-Host "Not on a branch"
}
```

**Bash (Linux/macOS/Git Bash on Windows):**
```bash
# Works on all Unix-like systems and Git Bash
branch=$(git branch --show-current 2>/dev/null || git rev-parse --abbrev-ref HEAD 2>/dev/null)
```

**Node.js (Cross-platform):**
```javascript
const { execSync } = require('child_process');

function getCurrentBranch() {
    try {
        return execSync('git branch --show-current', { encoding: 'utf-8' }).trim();
    } catch {
        try {
            const branch = execSync('git rev-parse --abbrev-ref HEAD', { encoding: 'utf-8' }).trim();
            return branch === 'HEAD' ? null : branch;
        } catch {
            return null;
        }
    }
}
```

---

## Programmatic Access

### Using libgit2 (C)

```c
#include <git2.h>
#include <stdio.h>

int main() {
    git_libgit2_init();

    git_repository *repo = NULL;
    git_reference *head_ref = NULL;

    // Open repository
    if (git_repository_open(&repo, ".") != 0) {
        fprintf(stderr, "Failed to open repository\n");
        return 1;
    }

    // Get HEAD reference
    if (git_repository_head(&head_ref, repo) == 0) {
        const char *branch_name = NULL;

        // Get the branch name from the reference
        if (git_branch_name(&branch_name, head_ref) == 0) {
            printf("Current branch: %s\n", branch_name);
        } else {
            printf("Detached HEAD state\n");
        }

        git_reference_free(head_ref);
    }

    git_repository_free(repo);
    git_libgit2_shutdown();

    return 0;
}
```

### Using LibGit2Sharp (C#/.NET)

```csharp
using LibGit2Sharp;

public static string GetCurrentBranch(string repoPath = ".")
{
    using var repo = new Repository(repoPath);

    if (repo.Head.IsCurrentRepositoryHead)
    {
        return repo.Head.FriendlyName;
    }

    // Detached HEAD
    return null;
}

// Usage
var branch = GetCurrentBranch();
if (branch != null)
{
    Console.WriteLine($"On branch: {branch}");
}
else
{
    Console.WriteLine("Detached HEAD state");
}
```

### Using pygit2 (Python)

```python
import pygit2

def get_current_branch(repo_path='.'):
    repo = pygit2.Repository(repo_path)

    if repo.head_is_detached:
        return None

    # Get the current branch name
    branch_name = repo.head.shorthand
    return branch_name

# Usage
branch = get_current_branch()
if branch:
    print(f"On branch: {branch}")
else:
    print("Detached HEAD state")
```

### Using go-git (Go)

```go
package main

import (
    "fmt"
    "github.com/go-git/go-git/v5"
    "github.com/go-git/go-git/v5/plumbing"
)

func getCurrentBranch(repoPath string) (string, error) {
    repo, err := git.PlainOpen(repoPath)
    if err != nil {
        return "", err
    }

    head, err := repo.Head()
    if err != nil {
        return "", err
    }

    if head.Name().IsBranch() {
        return head.Name().Short(), nil
    }

    // Detached HEAD
    return "", nil
}

func main() {
    branch, err := getCurrentBranch(".")
    if err != nil {
        fmt.Printf("Error: %v\n", err)
        return
    }

    if branch != "" {
        fmt.Printf("On branch: %s\n", branch)
    } else {
        fmt.Println("Detached HEAD state")
    }
}
```

### CI/CD Environment Variables Reference

| CI System | Primary Variable | Pull Request Variable | Notes |
|-----------|-----------------|----------------------|-------|
| GitHub Actions | `GITHUB_REF_NAME` | `GITHUB_HEAD_REF` | `GITHUB_REF` includes `refs/heads/` prefix |
| GitLab CI | `CI_COMMIT_BRANCH` | `CI_MERGE_REQUEST_SOURCE_BRANCH_NAME` | Empty for tag pipelines |
| Jenkins | `GIT_BRANCH` | `CHANGE_BRANCH` | Includes `origin/` prefix |
| Azure DevOps | `BUILD_SOURCEBRANCHNAME` | `SYSTEM_PULLREQUEST_SOURCEBRANCH` | May be `merge` for PRs |
| CircleCI | `CIRCLE_BRANCH` | `CIRCLE_BRANCH` | Same variable for both |
| Travis CI | `TRAVIS_BRANCH` | `TRAVIS_PULL_REQUEST_BRANCH` | |
| Bitbucket Pipelines | `BITBUCKET_BRANCH` | `BITBUCKET_BRANCH` | |

---

## Related Information

### Getting the Remote Tracking Branch

```bash
# Get the upstream branch for the current branch
git rev-parse --abbrev-ref --symbolic-full-name @{upstream}
# Output: origin/main

# Or using for-each-ref
git for-each-ref --format='%(upstream:short)' $(git symbolic-ref -q HEAD)
```

### Getting the Upstream Branch Name

```bash
# Show tracking relationship
git branch -vv
# Output:
# * main   abc1234 [origin/main] Commit message
#   feature def5678 [origin/feature: ahead 2] Another message

# Get just the upstream for current branch
git config --get branch.$(git branch --show-current).remote
# Output: origin

git config --get branch.$(git branch --show-current).merge
# Output: refs/heads/main
```

### Checking if a Branch Exists

**Local Branch:**
```bash
# Using plumbing command (recommended for scripts)
git show-ref --verify --quiet refs/heads/branch-name
if [ $? -eq 0 ]; then
    echo "Branch exists"
fi

# Or using rev-parse
git rev-parse --verify --quiet branch-name
```

**Remote Branch:**
```bash
# Check if remote branch exists
git ls-remote --exit-code --heads origin branch-name
if [ $? -eq 0 ]; then
    echo "Remote branch exists"
fi
```

### Setting Up Tracking

```bash
# Set upstream for existing branch
git branch --set-upstream-to=origin/main main
# Or shorthand
git branch -u origin/main

# Create branch with tracking
git checkout -b feature origin/feature
# Or
git checkout --track origin/feature

# Push and set upstream in one command
git push -u origin feature
```

---

## Quick Reference Table

| Goal | Command | Git Version |
|------|---------|-------------|
| Get current branch (modern) | `git branch --show-current` | 2.22+ |
| Get current branch (legacy) | `git rev-parse --abbrev-ref HEAD` | 1.7+ |
| Get branch (detect detached) | `git symbolic-ref -q --short HEAD` | Early |
| Check if detached HEAD | `git symbolic-ref -q HEAD >/dev/null; echo $?` | Early |
| Get upstream branch | `git rev-parse --abbrev-ref @{upstream}` | 1.7+ |
| Check local branch exists | `git show-ref --verify --quiet refs/heads/NAME` | Early |
| Check remote branch exists | `git ls-remote --exit-code --heads origin NAME` | Early |
| List all local branches | `git branch` | Early |
| List branches with tracking | `git branch -vv` | Early |

---

## Recommendations Summary

1. **For modern scripts (Git 2.22+):** Use `git branch --show-current`
2. **For maximum compatibility:** Use `git rev-parse --abbrev-ref HEAD` with fallback handling
3. **For detecting detached HEAD via exit code:** Use `git symbolic-ref -q --short HEAD`
4. **In CI/CD pipelines:** Check environment variables first, then fall back to git commands
5. **Avoid:** Parsing `git branch` output in scripts (fragile, format can change)

---

## Sources

- [Git Official Documentation - git-branch](https://git-scm.com/docs/git-branch)
- [Git Official Documentation - git-rev-parse](https://git-scm.com/docs/git-rev-parse)
- [Git Official Documentation - git-symbolic-ref](https://git-scm.com/docs/git-symbolic-ref)
- [Highlights from Git 2.22 - GitHub Blog](https://github.blog/open-source/git/highlights-from-git-2-22/)
- [Baeldung - How to Get the Current Branch Name in Git](https://www.baeldung.com/ops/git-current-branch-name)
- [Adam Johnson - Git: Output just the current branch name](https://adamj.eu/tech/2023/08/20/git-output-just-current-branch-name/)
- [libgit2 Branch API Documentation](https://libgit2.org/docs/reference/main/branch/index.html)
- [GitLab CI/CD Predefined Variables](https://docs.gitlab.com/ci/variables/predefined_variables/)
- [Azure DevOps Predefined Variables](https://learn.microsoft.com/en-us/azure/devops/pipelines/build/variables)
- [CircleCI Project Values and Variables](https://circleci.com/docs/reference/variables/)
- [GitHub - How to check if a git branch exists](https://gist.github.com/iridiumcao/714d3d0a9137ce614c26e4e10d185291)
