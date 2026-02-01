# Phase 102: Built-in Doc-Type Schemas

> **Category**: Skills System
> **Prerequisites**: Phase 009 (Plugin Directory Structure), Phase 014 (Schema Validation Library Integration)
> **Estimated Effort**: 3-4 hours
> **Status**: Pending

---

## Objective

Create the five built-in doc-type schema files that are embedded within the plugin under `${CLAUDE_PLUGIN_ROOT}/skills/`. These schemas define the structure and validation rules for the core document types: problem, insight, codebase, tool, and style.

---

## Success Criteria

- [ ] `skills/cdocs-problem/schema.yaml` created with complete schema definition
- [ ] `skills/cdocs-insight/schema.yaml` created with complete schema definition
- [ ] `skills/cdocs-codebase/schema.yaml` created with complete schema definition
- [ ] `skills/cdocs-tool/schema.yaml` created with complete schema definition
- [ ] `skills/cdocs-style/schema.yaml` created with complete schema definition
- [ ] All schemas use JSON Schema Draft 2020-12 format
- [ ] All schemas include `trigger_phrases` and `classification_hints` for distributed capture
- [ ] All schemas validate successfully with JsonSchema.Net
- [ ] Schema directory structure matches spec convention

---

## Specification References

| Document | Section | Relevance |
|----------|---------|-----------|
| [spec/doc-types/built-in-types.md](../spec/doc-types/built-in-types.md) | All 5 built-in schemas | Complete schema definitions with triggers and hints |
| [spec/configuration/schema-files.md](../spec/configuration/schema-files.md) | Built-in Schema Locations | Directory structure under `${CLAUDE_PLUGIN_ROOT}/skills/` |
| [spec/configuration/schema-files.md](../spec/configuration/schema-files.md) | Common Schema Structure | JSON Schema Draft 2020-12 format |

---

## Tasks

### Task 102.1: Create Problem Schema Directory and File

Create `plugins/csharp-compounding-docs/skills/cdocs-problem/schema.yaml`:

```yaml
# Problem & Solution Doc-Type Schema
# Mirrors the original compound-engineering paradigm: each solved problem teaches the system
$schema: "https://json-schema.org/draft/2020-12/schema"
title: "Problem & Solution Documentation Schema"
description: "Solved problems with symptoms, root cause, and solution"
type: object

# Distributed capture pattern: triggers and hints for auto-detection
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

required:
  - title
  - date
  - summary
  - problem_type
  - symptoms
  - root_cause
  - solution

properties:
  title:
    type: string
    description: "Title of the problem/solution document"
    minLength: 1
    maxLength: 200

  date:
    type: string
    pattern: '^\d{4}-\d{2}-\d{2}$'
    description: "Date documented (YYYY-MM-DD)"

  summary:
    type: string
    description: "One-line summary of the problem and solution"
    minLength: 1
    maxLength: 500

  problem_type:
    type: string
    enum:
      - bug
      - configuration
      - integration
      - performance
      - security
      - data
    description: "Category of the problem"

  symptoms:
    type: array
    items:
      type: string
    minItems: 1
    description: "Observable symptoms of the problem"

  root_cause:
    type: string
    description: "The underlying cause of the problem"
    minLength: 1

  solution:
    type: string
    description: "How the problem was fixed"
    minLength: 1

  component:
    type: string
    description: "Affected component/module"

  severity:
    type: string
    enum:
      - critical
      - high
      - medium
      - low
    description: "Severity level of the problem"

  prevention:
    type: string
    description: "How to prevent recurrence"

  tags:
    type: array
    items:
      type: string
    description: "Searchable keywords"

additionalProperties: false
```

---

### Task 102.2: Create Insight Schema Directory and File

Create `plugins/csharp-compounding-docs/skills/cdocs-insight/schema.yaml`:

```yaml
# Product/Project Insight Doc-Type Schema
# Captures significant learnings about the product being built
$schema: "https://json-schema.org/draft/2020-12/schema"
title: "Product/Project Insight Documentation Schema"
description: "Product, business, or domain learnings"
type: object

# Distributed capture pattern: triggers and hints for auto-detection
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

required:
  - title
  - date
  - summary
  - insight_type
  - impact_area

properties:
  title:
    type: string
    description: "Title of the insight document"
    minLength: 1
    maxLength: 200

  date:
    type: string
    pattern: '^\d{4}-\d{2}-\d{2}$'
    description: "Date documented (YYYY-MM-DD)"

  summary:
    type: string
    description: "One-line summary of the insight"
    minLength: 1
    maxLength: 500

  insight_type:
    type: string
    enum:
      - business_logic
      - user_behavior
      - domain_knowledge
      - feature_interaction
      - market_observation
    description: "Type of insight"

  impact_area:
    type: string
    description: "Which part of product this affects"
    minLength: 1

  confidence:
    type: string
    enum:
      - verified
      - hypothesis
      - observation
    description: "Confidence level of the insight"

  source:
    type: string
    description: "How this insight was discovered"

  tags:
    type: array
    items:
      type: string
    description: "Searchable keywords"

additionalProperties: false
```

