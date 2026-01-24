# Built-in Doc-Types

> **Status**: [DRAFT]
> **Parent**: [../doc-types.md](../doc-types.md)

---

> **Background**: The compound-engineering paradigm defines a philosophy where each solved problem teaches the system, accumulating knowledge over time. The Problems & Solutions doc-type mirrors this original concept. See [Compound Engineering Paradigm Research](../../research/compound-engineering-paradigm-research.md).

> **Background**: Each doc-type uses a YAML schema with `trigger_phrases` and `classification_hints`. For details on YAML schema validation approaches and JSON Schema integration, see [YAML Schema Formats](../../research/yaml-schema-formats.md).

---

## Overview

The plugin ships with five built-in doc-types that cover the most common categories of institutional knowledge. Each doc-type has a dedicated storage folder, a schema file, and a skill for capturing documents.

Each built-in doc-type has a schema with `trigger_phrases` and `classification_hints`. Each capture skill independently monitors for its own triggers (distributed capture pattern). When multiple skills trigger simultaneously, `/cdocs:capture-select` auto-invokes to let the user choose which doc-types to capture.

> **Background**: Claude Code skills use YAML frontmatter for metadata including invocation triggers. For comprehensive details on skill structure, auto-invoke mechanisms, and frontmatter fields, see [Claude Code Skills Research](../../research/claude-code-skills-research.md) and [Building Claude Code Skills Research](../../research/building-claude-code-skills-research.md).

---

## 1. Problems & Solutions (`problem`)

**Purpose**: Document solved problems for future reference (mirrors original compound-engineering).

**Folder**: `./csharp-compounding-docs/problems/`

**Skill**: `/cdocs:problem`

**Schema**:
```yaml
name: problem
description: Solved problems with symptoms, root cause, and solution

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

classification_hints:
  - "error message"
  - "stack trace"
  - "exception"
  - "null reference"
  - "debugging"
  - "root cause"
  - "symptoms"
  - "workaround"
  - "fix"

required_fields:
  - name: problem_type
    type: enum
    values: [bug, configuration, integration, performance, security, data]
  - name: symptoms
    type: array
    description: Observable symptoms of the problem
  - name: root_cause
    type: string
    description: The underlying cause
  - name: solution
    type: string
    description: How it was fixed

optional_fields:
  - name: component
    type: string
    description: Affected component/module
  - name: severity
    type: enum
    values: [critical, high, medium, low]
  - name: prevention
    type: string
    description: How to prevent recurrence
```

---

## 2. Product/Project Insights (`insight`)

**Purpose**: Capture significant learnings about the product being built.

**Folder**: `./csharp-compounding-docs/insights/`

**Skill**: `/cdocs:insight`

**Schema**:
```yaml
name: insight
description: Product, business, or domain learnings

trigger_phrases:
  - "users want"
  - "users prefer"
  - "interesting that"
  - "makes sense because"
  - "the reason is"
  - "apparently"
  - "learned that"
  - "realized"

classification_hints:
  - "business context"
  - "user behavior"
  - "product"
  - "feature"
  - "customer"
  - "domain"
  - "market"
  - "requirement"
  - "stakeholder"

required_fields:
  - name: insight_type
    type: enum
    values: [business_logic, user_behavior, domain_knowledge, feature_interaction, market_observation]
  - name: impact_area
    type: string
    description: Which part of product this affects

optional_fields:
  - name: confidence
    type: enum
    values: [verified, hypothesis, observation]
  - name: source
    type: string
    description: How this insight was discovered
```

---

## 3. Codebase Knowledge (`codebase`)

**Purpose**: Document architectural decisions, code patterns, and structural knowledge.

**Folder**: `./csharp-compounding-docs/codebase/`

**Skill**: `/cdocs:codebase`

