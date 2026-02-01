# Phase 101: /cdocs:research Utility Skill

> **Category**: Skills System
> **Prerequisites**: Phase 081 (Skills Directory Structure)
> **Estimated Effort**: 6-8 hours
> **Status**: Pending

---

## Objective

Implement the `/cdocs:research` utility skill that orchestrates research agents with knowledge-base context, synthesizes findings across agents using Sequential Thinking MCP, and routes significant findings to appropriate capture skills.

---

## Success Criteria

- [ ] `plugins/csharp-compounding-docs/skills/cdocs-research/SKILL.md` created with complete orchestration workflow
- [ ] Sequential Thinking MCP integration for research planning, finding synthesis, and capture routing
- [ ] Agent selection menu with presets (Quick Research, Full Research, Historical Focus, Custom)
- [ ] Pre-research context loading via `rag_query` for relevant documented knowledge
- [ ] Agent coordination workflow launching selected research agents
- [ ] Finding synthesis that de-duplicates, reconciles conflicts, and categorizes findings
- [ ] Capture routing that determines appropriate doc-type and offers capture via `/cdocs:problem`, `/cdocs:codebase`, etc.
- [ ] Post-research decision menu with view, capture, apply, and continue options
- [ ] Manual invocation only (no auto-invoke triggers)

---

## Specification References

| Document | Section | Relevance |
|----------|---------|-----------|
| [spec/skills/utility-skills.md](../spec/skills/utility-skills.md) | `/cdocs:research` | Full skill specification including behavior, menus, and MCP integration |
| [spec/agents.md](../spec/agents.md) | Research Agents | Agent definitions for best-practices-researcher, framework-docs-researcher, git-history-analyzer, repo-research-analyst |
| [spec/agents.md](../spec/agents.md) | MCP Server Integration | How agents leverage `rag_query` for context |
| [spec/skills/skill-patterns.md](../spec/skills/skill-patterns.md) | SKILL.md Template | Standard skill file structure |
| [spec/skills.md](../spec/skills.md) | Built-in Skills | Skill naming conventions and categories |

---

## Tasks

### Task 101.1: Create Skill Directory Structure

Create the skill directory and supporting files:

```bash
mkdir -p plugins/csharp-compounding-docs/skills/cdocs-research/references
```

**Directory Structure**:
```
plugins/csharp-compounding-docs/skills/cdocs-research/
├── SKILL.md              # Main skill definition
└── references/
    └── agent-selection.md  # Agent selection guidance
```

---

### Task 101.2: Create SKILL.md Frontmatter

Create `plugins/csharp-compounding-docs/skills/cdocs-research/SKILL.md` with YAML frontmatter:

```yaml
---
name: cdocs:research
description: |
  Orchestrate research agents with knowledge-base context and capture significant findings.
  This skill coordinates multiple research agents (best-practices-researcher, framework-docs-researcher,
  git-history-analyzer, repo-research-analyst), synthesizes their findings using Sequential Thinking MCP,
  and routes discoveries to appropriate documentation capture skills.
allowed-tools:
  - Read
  - Write
  - Bash
  - mcp__sequential-thinking__sequentialthinking
  - mcp__csharp-compounding-docs__rag_query
preconditions:
  - Project activated via /cdocs:activate
  - Sequential Thinking MCP server available
---
```

**Key Properties**:
- No `auto-invoke` section - this is manual invocation only
- `mcp__sequential-thinking__sequentialthinking` for research planning, synthesis, and routing decisions
- `mcp__csharp-compounding-docs__rag_query` for pre-research context loading
- Bash allowed for spawning research agents

---

### Task 101.3: Create Research Planning Section

Add the research planning workflow that uses Sequential Thinking MCP:

