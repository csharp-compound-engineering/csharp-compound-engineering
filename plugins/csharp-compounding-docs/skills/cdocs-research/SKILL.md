---
name: cdocs:research
description: Orchestrate research agents with knowledge-base context integration and automated finding capture
allowed-tools:
  - Bash
  - Read
  - WebSearch
  - mcp__sequential-thinking__sequentialthinking
preconditions:
  - Project activated via /cdocs:activate
  - Sequential Thinking MCP available
---

# Research Orchestration Skill

## Purpose

Orchestrate research agents with compound documentation context. Integrate knowledge-base findings with external research, synthesize results, and capture significant discoveries.

## Invocation

**Manual** - User wants research with compounding docs integration.

**Trigger Phrases**:
- "research X with compound docs"
- "investigate X using knowledge base"
- "what do we know about X"
- "research best practices for X"

## Research Agents

**Available Agents**:

1. **best-practices-researcher**
   - External best practices + internal documented patterns
   - Sources: Industry blogs, documentation, past internal solutions

2. **framework-docs-researcher**
   - Framework documentation + internal usage context
   - Sources: Official docs, internal documented usage patterns

3. **git-history-analyzer**
   - Code evolution analysis + documented decisions
   - Sources: Git history, commit messages, related documentation

4. **repo-research-analyst**
   - Repository conventions + documented standards
   - Sources: Codebase patterns, internal style guides

## Process

### Step 1: Research Planning (Sequential Thinking)

Use Sequential Thinking MCP to analyze the research question and plan execution.

**Thinking Process**:
```
Thought 1: Analyze research question
- What is the user trying to learn?
- What domains are relevant?
- What prior knowledge might exist?

Thought 2: Determine relevant agents
- Which agents can contribute?
- Are there dependencies between agents?
- What's the execution order?

Thought 3: Plan context loading
- What compound docs are relevant?
- Should we load critical docs first?
- What search terms for RAG?

Final: Execution plan
- Selected agents: [list]
- Execution order: sequential or parallel
- Context loading strategy: [approach]
```

**Output**: Research plan with selected agents and execution strategy.

### Step 2: Pre-Research Context Loading

Before launching agents, load relevant documented knowledge.

**Actions**:

1. **Query RAG for Context**:
   - Use broad semantic search for research topic
   - Load critical and important level docs first
   - Example: `/cdocs:query "authentication patterns" --level=critical,important`

2. **Extract Key Insights**:
   - Parse returned documentation
   - Summarize existing knowledge
   - Identify gaps that research should fill

3. **Present Context Summary**:
   ```
   Pre-Research Context
   ====================

   Loaded Knowledge:
   - 3 critical documents on authentication
   - 5 important documents on security patterns
   - 2 past problems related to auth flows

   Key Insights:
   - JWT token validation pattern established
   - Previous OAuth integration issue documented
   - Security best practices from 2024-12 audit

   Research Focus:
   - Latest OAuth 2.1 changes
   - Industry best practices not yet documented
   - Framework-specific implementation guidance
   ```

### Step 3: Agent Selection Menu

Present interactive agent selection:

```
Research-Informed Review
========================

Research Question: "OAuth 2.1 implementation best practices"

Select research agents (space to toggle, enter to confirm):

Research Agents:
[x] best-practices-researcher
    - External best practices with compound docs
    - Focus: Industry standards, security patterns

[x] framework-docs-researcher
    - Framework documentation + internal context
    - Focus: .NET OAuth libraries, integration patterns

[ ] git-history-analyzer
    - Code evolution analysis
    - Focus: Past authentication implementations

[ ] repo-research-analyst
    - Repository conventions
    - Focus: Current codebase patterns

Presets:
1. Quick Research (best practices + framework docs)
2. Full Research (all agents)
3. Historical Focus (git + repo analysis)
4. Custom selection (current)

Selection: 1-4 or Enter to proceed
```

**Preset Configurations**:

| Preset | Agents | Use Case |
|--------|--------|----------|
| Quick Research | best-practices + framework-docs | Fast external guidance |
| Full Research | All agents | Comprehensive investigation |
| Historical Focus | git-history + repo-research | Understanding past decisions |
| Custom | User-selected | Targeted investigation |

### Step 4: Research Agent Orchestration

Launch selected agents with compound docs context.

**For Each Selected Agent**:

1. **Prepare Agent Context**:
   - Provide research question
   - Include relevant compound docs
   - Set agent-specific focus areas

2. **Execute Agent**:
   - Run agent research process
   - Augment with compound docs
   - Collect findings

3. **Track Progress**:
   ```
   Research in Progress
   ====================

   ✓ best-practices-researcher (completed in 45s)
   ⏳ framework-docs-researcher (30s elapsed)
   ⏸ git-history-analyzer (waiting)
   ⏸ repo-research-analyst (waiting)
   ```

