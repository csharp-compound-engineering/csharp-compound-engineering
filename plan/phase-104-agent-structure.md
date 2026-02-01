# Phase 104: Agent File Structure

## Phase Overview

| Attribute | Value |
|-----------|-------|
| **Phase ID** | 104 |
| **Phase Name** | Agent File Structure |
| **Category** | Agents |
| **Estimated Effort** | 4-6 hours |
| **Priority** | Medium |
| **Prerequisites** | Phase 009 (Plugin Directory Structure) |

## Objective

Define and implement the agent markdown file structure for the csharp-compounding-docs plugin. This phase establishes the standard format for agent definitions including YAML frontmatter, model configuration, MCP access, and knowledge base integration via `rag_query`.

---

## Background

### What Are Claude Code Agents?

Agents are specialized Claude Code definitions that provide research and review capabilities. Unlike skills (which inject instructions into the current context), agents:

- Have isolated context windows
- Can be configured with specific model settings
- Inherit or restrict tool access
- Return summarized results to the parent conversation

### Agent vs Skill Distinction

| Feature | Agents | Skills |
|---------|--------|--------|
| Context | Isolated context window | Injected into current context |
| Execution | Independent Claude instance | Same Claude instance |
| Primary Trigger | Task delegation via `Task` tool | Description matching or slash command |
| Return Value | Summarized results | Inline content modification |

---

## Requirements

### Functional Requirements

1. **FR-104.1**: Define agent markdown file format with YAML frontmatter
2. **FR-104.2**: Establish directory structure under `plugins/csharp-compounding-docs/agents/research/`
3. **FR-104.3**: Configure `model: inherit` for all agents
4. **FR-104.4**: Document MCP server access patterns for agents
5. **FR-104.5**: Define `rag_query` access for knowledge base integration

### Non-Functional Requirements

1. **NFR-104.1**: Agent files must be valid markdown with valid YAML frontmatter
2. **NFR-104.2**: Agent descriptions must be under 1024 characters
3. **NFR-104.3**: Agent names must use kebab-case, lowercase, max 64 characters

---

## Directory Structure

### Plugin Agent Location

```
plugins/csharp-compounding-docs/
├── plugin.json
├── agents/
│   └── research/
│       ├── best-practices-researcher.md
│       ├── framework-docs-researcher.md
│       ├── git-history-analyzer.md
│       └── repo-research-analyst.md
├── skills/
│   └── cdocs/
│       └── [skill folders]
└── mcp-servers/
    └── [MCP configuration]
```

### Why This Location?

- Plugin agents are stored in the plugin's `agents/` directory
- Categorized by purpose (`research/`) for organization
- Follows Claude Code plugin structure conventions
- Agents discovered when plugin is installed

---

## Agent File Format

### Complete Agent Template

```markdown
---
name: agent-name
description: "Clear description of when Claude should delegate to this agent. Include specific scenarios and use cases. Max 1024 characters."
model: inherit
---

# Agent Name

You are a [role description] specialized in [domain].

## Purpose

[Concise statement of the agent's primary function]

## MCP Server Access

You have access to the following MCP servers:
- **Context7**: Framework patterns and documentation via `mcp__context7__*` tools
- **Microsoft Docs**: .NET/C# documentation via `mcp__microsoft-docs__*` tools
- **Sequential Thinking**: Multi-step reasoning via `mcp__sequential-thinking__sequentialthinking`

## Knowledge Base Access

Before performing external research, always query the compound docs knowledge base:

```
Call rag_query with your topic to retrieve existing documented patterns
```

This ensures:
1. Leveraging existing internal knowledge
2. Avoiding duplicate documentation
3. Grounding recommendations in project context

## Workflow

### Phase 1: Internal Knowledge Check
1. Call `rag_query` with the research topic
2. Review any existing compound docs
3. Note gaps that require external research

### Phase 2: External Research
1. Query relevant MCP servers
2. Search web sources if needed
3. Gather authoritative references

### Phase 3: Synthesis
1. Reconcile internal and external findings
2. Prioritize project-specific documentation
3. Cite all sources

## Output Format

[Describe expected output structure]

## Constraints

### Must Do
- [Required behaviors]

### Must Not Do
- [Prohibited behaviors]
```

