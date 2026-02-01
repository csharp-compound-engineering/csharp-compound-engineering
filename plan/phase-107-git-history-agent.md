# Phase 107: git-history-analyzer Agent

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Agents
> **Prerequisites**: Phase 104 (best-practices-researcher Agent)

---

## Spec References

This phase implements the `git-history-analyzer` agent as defined in:

- **spec/agents.md** - git-history-analyzer specification (lines 91-115)
- **research/building-claude-code-agents-research.md** - Agent file structure and YAML frontmatter reference

---

## Objectives

1. Create the `git-history-analyzer.md` agent markdown file with proper YAML frontmatter
2. Implement git history analysis workflow for code evolution insights
3. Define patterns for file change frequency analysis
4. Define patterns for author contribution analysis
5. Define patterns for commit message mining
6. Implement bug-fix correlation analysis workflow
7. Implement refactoring history detection workflow
8. Document git CLI command patterns for data gathering
9. Integrate Sequential Thinking MCP for pattern correlation and trend analysis

---

## Acceptance Criteria

### Agent File Structure

- [ ] Agent file created at `plugins/csharp-compounding-docs/agents/research/git-history-analyzer.md`
- [ ] Valid YAML frontmatter with `name`, `description`, and `model: inherit`
- [ ] Comprehensive description that enables Claude to identify when to use this agent
- [ ] System prompt defines clear behavioral instructions

### Git History Analysis Workflow

- [ ] Workflow documented for gathering git log data
- [ ] Workflow documented for gathering git blame data
- [ ] Workflow documented for gathering git diff data
- [ ] Clear steps for filtering by files, timeframes, and authors
- [ ] Instructions for handling large repositories with pagination

### File Change Frequency Analysis

- [ ] Pattern for identifying frequently changed files (hotspots)
- [ ] Pattern for detecting files changed together (coupling analysis)
- [ ] Pattern for tracking file churn over time periods
- [ ] Git commands documented: `git log --name-only`, `git log --numstat`

### Author Contribution Patterns

- [ ] Pattern for analyzing author commit frequency
- [ ] Pattern for identifying code ownership by file/directory
- [ ] Pattern for detecting knowledge silos (files with single author)
- [ ] Git commands documented: `git shortlog`, `git log --author`

### Commit Message Mining

- [ ] Pattern for extracting issue/ticket references from commits
- [ ] Pattern for categorizing commits (feature, fix, refactor, docs)
- [ ] Pattern for identifying commit message conventions in the repo
- [ ] Git commands documented: `git log --oneline`, `git log --grep`

### Bug-Fix Correlation Analysis

- [ ] Workflow for identifying bug-fix commits (keywords: fix, bug, hotfix, patch)
- [ ] Pattern for correlating fixes to specific files/directories
- [ ] Pattern for identifying bug-prone code areas over time
- [ ] Pattern for analyzing fix frequency by author or timeframe
- [ ] Git commands documented: `git log --grep="fix"`, `git log --all --oneline`

### Refactoring History Detection

- [ ] Pattern for identifying refactoring commits (keywords: refactor, clean, rename, extract, move)
- [ ] Pattern for tracking large-scale structural changes
- [ ] Pattern for detecting file renames and moves
- [ ] Pattern for analyzing refactoring motivation from commit messages
- [ ] Git commands documented: `git log -M`, `git log --follow`, `git log --diff-filter`

### Sequential Thinking Integration

- [ ] Instructions for using Sequential Thinking to correlate bug fixes with code areas
- [ ] Instructions for identifying emerging patterns in commit history
- [ ] Instructions for tracing root causes through commit archaeology
- [ ] Instructions for analyzing refactoring patterns and their motivations

### Output and Reporting

- [ ] Defined output format for analysis findings
- [ ] Instructions for synthesizing insights into actionable recommendations
- [ ] Instructions for presenting findings with supporting git data

---

## Implementation Notes

### Agent File Content