**Agent Execution Modes**:

- **Sequential**: Execute agents one at a time (safer, more context)
- **Parallel**: Execute agents simultaneously (faster)
- **Hybrid**: Core agents sequential, supplementary parallel

### Step 5: Finding Synthesis (Sequential Thinking)

Use Sequential Thinking MCP to synthesize findings from all agents.

**Synthesis Process**:
```
Thought 1: Collect all findings
- best-practices: 5 recommendations
- framework-docs: 3 implementation patterns
- Internal docs: 2 related past solutions

Thought 2: De-duplicate overlapping findings
- Finding A from best-practices = Finding B from framework-docs
- Consolidate into single recommendation

Thought 3: Reconcile conflicts
- Best practice X conflicts with internal pattern Y
- Evaluate: Is internal pattern outdated?
- Decision: Recommend updating internal pattern

Thought 4: Categorize by relevance
- Critical: Must implement (security requirements)
- Important: Should implement (best practices)
- Optional: Nice to have (optimizations)

Thought 5: Assess actionability
- Immediately actionable: Clear next steps
- Requires investigation: Need more context
- Informational only: Good to know

Final: Synthesized recommendations
- 3 critical actions
- 5 important considerations
- 2 informational insights
- Links to 4 related internal docs
```

**Output**: Coherent, prioritized recommendations with supporting evidence.

### Step 6: Present Research Results

Display synthesized findings with clear categorization.

```
Research Complete
=================

Research Question: OAuth 2.1 implementation best practices

Critical Findings (3)
---------------------
1. OAuth 2.1 deprecates implicit flow
   - Source: best-practices-researcher
   - Internal Context: We use implicit flow in 2 services
   - Action: Migrate to authorization code + PKCE
   - Related: ./csharp-compounding-docs/problems/oauth-security-20241215.md

2. Refresh token rotation required
   - Source: framework-docs-researcher
   - Internal Context: Not currently implemented
   - Action: Implement in AuthService
   - Framework: Microsoft.AspNetCore.Authentication.OAuth supports this

3. PKCE now mandatory for all clients
   - Source: best-practices-researcher
   - Internal Context: Only used for mobile apps
   - Action: Update web client implementation

Important Considerations (5)
-----------------------------
1. Use secure token storage
   - Source: best-practices-researcher
   - Internal Context: Documented in style guide
   - Status: Already implemented ✓

[... more findings ...]

Informational Insights (2)
--------------------------
1. OAuth 2.1 consolidates best practices
   - Source: framework-docs-researcher
   - Context: Formalizes patterns we already follow

Related Internal Documentation
------------------------------
- ./csharp-compounding-docs/problems/oauth-security-20241215.md
- ./csharp-compounding-docs/codebase/auth-service-architecture-20241120.md
- ./csharp-compounding-docs/style/oauth-implementation-guide-20240901.md
- ./csharp-compounding-docs/tools/jwt-validation-library-20240815.md

Gaps in Internal Documentation
-------------------------------
- No documented pattern for refresh token rotation
- Missing guidance on PKCE implementation
- OAuth 2.1 migration strategy not documented

Recommended Captures:
1. Problem: Implicit flow deprecation and migration plan
2. Codebase: OAuth 2.1 architecture decision
3. Style: Updated OAuth implementation guide
```

### Step 7: Capture Routing (Sequential Thinking)

Use Sequential Thinking MCP to determine which findings should be captured.

**Capture Decision Process**:
```
Thought 1: Evaluate finding significance
- Is this a novel discovery?
- Will this be referenced in future?
- Does this fill a documentation gap?

Thought 2: Determine appropriate doc-type
- Problem: Issues discovered + solutions
- Codebase: Architecture/design decisions
- Style: Implementation patterns/guidelines
- Tool: Library/framework usage
- Insight: General learnings

Thought 3: Check for existing docs
- Search for related documentation
- Avoid duplicating existing content
- Consider updating vs. creating new

Thought 4: Prioritize captures
- Critical: Must document (security, breaking changes)
- Important: Should document (best practices, patterns)
- Optional: Nice to document (general knowledge)

Final: Capture recommendations
- Create new: [list with doc-types]
- Update existing: [list with paths]
- Skip: [list with reasons]
```

### Step 8: Offer Capture Options

Present capture menu for significant findings.

```
Capture Research Findings
=========================

Recommended captures for this research:

1. Problem Documentation
   ✓ OAuth implicit flow deprecation
   - Status: New finding, fills gap
   - Captures: Issue + migration plan
   - Skill: /cdocs:problem

2. Architecture Documentation
   ✓ OAuth 2.1 migration architecture
   - Status: Updates existing auth architecture
   - Updates: ./csharp-compounding-docs/codebase/auth-service-architecture.md
   - Skill: /cdocs:codebase

3. Style Guide Update
   ✓ OAuth 2.1 implementation patterns
   - Status: Updates existing style guide
   - Updates: ./csharp-compounding-docs/style/oauth-implementation-guide.md
   - Skill: /cdocs:style

Capture Options:
1. Capture all recommended (guided workflow)
2. Capture selected findings
3. Manual capture later
4. Skip capture, continue workflow

Selection (1-4):
```

