# Phase 095: /cdocs:create-type Meta Skill

> **Status**: NOT_STARTED
> **Effort Estimate**: 6-8 hours
> **Category**: Skills System
> **Prerequisites**: Phase 081 (Skills Directory Structure), Phase 014 (Schema Validation Library Integration)

---

## Spec References

This phase implements the `/cdocs:create-type` meta skill defined in:

- **spec/skills/meta-skills.md** - [/cdocs:create-type](../spec/skills/meta-skills.md#cdocscreate-type) - Complete skill specification
- **spec/doc-types/custom-types.md** - [Custom Doc-Types](../spec/doc-types/custom-types.md) - Schema structure and classification metadata
- **research/sequential-thinking-mcp-verification.md** - Sequential Thinking MCP package verification
- **research/yaml-schema-formats.md** - YAML schema format patterns
- **research/building-claude-code-skills-research.md** - Skill file structure and YAML frontmatter

---

## Objectives

1. Create the SKILL.md file for `/cdocs:create-type` meta skill
2. Implement user interview workflow for gathering custom type details
3. Integrate Sequential Thinking MCP for schema design reasoning
4. Generate schema files with classification metadata
5. Generate dedicated capture skills for new doc-types
6. Update project configuration with new doc-type registration

---

## Acceptance Criteria

### SKILL.md File Creation

- [ ] SKILL.md file created at `plugins/csharp-compounding-docs/skills/cdocs-create-type/SKILL.md`
- [ ] YAML frontmatter includes required fields:
  - [ ] `name: cdocs:create-type`
  - [ ] `description: Create a new custom doc-type with dedicated skill and schema`
  - [ ] `allowed-tools: [Read, Write, Bash, mcp__sequential-thinking__sequentialthinking]`
  - [ ] `preconditions: [Project activated via /cdocs:activate]`
- [ ] Skill instructions are comprehensive and actionable

### User Interview Workflow

- [ ] Interview prompts for doc-type name (kebab-case validation)
- [ ] Interview prompts for description (human-readable purpose)
- [ ] Interview prompts for required fields with types and enums
- [ ] Interview prompts for optional fields with types
- [ ] Interview prompts for trigger phrases (auto-invoke patterns)
- [ ] Interview prompts for classification hints (semantic signals)
- [ ] Name validation ensures no collision with built-in types
- [ ] Name validation ensures no collision with existing custom types

### Sequential Thinking MCP Integration

- [ ] Schema design reasoning via `mcp__sequential-thinking__sequentialthinking`
- [ ] Field relationship analysis and type recommendation
- [ ] Trigger phrase generation with semantic coverage analysis
- [ ] Classification hint validation for uniqueness vs existing types
- [ ] Two-stage classification strategy validation

### Schema File Generation

- [ ] Schema file generated at `./csharp-compounding-docs/schemas/{name}.schema.yaml`
- [ ] Schema includes all metadata fields:
  - [ ] `name` - Doc-type identifier
  - [ ] `description` - Human-readable description
  - [ ] `folder` - Storage folder name
  - [ ] `trigger_phrases` - Auto-invoke patterns
  - [ ] `classification_hints` - Semantic signals
  - [ ] `required_fields` - Required frontmatter fields
  - [ ] `optional_fields` - Optional frontmatter fields
- [ ] Field definitions include `name`, `type`, `description`
- [ ] Enum fields include `values` array

### Dedicated Skill Generation

- [ ] Skill file generated at `.claude/skills/cdocs-{name}/SKILL.md` (project scope)
- [ ] Generated skill includes YAML frontmatter:
  - [ ] `name: cdocs:{name}`
  - [ ] `description: Capture {name} documentation`
  - [ ] `allowed-tools: [Read, Write]`
  - [ ] `preconditions: [Project activated via /cdocs:activate]`
  - [ ] `auto-invoke.trigger: conversation-pattern`
  - [ ] `auto-invoke.patterns: [<trigger_phrases>]`
- [ ] Generated skill instructions include schema validation
- [ ] Generated skill instructions include frontmatter template

### Config Registration

- [ ] Project config updated at `./csharp-compounding-docs/config.json`
- [ ] New entry added to `custom_doc_types` array:
  - [ ] `name` - Doc-type identifier
  - [ ] `description` - Human-readable description
  - [ ] `folder` - Storage folder name
  - [ ] `schema_file` - Path to schema file
  - [ ] `skill_name` - Skill invocation name
- [ ] Config JSON remains valid after update

---

## Implementation Notes

### SKILL.md Structure

```yaml
---
name: cdocs:create-type
description: Create a new custom doc-type with dedicated skill and schema
allowed-tools:
  - Read
  - Write
  - Bash
  - mcp__sequential-thinking__sequentialthinking
preconditions:
  - Project activated via /cdocs:activate
---

# /cdocs:create-type

Create a new custom doc-type with a dedicated capture skill and schema.

## Overview

This skill guides you through creating a project-specific doc-type that:
- Has its own schema defining required and optional fields
- Has a dedicated capture skill (`/cdocs:{name}`) with auto-invoke triggers
- Is registered in the project configuration
- Stores documents in a dedicated folder

## Workflow

### Step 1: Gather Requirements

Interview the user to collect:

1. **Name** (kebab-case, e.g., `api-contract`)
   - Validate: Must be kebab-case
   - Validate: Must not conflict with built-in types (problem, insight, codebase, tool, style)
   - Validate: Must not conflict with existing custom types

2. **Description** (human-readable purpose)
   - Example: "API design decisions and contract specifications"

3. **Required Fields** (must be present in every document)
   - For each field, collect: name, type, description
   - Supported types: string, enum, array, boolean
   - For enum types, also collect allowed values

4. **Optional Fields** (may be present in documents)
   - Same structure as required fields

5. **Trigger Phrases** (phrases that auto-trigger capture)
   - Example: ["API endpoint", "contract change", "breaking change"]
   - Should be specific enough to avoid false positives
   - Should cover semantic variations

6. **Classification Hints** (semantic signals for content matching)
   - Example: ["HTTP method", "request/response", "REST", "versioning"]
   - Used in two-stage classification with trigger phrases

### Step 2: Design Schema (Sequential Thinking)

Use Sequential Thinking MCP to:

1. Analyze field relationships and recommend appropriate types/constraints
2. Generate comprehensive trigger phrases with semantic coverage
3. Validate classification hints don't overlap with existing doc-types
4. Ensure trigger phrases and classification hints work together

Example thought sequence:
```
Thought 1: Analyzing the requested doc-type "api-contract"...
Thought 2: Field "endpoint" should be string type for flexibility...
Thought 3: Field "change_type" should be enum to constrain valid values...
Thought 4: Trigger phrases should cover both REST and GraphQL contexts...
Thought 5: Classification hints should distinguish from "codebase" type...
```

### Step 3: Generate Schema File

Create `./csharp-compounding-docs/schemas/{name}.schema.yaml`:

```yaml
# Generated by /cdocs:create-type
name: {name}
description: {description}
folder: {folder}

trigger_phrases:
  - "phrase 1"
  - "phrase 2"

classification_hints:
  - "hint 1"
  - "hint 2"

required_fields:
  - name: field_name
    type: string|enum|array|boolean
    description: Field description
    values: [only for enum type]

optional_fields:
  - name: field_name
    type: string|enum|array|boolean
    description: Field description
```

### Step 4: Generate Capture Skill

Create `.claude/skills/cdocs-{name}/SKILL.md`:

```yaml
---
name: cdocs:{name}
description: Capture {name} documentation
allowed-tools:
  - Read
  - Write
preconditions:
  - Project activated via /cdocs:activate
auto-invoke:
  trigger: conversation-pattern
  patterns:
    - "trigger phrase 1"
    - "trigger phrase 2"
---

# /cdocs:{name}

Capture {description}.

## When to Capture

This skill auto-invokes when conversation contains:
{list trigger phrases}

Classification hints that confirm relevance:
{list classification hints}

## Document Structure

Create a new markdown file at:
`./csharp-compounding-docs/{folder}/{date}-{slug}.md`

### Required Frontmatter

```yaml
---
doc_type: {name}
title: [Descriptive title]
date: [YYYY-MM-DD]
summary: [One-line summary]
significance: [1-5]
{required fields with types}
---
```

### Optional Frontmatter

{optional fields with types}

### Body Content

[Instructions for body content specific to this doc-type]

## Validation

Before saving, validate:
1. All required frontmatter fields are present
2. Enum fields contain valid values
3. Field types match expected types
4. Filename follows naming convention
```

### Step 5: Update Project Configuration

Read and update `./csharp-compounding-docs/config.json`:

```json
{
  "custom_doc_types": [
    {
      "name": "{name}",
      "description": "{description}",
      "folder": "{folder}",
      "schema_file": "./schemas/{name}.schema.yaml",
      "skill_name": "cdocs:{name}"
    }
  ]
}
```

### Step 6: Report Completion

Inform user:
1. Schema file location
2. Skill file location
3. How to use the new skill (`/cdocs:{name}`)
4. How trigger phrases will auto-invoke the skill
5. How to test and iterate on trigger phrases

## Error Handling

- If name conflicts with existing type, prompt for different name
- If schema directory doesn't exist, create it
- If config update fails, report error and restore backup
- If Sequential Thinking unavailable, proceed with user-provided values

## Important Notes

- Custom skills are placed in `.claude/skills/` (project scope), NOT in the plugin folder
- This ensures custom doc-types are version-controlled with the project
- Each custom type operates independently like built-in types
```

### Interview Example Dialog

```
User: /cdocs:create-type

Claude: I'll help you create a new custom doc-type. Let's gather the details.

**What name would you like for this doc-type?** (kebab-case, e.g., "api-contract", "meeting-note")

User: api-contract

Claude: Great! "api-contract" is valid and doesn't conflict with existing types.

**What's the purpose of this doc-type?** (one sentence description)

User: API design decisions and contract specifications

Claude: Perfect. Now let's define the fields.

**What required fields should every document have?**

For each field, I need:
- Field name (snake_case)
- Type (string, enum, array, boolean)
- Description
- For enum: allowed values

User: endpoint (string) - API endpoint path, change_type (enum: new, breaking, non_breaking, deprecated) - Type of change

Claude: Got it. Any optional fields?

User: http_method (enum: GET, POST, PUT, PATCH, DELETE), affected_consumers (array) - List of consumers impacted

Claude: Now for auto-invocation.

**What phrases should trigger this doc-type?** (phrases that indicate API contract discussion)

User: API endpoint, contract change, breaking change, API design

Claude: And **what classification hints** help confirm this is the right doc-type? (semantic signals)

User: HTTP method, request/response, REST, GraphQL, versioning, payload

Claude: Let me analyze this with Sequential Thinking...

[Uses mcp__sequential-thinking__sequentialthinking to validate and enhance]

I'm ready to generate your custom doc-type. Here's what I'll create:

1. Schema: `./csharp-compounding-docs/schemas/api-contract.schema.yaml`
2. Skill: `.claude/skills/cdocs-api-contract/SKILL.md`
3. Config update: `./csharp-compounding-docs/config.json`

Shall I proceed?
```

### Generated Schema File Example

```yaml
# ./csharp-compounding-docs/schemas/api-contract.schema.yaml
# Generated by /cdocs:create-type on 2025-01-24

name: api-contract
description: API design decisions and contract specifications
folder: api-contracts

trigger_phrases:
  - "API endpoint"
  - "contract change"
  - "breaking change"
  - "API design"
  - "endpoint behavior"
  - "REST API"
  - "GraphQL schema"

classification_hints:
  - "HTTP method"
  - "request/response"
  - "REST"
  - "GraphQL"
  - "versioning"
  - "endpoint"
  - "payload"
  - "status code"

required_fields:
  - name: endpoint
    type: string
    description: API endpoint path
  - name: change_type
    type: enum
    values: [new, breaking, non_breaking, deprecated]
    description: Type of API change

optional_fields:
  - name: http_method
    type: enum
    values: [GET, POST, PUT, PATCH, DELETE]
    description: HTTP method for the endpoint
  - name: affected_consumers
    type: array
    description: List of known consumers affected by this change
  - name: migration_guide
    type: string
    description: Instructions for consumers to migrate
```

### Generated Skill File Example

```yaml
# .claude/skills/cdocs-api-contract/SKILL.md
---
name: cdocs:api-contract
description: Capture API contract documentation
allowed-tools:
  - Read
  - Write
preconditions:
  - Project activated via /cdocs:activate
auto-invoke:
  trigger: conversation-pattern
  patterns:
    - "API endpoint"
    - "contract change"
    - "breaking change"
    - "API design"
    - "endpoint behavior"
    - "REST API"
    - "GraphQL schema"
---

# /cdocs:api-contract

Capture API design decisions and contract specifications.

## When to Capture

This skill auto-invokes when conversation contains phrases like:
- "API endpoint"
- "contract change"
- "breaking change"
- "API design"
- "endpoint behavior"

Classification hints that confirm this is relevant content:
- HTTP method discussions
- Request/response formats
- REST or GraphQL patterns
- API versioning topics
- Payload structures

**Two-Stage Classification**: If trigger phrases match but classification hints don't, ask the user before capturing.

## Document Structure

Create a new markdown file at:
`./csharp-compounding-docs/api-contracts/YYYY-MM-DD-{slug}.md`

### Frontmatter Template

```yaml
---
doc_type: api-contract
title: [Descriptive title for the API change]
date: [YYYY-MM-DD]
summary: [One-line summary of the change]
significance: [1-5, where 5 is most significant]
endpoint: [API endpoint path, e.g., /api/v2/users]
change_type: [new | breaking | non_breaking | deprecated]
http_method: [GET | POST | PUT | PATCH | DELETE]  # optional
affected_consumers: []  # optional, list of affected consumers
migration_guide: |  # optional, multiline string
  Migration instructions...
---
```

### Body Content

Structure the document body with:

1. **Context** - Why this API change was needed
2. **Change Details** - Specific changes to the contract
3. **Before/After** - If applicable, show the difference
4. **Impact Assessment** - Who/what is affected
5. **Migration Path** - If breaking, how to migrate

## Validation Checklist

Before saving, verify:

- [ ] `doc_type` is "api-contract"
- [ ] `title` is descriptive and specific
- [ ] `date` is in YYYY-MM-DD format
- [ ] `summary` is a single line
- [ ] `significance` is between 1 and 5
- [ ] `endpoint` is a valid API path
- [ ] `change_type` is one of: new, breaking, non_breaking, deprecated
- [ ] If `http_method` provided, it's a valid HTTP method
- [ ] If `affected_consumers` provided, it's an array

## Example Document

```yaml
---
doc_type: api-contract
title: User Profile Endpoint V2 Migration
date: 2025-01-24
summary: Breaking change to /api/users/{id} response structure
significance: 5
endpoint: /api/v2/users/{id}
change_type: breaking
http_method: GET
affected_consumers:
  - mobile-app
  - partner-portal
migration_guide: |
  Update response parsing to use new nested structure.
  See migration guide: /docs/api-v2-migration.md
---

## Context

The existing user endpoint returned a flat JSON structure that...

## Change Details

The response now uses a nested structure for address information...

## Before/After

Before (v1):
```json
{ "name": "...", "street": "...", "city": "..." }
```

After (v2):
```json
{ "name": "...", "address": { "street": "...", "city": "..." } }
```

## Impact Assessment

All consumers parsing the response directly will need updates...

## Migration Path

1. Update response parsers to handle nested structure
2. Test against staging API
3. Deploy before deprecation date (2025-03-01)
```
```

---

## File Structure

After implementation, the following files should exist:

```
plugins/csharp-compounding-docs/
└── skills/
    └── cdocs-create-type/
        └── SKILL.md

# Generated files (per custom type):
.claude/skills/
└── cdocs-{name}/
    └── SKILL.md

./csharp-compounding-docs/
└── schemas/
    └── {name}.schema.yaml
```

---

## Dependencies

### Depends On
- **Phase 081**: Skills Directory Structure (skill file location)
- **Phase 014**: Schema Validation Library (schema format understanding)
- **Phase 009**: Plugin Directory Structure (plugin paths)
- **Phase 010**: Project Configuration (config.json format)

### Blocks
- Custom doc-type capture skills (depends on create-type to generate them)
- Doc-type enumeration tools (will list custom types created here)

---

## Verification Steps

After completing this phase, verify:

1. **SKILL.md Validity**:
   - YAML frontmatter parses without errors
   - All required fields present in frontmatter
   - Instructions are clear and complete

2. **Interview Workflow**:
   - Name validation rejects invalid formats
   - Name validation catches conflicts with existing types
   - All field types are supported
   - Enum values are collected properly

3. **Schema Generation**:
   - Schema file is valid YAML
   - All metadata fields present
   - Field definitions are complete
   - Trigger phrases and hints included

4. **Skill Generation**:
   - Generated skill file is valid YAML
   - Auto-invoke patterns match trigger phrases
   - Instructions reference schema fields correctly
   - Validation checklist covers all required fields

5. **Config Update**:
   - Config JSON remains valid
   - New type appears in custom_doc_types array
   - All required fields populated

### Manual Verification

```bash
# Verify SKILL.md exists
cat plugins/csharp-compounding-docs/skills/cdocs-create-type/SKILL.md

# Test YAML parsing
yq eval '.' plugins/csharp-compounding-docs/skills/cdocs-create-type/SKILL.md
```

---

## Testing Notes

Create manual test scenarios:

1. **Happy Path**: Create "api-contract" type with all fields
2. **Minimal Type**: Create type with only required fields, no optional
3. **Name Conflict**: Try creating "problem" (built-in) - should reject
4. **Invalid Name**: Try creating "API Contract" (not kebab-case) - should reject
5. **Sequential Thinking Unavailable**: Verify graceful fallback

---

## Notes

- Custom skills go in `.claude/skills/` (project scope), not plugin folder
- This ensures custom types are version-controlled with the project
- Each custom type's skill operates independently like built-in skills
- Sequential Thinking MCP enhances but is not required for basic functionality
- The skill should work even if Sequential Thinking server is unavailable
- Trigger phrases should be validated for semantic coverage
- Classification hints should be checked for overlap with existing types
