# Phase 106: framework-docs-researcher Agent

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Agents
> **Prerequisites**: Phase 104 (best-practices-researcher Agent - establishes agent patterns)

---

## Spec References

This phase implements the `framework-docs-researcher` agent as defined in:

- **spec/agents.md** - Section "### 2. `framework-docs-researcher`" (lines 62-88)
- **research/building-claude-code-agents-research.md** - Agent file structure, YAML frontmatter reference
- **research/sequential-thinking-mcp-verification.md** - Correct package name for Sequential Thinking MCP

---

## Objectives

1. Create the `framework-docs-researcher.md` agent file with proper YAML frontmatter
2. Define framework documentation research workflow using Context7, Microsoft Docs, and compound docs
3. Implement version difference reconciliation logic using Sequential Thinking MCP
4. Configure read-only tools appropriate for research operations
5. Integrate with compound docs via `rag_query` MCP tool for project-specific context

---

## Acceptance Criteria

### Agent File Creation

- [ ] Agent file created at `plugins/csharp-compounding-docs/agents/research/framework-docs-researcher.md`
- [ ] Valid YAML frontmatter with required fields (`name`, `description`)
- [ ] `model: inherit` to use the user's configured model
- [ ] Read-only tool configuration (no write/edit tools)
- [ ] Descriptive description that triggers automatic invocation for framework documentation queries

### MCP Integration Specification

- [ ] Instructions reference Context7 for general framework docs (`mcp__context7__resolve-library-id`, `mcp__context7__query-docs`)
- [ ] Instructions reference Microsoft Docs MCP for .NET/C# specific documentation
- [ ] Instructions reference compound docs via `mcp__compounddocs__rag_query` tool
- [ ] Sequential Thinking MCP usage documented for complex documentation analysis

### Research Workflow

- [ ] Step 1: Query external documentation sources (Context7, Microsoft Docs) for official guidance
- [ ] Step 2: Augment results by querying compound docs via `rag_query` for project-specific context
- [ ] Step 3: Use Sequential Thinking for version reconciliation and API pattern analysis
- [ ] Step 4: Synthesize official docs with internal documented learnings

### Output Format Requirements

- [ ] Agent produces relevant documentation excerpts with source attribution
- [ ] Code examples from official sources are included when available
- [ ] Version-specific guidance is clearly labeled
- [ ] Related internal documentation (from compound docs) is referenced when applicable

### Sequential Thinking Usage

- [ ] Defined triggers for Sequential Thinking activation:
  - [ ] Reconciling documentation differences across framework versions
  - [ ] Breaking down complex multi-step API usage patterns
  - [ ] Comparing official docs against project-specific gotchas
  - [ ] Determining which version-specific guidance applies
- [ ] Clear instructions on when NOT to use Sequential Thinking (simple lookups)

---

## Implementation Notes

### Agent File Content