```markdown
# Research Orchestration Skill

## Overview

This skill orchestrates research agents with compound docs knowledge-base context. It uses Sequential Thinking MCP for:
1. **Research Planning** - Analyzing the question and selecting relevant agents
2. **Finding Synthesis** - De-duplicating, reconciling, and categorizing agent findings
3. **Capture Routing** - Determining appropriate doc-types for significant findings

## Intake

**Input**: Research question or topic from user

**Manual Invocation Only**: This skill does not auto-invoke. User must explicitly request `/cdocs:research`.

## Process

### Step 1: Research Planning (Sequential Thinking)

Use Sequential Thinking MCP to plan the research:

**Thinking Steps**:
1. Analyze the research question - what domains does it touch?
2. Identify which agents are most relevant:
   - `best-practices-researcher` - External best practices, community patterns
   - `framework-docs-researcher` - Framework/library documentation
   - `git-history-analyzer` - Code evolution, commit patterns, bug-fix correlation
   - `repo-research-analyst` - Repository conventions, naming patterns
3. Determine if agents have dependencies (e.g., need repo context before best practices)
4. Plan execution order (parallel where possible, sequential when dependent)

**Output**: List of selected agents with execution plan
```

---

### Task 101.4: Create Agent Selection Menu

Add the agent selection interface:

```markdown
### Step 2: Agent Selection

Present the agent selection menu to the user:

~~~
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

Enter preset number or toggle individual agents:
~~~

**Preset Definitions**:
| Preset | Agents | Use Case |
|--------|--------|----------|
| Quick Research | best-practices, framework-docs | Fast answers, external guidance |
| Full Research | All 4 agents | Comprehensive analysis |
| Historical Focus | git-history, repo-research | Understanding code evolution |
| Custom | User-selected | Specific research needs |

**BLOCKING**: Wait for user selection before proceeding.
```

---

### Task 101.5: Create Pre-Research Context Loading

Add the context loading step using `rag_query`:

```markdown
### Step 3: Pre-Research Context Loading

Before launching agents, gather existing knowledge from compound docs:

1. **Query RAG for Related Knowledge**:
   ```
   Call MCP tool: rag_query
   Parameters:
     query: [Research question/topic]
     top_k: 10
     include_external: false
   ```

2. **Load Critical Documents**:
   - Filter results for `promotion_level: critical`
   - These MUST be considered in the research

3. **Present Context Summary**:
   ~~~
   Existing Knowledge Base Context:

   Related Documents Found: [count]
   - [doc-type]: [title] (relevance: [score])
   - [doc-type]: [title] (relevance: [score])
   ...

   Critical Documents (must consider):
   - [path]: [title]

   Proceeding with research agents...
   ~~~

**Purpose**: Agents receive this context to avoid duplicating existing knowledge and to build upon documented patterns.
```

---

### Task 101.6: Create Agent Orchestration Workflow

Add the agent execution workflow:

```markdown
### Step 4: Research Agent Orchestration

Launch selected research agents with knowledge-base context:

**For Each Selected Agent**:

1. **Spawn Agent**:
   ```bash
   # Using Claude Code's agent spawning mechanism
   # Each agent receives the research question + context summary
   ```

2. **Agent Context Injection**:
   Each agent receives:
   - Research question/topic
   - RAG context summary from Step 3
   - Specific focus areas from planning (Step 1)

3. **Parallel vs Sequential**:
   - **Parallel**: Agents without dependencies run simultaneously
   - **Sequential**: If planning identified dependencies, respect order

**Agent Execution Progress**:
~~~
Research Progress:

[x] best-practices-researcher - Complete (3 findings)
[x] framework-docs-researcher - Complete (2 findings)
[ ] git-history-analyzer - Running...
[ ] repo-research-analyst - Pending

Elapsed: 45s
~~~

**Collect Agent Findings**:
Store each agent's findings for synthesis:
- Finding text/content
- Source citations
- Confidence level
- Related code/files (if any)
```

---

### Task 101.7: Create Finding Synthesis (Sequential Thinking)

Add the synthesis workflow:

```markdown
### Step 5: Finding Synthesis (Sequential Thinking)

Use Sequential Thinking MCP to synthesize findings across all agents:

**Thinking Steps**:
1. **Collect All Findings**: Gather findings from all completed agents
2. **De-duplicate**: Identify overlapping findings that say the same thing
3. **Reconcile Conflicts**: When agents disagree:
   - Note the conflict explicitly
   - Weigh based on source authority (official docs > community patterns)
   - Present both perspectives if unresolvable
4. **Categorize Findings**:
   - Actionable recommendations (can implement now)
   - Background context (good to know)
   - Warnings/gotchas (avoid these pitfalls)
   - Further research needed (open questions)
5. **Rank by Relevance**: Order findings by applicability to the research question
6. **Synthesize Narrative**: Create coherent recommendations from individual findings

**Output**: Unified findings report with:
- Executive summary (1-2 sentences)
- Categorized findings
- Conflicts noted
- Confidence assessment
```

