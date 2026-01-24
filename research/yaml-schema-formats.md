# YAML Schema Formats: Comprehensive Research

## Table of Contents
1. [JSON Schema for YAML](#1-json-schema-for-yaml)
2. [YAML Schema Specifications](#2-yaml-schema-specifications-yaml-11-vs-12)
3. [Popular Tools and Validators](#3-popular-yaml-schema-tools-and-validators)
4. [Schema Definition Patterns](#4-schema-definition-patterns)
5. [Best Practices](#5-best-practices-for-yaml-schema-design)
6. [Practical Examples](#6-practical-examples)

---

## 1. JSON Schema for YAML

### Why JSON Schema?

JSON Schema is the **most portable and broadly supported choice** for YAML validation. Since YAML is a superset of JSON, JSON Schema can validate most YAML documents effectively.

**Key advantages:**
- Mature specification with broad tooling support
- Cross-platform compatibility
- Excellent IDE integration
- Large ecosystem of validators across all major languages

### How It Works

Under the hood, YAML content is converted to JSON and then validated against the JSON Schema. This works seamlessly because:
- YAML's data model closely matches JSON's
- Most configuration files use JSON-compatible YAML features

### Core JSON Schema Keywords

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://example.com/my-config.schema.json",
  "title": "Application Configuration",
  "description": "Schema for application configuration files",
  "type": "object",
  "properties": {
    "name": {
      "type": "string",
      "description": "Application name"
    },
    "port": {
      "type": "integer",
      "minimum": 1,
      "maximum": 65535
    },
    "enabled": {
      "type": "boolean",
      "default": true
    }
  },
  "required": ["name", "port"]
}
```

### Validation Keywords by Type

**For strings:**
- `minLength`, `maxLength` - Length constraints
- `pattern` - Regex validation
- `format` - Predefined formats (email, uri, date-time, etc.)
- `enum` - Allowed values list

**For numbers:**
- `minimum`, `maximum` - Inclusive bounds
- `exclusiveMinimum`, `exclusiveMaximum` - Exclusive bounds
- `multipleOf` - Divisibility constraint

**For arrays:**
- `items` - Schema for array elements
- `minItems`, `maxItems` - Length constraints
- `uniqueItems` - Enforce uniqueness
- `contains` - At least one matching item

**For objects:**
- `properties` - Define property schemas
- `required` - Required property names
- `additionalProperties` - Control extra properties
- `patternProperties` - Properties matching patterns

### Composition Keywords

```json
{
  "allOf": [{ "$ref": "#/$defs/base" }, { "properties": {...} }],
  "anyOf": [{ "type": "string" }, { "type": "number" }],
  "oneOf": [{ "$ref": "#/$defs/optionA" }, { "$ref": "#/$defs/optionB" }],
  "not": { "type": "null" }
}
```

### Reusable Definitions with $defs

```json
{
  "$defs": {
    "address": {
      "type": "object",
      "properties": {
        "street": { "type": "string" },
        "city": { "type": "string" },
        "zipCode": { "type": "string", "pattern": "^[0-9]{5}$" }
      },
      "required": ["street", "city"]
    }
  },
  "type": "object",
  "properties": {
    "homeAddress": { "$ref": "#/$defs/address" },
    "workAddress": { "$ref": "#/$defs/address" }
  }
}
```

**Note:** In JSON Schema drafts 06 and 07, `$defs` was called `definitions`. Starting with draft 2019-09, `$defs` is the standard keyword.

---

## 2. YAML Schema Specifications (YAML 1.1 vs 1.2)

### YAML's Native Schema System

YAML has three built-in schemas with increasing type support:

| Schema | Types Supported |
|--------|-----------------|
| **Failsafe** | Maps, sequences, strings only |
| **JSON** | Boolean, null, int, float + failsafe types |
| **Core** | JSON types with human-readable forms |

### Critical Differences: YAML 1.1 vs 1.2

#### Boolean Handling

| Value | YAML 1.1 | YAML 1.2 |
|-------|----------|----------|
| `true`, `True`, `TRUE` | Boolean | Boolean |
| `false`, `False`, `FALSE` | Boolean | Boolean |
| `yes`, `Yes`, `YES` | Boolean | **String** |
| `no`, `No`, `NO` | Boolean | **String** |
| `on`, `On`, `ON` | Boolean | **String** |
| `off`, `Off`, `OFF` | Boolean | **String** |
| `y`, `Y` | Boolean | **String** |
| `n`, `N` | Boolean | **String** |

**The "Norway Problem":** In YAML 1.1, the country code `NO` for Norway becomes boolean `false`!

#### Numeric Values

| Feature | YAML 1.1 | YAML 1.2 |
|---------|----------|----------|
| Octal prefix | `010` = 8 | `0o10` = 8; `010` = 10 |
| Underscores | `1_000` allowed | Not allowed |
| Sexagesimal | `1:02:03` = 3723 | String literal |
| Binary | `0b1010` allowed | Not supported |

#### Dropped Features in 1.2

- `!!pairs`, `!!omap`, `!!set`, `!!timestamp`, `!!binary` types
- Merge key `<<` and value key `=` special mappings
- Next-line `\x85`, line-separator `\u2028`, paragraph-separator `\u2029` as line breaks

### Library Implementation Reality

**Important:** Many popular libraries still default to YAML 1.1 behavior:
- **PyYAML (Python)**: YAML 1.1
- **libyaml (C)**: YAML 1.1
- **go-yaml (Go)**: Custom hybrid 1.1/1.2
- **js-yaml (JavaScript)**: YAML 1.2 (since v4)

**Recommendation:** Always explicitly specify your target YAML version and test with your specific parser.

---

## 3. Popular YAML Schema Tools and Validators

### yamllint

**Purpose:** Syntax linting and style checking (not schema validation)

**Installation:**
```bash
pip install yamllint
```

**Configuration file (`.yamllint.yaml`):**
```yaml
extends: default

rules:
  line-length:
    max: 120
    level: warning

  indentation:
    spaces: 2
    indent-sequences: consistent

  comments:
    require-starting-space: true
    min-spaces-from-content: 2

  truthy:
    level: warning
    check-keys: false

  document-start: disable

ignore: |
  /vendor/
  *.generated.yaml
```

**Available rules:**
- `braces`, `brackets` - Spacing in flow collections
- `colons`, `commas` - Spacing around punctuation
- `comments`, `comments-indentation` - Comment formatting
- `document-start`, `document-end` - `---` and `...` markers
- `empty-lines`, `empty-values` - Blank content
- `hyphens` - List marker spacing
- `indentation` - Consistent indentation
- `key-duplicates` - Prevent duplicate keys
- `key-ordering` - Alphabetical keys
- `line-length` - Maximum line width
- `new-line-at-end-of-file`, `new-lines` - File endings
- `octal-values` - Detect ambiguous octals
- `quoted-strings` - Quote consistency
- `trailing-spaces` - Whitespace cleanup
- `truthy` - Detect boolean ambiguity

**Note:** yamllint does NOT support JSON Schema validation. Use it alongside schema validators.

### YAML Language Server (Red Hat)

**Purpose:** IDE integration with JSON Schema validation

**Key features:**
- JSON Schema 7 support
- Autocompletion from schemas
- Hover documentation
- Real-time validation
- Schema Store integration

**VS Code Configuration (`settings.json`):**
```json
{
  "yaml.schemas": {
    "https://json.schemastore.org/github-workflow.json": "/.github/workflows/*.yml",
    "./schemas/my-config.schema.json": ["config.yaml", "config.yml"],
    "https://example.com/schema.json": ["*.myformat.yaml"]
  },
  "yaml.schemaStore.enable": true,
  "yaml.validate": true,
  "yaml.format.enable": true,
  "yaml.yamlVersion": "1.2"
}
```

**Inline schema association (modeline):**
```yaml
# yaml-language-server: $schema=https://json.schemastore.org/github-workflow.json
name: CI Pipeline
on: [push, pull_request]
jobs:
  build:
    runs-on: ubuntu-latest
```

### Kubeconform (Kubernetes)

**Purpose:** Fast Kubernetes manifest validation with CRD support

**Installation:**
```bash
# macOS
brew install kubeconform

# Go
go install github.com/yannh/kubeconform/cmd/kubeconform@latest
```

**Usage:**
```bash
# Validate Kubernetes manifests
kubeconform -summary deployment.yaml

# With custom schemas for CRDs
kubeconform -schema-location default \
  -schema-location 'https://my-schemas.example.com/{{ .ResourceKind }}.json' \
  manifests/
```

### kubectl-validate

**Purpose:** Server-parity Kubernetes validation

**Features:**
- Uses actual Kubernetes apiserver validation code
- Most accurate error messages
- CEL validation support
- Native CRD understanding

**Usage:**
```bash
kubectl validate -f deployment.yaml
kubectl validate -f manifests/ --recursive
```

### Yamale (Python)

**Purpose:** Python-native YAML schema validation

**Installation:**
```bash
pip install yamale
```

**Schema syntax (`schema.yaml`):**
```yaml
name: str()
age: int(min=0, max=150)
email: str(required=False)
role: enum('admin', 'user', 'guest')
tags: list(str(), min=1)
metadata: map(str(), any())
settings: include('settings_schema')
---
settings_schema:
  debug: bool()
  log_level: enum('debug', 'info', 'warning', 'error')
  timeout: num(min=0)
```

**Python usage:**
```python
import yamale

schema = yamale.make_schema('./schema.yaml')
data = yamale.make_data('./config.yaml')

try:
    yamale.validate(schema, data)
    print('Validation successful!')
except yamale.YamaleError as e:
    print(f'Validation failed:\n{e}')
```

**Built-in validators:**
- `str()`, `int()`, `num()`, `bool()` - Basic types
- `enum('a', 'b', 'c')` - Enumeration
- `list(validator)`, `map(key_validator, value_validator)` - Collections
- `any()` - Accept any value
- `include('name')` - Reference other schemas
- `regex('pattern')` - Regex matching
- `ip()`, `mac()`, `date()`, `timestamp()` - Special formats

### AJV (JavaScript)

**Purpose:** Fast JSON Schema validator for Node.js

**Installation:**
```bash
npm install ajv ajv-formats js-yaml
```

**Usage:**
```javascript
import Ajv from 'ajv';
import addFormats from 'ajv-formats';
import yaml from 'js-yaml';
import fs from 'fs';

const ajv = new Ajv({ allErrors: true });
addFormats(ajv);

const schema = JSON.parse(fs.readFileSync('schema.json', 'utf8'));
const data = yaml.load(fs.readFileSync('config.yaml', 'utf8'));

const validate = ajv.compile(schema);
const valid = validate(data);

if (!valid) {
  console.log('Validation errors:', validate.errors);
}
```

### Python jsonschema

**Purpose:** JSON Schema validation in Python

**Usage:**
```python
import yaml
import jsonschema

with open('schema.json') as f:
    schema = json.load(f)

with open('config.yaml') as f:
    data = yaml.safe_load(f)

try:
    jsonschema.validate(data, schema)
    print('Valid!')
except jsonschema.ValidationError as e:
    print(f'Invalid: {e.message}')
```

---

## 4. Schema Definition Patterns

### Pattern 1: Configuration File Schema

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://example.com/app-config.schema.json",
  "title": "Application Configuration",
  "type": "object",
  "properties": {
    "app": {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "minLength": 1,
          "maxLength": 50
        },
        "version": {
          "type": "string",
          "pattern": "^\\d+\\.\\d+\\.\\d+$"
        },
        "environment": {
          "type": "string",
          "enum": ["development", "staging", "production"]
        }
      },
      "required": ["name", "version"]
    },
    "server": {
      "type": "object",
      "properties": {
        "host": {
          "type": "string",
          "format": "hostname"
        },
        "port": {
          "type": "integer",
          "minimum": 1,
          "maximum": 65535,
          "default": 8080
        },
        "ssl": {
          "type": "object",
          "properties": {
            "enabled": { "type": "boolean", "default": false },
            "cert": { "type": "string" },
            "key": { "type": "string" }
          },
          "if": {
            "properties": { "enabled": { "const": true } }
          },
          "then": {
            "required": ["cert", "key"]
          }
        }
      },
      "required": ["host"]
    },
    "database": {
      "type": "object",
      "properties": {
        "driver": {
          "type": "string",
          "enum": ["postgres", "mysql", "sqlite", "mongodb"]
        },
        "connection": {
          "oneOf": [
            {
              "type": "string",
              "format": "uri",
              "description": "Connection string"
            },
            {
              "type": "object",
              "properties": {
                "host": { "type": "string" },
                "port": { "type": "integer" },
                "database": { "type": "string" },
                "username": { "type": "string" },
                "password": { "type": "string" }
              },
              "required": ["host", "database"]
            }
          ]
        }
      },
      "required": ["driver", "connection"]
    },
    "logging": {
      "type": "object",
      "properties": {
        "level": {
          "type": "string",
          "enum": ["debug", "info", "warn", "error"],
          "default": "info"
        },
        "format": {
          "type": "string",
          "enum": ["json", "text"],
          "default": "json"
        },
        "outputs": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "type": {
                "type": "string",
                "enum": ["console", "file", "syslog"]
              },
              "path": { "type": "string" }
            },
            "required": ["type"],
            "if": {
              "properties": { "type": { "const": "file" } }
            },
            "then": {
              "required": ["path"]
            }
          },
          "minItems": 1
        }
      }
    }
  },
  "required": ["app", "server"]
}
```

**Corresponding YAML:**
```yaml
app:
  name: MyApplication
  version: 1.2.3
  environment: production

server:
  host: api.example.com
  port: 443
  ssl:
    enabled: true
    cert: /etc/ssl/certs/app.crt
    key: /etc/ssl/private/app.key

database:
  driver: postgres
  connection:
    host: db.example.com
    port: 5432
    database: myapp
    username: appuser
    password: ${DB_PASSWORD}

logging:
  level: info
  format: json
  outputs:
    - type: console
    - type: file
      path: /var/log/myapp/app.log
```

### Pattern 2: CI/CD Pipeline Schema

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "CI/CD Pipeline Configuration",
  "$defs": {
    "step": {
      "type": "object",
      "properties": {
        "name": { "type": "string" },
        "run": { "type": "string" },
        "uses": { "type": "string" },
        "with": {
          "type": "object",
          "additionalProperties": true
        },
        "env": {
          "type": "object",
          "additionalProperties": { "type": "string" }
        },
        "if": { "type": "string" },
        "continue-on-error": { "type": "boolean" },
        "timeout-minutes": { "type": "integer", "minimum": 1 }
      },
      "oneOf": [
        { "required": ["run"] },
        { "required": ["uses"] }
      ]
    },
    "job": {
      "type": "object",
      "properties": {
        "runs-on": {
          "oneOf": [
            { "type": "string" },
            { "type": "array", "items": { "type": "string" } }
          ]
        },
        "needs": {
          "oneOf": [
            { "type": "string" },
            { "type": "array", "items": { "type": "string" } }
          ]
        },
        "steps": {
          "type": "array",
          "items": { "$ref": "#/$defs/step" },
          "minItems": 1
        },
        "env": {
          "type": "object",
          "additionalProperties": { "type": "string" }
        },
        "if": { "type": "string" },
        "timeout-minutes": { "type": "integer" },
        "strategy": {
          "type": "object",
          "properties": {
            "matrix": { "type": "object" },
            "fail-fast": { "type": "boolean" },
            "max-parallel": { "type": "integer" }
          }
        }
      },
      "required": ["runs-on", "steps"]
    }
  },
  "type": "object",
  "properties": {
    "name": { "type": "string" },
    "on": {
      "oneOf": [
        { "type": "string" },
        { "type": "array", "items": { "type": "string" } },
        {
          "type": "object",
          "properties": {
            "push": { "type": "object" },
            "pull_request": { "type": "object" },
            "schedule": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "cron": { "type": "string" }
                }
              }
            },
            "workflow_dispatch": { "type": "object" }
          }
        }
      ]
    },
    "env": {
      "type": "object",
      "additionalProperties": { "type": "string" }
    },
    "jobs": {
      "type": "object",
      "additionalProperties": { "$ref": "#/$defs/job" },
      "minProperties": 1
    }
  },
  "required": ["name", "on", "jobs"]
}
```

### Pattern 3: API Configuration with Discriminated Unions

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "API Endpoint Configuration",
  "$defs": {
    "authNone": {
      "type": "object",
      "properties": {
        "type": { "const": "none" }
      },
      "required": ["type"]
    },
    "authApiKey": {
      "type": "object",
      "properties": {
        "type": { "const": "api-key" },
        "header": { "type": "string" },
        "key": { "type": "string" }
      },
      "required": ["type", "header", "key"]
    },
    "authOAuth2": {
      "type": "object",
      "properties": {
        "type": { "const": "oauth2" },
        "tokenUrl": { "type": "string", "format": "uri" },
        "clientId": { "type": "string" },
        "clientSecret": { "type": "string" },
        "scopes": {
          "type": "array",
          "items": { "type": "string" }
        }
      },
      "required": ["type", "tokenUrl", "clientId", "clientSecret"]
    }
  },
  "type": "object",
  "properties": {
    "endpoints": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "name": { "type": "string" },
          "url": { "type": "string", "format": "uri" },
          "method": {
            "type": "string",
            "enum": ["GET", "POST", "PUT", "PATCH", "DELETE"]
          },
          "auth": {
            "oneOf": [
              { "$ref": "#/$defs/authNone" },
              { "$ref": "#/$defs/authApiKey" },
              { "$ref": "#/$defs/authOAuth2" }
            ]
          }
        },
        "required": ["name", "url", "method", "auth"]
      }
    }
  }
}
```

**Corresponding YAML:**
```yaml
endpoints:
  - name: Public API
    url: https://api.example.com/public
    method: GET
    auth:
      type: none

  - name: Protected API
    url: https://api.example.com/data
    method: POST
    auth:
      type: api-key
      header: X-API-Key
      key: ${API_KEY}

  - name: OAuth Service
    url: https://api.example.com/secure
    method: GET
    auth:
      type: oauth2
      tokenUrl: https://auth.example.com/token
      clientId: my-client
      clientSecret: ${CLIENT_SECRET}
      scopes:
        - read
        - write
```

---

## 5. Best Practices for YAML Schema Design

### Schema Organization

1. **Use `$defs` for reusable components**
   - Define common structures once
   - Reference with `$ref`
   - Keep definitions close to usage

2. **Split large schemas into multiple files**
   - One schema per resource type
   - Use `$ref` with relative URIs
   - Maintain a schema index/catalog

3. **Version your schemas**
   - Include version in `$id`
   - Support schema evolution
   - Document breaking changes

### Validation Strategy

```
┌─────────────────────────────────────────────────────────────┐
│                    Validation Layers                         │
├─────────────────────────────────────────────────────────────┤
│  1. Syntax Check (yamllint)                                 │
│     - Valid YAML syntax                                      │
│     - Consistent formatting                                  │
│     - Style compliance                                       │
├─────────────────────────────────────────────────────────────┤
│  2. Schema Validation (JSON Schema / Yamale)                │
│     - Type checking                                          │
│     - Required fields                                        │
│     - Value constraints                                      │
├─────────────────────────────────────────────────────────────┤
│  3. Semantic Validation (Custom / CEL)                      │
│     - Cross-field dependencies                               │
│     - Business logic rules                                   │
│     - External references                                    │
└─────────────────────────────────────────────────────────────┘
```

### Documentation Practices

1. **Use `title` and `description` generously**
```json
{
  "properties": {
    "timeout": {
      "type": "integer",
      "title": "Request Timeout",
      "description": "Maximum time in seconds to wait for a response. Set to 0 for no timeout.",
      "minimum": 0,
      "default": 30
    }
  }
}
```

2. **Provide examples**
```json
{
  "properties": {
    "email": {
      "type": "string",
      "format": "email",
      "examples": ["user@example.com", "admin@company.org"]
    }
  }
}
```

3. **Document defaults explicitly**
```json
{
  "properties": {
    "retries": {
      "type": "integer",
      "default": 3,
      "description": "Number of retry attempts. Defaults to 3 if not specified."
    }
  }
}
```

### Error Messages

1. **Use `errorMessage` (with ajv-errors)**
```json
{
  "properties": {
    "port": {
      "type": "integer",
      "minimum": 1,
      "maximum": 65535,
      "errorMessage": {
        "minimum": "Port must be at least 1",
        "maximum": "Port cannot exceed 65535"
      }
    }
  }
}
```

### IDE Integration

1. **Register with JSON Schema Store** for public schemas
2. **Use modelines** for project-specific schemas:
   ```yaml
   # yaml-language-server: $schema=./schemas/config.schema.json
   ```
3. **Configure workspace settings** for team consistency

### YAML-Specific Considerations

1. **Handle the YAML 1.1 vs 1.2 problem**
   - Explicitly document expected YAML version
   - Use quoted strings for ambiguous values: `"yes"`, `"no"`, `"on"`, `"off"`
   - Enable `truthy` rule in yamllint

2. **Be careful with type coercion**
   - `010` might be octal (8) or decimal (10)
   - Quote version numbers: `"1.0"` not `1.0`
   - Quote country codes: `"NO"` not `NO`

3. **Consider anchors and aliases**
   - JSON Schema cannot validate anchor definitions
   - Validate the resolved/expanded document
   - Document expected anchor patterns separately

---

## 6. Practical Examples

### Example: Complete Validation Setup

**Project structure:**
```
project/
├── .yamllint.yaml
├── schemas/
│   └── config.schema.json
├── config/
│   ├── development.yaml
│   ├── staging.yaml
│   └── production.yaml
└── scripts/
    └── validate.sh
```

**`.yamllint.yaml`:**
```yaml
extends: default

rules:
  line-length:
    max: 120
  indentation:
    spaces: 2
  truthy:
    check-keys: true
  document-start: disable

ignore: |
  /node_modules/
  /vendor/
```

**`validate.sh`:**
```bash
#!/bin/bash
set -e

echo "Running YAML lint..."
yamllint config/

echo "Running schema validation..."
for file in config/*.yaml; do
  echo "Validating $file..."
  ajv validate -s schemas/config.schema.json -d "$file"
done

echo "All validations passed!"
```

### Example: GitHub Action for YAML Validation

```yaml
name: Validate YAML

on:
  push:
    paths:
      - '**.yaml'
      - '**.yml'
  pull_request:
    paths:
      - '**.yaml'
      - '**.yml'

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: YAML Lint
        uses: ibiqlik/action-yamllint@v3
        with:
          config_file: .yamllint.yaml

      - name: JSON/YAML Schema Validation
        uses: GrantBirki/json-yaml-validate@v3
        with:
          yaml_schema: schemas/config.schema.json
          yaml_files: |
            config/*.yaml
```

---

## Summary

| Approach | Best For | Limitations |
|----------|----------|-------------|
| **JSON Schema** | Universal validation, IDE support, cross-platform | Cannot validate YAML-specific features (anchors) |
| **yamllint** | Syntax/style linting | No schema validation |
| **YAML Language Server** | IDE integration | Editor-dependent |
| **Yamale** | Python projects, simple schemas | Python-only |
| **Kubeconform** | Kubernetes manifests | K8s-specific |
| **Custom validators** | Complex business logic | Maintenance burden |

**Recommended approach:**
1. Use **yamllint** for syntax/style checking
2. Use **JSON Schema** with language-appropriate validator for structure
3. Configure **YAML Language Server** in IDE for real-time feedback
4. Add **custom validation** for complex business rules
5. Integrate all validators into CI/CD pipeline

---

## References

- [JSON Schema Specification](https://json-schema.org/)
- [JSON Schema for YAML](https://json-schema-everywhere.github.io/yaml)
- [YAML 1.2 Specification](https://yaml.org/spec/1.2.2/)
- [yamllint Documentation](https://yamllint.readthedocs.io/)
- [YAML Language Server](https://github.com/redhat-developer/yaml-language-server)
- [JSON Schema Store](https://www.schemastore.org/)
- [Yamale](https://github.com/23andMe/Yamale)
- [Kubeconform](https://github.com/yannh/kubeconform)
- [kubectl-validate](https://github.com/kubernetes-sigs/kubectl-validate)
