# Agents Structure Summary

This file contains the summary for the agents specification.

---

## spec/agents.md

### What This File Covers

This specification defines **4 research agents** for the csharp-compounding-docs Claude Code plugin. Agents are specialized Claude Code agent definitions that provide review and research capabilities alongside the plugin's skills and MCP server.

### Agent Definitions

1. **best-practices-researcher** - Gathers external best practices by searching compound docs, Context7, Microsoft Docs MCP, and web sources, then synthesizes findings with Sequential Thinking MCP.

2. **framework-docs-researcher** - Researches framework documentation questions by querying Context7, Microsoft Docs, and compound docs, reconciling version differences via Sequential Thinking.

3. **git-history-analyzer** - Analyzes git history for patterns including file change frequency, author contributions, commit message mining, bug-fix correlation, and refactoring history.

4. **repo-research-analyst** - Researches repository structure and conventions (naming, test organization, config patterns), proposing documentation updates for user approval.

### Key Technical Details

- Agents are markdown files with YAML frontmatter stored in `plugins/csharp-compounding-docs/agents/research/`
- All agents use `model: inherit` to use the user's configured model
- MCP integrations: Context7, Microsoft Docs, Sequential Thinking
- Agents can call `rag_query` to access the plugin's compound docs knowledge base
- Agents suggest capture skills (`/cdocs:problem`, `/cdocs:codebase`) but never auto-commit documentation

### Structural Relationships

- **Parent**: [SPEC.md](../SPEC.md) - The root specification document
- **Siblings** (other spec files at same level):
  - doc-types.md
  - mcp-server.md
  - infrastructure.md
  - skills.md (referenced for `/cdocs:research` skill that orchestrates these agents)
  - marketplace.md
  - configuration.md
  - testing.md
  - observability.md
  - research-index.md
- **Referenced Research Documents**:
  - research/claude-code-agents-research.md
  - research/building-claude-code-agents-research.md
  - research/compound-engineering-paradigm-research.md
  - research/claude-code-skills-research.md
  - research/sequential-thinking-mcp-verification.md
