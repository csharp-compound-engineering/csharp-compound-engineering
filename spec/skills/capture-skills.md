# Capture Skills

> **Status**: [DRAFT]
> **Parent**: [../skills.md](../skills.md)

> **Background**: Capture skills are adapted from the `compound-docs` skill in the original compound-engineering-plugin, which implements the core "compound" step of the Plan-Work-Review-Compound workflow. See [Compound Engineering Paradigm Research](../../research/compound-engineering-paradigm-research.md).

---

## Overview

Capture skills detect knowledge patterns in conversations and create structured documentation. Each skill has its own auto-invoke triggers and validates against specific schemas.

> **Background**: Auto-invoke triggering relies on Claude's semantic matching of skill descriptions against conversation context. For details on how skill descriptions drive automatic activation and best practices for trigger reliability, see [Claude Code Skills Research](../../research/claude-code-skills-research.md#auto-invoke-mechanisms).

**Note on Indexing**: Doc-creation skills write markdown files to disk. The file watcher service automatically indexes new/modified files to the vector database. Skills do not need to call `index_document` explicitly unless manual re-indexing is required.

> **Background**: The file watcher uses .NET's `FileSystemWatcher` with debouncing and a processing queue to handle file changes efficiently. See [.NET FileSystemWatcher for RAG Embedding Synchronization](../../research/dotnet-file-watcher-embeddings-research.md).

