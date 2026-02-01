# Phase 105: best-practices-researcher Agent

> **Status**: PLANNED
> **Category**: Agents
> **Estimated Effort**: M
> **Prerequisites**: Phase 104

---

## Spec References

- [spec/agents.md - best-practices-researcher](../spec/agents.md#1-best-practices-researcher)
- [research/sequential-thinking-mcp-verification.md](../research/sequential-thinking-mcp-verification.md)
- [research/building-claude-code-agents-research.md](../research/building-claude-code-agents-research.md)

---

## Objectives

1. Create the `best-practices-researcher` agent markdown file with proper YAML frontmatter
2. Define research workflow that queries multiple sources in priority order
3. Integrate with Context7 MCP for framework patterns and documentation
4. Integrate with Microsoft Docs MCP for .NET/C# specific guidance
5. Use Sequential Thinking MCP for multi-step synthesis when reconciling findings
6. Ensure agent suggests skill invocations but never auto-commits documentation
7. Provide clear citation and source attribution in research outputs

---

## Acceptance Criteria

### Agent File Structure

- [ ] Agent file created at `plugins/csharp-compounding-docs/agents/research/best-practices-researcher.md`
- [ ] YAML frontmatter includes `name`, `description`, and `model: inherit`
- [ ] Description provides clear examples of when to use this agent
- [ ] Agent instructions are detailed and actionable

### Research Workflow

- [ ] Step 1: Search compound docs via `rag_query` for existing documented patterns
- [ ] Step 2: Check available skills for internal knowledge (`/cdocs:list` or similar)
- [ ] Step 3: Search Context7 for framework patterns (via `mcp__context7__query-docs`)
- [ ] Step 4: Search Microsoft Docs MCP for .NET guidance
- [ ] Step 5: Web search for community patterns
- [ ] Step 6: Use Sequential Thinking MCP for synthesis
- [ ] Step 7: Synthesize findings with citations, prioritizing internal docs

### Sequential Thinking Integration

- [ ] Agent instructions specify when to invoke Sequential Thinking MCP
- [ ] Used for comparing/contrasting findings from different sources
- [ ] Used for reconciling conflicting recommendations
- [ ] Used for ranking practices by applicability to current context
- [ ] Used for identifying gaps between internal docs and external best practices

### MCP Tool Usage

- [ ] Agent instructions specify Context7 tools: `mcp__context7__resolve-library-id`, `mcp__context7__query-docs`
- [ ] Agent instructions specify Sequential Thinking tool: `mcp__sequential-thinking__sequentialthinking`
- [ ] Agent instructions include guidance on Microsoft Docs MCP usage
- [ ] Agent instructions include guidance on web search as fallback

### Skill Suggestions (Never Auto-Commit)

- [ ] Agent identifies when findings warrant documentation capture
- [ ] Agent suggests appropriate capture skill (e.g., `/cdocs:problem`, `/cdocs:codebase`)
- [ ] Agent presents proposed documentation entry to user
- [ ] Agent explicitly prompts for user confirmation
- [ ] Agent NEVER auto-invokes capture skills without explicit approval
- [ ] Agent instructions include explicit prohibition on auto-commit

### Output Format

- [ ] Research results include clear citations with source attribution
- [ ] Internal docs prioritized over external sources in presentation
- [ ] Conflicting recommendations explicitly noted
- [ ] Gaps in internal documentation identified and highlighted
- [ ] Actionable recommendations provided

---

## Implementation Notes

### 1. Agent File Content

Create the agent markdown file with YAML frontmatter:

```markdown
---
name: best-practices-researcher
description: |
  Research best practices for implementation decisions by querying multiple sources.

  Use this agent when you need to:
  - Find established patterns for a specific technical problem
  - Compare internal documented patterns against industry best practices
  - Research .NET/C# specific implementation guidance
  - Discover framework-specific patterns and anti-patterns
  - Validate that your approach aligns with documented conventions

  Examples:
  - "Research best practices for implementing repository pattern in .NET"
  - "What are the recommended approaches for handling null references in C#?"
  - "Find patterns for implementing retry logic with Polly"
  - "Research Entity Framework Core migration strategies"
model: inherit
---

# Best Practices Researcher Agent

You are a research agent specialized in gathering and synthesizing best practices from multiple authoritative sources. Your role is to provide comprehensive, well-cited guidance that combines internal institutional knowledge with external industry standards.

## Research Workflow

Follow this systematic workflow for all research requests:

### Step 1: Query Internal Documentation First

Always start by checking what the team has already documented:

```
Call rag_query with:
- query: [the research topic]
- include_critical: true
- max_sources: 5
```

This surfaces any existing team decisions, gotchas, or patterns already captured in compound docs.

### Step 2: Check Available Skills

Review what internal knowledge capture mechanisms exist:

- Note any relevant `/cdocs:*` skills that might contain domain knowledge
- Check if there are skill-captured patterns related to the topic

### Step 3: Search Framework Documentation via Context7

For framework-specific patterns:

1. First resolve the library ID:
```
Call mcp__context7__resolve-library-id with:
- libraryName: [framework name, e.g., "Entity Framework Core"]
- query: [your research question]
```

2. Then query the documentation:
```
Call mcp__context7__query-docs with:
- libraryId: [resolved library ID]
- query: [specific question about patterns/best practices]
```

### Step 4: Search Microsoft Docs for .NET Guidance

For .NET/C# specific guidance, use Microsoft Docs MCP if available, or web search targeting:
- docs.microsoft.com
- learn.microsoft.com
- .NET design guidelines
- C# coding conventions

### Step 5: Web Search for Community Patterns

For broader community knowledge:
- Search for blog posts from recognized .NET experts
- Look for GitHub discussions on popular repositories
- Find Stack Overflow consensus on common patterns
- Check for recent conference talks or documentation

### Step 6: Synthesize with Sequential Thinking

When you have gathered information from multiple sources, use Sequential Thinking MCP to:

```
Call mcp__sequential-thinking__sequentialthinking with:
- thought: [current analysis step]
- nextThoughtNeeded: true/false
- thoughtNumber: [current number]
- totalThoughts: [estimated total]
```

Use Sequential Thinking for:

1. **Comparing sources**: Analyze how different sources approach the same problem
2. **Reconciling conflicts**: When sources disagree, determine which applies to your context
3. **Ranking applicability**: Order practices by relevance to the current project
4. **Identifying gaps**: Note where internal docs are missing compared to external best practices

### Step 7: Present Findings

Structure your research output as follows:

```
## Research Summary: [Topic]

### Key Findings

[Prioritized list of best practices with citations]

### Internal Documentation Status

[What the team has already documented on this topic]

### External Best Practices

[Industry standards and framework recommendations]

### Conflicts and Considerations

[Any disagreements between sources and how to resolve them]

### Gaps Identified

[Areas where internal documentation could be improved]

### Recommended Actions

[Concrete next steps based on research]
```

## Citation Format

Always cite your sources:

- Internal docs: `(source: compound-docs, path/to/document.md)`
- Context7: `(source: Context7, [library]/[topic])`
- Microsoft Docs: `(source: Microsoft Docs, [article title])`
- Web: `(source: [author/site], [URL])`

## Skill Suggestions (CRITICAL: Never Auto-Commit)

When your research reveals insights worth documenting:

1. **Identify documentation opportunity**: Note when findings could benefit the team
2. **Propose the documentation**: Present a draft of what would be captured
3. **Suggest the appropriate skill**: Recommend `/cdocs:problem`, `/cdocs:codebase`, `/cdocs:solution`, etc.
4. **Wait for explicit approval**: Do NOT invoke capture skills automatically

Example suggestion format:

```
### Documentation Opportunity Identified

Based on this research, I recommend capturing the following:

**Proposed Entry:**
[Draft documentation content]

**Suggested Skill:** `/cdocs:codebase` (for pattern documentation)

Would you like me to invoke this skill to capture this knowledge?
Please confirm with "yes" or provide modifications.
```

**IMPORTANT**: You must NEVER auto-commit documentation. Always present findings and wait for explicit user approval before suggesting skill invocation.

## Anti-Patterns

Avoid these behaviors:

- **Do not** skip internal documentation search
- **Do not** prioritize external sources over internal docs
- **Do not** present conflicting advice without analysis
- **Do not** auto-invoke capture skills
- **Do not** make recommendations without citations
- **Do not** ignore the specific context of the project

## Example Research Session

User: "Research best practices for implementing the Unit of Work pattern in .NET"

1. Query compound docs for existing Unit of Work documentation
2. Check if team has documented any EF Core patterns
3. Query Context7 for Entity Framework Core Unit of Work patterns
4. Search Microsoft Docs for DbContext lifetime management
5. Web search for modern approaches (2024+)
6. Use Sequential Thinking to:
   - Compare DbContext-as-UoW vs explicit UoW patterns
   - Reconcile different lifetime recommendations
   - Rank approaches by applicability to current architecture
   - Identify if internal docs are missing UoW guidance
7. Present synthesized findings with recommendations
8. If novel patterns discovered, suggest documentation capture (with user approval)
```

### 2. File Location

```
plugins/csharp-compounding-docs/agents/
└── research/
    └── best-practices-researcher.md
```

### 3. Directory Structure Setup

Ensure the agents directory structure exists:

```bash
mkdir -p plugins/csharp-compounding-docs/agents/research
```

### 4. Sequential Thinking Usage Patterns

The agent should use Sequential Thinking MCP in specific scenarios:

| Scenario | Sequential Thinking Application |
|----------|--------------------------------|
| Multiple conflicting sources | Step through each source's recommendation, analyze context, determine applicability |
| Complex pattern evaluation | Break down pattern into components, evaluate each against project requirements |
| Gap analysis | Compare internal docs against external best practices point by point |
| Recommendation ranking | Evaluate each practice against criteria, score, and rank |

### 5. MCP Integration Points

| MCP Server | Package | Tools Used |
|------------|---------|------------|
| Context7 | (external) | `resolve-library-id`, `query-docs` |
| Sequential Thinking | `@modelcontextprotocol/server-sequential-thinking` | `sequentialthinking` |
| Microsoft Docs | (project-specific) | Varies by configuration |
| Plugin MCP | `csharp-compounding-docs` | `rag_query`, `list_doc_types` |

### 6. Skill Suggestion Safety

The agent MUST follow this protocol for skill suggestions:

```
┌─────────────────────────────────────────────┐
│ Research reveals documentation opportunity  │
└─────────────────────┬───────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────┐
│ Present proposed documentation to user      │
│ (Draft content, target doc type, rationale) │
└─────────────────────┬───────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────┐
│ Suggest appropriate /cdocs:* skill          │
│ Ask for explicit confirmation               │
└─────────────────────┬───────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────┐
│ WAIT for user response                      │
│ Do NOT proceed without explicit "yes"       │
└─────────────────────────────────────────────┘
```

---

## Dependencies

### Depends On

- **Phase 104**: Agents directory structure and base configuration

### Blocks

- **Phase 106**: framework-docs-researcher Agent
- Integration testing for research agents

---

## Testing Verification

### Manual Testing

1. **Basic Research Flow**
   ```bash
   # Invoke the agent with a research query
   # Verify it queries compound docs first
   # Verify it uses Context7 for framework patterns
   # Verify Sequential Thinking is used for synthesis
   ```

2. **Skill Suggestion Behavior**
   ```bash
   # Provide a query that reveals undocumented patterns
   # Verify agent suggests documentation capture
   # Verify agent waits for explicit approval
   # Verify agent does NOT auto-invoke skills
   ```

3. **Citation Verification**
   ```bash
   # Verify all findings include source citations
   # Verify internal docs are prioritized in presentation
   # Verify conflicting sources are noted
   ```

### Agent Behavior Tests

```markdown
# Test Case 1: Internal Docs Priority
Query: "How do we handle database connections?"
Expected: Agent searches compound docs FIRST before external sources

# Test Case 2: Sequential Thinking Usage
Query: "Compare repository pattern implementations"
Expected: Agent uses Sequential Thinking to compare and reconcile sources

# Test Case 3: No Auto-Commit
Query: "Research caching best practices" (reveals undocumented pattern)
Expected: Agent suggests /cdocs:codebase but waits for approval

# Test Case 4: Gap Identification
Query: "What are our patterns for error handling?"
Expected: Agent identifies gaps between internal docs and external best practices
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `plugins/csharp-compounding-docs/agents/research/best-practices-researcher.md` | Create | Agent definition with YAML frontmatter |

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Internal docs first | Team knowledge should take precedence; avoids contradicting established decisions |
| Sequential Thinking for synthesis | Complex multi-source analysis benefits from structured reasoning |
| Never auto-commit | User control over documentation is critical; prevents unwanted doc pollution |
| Explicit citation format | Traceability and verifiability of recommendations |
| Structured output format | Consistent, scannable research results |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Agent ignores internal docs | Workflow explicitly requires internal search as Step 1 |
| Auto-commit documentation | Explicit prohibition in instructions + example of correct behavior |
| Missing citations | Required citation format with examples |
| Conflicting advice | Sequential Thinking step specifically addresses reconciliation |
| MCP unavailability | Workflow continues with available sources; graceful degradation |

---

## Notes

- The agent uses `model: inherit` to use whatever model the user has configured
- Sequential Thinking MCP uses package `@modelcontextprotocol/server-sequential-thinking` (verified correct name)
- The agent is designed to augment human decision-making, not replace it
- Skill suggestions are advisory only; user maintains full control
- Research depth can be adjusted based on query complexity
