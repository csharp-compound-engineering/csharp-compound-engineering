# Agents Specification

> **Status**: [DRAFT]
> **Parent**: [SPEC.md](../SPEC.md)

---

## Overview

Agents are Claude Code agent definitions that work alongside the plugin. Unlike skills (which capture/retrieve documentation) and the MCP server (which provides vector storage), agents provide specialized review and research capabilities.

This plugin includes language-agnostic agents ported from the original compound-engineering plugin. Language-specific agents (Rails, Python, etc.) are excluded.

> **Background**: For comprehensive details on Claude Code's agent system including built-in agent types, spawning mechanisms, configuration options, coordination patterns, and best practices, see [Claude Code Agents Research](../research/claude-code-agents-research.md).

> **Background**: For practical guidance on creating agent files, YAML frontmatter schemas, tool configuration, hooks integration, and distribution patterns, see [Building Custom Claude Code Agents](../research/building-claude-code-agents-research.md).

> **Background**: These agents are adapted from the compound-engineering-plugin which implements the compound engineering paradigm. For the full paradigm overview including the four pillars (Plan, Delegate, Assess, Compound) and workflow patterns, see [Compound Engineering Paradigm Research](../research/compound-engineering-paradigm-research.md).

---

## Agent Categories

| Category | Count | Purpose |
|----------|-------|---------|
| Research | 4 | Best practices, documentation, git history |

**Total**: 4 agents

---

## Research Agents

### 1. `best-practices-researcher`

> **Origin**: Adapted from `best-practices-researcher` agent in [compound-engineering-plugin](https://github.com/anthropics/compound-engineering-plugin)

**Purpose**: Gather external best practices and examples.

**MCP Integration**:
- **Context7**: Framework patterns and documentation
- **Microsoft Docs**: .NET/C# specific guidance
- **Sequential Thinking**: Multi-step synthesis when reconciling findings from multiple sources

> **Note**: The Sequential Thinking MCP server uses package `@modelcontextprotocol/server-sequential-thinking`. See [Sequential Thinking MCP Verification](../research/sequential-thinking-mcp-verification.md) for correct configuration.

**Behavior**:
1. Search compound docs via `rag_query` for existing documented patterns
2. Check available skills for internal knowledge
3. Search Context7 for framework patterns
4. Search Microsoft Docs MCP for .NET guidance
5. Web search for community patterns
6. Use Sequential Thinking to:
   - Compare and contrast findings from different sources
   - Reconcile conflicting recommendations
   - Rank practices by applicability to current context
   - Identify gaps between internal docs and external best practices
7. Synthesize findings with citations, prioritizing internal docs

---

### 2. `framework-docs-researcher`

> **Origin**: Adapted from `framework-docs-researcher` agent in [compound-engineering-plugin](https://github.com/anthropics/compound-engineering-plugin)

**Purpose**: Research framework documentation for specific questions.

**MCP Integration**:
- **Context7**: General framework docs
- **Microsoft Docs**: .NET/C# specific docs
- **Sequential Thinking**: Complex documentation analysis and version reconciliation

**Behavior**:
1. Query external documentation sources (Context7, Microsoft Docs)
2. Augment results by querying compound docs via `rag_query` for project-specific context
3. Use Sequential Thinking to:
   - Reconcile documentation differences across framework versions
   - Break down complex multi-step API usage patterns
   - Compare official docs against project-specific gotchas
   - Determine which version-specific guidance applies
4. Synthesize official docs with internal documented learnings

**Output**:
- Relevant documentation excerpts
- Code examples from official sources
- Version-specific guidance
- Related internal documentation (if any)

---

### 3. `git-history-analyzer`

> **Origin**: Adapted from `git-history-analyzer` agent in [compound-engineering-plugin](https://github.com/anthropics/compound-engineering-plugin)

**Purpose**: Analyze git history and code evolution.

**MCP Integration**:
- **Sequential Thinking**: Pattern correlation and trend analysis across commit history

**Capabilities**:
- File change frequency analysis
- Author contribution patterns
- Commit message mining
- Bug-fix correlation
- Refactoring history

**Behavior**:
1. Gather git log, blame, and diff data for relevant files/timeframes
2. Use Sequential Thinking to:
   - Correlate bug fixes with code areas, authors, or time periods
   - Identify emerging patterns and trends in commit history
   - Trace root causes by working backwards through commits
   - Analyze refactoring patterns and their motivations
3. Synthesize findings into actionable insights

---

### 4. `repo-research-analyst`

> **Origin**: Adapted from `repo-research-analyst` agent in [compound-engineering-plugin](https://github.com/anthropics/compound-engineering-plugin)

**Purpose**: Research repository structure and conventions.

**MCP Integration**:
- **Sequential Thinking**: Convention inference and pattern validation across codebase

**Analysis Areas**:
- Project structure patterns
- Naming conventions in use
- Test organization
- Configuration patterns
- Documentation structure

**Behavior**:
1. Analyze repository structure and conventions
2. Query compound docs to avoid duplicating existing knowledge
3. Use Sequential Thinking to:
   - Infer unstated conventions from multiple code examples
   - Validate that detected patterns are consistent across the codebase
   - Identify anomalies and deviations from established patterns
   - Determine which patterns are intentional vs. incidental
4. When non-trivial insights are discovered:
   - Present findings to user with proposed doc entry
   - Prompt for confirmation before capturing to compound docs
   - User can veto, modify, or approve the update
5. Never auto-commit documentation without explicit user approval

---

## Agent File Structure

Each agent is defined as a markdown file with YAML frontmatter:

```
plugins/csharp-compounding-docs/agents/
└── research/
    ├── best-practices-researcher.md
    ├── framework-docs-researcher.md
    ├── git-history-analyzer.md
    └── repo-research-analyst.md
```

### Agent Frontmatter Format

```yaml
---
name: agent-name
description: "Detailed description with examples..."
model: inherit
---

[Agent instructions and behavior definition]
```

**Key Properties**:
- `model: inherit` - Uses the user's configured model
- Rich description with examples
- Detailed behavioral instructions
- Specific patterns and anti-patterns
- Reporting protocols

---

## MCP Server Integration

> **Background**: For details on how skills work alongside agents including auto-invoke mechanisms, MCP tool integration patterns, and the relationship between skills and agents, see [Claude Code Skills Research](../research/claude-code-skills-research.md).

Agents can leverage the plugin's MCP server for context:

```markdown
### Before Review

1. Call `rag_query` with the review topic to retrieve relevant documented knowledge
2. Check for critical-level documents that must be considered
3. Load any related problem/solution documentation

### During Review

4. Reference documented patterns and anti-patterns
5. Link findings to existing knowledge base entries

### After Review

6. Suggest appropriate capture skill (`/cdocs:problem`, `/cdocs:codebase`, etc.) if findings warrant documentation
```

---

## Open Questions

1. Should agents be able to auto-invoke capture skills (e.g., `/cdocs:problem`) after finding significant patterns?
2. How should agents interact with promotion levels (critical docs)?

## Resolved Questions

1. ~~Should there be a `/cdocs:review` skill that orchestrates multiple agents?~~ **Resolved**: Implemented as `/cdocs:research` - see [skills.md](./skills.md#14-cdocsresearch).