---

### Task 101.8: Create Capture Routing (Sequential Thinking)

Add the capture routing decision logic:

```markdown
### Step 6: Capture Routing (Sequential Thinking)

For significant findings, determine appropriate documentation capture:

**Thinking Steps**:
1. **Identify Capture-Worthy Findings**: Which findings are significant enough to document?
   - Novel patterns not in existing docs
   - Important gotchas/warnings
   - Resolved conflicts with clear winner
2. **Classify by Doc-Type**:
   | Finding Type | Recommended Doc-Type | Skill |
   |--------------|---------------------|-------|
   | Bug fix or error resolution | Problem | `/cdocs:problem` |
   | Architectural insight | Codebase | `/cdocs:codebase` |
   | Library/tool guidance | Tool | `/cdocs:tool` |
   | Coding convention | Style | `/cdocs:style` |
   | Project/product insight | Insight | `/cdocs:insight` |
3. **Check for Duplicates**: Query RAG to ensure not duplicating existing docs
4. **Generate Capture Suggestions**: For each capture-worthy finding, prepare:
   - Recommended doc-type
   - Draft title
   - Key content points

**Output**: List of capture suggestions with doc-type routing
```

---

### Task 101.9: Create Findings Presentation

Add the findings display:

```markdown
### Step 7: Present Findings

Display the synthesized research results:

~~~
Research Complete

Research Topic: [original question]

Executive Summary:
[1-2 sentence synthesis]

Findings by Category:

ACTIONABLE RECOMMENDATIONS:
1. [Finding] - Source: [agent/citation]
2. [Finding] - Source: [agent/citation]

BACKGROUND CONTEXT:
1. [Finding] - Source: [agent/citation]

WARNINGS/GOTCHAS:
1. [Finding] - Source: [agent/citation]

CONFLICTS NOTED:
- [Topic]: [Agent A] recommends X, [Agent B] recommends Y
  Resolution: [recommendation or "requires judgment"]

FURTHER RESEARCH:
- [Open question]

Related Internal Docs: [count] documents
- [doc-type]: [title] (relevance: [score])

Capture Suggestions:
- [Finding 1] → Suggest: /cdocs:[type] - "[draft title]"
- [Finding 2] → Suggest: /cdocs:[type] - "[draft title]"
~~~
```

---

### Task 101.10: Create Post-Research Decision Menu

Add the post-research workflow:

```markdown
### Step 8: Post-Research Options

Present the decision menu:

~~~
Research Complete

Findings Summary:
- Best Practices: [count] recommendations
- Framework Guidance: [count] relevant patterns
- Related Internal Docs: [count] documents
- Capture Suggestions: [count] findings worth documenting

What's next?
1. View detailed findings
2. Capture findings as documentation
3. Apply recommendations
4. Run additional research
5. Done
~~~

**Option Behaviors**:

**1. View Detailed Findings**:
- Show full findings report from Step 7
- Include all citations and sources
- Return to this menu after viewing

**2. Capture Findings as Documentation**:
- Present capture suggestions from Step 6
- User selects which to capture
- For each selection, invoke appropriate skill:
  - `/cdocs:problem` for problem/solution findings
  - `/cdocs:codebase` for architectural insights
  - `/cdocs:tool` for tool/library guidance
  - `/cdocs:style` for convention findings
  - `/cdocs:insight` for product/project insights
- Pre-populate skill with finding content
- Return to this menu after capture

**3. Apply Recommendations**:
- List actionable recommendations
- User selects which to implement
- Provide step-by-step guidance for implementation
- Track which recommendations were applied

**4. Run Additional Research**:
- Return to Step 1 with new research question
- Optionally carry forward existing context

**5. Done**:
- End skill execution
- Offer to save research session notes (optional)
```

---

### Task 101.11: Create Agent Selection Reference

Create `plugins/csharp-compounding-docs/skills/cdocs-research/references/agent-selection.md`:

