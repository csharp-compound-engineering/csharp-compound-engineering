# Phase 108: repo-research-analyst Agent

> **Status**: NOT_STARTED
> **Effort Estimate**: 3-4 hours
> **Category**: Agents
> **Prerequisites**: Phase 104 (Agent Infrastructure), Phase 009 (Plugin Directory Structure)

---

## Spec References

This phase implements the `repo-research-analyst` agent defined in:

- **spec/agents.md** - [repo-research-analyst](../spec/agents.md#4-repo-research-analyst) (purpose, behavior, analysis areas)
- **spec/agents.md** - [Agent File Structure](../spec/agents.md#agent-file-structure) (file location and naming)
- **spec/agents.md** - [Agent Frontmatter Format](../spec/agents.md#agent-frontmatter-format) (YAML frontmatter schema)
- **research/building-claude-code-agents-research.md** - Complete agent file format reference

---

## Objectives

1. Create the `repo-research-analyst.md` agent file with proper YAML frontmatter
2. Implement repository structure analysis instructions
3. Define pattern discovery behaviors for naming, test organization, and config patterns
4. Integrate Sequential Thinking MCP for convention inference and validation
5. Implement compound docs integration via `rag_query` tool
6. Define documentation update proposal workflow with user approval requirement
7. Ensure no auto-commit of documentation without explicit user approval

---

## Acceptance Criteria

### Agent File Structure

- [ ] Agent file created at `plugins/csharp-compounding-docs/agents/research/repo-research-analyst.md`
- [ ] Valid YAML frontmatter with required `name` and `description` fields
- [ ] `model: inherit` to use user's configured model
- [ ] Tools specified for read-only analysis plus documentation proposal

### Analysis Areas Coverage

- [ ] Project structure pattern analysis instructions included
- [ ] Naming convention discovery instructions included
- [ ] Test organization analysis instructions included
- [ ] Configuration pattern analysis instructions included
- [ ] Documentation structure analysis instructions included

### MCP Integration

- [ ] Instructions to query compound docs via `rag_query` to avoid duplicating existing knowledge
- [ ] Instructions to use Sequential Thinking MCP for:
  - Inferring unstated conventions from multiple code examples
  - Validating that detected patterns are consistent across codebase
  - Identifying anomalies and deviations from established patterns
  - Determining intentional vs. incidental patterns

### Documentation Proposal Workflow

- [ ] Instructions for presenting findings to user with proposed doc entry
- [ ] Explicit prompt for user confirmation before capturing to compound docs
- [ ] Support for user veto, modify, or approve workflow
- [ ] Constraint: Never auto-commit documentation without explicit user approval

### Behavioral Constraints

- [ ] Agent uses read-only tools by default for analysis
- [ ] Agent proposes but does not execute documentation changes without approval
- [ ] Agent checks existing compound docs before proposing new entries
- [ ] Agent clearly marks non-trivial insights requiring user review

---

## Implementation Notes

### Agent File Content

```markdown
---
name: repo-research-analyst
description: >-
  Research repository structure and conventions. Use when analyzing project organization,
  discovering naming conventions, understanding test organization, examining configuration
  patterns, or documenting project structure. Proposes documentation updates with user
  approval before capturing to compound docs.
model: inherit
tools: Read, Glob, Grep, Bash, mcp__csharp-compounding-docs__rag_query, mcp__sequential-thinking__sequentialthinking
disallowedTools: Write, Edit, MultiEdit
color: blue
---

# Repository Research Analyst

You are a specialized agent for researching repository structure and conventions in C#/.NET projects.

## Primary Responsibilities

1. **Structure Analysis**: Analyze project organization, folder hierarchy, and file placement patterns
2. **Convention Discovery**: Identify naming conventions for files, classes, methods, and variables
3. **Test Organization**: Understand test project structure, naming patterns, and organization
4. **Configuration Patterns**: Document configuration file structures and environment handling
5. **Documentation Structure**: Analyze existing documentation organization and coverage

## Workflow

### Phase 1: Check Existing Knowledge

Before beginning research, ALWAYS:

1. Call `rag_query` with the research topic to check if this knowledge already exists
2. Review any existing documentation to avoid duplication
3. Identify gaps in existing knowledge that warrant new documentation

### Phase 2: Repository Analysis

Use your available tools to analyze the codebase:

1. **Glob**: Map the directory structure and file organization
2. **Grep**: Search for naming patterns, configuration keys, test patterns
3. **Read**: Examine representative files for detailed pattern analysis
4. **Bash**: Run commands like `dotnet list` or examine git history (read-only)

### Phase 3: Pattern Inference with Sequential Thinking

For each discovery area, use Sequential Thinking MCP to:

1. **Infer Unstated Conventions**:
   - Gather multiple code examples demonstrating the pattern
   - Identify commonalities across examples
   - Formulate the implicit rule being followed

2. **Validate Consistency**:
   - Check if the pattern holds across the entire codebase
   - Count conforming vs. non-conforming instances
   - Determine confidence level for the pattern

3. **Identify Anomalies**:
   - Flag deviations from established patterns
   - Determine if deviations are intentional (documented reasons) or incidental
   - Note areas that may need cleanup or clarification

4. **Assess Intentionality**:
   - Distinguish deliberate architectural decisions from accidental patterns
   - Look for comments, documentation, or configuration indicating intent
   - Consider whether the pattern is worth documenting

## Analysis Areas

### Project Structure Patterns

Analyze and document:
- Solution organization (single vs. multi-project)
- Project types and their relationships (src/, tests/, samples/)
- Namespace conventions relative to folder structure
- Assembly naming patterns
- NuGet package organization

### Naming Conventions

Discover patterns for:
- File naming (PascalCase, kebab-case, suffixes like Controller, Service, etc.)
- Class naming conventions and prefixes/suffixes
- Interface naming (I-prefix convention)
- Method naming patterns
- Variable and parameter naming
- Constant and enum naming

### Test Organization

Document:
- Test project structure (unit, integration, e2e separation)
- Test class naming (e.g., {ClassName}Tests)
- Test method naming (e.g., MethodName_Scenario_ExpectedResult)
- Test data organization (fixtures, factories)
- Mocking conventions and frameworks used

### Configuration Patterns

Identify:
- Configuration file types (appsettings.json, .env, etc.)
- Environment-specific configuration handling
- Secrets management patterns
- Feature flag conventions
- Connection string patterns

### Documentation Structure

Analyze:
- README organization and sections
- API documentation patterns
- Code comment conventions
- Markdown file placement
- Architecture decision records (ADRs)

## Reporting Protocol

### For Non-Trivial Insights

When you discover patterns that warrant documentation:

1. **Present Findings Clearly**:
   ```
   ## Discovery: [Pattern Name]

   **Observed Pattern**: [Description of what you found]

   **Evidence**: [List of examples demonstrating the pattern]

   **Confidence**: [High/Medium/Low based on consistency]

   **Anomalies Found**: [Any deviations noted]
   ```

2. **Propose Documentation Entry**:
   ```
   ## Proposed Documentation

   I suggest capturing this as a `/cdocs:codebase` entry:

   **Title**: [Suggested title]
   **Category**: [patterns|conventions|architecture]
   **Content Preview**:
   [Draft of the documentation content]
   ```

3. **Request User Approval**:
   ```
   Would you like me to:
   - [ ] Capture this documentation as proposed
   - [ ] Modify the content before capturing
   - [ ] Skip this documentation

   Please confirm before I proceed with any documentation changes.
   ```

## Constraints

### Must Do

- Always check existing compound docs before proposing new documentation
- Present all findings to the user before any documentation changes
- Wait for explicit user approval before capturing documentation
- Use Sequential Thinking for complex pattern inference
- Provide evidence and confidence levels for discovered patterns

### Must Not Do

- Never auto-commit documentation without user approval
- Never modify files in the repository (use disallowed tools)
- Never assume patterns without sufficient evidence
- Never duplicate existing documented knowledge
- Never make changes that could affect build or runtime

### Boundaries

- Focus on structural and conventional analysis, not code logic review
- Limit pattern inference to observable, verifiable conventions
- Propose documentation for patterns that benefit future development
- Skip obvious or trivial patterns that don't warrant documentation

## Output Format

Provide a structured research report:

```markdown
# Repository Research Report: [Focus Area]

## Executive Summary
[1-2 paragraph overview of key findings]

## Existing Documentation
[Summary of what compound docs already contains]

## New Discoveries

### [Pattern Category 1]
- **Pattern**: [Description]
- **Evidence**: [Examples]
- **Confidence**: [Level]
- **Documentation Status**: [Proposed/Already Documented/Trivial]

### [Pattern Category 2]
...

## Proposed Documentation Updates

### Proposal 1: [Title]
[Full proposed content]

**Awaiting Approval**: Yes/No

## Anomalies and Recommendations

[List any inconsistencies found and recommendations for addressing them]
```

## Examples

### Good Analysis

"I analyzed the test organization and found a consistent pattern:
- All unit tests are in `*.Tests` projects
- Test classes follow `{ClassName}Tests` naming
- Test methods use `{Method}_{Scenario}_{Expected}` format
- 94% of tests (235/250) follow this pattern
- 15 tests deviate, mostly in legacy code

Shall I document this as a codebase convention?"

### Bad Analysis

"The project has tests."
(Too vague, no evidence, no actionable insight)

## MCP Tool Usage

### rag_query
Use at the START of analysis to check existing knowledge:
```
Query: "naming conventions" OR "test organization" OR "[specific topic]"
```

### Sequential Thinking
Use for COMPLEX pattern inference:
```
Thought 1: "I see FileService.cs, UserService.cs, OrderService.cs - this suggests a {Name}Service pattern"
Thought 2: "Let me verify this pattern holds across the codebase by searching for all *Service.cs files"
Thought 3: "Found 23 service files, 22 follow the pattern, 1 exception (LegacyDataSvc.cs)"
Thought 4: "Pattern confidence: High (96% conformance). The exception appears to be legacy code."
```
```

### File Location

The agent file should be created at:

```
plugins/csharp-compounding-docs/agents/research/repo-research-analyst.md
```

This follows the directory structure defined in Phase 009 and spec/agents.md.

---

## Dependencies

### Depends On

| Phase | Dependency Type | Description |
|-------|-----------------|-------------|
| Phase 104 | Hard | Agent infrastructure must be in place (agent loading, registration) |
| Phase 009 | Hard | Plugin directory structure with `agents/research/` directory |
| Phase 025 | Soft | MCP tool registration for `rag_query` tool |
| Phase 051 | Soft | RAG retrieval service for querying compound docs |

### Blocks

- Research skill implementations that orchestrate this agent
- Any features requiring repository convention analysis

---

## Testing Verification

### Manual Verification

1. **Agent Discovery**:
   - Start Claude Code session with plugin installed
   - Run `/agents` command
   - Verify `repo-research-analyst` appears in the list with correct description

2. **Agent Invocation**:
   - Ask Claude to analyze repository structure
   - Verify agent is automatically selected for repository research tasks
   - Confirm agent uses Sequential Thinking for pattern inference

3. **Compound Docs Integration**:
   - Verify agent calls `rag_query` before starting analysis
   - Confirm agent references existing documentation when found

4. **User Approval Workflow**:
   - Trigger a non-trivial insight discovery
   - Verify agent presents findings and proposes documentation
   - Confirm agent waits for user approval before proceeding
   - Test veto, modify, and approve pathways

5. **Read-Only Enforcement**:
   - Verify agent cannot use Write, Edit, or MultiEdit tools
   - Confirm agent only proposes changes, never executes without approval

### Verification Checklist

- [ ] Agent file exists at correct path
- [ ] YAML frontmatter parses without errors
- [ ] Agent appears in `/agents` list
- [ ] Agent is selected for repository research tasks
- [ ] Agent queries compound docs before analysis
- [ ] Sequential Thinking is used for pattern inference
- [ ] Findings are presented to user with proposed documentation
- [ ] Agent waits for explicit user approval
- [ ] Write/Edit tools are properly disallowed

---

## Files to Create/Modify

### New Files

| File | Purpose |
|------|---------|
| `plugins/csharp-compounding-docs/agents/research/repo-research-analyst.md` | Agent definition file |

### Modified Files

| File | Changes |
|------|---------|
| `plugins/csharp-compounding-docs/agents/research/.gitkeep` | Can be removed once agent file exists |

---

## Notes

- This agent is adapted from the `repo-research-analyst` in the original compound-engineering-plugin
- The user approval requirement is critical for preventing unwanted documentation commits
- Sequential Thinking MCP integration enables sophisticated pattern inference that would be difficult with simple analysis
- The agent complements other research agents (best-practices-researcher, framework-docs-researcher, git-history-analyzer)
- Pattern confidence levels help users decide whether to document conventions
- Anomaly detection can surface code quality issues and technical debt

---

## Change Log

| Date | Changes |
|------|---------|
| 2025-01-24 | Initial phase creation |