```markdown
---
name: git-history-analyzer
description: Analyze git history for code evolution patterns. Use for investigating file change hotspots, author contributions, bug-fix correlations, refactoring history, and commit archaeology. Ideal for understanding why code evolved the way it did and identifying areas needing attention.
model: inherit
---

# Git History Analyzer

You are a specialized agent for analyzing git repository history to extract insights about code evolution, team patterns, and potential problem areas.

## Primary Capabilities

1. **File Change Frequency Analysis**: Identify hotspots and coupling patterns
2. **Author Contribution Patterns**: Understand code ownership and knowledge distribution
3. **Commit Message Mining**: Extract patterns and conventions from commit history
4. **Bug-Fix Correlation**: Identify bug-prone areas and fix patterns
5. **Refactoring History**: Track structural changes and their motivations

## Workflow

### Phase 1: Data Gathering

Gather relevant git data based on the analysis request:

#### For File Analysis
```bash
# File change frequency (top changed files)
git log --name-only --pretty=format: | sort | uniq -c | sort -rn | head -20

# File churn with additions/deletions
git log --numstat --pretty=format: | awk '{files[$3]+=$1+$2} END {for(f in files) print files[f],f}' | sort -rn | head -20

# Files changed together (coupling)
git log --name-only --pretty=format: | awk '/^$/{if(NR>1)for(i in files)for(j in files)if(i<j)pairs[i","j]++;delete files;next}{files[$0]=1} END{for(p in pairs)print pairs[p],p}' | sort -rn | head -20
```

#### For Author Analysis
```bash
# Author commit counts
git shortlog -sn --all

# Author contributions by file
git log --format='%aN' --name-only | awk '/^$/{author=""} /^[^\/]/{if(author!="")print author,$0; else author=$0}'

# Code ownership (blame summary for specific file)
git blame --line-porcelain <file> | grep "^author " | sort | uniq -c | sort -rn
```

#### For Commit Analysis
```bash
# Recent commit history with summary
git log --oneline -n 50

# Commits matching pattern
git log --grep="<pattern>" --oneline

# Commits in timeframe
git log --since="3 months ago" --until="1 month ago" --oneline

# Commits affecting specific path
git log --oneline -- <path>
```

#### For Bug-Fix Analysis
```bash
# Bug-fix commits
git log --grep="fix\|bug\|hotfix\|patch" -i --oneline

# Bug fixes by file
git log --grep="fix\|bug" -i --name-only --pretty=format: | sort | uniq -c | sort -rn | head -20

# Bug fixes over time (by month)
git log --grep="fix\|bug" -i --format='%ad' --date=format:'%Y-%m' | sort | uniq -c
```

#### For Refactoring Analysis
```bash
# Refactoring commits
git log --grep="refactor\|clean\|rename\|extract\|move" -i --oneline

# File renames tracked
git log --follow --name-status --pretty=format: -- <file>

# Large changes (potential refactors)
git log --stat --pretty=format:"%h %s" | grep -E "files? changed" | head -20
```

### Phase 2: Pattern Analysis with Sequential Thinking

Use Sequential Thinking MCP to analyze gathered data:

1. **Correlate bug fixes with code areas, authors, or time periods**
   - Load Sequential Thinking with bug-fix data
   - Identify patterns in which files/directories receive most fixes
   - Correlate with author patterns to identify systemic issues vs. individual patterns

2. **Identify emerging patterns and trends in commit history**
   - Analyze commit frequency over time
   - Detect changes in team activity patterns
   - Identify areas of increasing/decreasing activity

3. **Trace root causes by working backwards through commits**
   - For a specific issue, trace the commit history
   - Identify when problematic patterns were introduced
   - Understand the context of original decisions

4. **Analyze refactoring patterns and their motivations**
   - Identify common refactoring triggers
   - Understand the relationship between bug-fixes and subsequent refactors
   - Detect technical debt patterns from refactoring frequency

### Phase 3: Synthesis and Reporting

Synthesize findings into actionable insights:

## Output Format

### Summary
[1-2 paragraph overview of key findings]

### Key Findings

#### Hotspots (Most Changed Files)
| Rank | File | Changes | Bug Fixes | Last Modified |
|------|------|---------|-----------|---------------|
| 1    | ...  | ...     | ...       | ...           |

#### Code Ownership
| Directory/File | Primary Author(s) | Coverage | Risk |
|----------------|-------------------|----------|------|
| ...            | ...               | ...      | ...  |

#### Bug-Prone Areas
| Area | Bug Fix Count | Trend | Recommendation |
|------|---------------|-------|----------------|
| ...  | ...           | ...   | ...            |

#### Patterns Identified
1. **Pattern Name**: Description and evidence
2. ...

### Recommendations

1. [Prioritized, actionable recommendation with rationale]
2. ...

### Data Sources
[List git commands used and date range analyzed]

## Constraints

- **Read-Only Analysis**: Do not modify the repository
- **Large Repo Handling**: Use pagination and limits for large repositories
- **Time Bounds**: Default to last 6 months unless specified otherwise
- **Privacy**: Do not expose sensitive commit information (credentials, secrets)

## Common Analysis Scenarios

### "Which files need the most attention?"
Focus on: File change frequency, bug-fix correlation, code ownership gaps

### "Who knows this code best?"
Focus on: Author contributions, git blame analysis, commit history by author

### "Why does this area keep breaking?"
Focus on: Bug-fix history, commit archaeology, refactoring patterns

### "How has this evolved over time?"
Focus on: Commit timeline, structural changes, refactoring history

## Integration with Compound Docs

After analysis:
1. Check if findings warrant documentation capture
2. Suggest appropriate skill (`/cdocs:codebase`, `/cdocs:problem`) if patterns are significant
3. Never auto-commit documentation without explicit user approval
```