**Schema**:
```yaml
name: codebase
description: Architecture decisions, code patterns, and structural knowledge

trigger_phrases:
  - "decided to"
  - "going with"
  - "settled on"
  - "our approach"
  - "the pattern is"
  - "architecture"
  - "structure"

classification_hints:
  - "architecture"
  - "module"
  - "component"
  - "pattern"
  - "structure"
  - "design decision"
  - "layer"
  - "dependency"
  - "separation"
  - "SOLID"

required_fields:
  - name: knowledge_type
    type: enum
    values: [architecture_decision, code_pattern, module_interaction, data_flow, dependency_rationale]
  - name: scope
    type: enum
    values: [system, module, component, function]

optional_fields:
  - name: files_involved
    type: array
    description: Relevant file paths
  - name: alternatives_considered
    type: array
    description: Other approaches that were evaluated
  - name: trade_offs
    type: string
    description: Trade-offs of this decision
```

---

## 4. Tools & Libraries (`tool`)

**Purpose**: Capture knowledge about tools, libraries, and dependencies.

**Folder**: `./csharp-compounding-docs/tools/`

**Skill**: `/cdocs:tool`

**Schema**:
```yaml
name: tool
description: Library gotchas, configuration nuances, and dependency knowledge

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

classification_hints:
  - "library"
  - "package"
  - "NuGet"
  - "dependency"
  - "version"
  - "configuration"
  - "API"
  - "SDK"
  - "framework"
  - "third-party"

required_fields:
  - name: tool_name
    type: string
    description: Name of tool/library
  - name: version
    type: string
    description: Version where behavior observed
  - name: knowledge_type
    type: enum
    values: [gotcha, configuration, integration, performance, workaround, deprecation]

optional_fields:
  - name: official_docs_gap
    type: boolean
    description: Is this missing from official docs?
  - name: related_tools
    type: array
    description: Related tools/libraries
```

---

## 5. Coding Styles & Preferences (`style`)

**Purpose**: Document coding conventions, preferences, and team standards.

**Folder**: `./csharp-compounding-docs/styles/`

**Skill**: `/cdocs:style`

**Schema**:
```yaml
name: style
description: Coding conventions, preferences, and team standards

trigger_phrases:
  - "always"
  - "never"
  - "prefer"
  - "convention"
  - "standard"
  - "rule"
  - "don't forget"
  - "remember to"

classification_hints:
  - "convention"
  - "style"
  - "naming"
  - "formatting"
  - "standard"
  - "best practice"
  - "guideline"
  - "rule"
  - "preference"

required_fields:
  - name: style_type
    type: enum
    values: [naming, architecture, error_handling, testing, documentation, formatting, async_patterns]
  - name: scope
    type: enum
    values: [project, module, team]
  - name: rationale
    type: string
    description: Why this style is preferred

optional_fields:
  - name: examples
    type: array
    description: Good and bad examples
  - name: exceptions
    type: string
    description: When this rule doesn't apply
```

---

## Capture Guidelines

### DO Capture

- Non-obvious solutions that took investigation
- Architectural decisions and their rationale
- Library quirks that caused confusion
- Coding patterns the team should follow
- Product insights that inform future development

### DO NOT Capture

- Trivial fixes (typos, syntax errors)
- Information easily found in official documentation
- Temporary workarounds with known expiration
- Personal preferences not agreed upon by team

### Exception: Common Misconceptions

Even seemingly trivial items warrant documentation if:
- The misconception is common (you've seen it multiple times)
- The mistake is easy to make
- The correct approach is non-obvious

---

## Cross-Referencing

Documents can reference each other using standard markdown links.

### Link Format

```markdown
See also: [Database connection pooling issue](../problems/db-pool-exhaustion-20250115.md)
```

### Resolution Behavior

When the MCP server returns RAG results:
1. Parse the source documents with Markdig
2. Extract all relative markdown links
3. Resolve links to absolute paths
4. Return linked document metadata (path, char count) alongside main results
5. Agent decides whether to load linked documents based on token budget

> **Background**: Markdig is the recommended .NET markdown parser, providing full AST traversal, built-in YAML frontmatter support via the `UseYamlFrontMatter()` extension, and `Descendants<LinkInline>()` for link extraction. See [.NET Markdown Parser Research](../../research/dotnet-markdown-parser-research.md).