### YAML Frontmatter Schema

#### Required Fields

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| `name` | string | Max 64 chars, lowercase, letters/numbers/hyphens only | Unique agent identifier |
| `description` | string | Max 1024 chars, non-empty | When Claude should delegate to this agent |

#### Optional Fields

| Field | Type | Values | Default | Description |
|-------|------|--------|---------|-------------|
| `model` | string | `sonnet`, `opus`, `haiku`, `inherit` | `inherit` | Model to use for agent |
| `tools` | string | Comma-separated tool names | All inherited | Tool allowlist |
| `disallowedTools` | string | Comma-separated tool names | None | Tool denylist |
| `permissionMode` | string | `default`, `acceptEdits`, `plan` | `default` | Permission handling |
| `color` | string | `red`, `blue`, `green`, `yellow`, `purple`, `orange`, `pink`, `cyan` | `automatic` | UI color |
| `skills` | string | Comma-separated skill names | None | Auto-load skills |

---

## Model Configuration

### Why `model: inherit`?

All agents in this plugin use `model: inherit` because:

1. **User Preference**: Respects the user's configured model choice
2. **Cost Control**: Users control their own API costs
3. **Flexibility**: Works with any model the user has access to
4. **Consistency**: Matches the parent conversation's capabilities

### Alternative Model Options

While we use `inherit`, here's when alternatives might be chosen:

| Model | Use Case |
|-------|----------|
| `inherit` | Default - use user's model (recommended) |
| `opus` | Complex reasoning requiring highest capability |
| `sonnet` | Balanced performance for most tasks |
| `haiku` | Fast, simple tasks with cost optimization |

---

## MCP Server Integration

### Available MCP Servers for Agents

Agents inherit MCP tool access from the parent session. The following MCP servers are relevant:

#### Context7

```markdown
## MCP: Context7
- Query framework documentation
- Retrieve code examples
- Tools: mcp__context7__resolve-library-id, mcp__context7__query-docs
```

#### Microsoft Docs

```markdown
## MCP: Microsoft Docs
- Query .NET/C# documentation
- Access API references
- Tools: mcp__microsoft-docs__* (varies by configuration)
```

#### Sequential Thinking

```markdown
## MCP: Sequential Thinking
- Multi-step reasoning
- Complex synthesis tasks
- Tool: mcp__sequential-thinking__sequentialthinking
```

### MCP Access Pattern in Agent Instructions

```markdown
## Using MCP Servers

When researching, use MCP tools in this order:

1. **Internal First**: Call `rag_query` for compound docs
2. **Context7**: For framework-specific patterns
   ```
   mcp__context7__resolve-library-id → mcp__context7__query-docs
   ```
3. **Microsoft Docs**: For .NET/C# guidance
4. **Web Search**: For community patterns (WebSearch tool)
5. **Sequential Thinking**: For synthesis
   ```
   mcp__sequential-thinking__sequentialthinking
   ```
```

---

## Knowledge Base Access via rag_query

### What is rag_query?

The `rag_query` tool is provided by the csharp-compounding-docs MCP server. It enables agents to:

1. Search the compound docs knowledge base
2. Retrieve relevant documented patterns
3. Access project-specific conventions
4. Find related problem/solution documentation

### Agent Instructions for rag_query

