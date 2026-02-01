# Phase 087: /cdocs:codebase Capture Skill

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Skills System
> **Prerequisites**: Phase 081 (Skills Framework Base), Phase 082 (Capture Skill Base Class), Phase 083 (Problem Skill)

---

## Spec References

This phase implements the `/cdocs:codebase` capture skill as defined in:

- **spec/skills/capture-skills.md** - [/cdocs:codebase](../spec/skills/capture-skills.md#cdocscodebase) - Skill behavior and trigger phrases
- **spec/doc-types/built-in-types.md** - [Codebase Knowledge](../spec/doc-types/built-in-types.md#3-codebase-knowledge-codebase) - Schema definition
- **research/claude-code-skills-research.md** - YAML frontmatter and auto-invoke mechanisms
- **research/building-claude-code-skills-research.md** - Skill file structure

---

## Objectives

1. Create the SKILL.md file for `/cdocs:codebase` capture skill
2. Implement auto-invoke trigger phrases for architectural decisions
3. Create schema.yaml for codebase doc-type validation
4. Configure output directory as `codebase/`
5. Support all schema fields including patterns, decisions, rationale
6. Implement code reference linking for `files_involved` field

---

## Acceptance Criteria

### SKILL.md File Creation
- [ ] Skill file created at `plugins/csharp-compounding-docs/skills/cdocs-codebase.md`
- [ ] YAML frontmatter includes:
  - [ ] `name: cdocs:codebase`
  - [ ] `description` with semantic matching keywords for auto-invoke
  - [ ] `trigger_phrases` array from spec
  - [ ] `doc_type: codebase`
  - [ ] `output_dir: codebase/`
- [ ] Skill instructions clearly define capture behavior
- [ ] MCP integration section references Sequential Thinking for trade-off analysis

### Trigger Phrase Configuration
- [ ] All trigger phrases from spec implemented:
  - [ ] "decided to"
  - [ ] "going with"
  - [ ] "settled on"
  - [ ] "our approach"
  - [ ] "the pattern is"
  - [ ] "architecture"
  - [ ] "structure"
- [ ] Classification hints for enhanced detection:
  - [ ] "module"
  - [ ] "component"
  - [ ] "pattern"
  - [ ] "design decision"
  - [ ] "layer"
  - [ ] "dependency"
  - [ ] "separation"
  - [ ] "SOLID"

### Schema Definition
- [ ] Schema file created at `plugins/csharp-compounding-docs/schemas/codebase.schema.yaml`
- [ ] Required fields defined:
  - [ ] `knowledge_type` - enum: `architecture_decision`, `code_pattern`, `module_interaction`, `data_flow`, `dependency_rationale`
  - [ ] `scope` - enum: `system`, `module`, `component`, `function`
- [ ] Optional fields defined:
  - [ ] `files_involved` - array of file paths
  - [ ] `alternatives_considered` - array of alternatives evaluated
  - [ ] `trade_offs` - string describing trade-offs
- [ ] Common required fields inherited (doc_type, title, date, summary, significance)

### Code Reference Linking
- [ ] `files_involved` field supports relative paths from project root
- [ ] Skill instructions guide extraction of relevant file paths from conversation
- [ ] File paths validated as existing files when possible
- [ ] Links rendered as clickable in markdown output

### Output Configuration
- [ ] Documents written to `./csharp-compounding-docs/codebase/`
- [ ] Filename format: `{sanitized-title}-{YYYYMMDD}.md`
- [ ] Frontmatter includes all required and applicable optional fields
- [ ] Body contains detailed rationale and context

---

## Implementation Notes

### Skill File Structure

Create `plugins/csharp-compounding-docs/skills/cdocs-codebase.md`:

```markdown
---
name: cdocs:codebase
description: |
  Capture codebase architectural knowledge and design decisions.
  Auto-triggers when discussing architecture decisions, code patterns, module interactions,
  data flow designs, or dependency rationale. Detects phrases like "decided to", "our approach",
  "the pattern is", "architecture", "structure", or when explaining why code is organized
  in a particular way.
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
doc_type: codebase
output_dir: codebase/
schema: codebase.schema.yaml
---

# /cdocs:codebase - Capture Codebase Knowledge

## Purpose

Capture architectural decisions, code patterns, and structural knowledge about the codebase.
This skill documents the "why" behind code organization, not just the "what".

## When to Use

This skill should be invoked when:
- Making or explaining an architectural decision
- Documenting a code pattern used in the project
- Explaining how modules interact with each other
- Describing data flow through the system
- Justifying a dependency choice

## Capture Process

1. **Identify the Knowledge Type**
   - `architecture_decision` - High-level structural choices
   - `code_pattern` - Recurring implementation patterns
   - `module_interaction` - How components communicate
   - `data_flow` - How data moves through the system
   - `dependency_rationale` - Why specific dependencies were chosen

2. **Determine the Scope**
   - `system` - Affects entire application
   - `module` - Affects a specific module/namespace
   - `component` - Affects a single class or service
   - `function` - Affects method-level implementation

3. **Gather Context from Conversation**
   - Extract the decision or pattern being discussed
   - Identify alternatives that were considered
   - Document trade-offs and rationale
   - List relevant file paths involved

4. **Use Sequential Thinking MCP** (for complex decisions)
   - Analyze trade-offs between alternatives
   - Document reasoning chain for the decision
   - Identify potential future implications

5. **Validate and Generate Document**
   - Validate against codebase schema
   - Generate filename: `{sanitized-title}-{YYYYMMDD}.md`
   - Write to `./csharp-compounding-docs/codebase/`

## Schema Fields

### Required Fields
| Field | Type | Description |
|-------|------|-------------|
| `doc_type` | string | Always "codebase" |
| `title` | string | Descriptive title of the knowledge |
| `date` | string | YYYY-MM-DD format |
| `summary` | string | One-line summary for search results |
| `significance` | enum | architectural, behavioral, performance, correctness, convention, integration |
| `knowledge_type` | enum | architecture_decision, code_pattern, module_interaction, data_flow, dependency_rationale |
| `scope` | enum | system, module, component, function |

### Optional Fields
| Field | Type | Description |
|-------|------|-------------|
| `files_involved` | array | Relative paths to relevant files |
| `alternatives_considered` | array | Other approaches that were evaluated |
| `trade_offs` | string | Trade-offs of this decision |
| `tags` | array | Searchable tags |
| `related_docs` | array | Links to related documentation |

## Example Output

```yaml
---
doc_type: codebase
title: Repository Pattern with Unit of Work
date: 2025-01-24
summary: Using repository pattern with unit of work for data access layer
significance: architectural
knowledge_type: architecture_decision
scope: system
files_involved:
  - src/CompoundDocs.Data/Repositories/IRepository.cs
  - src/CompoundDocs.Data/Repositories/BaseRepository.cs
  - src/CompoundDocs.Data/UnitOfWork/IUnitOfWork.cs
alternatives_considered:
  - Direct DbContext injection
  - CQRS with MediatR
  - Active Record pattern
trade_offs: |
  Adds abstraction layer but provides better testability and
  separation of concerns. Slightly more boilerplate code.
tags:
  - data-access
  - repository-pattern
  - entity-framework
---

# Repository Pattern with Unit of Work

## Decision

We implement the Repository pattern combined with Unit of Work for all data access operations.

## Rationale

1. **Testability**: Repositories can be easily mocked for unit testing
2. **Separation of Concerns**: Business logic doesn't depend on EF Core directly
3. **Consistency**: All data access follows the same patterns
4. **Transaction Management**: Unit of Work provides explicit transaction boundaries

## Alternatives Considered

### Direct DbContext Injection
- Simpler but couples business logic to EF Core
- Harder to unit test without in-memory database

### CQRS with MediatR
- More powerful but adds significant complexity
- Better suited for larger applications with complex domain logic

### Active Record Pattern
- Very simple but violates separation of concerns
- Not idiomatic in C#/.NET ecosystem

## Trade-offs

- Additional abstraction layer adds some boilerplate
- Developers must understand the pattern
- May be overkill for simple CRUD operations

## Files Involved

- `src/CompoundDocs.Data/Repositories/IRepository.cs` - Generic repository interface
- `src/CompoundDocs.Data/Repositories/BaseRepository.cs` - Base implementation
- `src/CompoundDocs.Data/UnitOfWork/IUnitOfWork.cs` - Unit of Work interface
```

## MCP Integration

### Sequential Thinking
Use Sequential Thinking MCP when:
- Multiple architectural alternatives need systematic comparison
- Trade-offs are complex and interconnected
- Decision has long-term implications requiring careful analysis

Example prompt for Sequential Thinking:
> "Analyze the trade-offs between [alternatives] for [architectural decision].
> Consider maintainability, testability, performance, and team familiarity."

## Capture Guidelines

### DO Capture
- Architectural decisions that affect multiple components
- Patterns that should be followed project-wide
- Non-obvious module interactions
- Dependency choices with specific rationale

### DO NOT Capture
- Standard framework conventions (document in external docs instead)
- Obvious patterns that any .NET developer would recognize
- Temporary architectural choices during prototyping
- Decisions still under active discussion

### Exception: Common Misconceptions
Even standard patterns warrant documentation if:
- The team has made mistakes applying them before
- There's a project-specific variation
- The "why" isn't obvious from the code alone
```

---

### Schema File Structure

Create `plugins/csharp-compounding-docs/schemas/codebase.schema.yaml`:

```yaml
$schema: "https://json-schema.org/draft/2020-12/schema"
title: "Codebase Knowledge Document Schema"
description: "Schema for architecture decisions, code patterns, and structural knowledge"
type: object

required:
  - doc_type
  - title
  - date
  - summary
  - significance
  - knowledge_type
  - scope

properties:
  doc_type:
    type: string
    const: "codebase"
    description: "Document type identifier"

  title:
    type: string
    minLength: 1
    description: "Descriptive title of the architectural knowledge"

  date:
    type: string
    pattern: '^\d{4}-\d{2}-\d{2}$'
    description: "Date in YYYY-MM-DD format"

  summary:
    type: string
    minLength: 1
    description: "One-line summary for search results"

  significance:
    type: string
    enum:
      - architectural
      - behavioral
      - performance
      - correctness
      - convention
      - integration
    description: "Significance level of the knowledge"

  knowledge_type:
    type: string
    enum:
      - architecture_decision
      - code_pattern
      - module_interaction
      - data_flow
      - dependency_rationale
    description: "Category of codebase knowledge"

  scope:
    type: string
    enum:
      - system
      - module
      - component
      - function
    description: "Scope of impact for this knowledge"

  files_involved:
    type: array
    items:
      type: string
    description: "Relative paths to relevant source files"

  alternatives_considered:
    type: array
    items:
      type: string
    description: "Other approaches that were evaluated"

  trade_offs:
    type: string
    description: "Trade-offs and considerations for this decision"

  tags:
    type: array
    items:
      type: string
    description: "Searchable tags for categorization"

  related_docs:
    type: array
    items:
      type: string
    description: "Paths to related documentation files"

  supersedes:
    type: string
    description: "Path to document this supersedes"

  promotion_level:
    type: string
    enum:
      - standard
      - important
      - critical
    default: standard
    description: "Visibility/priority level"
```

---

## File Structure

After implementation, the following files should exist:

```
plugins/csharp-compounding-docs/
├── skills/
│   └── cdocs-codebase.md
└── schemas/
    └── codebase.schema.yaml
```

---

## Dependencies

### Depends On
- **Phase 081**: Skills Framework Base - Provides skill loading and execution infrastructure
- **Phase 082**: Capture Skill Base Class - Provides common capture behavior
- **Phase 083**: Problem Skill - Pattern reference for capture skill implementation

### Blocks
- **Phase 088**: Insight Skill - Follows same capture skill pattern
- **Phase 089**: Tool Skill - Follows same capture skill pattern
- **Phase 090**: Style Skill - Follows same capture skill pattern
- Query skills that search codebase documents

---

## Verification Steps

After completing this phase, verify:

1. **Skill File Validation**
   - YAML frontmatter parses correctly
   - All required fields present
   - Trigger phrases match spec exactly
   - Description contains auto-invoke keywords

2. **Schema Validation**
   - Schema file parses as valid YAML
   - JSON Schema validates correctly
   - All enum values match spec
   - Required fields enforced

3. **Manual Invocation Test**
   - `/cdocs:codebase` invokes skill successfully
   - Skill prompts for required fields
   - Document generated in correct directory
   - Filename format correct

4. **Auto-Invoke Trigger Test**
   - Conversation containing "decided to use repository pattern" triggers suggestion
   - Conversation containing "our approach is to separate concerns" triggers suggestion
   - Conversation containing "the pattern is dependency injection" triggers suggestion

5. **Schema Enforcement Test**
   - Missing `knowledge_type` - validation error
   - Invalid `scope` value - validation error
   - Valid document - passes validation

6. **Code Reference Test**
   - `files_involved` accepts array of paths
   - Paths render correctly in output markdown

---

## Testing Notes

### Unit Tests

Create tests in `tests/CompoundDocs.Tests/Skills/`:

```csharp
// CodebaseSkillTests.cs
[Fact] public void Skill_HasCorrectTriggerPhrases()
[Fact] public void Skill_OutputDirIsCodebase()
[Fact] public void Skill_SchemaReferenceIsCorrect()

// CodebaseSchemaTests.cs
[Fact] public async Task Schema_ValidatesRequiredFields()
[Fact] public async Task Schema_RejectsInvalidKnowledgeType()
[Fact] public async Task Schema_RejectsInvalidScope()
[Fact] public async Task Schema_AcceptsOptionalFilesInvolved()
[Fact] public async Task Schema_AcceptsOptionalAlternatives()
```

### Integration Tests

```csharp
// CodebaseSkillIntegrationTests.cs
[Fact] public async Task CaptureSkill_CreatesDocumentInCorrectDirectory()
[Fact] public async Task CaptureSkill_GeneratesValidFrontmatter()
[Fact] public async Task CaptureSkill_SanitizesFilename()
[Fact] public async Task CaptureSkill_IncludesFilesInvolved()
```

---

## Notes

- **Trigger Phrase Overlap**: "decided to" and "settled on" may overlap with other capture skills; this is expected and handled by `/cdocs:capture-select`
- **Sequential Thinking Integration**: The skill should recommend (not require) Sequential Thinking for complex multi-alternative decisions
- **File Path Validation**: File paths in `files_involved` should be validated when possible, but missing files should not block capture (files may be deleted later)
- **Scope Determination**: When scope is ambiguous, prefer broader scope (system > module > component > function)
- **Auto-Invoke Sensitivity**: Architecture-related phrases are common; tune auto-invoke to require multiple signals before triggering

---

## Change Log

| Date | Changes |
|------|---------|
| 2025-01-24 | Initial phase creation |