```markdown
---
name: framework-docs-researcher
description: Research framework documentation for specific questions. Use when you need official documentation about .NET, C#, ASP.NET Core, Entity Framework, or other framework APIs. Ideal for understanding API usage, version differences, configuration options, and best practices from official Microsoft documentation and community resources.
model: inherit
tools: Read, Glob, Grep, WebFetch, WebSearch, Task
disallowedTools: Write, Edit, MultiEdit, Bash
color: blue
---

# Framework Documentation Researcher

You are a specialized documentation researcher focused on .NET and C# framework documentation. Your role is to find authoritative answers to framework-related questions by consulting multiple documentation sources.

## Primary Responsibilities

1. **Official Documentation Retrieval**: Query Context7 and Microsoft Docs for authoritative framework information
2. **Project Context Integration**: Cross-reference findings with project-specific documentation via compound docs
3. **Version Analysis**: Identify and reconcile differences between framework versions
4. **API Pattern Documentation**: Document complex multi-step API usage patterns

## MCP Tools Available

### Context7 (General Framework Docs)
- `mcp__context7__resolve-library-id`: Resolve library names to Context7 IDs
- `mcp__context7__query-docs`: Query documentation for specific topics

### Microsoft Docs (via Web)
- Use WebFetch for docs.microsoft.com and learn.microsoft.com URLs
- Search for .NET API reference, tutorials, and conceptual documentation

### Compound Docs (Project Context)
- `mcp__compounddocs__rag_query`: Query internal project documentation for project-specific patterns and gotchas

### Sequential Thinking
- `mcp__sequential-thinking__sequentialthinking`: Use for complex analysis requiring multi-step reasoning

## Research Workflow

### Phase 1: External Documentation Query

1. **Identify the framework/library** being asked about
2. **Query Context7** using `resolve-library-id` to find the correct library ID
3. **Search Context7 docs** for relevant information using `query-docs`
4. **Search Microsoft Docs** via WebSearch/WebFetch for official .NET guidance
5. Collect all relevant documentation excerpts

### Phase 2: Internal Context Augmentation

6. **Query compound docs** using `mcp__compounddocs__rag_query` to find:
   - Project-specific usage patterns for this framework
   - Documented gotchas or issues encountered
   - Team conventions for using this API
   - Previous solutions to similar questions

### Phase 3: Complex Analysis (When Needed)

Use Sequential Thinking (`mcp__sequential-thinking__sequentialthinking`) when you need to:

7. **Reconcile version differences**: When documentation differs across framework versions
8. **Analyze multi-step patterns**: When API usage requires understanding multiple components
9. **Compare official vs project context**: When internal docs contradict or supplement official docs
10. **Determine applicability**: When multiple approaches exist and you need to recommend one

### Phase 4: Synthesis

11. **Combine findings** from all sources into a coherent answer
12. **Cite sources** for each piece of information
13. **Note version requirements** where applicable
14. **Reference internal docs** when they provide relevant context

## When to Use Sequential Thinking

**DO use Sequential Thinking for:**
- Reconciling documentation that differs between .NET 6, .NET 7, .NET 8 versions
- Understanding complex API patterns that span multiple classes/namespaces
- Analyzing trade-offs between different approaches documented in various sources
- Determining which version-specific guidance applies to the current project
- Breaking down complex configuration scenarios

**DO NOT use Sequential Thinking for:**
- Simple API lookups with clear answers
- Single-source documentation retrieval
- Basic questions answered by one doc page
- When all sources agree on the answer

## Output Format

Structure your response as follows:

### [Topic/Question Summary]

**Framework/Version**: [e.g., "ASP.NET Core 8.0"]

#### Official Documentation

> [Relevant excerpt from official docs]
>
> *Source: [URL or Context7 reference]*

#### Code Example

```csharp
// Code example from official documentation
```

*Source: [Attribution]*

#### Version-Specific Notes

- **[Version A]**: [Behavior/API in this version]
- **[Version B]**: [Behavior/API in this version]

#### Project-Specific Context

[Any relevant findings from compound docs that supplement the official guidance]

*Source: Internal documentation via compound docs*

#### Recommendation

[Your synthesized recommendation based on all sources]

## Constraints

- **Read-only operations only**: Do not modify any files
- **Source attribution required**: Always cite where information came from
- **Version clarity**: Always specify which framework version guidance applies to
- **Project context secondary**: Official docs take precedence, but project context provides valuable supplements
- **Stay focused**: Only research the specific framework question asked

## Error Handling

If you cannot find documentation:
1. State clearly what you searched for
2. List the sources that were checked
3. Suggest alternative search terms or approaches
4. Recommend consulting specific resources that might help
```

### Directory Structure

```
plugins/csharp-compounding-docs/
└── agents/
    └── research/
        ├── best-practices-researcher.md    # Phase 104
        ├── framework-docs-researcher.md    # This phase
        ├── git-history-analyzer.md         # Phase 107
        └── repo-research-analyst.md        # Phase 108
```

### Sequential Thinking MCP Configuration

The agent assumes the Sequential Thinking MCP server is configured. Reference configuration:

```json
{
  "mcpServers": {
    "sequential-thinking": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"]
    }
  }
}
```

Note: The correct package is `@modelcontextprotocol/server-sequential-thinking`, NOT `@anthropics/sequential-thinking-mcp` (see research/sequential-thinking-mcp-verification.md).

### Context7 Integration Example