---

### Task 102.3: Create Codebase Schema Directory and File

Create `plugins/csharp-compounding-docs/skills/cdocs-codebase/schema.yaml`:

```yaml
# Codebase Knowledge Doc-Type Schema
# Documents architectural decisions, code patterns, and structural knowledge
$schema: "https://json-schema.org/draft/2020-12/schema"
title: "Codebase Knowledge Documentation Schema"
description: "Architecture decisions, code patterns, and structural knowledge"
type: object

# Distributed capture pattern: triggers and hints for auto-detection
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

required:
  - title
  - date
  - summary
  - knowledge_type
  - scope

properties:
  title:
    type: string
    description: "Title of the codebase knowledge document"
    minLength: 1
    maxLength: 200

  date:
    type: string
    pattern: '^\d{4}-\d{2}-\d{2}$'
    description: "Date documented (YYYY-MM-DD)"

  summary:
    type: string
    description: "One-line summary of the knowledge"
    minLength: 1
    maxLength: 500

  knowledge_type:
    type: string
    enum:
      - architecture_decision
      - code_pattern
      - module_interaction
      - data_flow
      - dependency_rationale
    description: "Type of codebase knowledge"

  scope:
    type: string
    enum:
      - system
      - module
      - component
      - function
    description: "Scope of applicability"

  files_involved:
    type: array
    items:
      type: string
    description: "Relevant file paths"

  alternatives_considered:
    type: array
    items:
      type: string
    description: "Other approaches that were evaluated"

  trade_offs:
    type: string
    description: "Trade-offs of this decision"

  tags:
    type: array
    items:
      type: string
    description: "Searchable keywords"

additionalProperties: false
```

---

### Task 102.4: Create Tool Schema Directory and File

Create `plugins/csharp-compounding-docs/skills/cdocs-tool/schema.yaml`:

```yaml
# Tools & Libraries Doc-Type Schema
# Captures knowledge about tools, libraries, and dependencies
$schema: "https://json-schema.org/draft/2020-12/schema"
title: "Tools & Libraries Documentation Schema"
description: "Library gotchas, configuration nuances, and dependency knowledge"
type: object

# Distributed capture pattern: triggers and hints for auto-detection
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

required:
  - title
  - date
  - summary
  - tool_name
  - version
  - knowledge_type

properties:
  title:
    type: string
    description: "Title of the tool knowledge document"
    minLength: 1
    maxLength: 200

  date:
    type: string
    pattern: '^\d{4}-\d{2}-\d{2}$'
    description: "Date documented (YYYY-MM-DD)"

  summary:
    type: string
    description: "One-line summary of the tool knowledge"
    minLength: 1
    maxLength: 500

  tool_name:
    type: string
    description: "Name of tool/library"
    minLength: 1

  version:
    type: string
    description: "Version where behavior observed"
    minLength: 1

  knowledge_type:
    type: string
    enum:
      - gotcha
      - configuration
      - integration
      - performance
      - workaround
      - deprecation
    description: "Type of tool knowledge"

  official_docs_gap:
    type: boolean
    description: "Is this missing from official docs?"

  related_tools:
    type: array
    items:
      type: string
    description: "Related tools/libraries"

  tags:
    type: array
    items:
      type: string
    description: "Searchable keywords"

additionalProperties: false
```

---

### Task 102.5: Create Style Schema Directory and File

Create `plugins/csharp-compounding-docs/skills/cdocs-style/schema.yaml`:

```yaml
# Coding Styles & Preferences Doc-Type Schema
# Documents coding conventions, preferences, and team standards
$schema: "https://json-schema.org/draft/2020-12/schema"
title: "Coding Styles & Preferences Documentation Schema"
description: "Coding conventions, preferences, and team standards"
type: object

# Distributed capture pattern: triggers and hints for auto-detection
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

required:
  - title
  - date
  - summary
  - style_type
  - scope
  - rationale

properties:
  title:
    type: string
    description: "Title of the style document"
    minLength: 1
    maxLength: 200

  date:
    type: string
    pattern: '^\d{4}-\d{2}-\d{2}$'
    description: "Date documented (YYYY-MM-DD)"

  summary:
    type: string
    description: "One-line summary of the style rule"
    minLength: 1
    maxLength: 500

  style_type:
    type: string
    enum:
      - naming
      - architecture
      - error_handling
      - testing
      - documentation
      - formatting
      - async_patterns
    description: "Type of style rule"

  scope:
    type: string
    enum:
      - project
      - module
      - team
    description: "Scope of applicability"

  rationale:
    type: string
    description: "Why this style is preferred"
    minLength: 1

  examples:
    type: array
    items:
      type: string
    description: "Good and bad examples"

  exceptions:
    type: string
    description: "When this rule doesn't apply"

  tags:
    type: array
    items:
      type: string
    description: "Searchable keywords"

additionalProperties: false
```