```markdown
# Agent Selection Guide

## When to Use Each Agent

### best-practices-researcher

**Best For**:
- Learning community-accepted patterns
- Comparing approaches across projects
- Finding recommended implementations

**Sources**:
- Context7 framework documentation
- Microsoft Docs for .NET guidance
- Web search for community patterns
- Compound docs for internal patterns

**Example Questions**:
- "What's the best way to implement repository pattern in EF Core?"
- "How do other projects handle authentication?"

---

### framework-docs-researcher

**Best For**:
- Official API documentation
- Version-specific guidance
- Migration paths between versions

**Sources**:
- Context7 for general frameworks
- Microsoft Docs for .NET/C# specifics
- Compound docs for internal gotchas

**Example Questions**:
- "What changed in .NET 8 minimal APIs?"
- "How does IAsyncEnumerable work with EF Core?"

---

### git-history-analyzer

**Best For**:
- Understanding why code evolved
- Finding bug-prone areas
- Identifying refactoring opportunities

**Capabilities**:
- File change frequency
- Author patterns
- Commit message mining
- Bug-fix correlation

**Example Questions**:
- "Why was this service refactored?"
- "Which files change together most often?"

---

### repo-research-analyst

**Best For**:
- Learning project conventions
- Understanding organizational patterns
- Finding inconsistencies

**Analysis Areas**:
- Naming conventions
- Directory structure
- Test organization
- Configuration patterns

**Example Questions**:
- "What naming convention does this project use?"
- "How are integration tests organized?"

---

## Preset Recommendations

| Scenario | Recommended Preset |
|----------|-------------------|
| Quick guidance on implementation | Quick Research |
| Deep dive into unfamiliar area | Full Research |
| Understanding legacy code | Historical Focus |
| Specific targeted question | Custom (select relevant agent) |
```

---

## Verification Checklist

After completing all tasks, verify:

1. **Skill File Structure**:
   ```bash
   tree plugins/csharp-compounding-docs/skills/cdocs-research/
   ```
   Expected:
   ```
   cdocs-research/
   ├── SKILL.md
   └── references/
       └── agent-selection.md
   ```

2. **SKILL.md Validation**:
   - [ ] Frontmatter parses as valid YAML
   - [ ] `name: cdocs:research` matches convention
   - [ ] No `auto-invoke` section (manual only)
   - [ ] All required tools listed in `allowed-tools`
   - [ ] Preconditions include activation requirement

3. **Workflow Coverage**:
   - [ ] Step 1: Research Planning with Sequential Thinking
   - [ ] Step 2: Agent Selection Menu with presets
   - [ ] Step 3: Pre-Research Context Loading with `rag_query`
   - [ ] Step 4: Agent Orchestration workflow
   - [ ] Step 5: Finding Synthesis with Sequential Thinking
   - [ ] Step 6: Capture Routing with Sequential Thinking
   - [ ] Step 7: Findings Presentation format
   - [ ] Step 8: Post-Research Decision Menu

4. **MCP Integration**:
   - [ ] Sequential Thinking used for planning (Step 1)
   - [ ] `rag_query` used for context loading (Step 3)
   - [ ] Sequential Thinking used for synthesis (Step 5)
   - [ ] Sequential Thinking used for capture routing (Step 6)

5. **Agent Coordination**:
   - [ ] All 4 research agents referenced correctly
   - [ ] Parallel vs sequential execution explained
   - [ ] Context injection to agents documented

---

## Dependencies

| Phase | Dependency Type | Description |
|-------|-----------------|-------------|
| Phase 081 | Hard | Skills directory structure must exist |
| Phase 051 | Hard | `rag_query` MCP tool must be implemented |
| Phase 082-085 | Soft | Research agents should exist for full functionality |
| Phase 091-095 | Soft | Capture skills should exist for routing |

---

## Notes

- This skill is the most complex utility skill due to multi-agent coordination
- Sequential Thinking MCP is used three times: planning, synthesis, and routing
- The skill assumes research agents exist but can function with partial agent availability
- Capture routing suggestions are optional - user can decline all captures
- No auto-invoke - user must explicitly request research orchestration

---

## Change Log

| Date | Changes |
|------|---------|
| 2025-01-24 | Initial phase creation |