```markdown
## Knowledge Base Integration

### Before External Research

Always query the compound docs first:

1. Call `rag_query` with your research topic
2. Review returned documents for:
   - Existing patterns that address the question
   - Previously documented solutions
   - Project-specific conventions
   - Critical-level documents that must be considered

### Example Usage

To research "authentication patterns":
- Query: "authentication patterns security jwt"
- Review any matches before external research
- Note gaps that require new documentation

### After Research

If you discover new patterns not in compound docs:
- Suggest appropriate capture skill (`/cdocs:problem`, `/cdocs:codebase`)
- Never auto-commit - always get user approval first
```

### rag_query Tool Signature

```typescript
rag_query(params: {
  query: string;           // Natural language query
  doc_type?: string;       // Filter by document type
  limit?: number;          // Max results (default: 10)
  min_similarity?: number; // Minimum similarity threshold
}): Promise<SearchResult[]>
```

---

## Implementation Tasks

### Task 104.1: Create Agent Directory Structure

**File**: `plugins/csharp-compounding-docs/agents/research/`

```bash
mkdir -p plugins/csharp-compounding-docs/agents/research
```

### Task 104.2: Create Agent Template File

**File**: `plugins/csharp-compounding-docs/agents/AGENT_TEMPLATE.md`

```markdown
---
name: agent-name
description: "Description of when this agent should be used (max 1024 chars)"
model: inherit
---

# Agent Name

[Agent system prompt content]
```

### Task 104.3: Document Agent Frontmatter Schema

Create documentation of the frontmatter schema for plugin contributors.

### Task 104.4: Validate Agent Files

Create a validation script or test that verifies:
- Valid YAML frontmatter
- Required fields present
- Field constraints met
- Markdown body present

---

## Agent File Examples

### Example 1: best-practices-researcher.md

```markdown
---
name: best-practices-researcher
description: "Research external best practices and examples. Use when you need to gather industry patterns, framework recommendations, or community conventions for a specific topic. Searches compound docs first, then Context7, Microsoft Docs, and web sources."
model: inherit
---

# Best Practices Researcher

You are a research specialist focused on gathering and synthesizing best practices from multiple authoritative sources.

## Purpose

Gather external best practices, reconcile them with internal compound docs, and provide actionable recommendations grounded in both industry standards and project context.

## MCP Server Access

- **Context7**: Framework patterns via `mcp__context7__*`
- **Microsoft Docs**: .NET guidance via `mcp__microsoft-docs__*`
- **Sequential Thinking**: Multi-step synthesis via `mcp__sequential-thinking__sequentialthinking`

## Workflow

### Phase 1: Internal Knowledge Check
1. Call `rag_query` with the research topic
2. Identify existing documented patterns
3. Note any critical-level documents

### Phase 2: External Research
1. Query Context7 for framework patterns
2. Query Microsoft Docs for .NET guidance
3. Use WebSearch for community patterns

### Phase 3: Synthesis
1. Use Sequential Thinking to:
   - Compare internal vs external findings
   - Reconcile conflicting recommendations
   - Rank by applicability to project
2. Prioritize internal documentation
3. Cite all sources

## Output Format

### Research Findings

#### Internal Documentation
[Relevant compound docs with links]

#### External Best Practices
[Industry patterns with citations]

#### Synthesis
[Reconciled recommendations]

#### Gaps Identified
[Areas needing documentation]

## Constraints

### Must Do
- Always check compound docs first
- Cite all sources
- Prioritize internal documentation

### Must Not Do
- Auto-commit new documentation
- Ignore conflicting recommendations
- Skip internal knowledge check
```

### Example 2: git-history-analyzer.md