**Note on External MCP Servers**: Skills reference external MCP servers (Sequential Thinking, Context7, Microsoft Docs) in their "MCP Integration" sections. **Skills assume these servers are available** - the SessionStart hook has already verified their configuration. Skills should NOT include defensive checks for MCP availability. See [Core Principle #6](../../SPEC.md#6-external-mcp-dependencies-check-and-warn-never-install).

> **Background**: The SessionStart hook pattern for verifying MCP availability is documented in [Claude Code Hooks for Skill Auto-Invocation](../../research/claude-code-hooks-skill-invocation.md). For the correct Sequential Thinking MCP package name (`@modelcontextprotocol/server-sequential-thinking`), see [Sequential Thinking MCP Package Verification](../../research/sequential-thinking-mcp-verification.md).

---

## `/cdocs:problem`

> **Origin**: Adapted from `compound-docs` skill problem type in [compound-engineering-plugin](https://github.com/anthropics/compound-engineering-plugin)

**Purpose**: Capture a solved problem as documentation.

**Invocation**:
- **Manual**: User explicitly invokes `/cdocs:problem`
- **Auto-triggered**: Detects problem-solving indicators in conversation

**Auto-Invoke Triggers** (from schema):
```yaml
trigger_phrases:
  - "fixed"
  - "it's fixed"
  - "bug"
  - "the issue was"
  - "problem solved"
  - "resolved"
  - "exception"
  - "error"
  - "crash"
  - "failing"
```

**MCP Integration**:
- **Sequential Thinking**: Complex root cause analysis when multiple factors involved

**Behavior**:
1. Detect trigger phrase or manual invocation
2. Gather context from conversation (symptoms, root cause, solution)
3. Use Sequential Thinking for complex multi-factor root cause analysis
4. Validate against problem schema
5. Generate filename: `{sanitized-title}-{YYYYMMDD}.md`
6. Write to `./csharp-compounding-docs/problems/`
7. Confirm with decision menu

**Schema Fields** (see [doc-types.md](../doc-types.md)):
- `problem_type` (enum)
- `symptoms` (array)
- `root_cause` (string)
- `solution` (string)
- `severity` (enum)

---

## `/cdocs:insight`

> **Origin**: Adapted from `compound-docs` skill insight type in [compound-engineering-plugin](https://github.com/anthropics/compound-engineering-plugin)

**Purpose**: Capture product/project insight.

**Invocation**:
- **Manual**: User explicitly invokes `/cdocs:insight`
- **Auto-triggered**: Detects insight indicators in conversation

**Auto-Invoke Triggers** (from schema):
```yaml
trigger_phrases:
  - "users want"
  - "users prefer"
  - "interesting that"
  - "makes sense because"
  - "the reason is"
  - "apparently"
  - "learned that"
  - "realized"
```

**MCP Integration**:
- **Sequential Thinking**: Connecting insights to broader product/business context

**Behavior**:
1. Detect trigger phrase or manual invocation
2. Gather insight from conversation
3. Use Sequential Thinking to connect insight to broader context
4. Classify insight type
5. Validate against insight schema
6. Write to `./csharp-compounding-docs/insights/`

**Schema Fields**:
- `insight_type`: `business_logic`, `user_behavior`, `domain_knowledge`, `feature_interaction`, `market_observation`
- `impact_area`: Which part of product this affects
- `confidence`: `verified`, `hypothesis`, `observation`

---

## `/cdocs:codebase`

> **Origin**: Adapted from `compound-docs` skill codebase type in [compound-engineering-plugin](https://github.com/anthropics/compound-engineering-plugin)

**Purpose**: Capture codebase architectural knowledge.

**Invocation**:
- **Manual**: User explicitly invokes `/cdocs:codebase`
- **Auto-triggered**: Detects architecture decision indicators in conversation

**Auto-Invoke Triggers** (from schema):
```yaml
trigger_phrases:
  - "decided to"
  - "going with"
  - "settled on"
  - "our approach"
  - "the pattern is"
  - "architecture"
  - "structure"
```

**MCP Integration**:
- **Sequential Thinking**: Analyzing trade-offs and alternatives for architectural decisions

**Behavior**:
1. Detect trigger phrase or manual invocation
2. Extract architectural insight from conversation
3. Use Sequential Thinking to analyze trade-offs and document alternatives considered
4. Validate against codebase schema
5. Write to `./csharp-compounding-docs/codebase/`

**Schema Fields**:
- `knowledge_type`: `architecture_decision`, `code_pattern`, `module_interaction`, `data_flow`, `dependency_rationale`
- `scope`: `system`, `module`, `component`, `function`
- `files_involved`: Array of relevant file paths

---

## `/cdocs:tool`

> **Origin**: Adapted from `compound-docs` skill tool type in [compound-engineering-plugin](https://github.com/anthropics/compound-engineering-plugin)

**Purpose**: Capture tool/library knowledge.

**Invocation**:
- **Manual**: User explicitly invokes `/cdocs:tool`
- **Auto-triggered**: Detects tool/library gotcha indicators in conversation

**Auto-Invoke Triggers** (from schema):
```yaml
trigger_phrases:
  - "gotcha"
  - "watch out for"
  - "careful with"
  - "heads up"
  - "workaround"
  - "dependency"
  - "library"
  - "package"
  - "NuGet"
```

**MCP Integration**:
- **Sequential Thinking**: Determining if issue is version-specific or configuration-related

**Behavior**:
1. Detect trigger phrase or manual invocation
2. Extract tool insight from conversation
3. Use Sequential Thinking to determine version specificity and root cause
4. Validate against tool schema
5. Write to `./csharp-compounding-docs/tools/`

**Schema Fields**:
- `tool_name`: Name of tool/library
- `version`: Version where behavior observed
- `knowledge_type`: `gotcha`, `configuration`, `integration`, `performance`, `workaround`
- `official_docs_gap`: Boolean - is this missing from official docs?

---

## `/cdocs:style`

> **Origin**: Adapted from `compound-docs` skill style type in [compound-engineering-plugin](https://github.com/anthropics/compound-engineering-plugin)

**Purpose**: Capture coding style/preference.

**Invocation**:
- **Manual**: User explicitly invokes `/cdocs:style`
- **Auto-triggered**: Detects convention/style indicators in conversation

**Auto-Invoke Triggers** (from schema):
```yaml
trigger_phrases:
  - "always"
  - "never"
  - "prefer"
  - "convention"
  - "standard"
  - "rule"
  - "don't forget"
  - "remember to"
```

**MCP Integration**:
- **Sequential Thinking**: Evaluating style rationale and identifying exceptions

**Behavior**:
1. Detect trigger phrase or manual invocation
2. Extract style preference from conversation
3. Use Sequential Thinking to document rationale and identify valid exceptions
4. Validate against style schema
5. Write to `./csharp-compounding-docs/styles/`

**Schema Fields**:
- `style_type`: `naming`, `architecture`, `error_handling`, `testing`, `documentation`, `formatting`
- `scope`: `project`, `module`, `team`
- `rationale`: Why this style is preferred

---

## Related Documentation

- [Query Skills](./query-skills.md) - Search and retrieve captured documentation
- [Meta Skills](./meta-skills.md) - Create custom doc-types and handle multi-trigger conflicts
- [Utility Skills](./utility-skills.md) - Delete, promote, and manage documentation
- [Doc Types Specification](../doc-types.md) - Schema definitions and validation rules
