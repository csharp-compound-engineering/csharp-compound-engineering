---
name: cdocs:create-type
description: Create new custom doc-type with dedicated skill and schema
allowed-tools:
  - Read
  - Write
  - Bash
  - mcp__sequential-thinking__sequentialthinking  # Schema design reasoning
preconditions:
  - Project activated via /cdocs:activate
auto-invoke:
  trigger: none  # Manual invocation only
---

# Create Custom Doc-Type Skill

## Purpose

Create a new custom documentation type with its own schema, validation rules, classification metadata, and dedicated capture skill.

## Intake

Manual invocation: `/cdocs:create-type`

No auto-invocation - this is an administrative action requiring user intent.

## Process

### Step 1: Interview User

Gather information about the new doc-type through interactive questions.

**Ask about**:

1. **Name** (kebab-case identifier)
   - "What should we call this doc-type? (use kebab-case, e.g., 'api-contract')"
   - Validate: lowercase, hyphens only, no spaces

2. **Description** (one-line summary)
   - "Briefly describe what this doc-type captures:"

3. **Required Fields**
   - "What fields are required for this doc-type?"
   - For each field, ask:
     - Field name (snake_case)
     - Type (string, enum, array, date, boolean)
     - If enum: list of allowed values
     - Description

4. **Optional Fields**
   - "Any optional fields?"
   - Same prompts as required fields

5. **Trigger Phrases**
   - "What phrases in conversation should trigger this doc-type?"
   - Examples: "API endpoint", "breaking change", "performance issue"
   - Collect 5-10 phrases

6. **Classification Hints**
   - "What semantic signals indicate this doc-type is appropriate?"
   - Examples: "HTTP method", "REST", "latency", "throughput"
   - Collect 5-10 hints

**BLOCKING**: Wait for user responses before proceeding.

### Step 2: Reasoning with Sequential Thinking

Use the Sequential Thinking MCP to analyze the design:

**Call MCP**: `mcp__sequential-thinking__sequentialthinking`

**Reasoning prompts**:

1. **Field Relationship Analysis**
   ```
   Given these fields: {field_list}
   - Are there missing required fields?
   - Do any enum values overlap confusingly?
   - Should any fields have validation constraints (min/max length, regex)?
   - Are there implicit dependencies between fields?
   ```

2. **Trigger Phrase Coverage**
   ```
   Given these trigger phrases: {phrase_list}
   - Do they cover common variations?
   - Are any too broad (would match unrelated conversations)?
   - Are any redundant?
   - What additional phrases would improve coverage?
   ```

3. **Classification Hint Validation**
   ```
   Given classification hints: {hint_list}
   And existing doc-types: {existing_types}
   - Do hints overlap significantly with existing types?
   - Are hints specific enough to reduce false positives?
   - How do hints complement trigger phrases for two-stage classification?
   ```

4. **Schema Validation Logic**
   ```
   Given the schema design:
   - What validation rules should be enforced?
   - Are there cross-field constraints?
   - Should any fields have default values?
   ```

**Output**: Refined schema with reasoning notes.

Present reasoning to user and ask for confirmation or adjustments.

**BLOCKING**: Wait for user approval before proceeding.

### Step 3: Generate Schema File

Create schema file at:
- Path: `./csharp-compounding-docs/schemas/{name}.schema.yaml`

**Schema structure**:

```yaml
name: {name}
description: {description}
folder: {name}s  # Pluralized for folder name

# Classification metadata
trigger_phrases:
  - "{phrase1}"
  - "{phrase2}"
  # ... from user input

classification_hints:
  - "{hint1}"
  - "{hint2}"
  # ... from user input

required_fields:
  - name: {field_name}
    type: {type}
    description: {description}
    # For enum fields:
    values: [value1, value2, value3]
  # ... more fields

optional_fields:
  - name: {field_name}
    type: {type}
    description: {description}
  # ... more fields
```

Use Write tool to create the schema file.

### Step 4: Generate Dedicated Capture Skill

Create skill file at:
- Path: `./.claude/skills/cdocs-{name}/SKILL.md`

**Note**: Project scope (`.claude/skills/`) NOT plugin scope - ensures project-specific customization.

**Skill structure**:

```yaml
---
name: cdocs:{name}
description: {description}
allowed-tools:
  - Read
  - Write
preconditions:
  - Project activated via /cdocs:activate
auto-invoke:
  trigger: conversation-pattern
  patterns:
    {trigger_phrases from schema}
---

# {Name} Documentation Skill

## Purpose

{description}

## Intake

This skill captures {name} documentation from conversation context.

Auto-triggers when detecting: {trigger_phrases}

Manual invocation: `/cdocs:{name}`

## Process

### Step 1: Gather Context

Extract from conversation history:
- Core insight related to {name}
- Supporting details and examples
- Related context (files, versions, etc.)

**BLOCKING**: If critical context missing, ask user and WAIT.

### Step 2: Validate Schema

Load schema from `./csharp-compounding-docs/schemas/{name}.schema.yaml`.

Validate all required fields:
{list required fields}

**BLOCK if validation fails** - show specific errors.

### Step 3: Write Documentation

1. Generate filename: `{sanitized-title}-{YYYYMMDD}.md`
2. Create directory: `mkdir -p ./csharp-compounding-docs/{folder}/`
3. Write file with YAML frontmatter + markdown body

**Frontmatter structure**:
```yaml
---
type: {name}
{required_fields}
{optional_fields}
created: {ISO_8601_timestamp}
---
```

### Step 4: Post-Capture Options

✓ Documentation captured

File created: `./csharp-compounding-docs/{folder}/{filename}.md`

What's next?
1. Continue workflow
2. Link related docs
3. View documentation
4. Other

## Schema Reference

See `./csharp-compounding-docs/schemas/{name}.schema.yaml`

## Examples

[Include example frontmatter for this doc-type]
```

Use Write tool to create the skill file.

### Step 5: Update Configuration

Read `./csharp-compounding-docs/config.json`.

Add new doc-type to `doc_types` array:

```json
{
  "doc_types": [
    {
      "name": "{name}",
      "schema_path": "./schemas/{name}.schema.yaml",
      "folder": "{folder}",
      "enabled": true
    }
  ]
}
```

Use Write tool to update config.

### Step 6: Report Completion

Display confirmation to user:

```
✓ Custom doc-type created: {name}

Generated files:
- Schema: ./csharp-compounding-docs/schemas/{name}.schema.yaml
- Skill: ./.claude/skills/cdocs-{name}/SKILL.md
- Config: ./csharp-compounding-docs/config.json (updated)

How to use:
1. Reload Claude Code to activate the new skill
2. Use `/cdocs:{name}` to manually capture documentation
3. Auto-triggers on: {trigger_phrases}

Example:
  User: "We changed the API endpoint to use POST instead of GET"
  Claude: [Detects trigger "API endpoint"] Would you like to capture this as {name} documentation?
```

## Schema Reference

This skill generates schemas dynamically based on user input. See the generated `.schema.yaml` file for the custom doc-type.

## Examples

### Example 1: API Contract Doc-Type

**User Input**:
```
Name: api-contract
Description: API design decisions and contract specifications
Required fields:
  - endpoint (string): API endpoint path
  - change_type (enum): [new, breaking, non_breaking, deprecated]
  - method (enum): [GET, POST, PUT, PATCH, DELETE]
Optional fields:
  - affected_consumers (array): List of known consumers
Trigger phrases:
  - "API endpoint"
  - "contract change"
  - "breaking change"
  - "API design"
Classification hints:
  - "HTTP method"
  - "request/response"
  - "REST"
  - "versioning"
```

**Generated Schema** (`./csharp-compounding-docs/schemas/api-contract.schema.yaml`):
```yaml
name: api-contract
description: API design decisions and contract specifications
folder: api-contracts

trigger_phrases:
  - "API endpoint"
  - "contract change"
  - "breaking change"
  - "API design"

classification_hints:
  - "HTTP method"
  - "request/response"
  - "REST"
  - "versioning"

required_fields:
  - name: endpoint
    type: string
    description: API endpoint path

  - name: change_type
    type: enum
    values: [new, breaking, non_breaking, deprecated]
    description: Type of API change

  - name: method
    type: enum
    values: [GET, POST, PUT, PATCH, DELETE]
    description: HTTP method

optional_fields:
  - name: affected_consumers
    type: array
    description: List of known consumers affected
```

**Generated Skill** (`./.claude/skills/cdocs-api-contract/SKILL.md`):
```yaml
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
---

# API Contract Documentation Skill
[... skill instructions ...]
```

### Example 2: Performance Benchmark Doc-Type

**User Input**:
```
Name: performance-benchmark
Description: Performance test results and optimization findings
Required fields:
  - scenario (string): Test scenario description
  - metric (enum): [latency, throughput, memory, cpu]
  - baseline (string): Baseline measurement
  - result (string): Optimized measurement
Optional fields:
  - optimization (string): What changed
  - environment (string): Test environment details
Trigger phrases:
  - "performance test"
  - "benchmark"
  - "latency improved"
  - "throughput increased"
Classification hints:
  - "milliseconds"
  - "requests per second"
  - "MB/s"
  - "CPU usage"
```

## Notes

- Custom skills are project-scoped (`.claude/skills/`) not plugin-scoped
- Each custom doc-type gets its own independent capture skill
- Sequential Thinking helps validate schema design and avoid conflicts
- Trigger phrases and classification hints work together for two-stage classification
- Skills must be reloaded (restart Claude Code) to activate after creation