```markdown
---
name: git-history-analyzer
description: "Analyze git history and code evolution patterns. Use when investigating file change frequency, author contributions, commit patterns, bug-fix correlation, or refactoring history for a codebase area."
model: inherit
---

# Git History Analyzer

You are a git history analysis specialist focused on understanding code evolution and patterns.

## Purpose

Analyze git history to surface insights about code evolution, identify patterns in changes, and provide actionable recommendations based on historical data.

## MCP Server Access

- **Sequential Thinking**: Pattern correlation via `mcp__sequential-thinking__sequentialthinking`

## Capabilities

- File change frequency analysis
- Author contribution patterns
- Commit message mining
- Bug-fix correlation
- Refactoring history

## Workflow

### Phase 1: Data Gathering
1. Use git log, blame, and diff commands
2. Collect relevant history for the target area
3. Query compound docs for any documented patterns

### Phase 2: Analysis
1. Use Sequential Thinking to:
   - Correlate bug fixes with code areas
   - Identify emerging patterns
   - Trace root causes through commits
   - Analyze refactoring motivations

### Phase 3: Synthesis
1. Summarize key findings
2. Identify actionable insights
3. Suggest documentation if patterns warrant

## Output Format

### Analysis Summary
[Key findings overview]

### Change Patterns
[Frequency, authors, areas]

### Correlations
[Bug-fix patterns, refactoring trends]

### Recommendations
[Actionable insights]

## Constraints

### Must Do
- Use git commands (log, blame, diff)
- Apply Sequential Thinking for correlation
- Query compound docs for existing knowledge

### Must Not Do
- Modify git history
- Make assumptions without data
- Auto-document findings
```

---

## Validation Checklist

### Frontmatter Validation

- [ ] `name` is present and valid (kebab-case, max 64 chars)
- [ ] `description` is present and under 1024 characters
- [ ] `model` is set to `inherit` (or valid alternative if justified)
- [ ] YAML is syntactically valid
- [ ] No disallowed fields present

### Content Validation

- [ ] Agent has clear purpose statement
- [ ] MCP server access is documented
- [ ] `rag_query` usage is instructed
- [ ] Workflow steps are defined
- [ ] Output format is specified
- [ ] Constraints are listed

### Integration Validation

- [ ] Agent file is in correct directory
- [ ] File extension is `.md`
- [ ] Agent can be discovered by Claude Code
- [ ] MCP tools referenced are available

---

## Testing Strategy

### Manual Testing

1. Install plugin in Claude Code
2. Verify agents appear in `/agents` list
3. Test automatic invocation via matching description
4. Verify MCP tool access works
5. Test `rag_query` integration

### Automated Testing

```csharp
[Fact]
public void AgentFrontmatter_ShouldBeValid()
{
    var agentPath = "agents/research/best-practices-researcher.md";
    var content = File.ReadAllText(agentPath);

    var frontmatter = ParseYamlFrontmatter(content);

    Assert.NotNull(frontmatter.Name);
    Assert.True(frontmatter.Name.Length <= 64);
    Assert.NotNull(frontmatter.Description);
    Assert.True(frontmatter.Description.Length <= 1024);
    Assert.Equal("inherit", frontmatter.Model);
}
```

---

## Dependencies

### Upstream Dependencies

| Phase | Dependency | Type |
|-------|------------|------|
| Phase 009 | Plugin Directory Structure | Required |

### Downstream Dependencies

| Phase | Description |
|-------|-------------|
| Phase 105+ | Individual agent implementations |
| MCP Server | rag_query tool availability |

---

## Success Criteria

1. Agent directory structure created at `plugins/csharp-compounding-docs/agents/research/`
2. Agent file format documented with YAML frontmatter schema
3. All agents configured with `model: inherit`
4. MCP server access patterns documented
5. `rag_query` integration instructions included
6. Validation checklist defined
7. Example agent files provided

---

## References

- [spec/agents.md](/spec/agents.md) - Agent definitions
- [structure/agents.md](/structure/agents.md) - Agents summary
- [research/building-claude-code-agents-research.md](/research/building-claude-code-agents-research.md) - Agent creation guide
- [Claude Code Subagents Documentation](https://code.claude.com/docs/en/sub-agents)
- [Agent Skills Specification](https://github.com/anthropics/skills/blob/main/spec/agent-skills-spec.md)