### Step 9: Execute Captures

For each selected capture:

1. **Prepare Capture Context**:
   - Extract relevant finding details
   - Include supporting evidence
   - Link related documentation

2. **Invoke Capture Skill**:
   - Call appropriate `/cdocs:{type}` skill
   - Pre-populate with research findings
   - Guide user through capture process

3. **Link Research to Documentation**:
   - Reference research date in frontmatter
   - Link between related captures
   - Update existing docs if needed

**Example Capture Flow**:
```
Capturing: OAuth implicit flow deprecation

Invoking /cdocs:problem...

Problem: OAuth Implicit Flow Deprecation
=========================================

Research Date: 2025-01-25
Research Question: OAuth 2.1 implementation best practices
Source: best-practices-researcher

Symptom:
OAuth 2.1 deprecates implicit flow, which we currently use.

Root Cause:
Implicit flow has known security vulnerabilities. OAuth 2.1
consolidates best practices and removes deprecated flows.

Solution:
Migrate to authorization code flow with PKCE for all client types.

[Proceeding with capture workflow...]
```

### Step 10: Post-Research Menu

After research and optional captures complete:

```
Research Session Complete
=========================

Summary:
- Agents Run: 2 (best-practices, framework-docs)
- Findings: 10 (3 critical, 5 important, 2 informational)
- Related Docs: 4 existing documents found
- New Docs: 3 captured

Documentation Created:
✓ ./csharp-compounding-docs/problems/oauth-implicit-deprecation-20250125.md
✓ ./csharp-compounding-docs/codebase/oauth21-migration-architecture-20250125.md
✓ Updated: ./csharp-compounding-docs/style/oauth-implementation-guide.md

What's next?
============
1. View research summary
2. View captured documentation
3. Run additional research
4. Apply recommendations
5. Done
```

## MCP Integration

### Sequential Thinking MCP

**Package**: `@modelcontextprotocol/server-sequential-thinking`

**Used For**:
- Research planning (Step 1)
- Finding synthesis (Step 5)
- Capture routing decisions (Step 7)

**Why Sequential Thinking**:
- Multi-agent coordination requires thoughtful planning
- Synthesizing diverse findings needs careful analysis
- Capture decisions require evaluation of significance and novelty
- Helps avoid duplicate documentation

**Configuration**:
```json
{
  "mcpServers": {
    "sequential-thinking": {
      "command": "npx",
      "args": [
        "-y",
        "@modelcontextprotocol/server-sequential-thinking"
      ]
    }
  }
}
```

## Research Patterns

### Pattern 1: Quick External Research

**Use Case**: Need external best practices fast

**Flow**:
1. Select preset: Quick Research
2. Agents: best-practices + framework-docs
3. Execute in parallel
4. Synthesize findings
5. Optional capture

**Duration**: 1-3 minutes

### Pattern 2: Comprehensive Investigation

**Use Case**: Deep dive into complex topic

**Flow**:
1. Select preset: Full Research
2. Load all critical compound docs
3. Execute all agents
4. Thorough synthesis with Sequential Thinking
5. Multiple captures likely

**Duration**: 5-10 minutes

### Pattern 3: Historical Context

**Use Case**: Understand past decisions

**Flow**:
1. Select preset: Historical Focus
2. Load related past documentation
3. Agents: git-history + repo-research
4. Trace decision evolution
5. Update existing docs

**Duration**: 3-5 minutes

### Pattern 4: Knowledge Gap Fill

**Use Case**: Specific documentation gap identified

**Flow**:
1. Custom agent selection
2. Targeted search in compound docs
3. Execute relevant external research
4. Synthesize to fill gap
5. Capture new documentation

**Duration**: 2-4 minutes

## Integration with Other Skills

**Before Research**:
- `/cdocs:activate` - Ensure project context active
- `/cdocs:query` - Check existing knowledge

**During Research**:
- `/cdocs:search` - Find related internal docs
- `/cdocs:query-external` - Access external documentation

**After Research**:
- `/cdocs:problem` - Capture discovered issues
- `/cdocs:codebase` - Document architecture insights
- `/cdocs:style` - Capture best practices
- `/cdocs:tool` - Document library usage
- `/cdocs:insight` - Capture general learnings
- `/cdocs:promote` - Elevate critical findings

## Examples

### Example 1: OAuth Research

```
User: Research OAuth 2.1 implementation best practices