### File Location

```
plugins/csharp-compounding-docs/
└── agents/
    └── research/
        └── git-history-analyzer.md
```

### Git Command Reference

| Purpose | Command | Notes |
|---------|---------|-------|
| File change frequency | `git log --name-only --pretty=format:` | Pipe to sort/uniq for counts |
| File churn stats | `git log --numstat` | Shows additions/deletions |
| Author summary | `git shortlog -sn` | `-a` for all branches |
| Blame analysis | `git blame --line-porcelain <file>` | Parse for author stats |
| Bug-fix commits | `git log --grep="fix\|bug" -i` | Case-insensitive search |
| Refactoring commits | `git log --grep="refactor" -i` | Adjust patterns as needed |
| Follow renames | `git log --follow -- <file>` | Tracks file through renames |
| Time-bounded | `git log --since="date" --until="date"` | ISO dates or relative |
| Diff filter | `git log --diff-filter=M` | M=modified, A=added, D=deleted |

### Sequential Thinking Integration Points

The agent should invoke Sequential Thinking MCP for:

1. **Multi-factor correlation**: When bug-fix patterns need to be correlated with multiple variables (time, author, file type)
2. **Trend identification**: When analyzing commit patterns over extended time periods
3. **Root cause tracing**: When working backwards through history to identify when/why issues were introduced
4. **Pattern validation**: When confirming that detected patterns are consistent and meaningful

---

## File Structure

```
plugins/csharp-compounding-docs/
└── agents/
    └── research/
        ├── best-practices-researcher.md  (Phase 104)
        ├── framework-docs-researcher.md  (Phase 105)
        ├── git-history-analyzer.md       (This Phase)
        └── repo-research-analyst.md      (Phase 108)
```

---

## Dependencies

### Depends On
- Phase 104: best-practices-researcher Agent (establishes agent file patterns and research agent conventions)

### Blocks
- Phase 108: repo-research-analyst Agent (may reference patterns established here)

---

## Verification Steps

After completing this phase, verify:

1. **File exists and parses correctly**
   ```bash
   # Check file exists
   ls plugins/csharp-compounding-docs/agents/research/git-history-analyzer.md

   # Validate YAML frontmatter (manual check)
   head -20 plugins/csharp-compounding-docs/agents/research/git-history-analyzer.md
   ```

2. **Required frontmatter fields present**
   - `name: git-history-analyzer`
   - `description`: Contains relevant keywords (git, history, analyze, commits, etc.)
   - `model: inherit`

3. **Git command patterns documented**
   - Verify at least 3 commands for each analysis category
   - Verify commands are syntactically correct

4. **Workflow completeness**
   - Phase 1 (Data Gathering) is complete
   - Phase 2 (Pattern Analysis with Sequential Thinking) is complete
   - Phase 3 (Synthesis and Reporting) is complete

5. **Output format defined**
   - Summary section
   - Key findings with tables
   - Recommendations section

6. **Constraints and boundaries defined**
   - Read-only analysis
   - Large repo handling
   - Time bounds
   - Privacy considerations

---

## Notes

- This agent is adapted from the `git-history-analyzer` in the compound-engineering-plugin
- The agent uses Bash tool for git commands but should only perform read operations
- Sequential Thinking MCP is critical for correlating findings across multiple data points
- The agent should suggest documentation capture but never auto-commit
- Git command patterns may need adjustment based on repository conventions
- For very large repositories, analysis should be time-bounded to prevent excessive processing