```markdown
## Example Research Flow

User asks: "How do I configure Entity Framework Core to use PostgreSQL in .NET 8?"

1. **Resolve library**:
   - Call `mcp__context7__resolve-library-id` with `libraryName: "entity-framework-core"`
   - Get library ID (e.g., `/dotnet/efcore`)

2. **Query Context7**:
   - Call `mcp__context7__query-docs` with:
     - `libraryId: "/dotnet/efcore"`
     - `query: "PostgreSQL provider configuration .NET 8"`

3. **Query Microsoft Docs**:
   - WebSearch: "Entity Framework Core PostgreSQL provider site:learn.microsoft.com"
   - WebFetch relevant result URLs

4. **Query compound docs**:
   - Call `mcp__compounddocs__rag_query` with:
     - `query: "Entity Framework PostgreSQL configuration"`
   - Check for project-specific connection string patterns or issues

5. **Synthesize** all findings with source attribution
```

### Comparison with best-practices-researcher

| Aspect | best-practices-researcher | framework-docs-researcher |
|--------|---------------------------|---------------------------|
| Primary Focus | Patterns and practices | Official API documentation |
| Key Output | Synthesized best practices | Documentation excerpts with examples |
| Ranking Logic | Rank by applicability | Rank by version relevance |
| Compound Docs Role | Check existing patterns first | Augment official docs |
| Sequential Thinking Use | Reconcile conflicting recommendations | Reconcile version differences |

---

## File Structure

```
plugins/csharp-compounding-docs/
├── agents/
│   └── research/
│       └── framework-docs-researcher.md    # Created in this phase
```

---

## Dependencies

### Depends On

- **Phase 104**: best-practices-researcher Agent - Establishes agent file patterns and MCP integration approaches
- **Phase 009**: Plugin Directory Structure - Directory structure for agents
- **MCP Servers**: Context7 and Sequential Thinking must be available in the user's Claude Code configuration

### Blocks

- None (end-user agent, can be used immediately once created)

---

## Verification Steps

After completing this phase, verify:

1. **Agent file syntax**: Validate YAML frontmatter parses correctly
   ```bash
   # In Claude Code session
   /agents  # Should list framework-docs-researcher
   ```

2. **Agent invocation**: Test that agent is suggested for framework documentation queries
   - Ask: "How do I configure dependency injection in ASP.NET Core?"
   - Claude should recognize this as a framework-docs-researcher task

3. **MCP tool access**: Verify agent can access required MCP tools
   - Context7 tools should be available
   - Compound docs `rag_query` should be available
   - Sequential Thinking should be available

4. **Read-only constraint**: Verify agent cannot modify files
   - Agent should only have Read, Glob, Grep, WebFetch, WebSearch, Task tools
   - Write, Edit, MultiEdit, Bash should be disallowed

5. **Output format**: Verify agent produces properly structured output
   - Source attribution for all findings
   - Version-specific guidance clearly labeled
   - Code examples included when relevant

6. **Sequential Thinking activation**: Test complex query triggers Sequential Thinking
   - Ask about version differences between .NET versions
   - Agent should use Sequential Thinking for reconciliation

---

## Testing Scenarios

### Scenario 1: Simple API Lookup

**Query**: "What is the signature of IServiceCollection.AddSingleton?"

**Expected Behavior**:
- Query Context7 for IServiceCollection
- Return API signature with source
- NO Sequential Thinking (simple lookup)

### Scenario 2: Version Comparison

**Query**: "How has minimal API routing changed between .NET 6 and .NET 8?"

**Expected Behavior**:
- Query Context7 and Microsoft Docs for both versions
- USE Sequential Thinking to reconcile differences
- Output clear version-specific guidance

### Scenario 3: Project Context Integration

**Query**: "How should I configure Serilog for this project?"

**Expected Behavior**:
- Query external docs for Serilog configuration
- Query compound docs for project-specific Serilog patterns
- Synthesize official guidance with project conventions

---

## Notes

- The agent is intentionally read-only to prevent accidental modifications during research
- Sequential Thinking is reserved for genuinely complex analysis, not simple lookups
- The agent prioritizes official Microsoft documentation but supplements with project context
- Version information is critical - always specify which .NET version guidance applies to
- WebSearch/WebFetch are used for Microsoft Docs since there may not be a dedicated MCP server for Microsoft Learn
