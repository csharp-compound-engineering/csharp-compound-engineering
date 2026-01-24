# Doc-Type Schema Files

> **Status**: [DRAFT]
> **Parent**: [configuration.md](../configuration.md)

> **Background**: JSON Schema Draft 2020-12 is the standard for YAML schema validation, with comprehensive keyword support for strings, numbers, arrays, and objects, plus composition keywords like `allOf`, `anyOf`, and `oneOf`. See [YAML Schema Formats Research](../../research/yaml-schema-formats.md).

> **Background**: For .NET implementation, JsonSchema.Net combined with YamlDotNet provides full Draft 2020-12 compliance with System.Text.Json integration. See [JSON Schema Validation Libraries Research](../../research/json-schema-validation-libraries-research.md).

---

## Location

`./csharp-compounding-docs/schemas/{doc-type}.schema.yaml` or `.json`

---

## Format

Both YAML and JSON schemas are supported. The **file extension determines the parser used**:
- `.yaml` or `.yml` → YAML parser
- `.json` → JSON parser

**Note**: If the file content doesn't match its extension (e.g., JSON content in a `.yaml` file), parsing will fail with a schema validation error.

---

## Common Schema Structure

Schemas use standard [JSON Schema Draft 2020-12](https://json-schema.org/draft/2020-12/schema). Both YAML and JSON file formats are supported.

```yaml
# ./csharp-compounding-docs/schemas/api-contract.schema.yaml
$schema: "https://json-schema.org/draft/2020-12/schema"
title: "API Contract Documentation Schema"
type: object
required:
  - title
  - date
  - summary
  - contract_type
  - api_version

properties:
  title:
    type: string
    description: "Title of the API contract decision"

  date:
    type: string
    pattern: '^\d{4}-\d{2}-\d{2}$'
    description: "Date documented (YYYY-MM-DD)"

  summary:
    type: string
    description: "One-line summary"

  contract_type:
    type: string
    enum:
      - endpoint_design
      - request_format
      - response_format
      - error_handling
      - versioning
      - authentication
    description: "Type of API contract decision"

  api_version:
    type: string
    description: "API version this applies to"

  affected_endpoints:
    type: array
    items:
      type: string
    description: "List of affected API endpoints"

  breaking_change:
    type: boolean
    description: "Is this a breaking change?"

  tags:
    type: array
    items:
      type: string
    description: "Searchable keywords"
```

---

## Built-in Schema Locations

Built-in doc-type schemas are embedded in the plugin:

```
${CLAUDE_PLUGIN_ROOT}/skills/
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

**Note**: `${CLAUDE_PLUGIN_ROOT}` resolves to the plugin's installation directory (varies by installation scope). See [Plugin Installation Research](../../research/claude-code-plugin-installation-mechanism.md#environment-variable-claude_plugin_root) for details.