---

### Task 102.6: Create Directory Structure

Ensure the following directory structure exists under the plugin:

```
plugins/csharp-compounding-docs/skills/
├── cdocs-problem/
│   └── schema.yaml
├── cdocs-insight/
│   └── schema.yaml
├── cdocs-codebase/
│   └── schema.yaml
├── cdocs-tool/
│   └── schema.yaml
└── cdocs-style/
    └── schema.yaml
```

**Commands**:
```bash
mkdir -p plugins/csharp-compounding-docs/skills/cdocs-problem
mkdir -p plugins/csharp-compounding-docs/skills/cdocs-insight
mkdir -p plugins/csharp-compounding-docs/skills/cdocs-codebase
mkdir -p plugins/csharp-compounding-docs/skills/cdocs-tool
mkdir -p plugins/csharp-compounding-docs/skills/cdocs-style
```

---

### Task 102.7: Remove Placeholder .gitkeep

After creating schema files, remove the placeholder:

```bash
rm plugins/csharp-compounding-docs/skills/.gitkeep
```

---

## Verification Checklist

After completing all tasks, verify:

1. **Directory Structure**:
   ```bash
   tree plugins/csharp-compounding-docs/skills/
   ```
   Expected output:
   ```
   plugins/csharp-compounding-docs/skills/
   ├── cdocs-codebase/
   │   └── schema.yaml
   ├── cdocs-insight/
   │   └── schema.yaml
   ├── cdocs-problem/
   │   └── schema.yaml
   ├── cdocs-style/
   │   └── schema.yaml
   └── cdocs-tool/
       └── schema.yaml
   ```

2. **YAML Syntax Validation**:
   ```bash
   # Using yq or yamllint to validate YAML syntax
   yamllint plugins/csharp-compounding-docs/skills/cdocs-problem/schema.yaml
   yamllint plugins/csharp-compounding-docs/skills/cdocs-insight/schema.yaml
   yamllint plugins/csharp-compounding-docs/skills/cdocs-codebase/schema.yaml
   yamllint plugins/csharp-compounding-docs/skills/cdocs-tool/schema.yaml
   yamllint plugins/csharp-compounding-docs/skills/cdocs-style/schema.yaml
   ```

3. **JSON Schema Validation** (after Phase 014 is complete):
   - Load each schema using the `SchemaLoader` from Phase 014
   - Verify schemas parse without errors
   - Test validation with sample frontmatter documents

4. **Required Fields Coverage**:
   | Schema | Required Fields |
   |--------|-----------------|
   | problem | title, date, summary, problem_type, symptoms, root_cause, solution |
   | insight | title, date, summary, insight_type, impact_area |
   | codebase | title, date, summary, knowledge_type, scope |
   | tool | title, date, summary, tool_name, version, knowledge_type |
   | style | title, date, summary, style_type, scope, rationale |

5. **Trigger Phrases Count**:
   | Schema | Trigger Count |
   |--------|---------------|
   | problem | 10 |
   | insight | 8 |
   | codebase | 7 |
   | tool | 9 |
   | style | 8 |

6. **Classification Hints Count**:
   | Schema | Hints Count |
   |--------|-------------|
   | problem | 9 |
   | insight | 9 |
   | codebase | 10 |
   | tool | 10 |
   | style | 9 |

---

## Dependencies

| Phase | Dependency Type | Description |
|-------|-----------------|-------------|
| Phase 009 | Hard | Plugin directory structure must exist |
| Phase 014 | Hard | Schema validation library required for testing |
| Phase 100+ | Provides | Skill phases will reference these schemas |

---

## Notes

- **Distributed Capture Pattern**: Each built-in doc-type has its own `trigger_phrases` and `classification_hints` arrays. The capture skills independently monitor for their triggers. When multiple skills trigger simultaneously, `/cdocs:capture-select` auto-invokes to let the user choose.

- **JSON Schema Draft 2020-12**: All schemas declare `$schema: "https://json-schema.org/draft/2020-12/schema"` for consistency with the validation library.

- **Common Fields**: All five schemas share common base fields (title, date, summary, tags) but have type-specific required and optional fields.

- **additionalProperties: false**: All schemas enforce strict validation by disallowing extra fields not defined in the schema.

- **Schema Location Convention**: Built-in schemas live under `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-{type}/schema.yaml`. Custom doc-types created via `/cdocs:create-type` will have schemas at `./csharp-compounding-docs/schemas/{type}.schema.yaml`.

- **Enum Values**: Enum values are intentionally kept lowercase with underscores to match YAML conventions and simplify user input.

---

## Change Log

| Date | Changes |
|------|---------|
| 2025-01-24 | Initial phase creation |
