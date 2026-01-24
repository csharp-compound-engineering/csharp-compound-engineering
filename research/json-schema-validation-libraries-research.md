# JSON/YAML Schema Validation Libraries for .NET

**Research Date:** January 2026
**Use Case:** Validating doc-type YAML frontmatter against JSON Schema

## Executive Summary

For validating YAML frontmatter against JSON Schema with Draft 2020-12 support, **JsonSchema.Net** (from the json-everything project) combined with **Yaml2JsonNode** is the recommended solution. It provides modern System.Text.Json integration, full Draft 2020-12 compliance, MIT licensing, and active maintenance.

---

## Library Comparison

| Feature | JsonSchema.Net | NJsonSchema | Json.NET Schema | Corvus.JsonSchema |
|---------|---------------|-------------|-----------------|-------------------|
| **Latest Version** | 8.0.5 | 11.5.2 | Latest | 4.x |
| **Draft 2020-12** | Full support | No (Draft 4-7 only) | Full support | Full support |
| **YAML Support** | Via Yaml2JsonNode | Via NJsonSchema.Yaml | Manual conversion | No direct support |
| **.NET 8+ Support** | Yes | Yes | Yes | Yes |
| **License** | MIT | MIT | AGPL / Commercial | MIT |
| **JSON Library** | System.Text.Json | Newtonsoft.Json | Newtonsoft.Json | System.Text.Json |
| **Active Maintenance** | Yes (PowerShell uses it) | Yes | Yes | Yes |

---

## 1. JsonSchema.Net (json-everything)

**Recommendation: PRIMARY CHOICE**

### Package Information

| Package | Version | Purpose |
|---------|---------|---------|
| `JsonSchema.Net` | 8.0.5 | Core schema validation |
| `Yaml2JsonNode` | 2.4.0 | YAML to JsonNode conversion |
| `JsonSchema.Net.Generation` | 6.0.0 | Generate schemas from .NET types |

```xml
<PackageReference Include="JsonSchema.Net" Version="8.0.5" />
<PackageReference Include="Yaml2JsonNode" Version="2.4.0" />
```

### JSON Schema Draft Support

- Draft 6
- Draft 7
- Draft 2019-09
- **Draft 2020-12** (recommended)
- v1/2026 (upcoming)

### API Example: YAML Frontmatter Validation

```csharp
using System.Text.Json.Nodes;
using Json.Schema;
using YamlDotNet.RepresentationModel;
using Yaml2JsonNode;

public class FrontmatterValidator
{
    private readonly JsonSchema _schema;

    public FrontmatterValidator(string schemaJson)
    {
        _schema = JsonSchema.FromText(schemaJson);
    }

    public ValidationResult ValidateYamlFrontmatter(string yamlContent)
    {
        // Parse YAML using YamlDotNet
        var yaml = new YamlStream();
        yaml.Load(new StringReader(yamlContent));

        // Convert to JsonNode using Yaml2JsonNode
        var jsonNode = yaml.Documents[0].RootNode.ToJsonNode();

        // Evaluate against schema
        var result = _schema.Evaluate(jsonNode, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,  // Get detailed errors
            ValidateAs = Draft.Draft202012
        });

        return new ValidationResult
        {
            IsValid = result.IsValid,
            Errors = result.Details?
                .Where(d => !d.IsValid)
                .Select(d => new ValidationError
                {
                    Path = d.InstanceLocation?.ToString() ?? "",
                    Message = d.Errors?.Values.FirstOrDefault() ?? "Validation failed"
                })
                .ToList() ?? new List<ValidationError>()
        };
    }
}

public record ValidationResult
{
    public bool IsValid { get; init; }
    public List<ValidationError> Errors { get; init; } = new();
}

public record ValidationError
{
    public string Path { get; init; } = "";
    public string Message { get; init; } = "";
}
```

### Complete Usage Example

```csharp
// Define JSON Schema (Draft 2020-12)
var schemaJson = """
{
    "$schema": "https://json-schema.org/draft/2020-12/schema",
    "type": "object",
    "properties": {
        "title": { "type": "string", "minLength": 1 },
        "doc_type": {
            "type": "string",
            "enum": ["spec", "adr", "guide", "reference"]
        },
        "version": { "type": "string", "pattern": "^\\d+\\.\\d+\\.\\d+$" },
        "status": {
            "type": "string",
            "enum": ["draft", "review", "approved", "deprecated"]
        },
        "created": { "type": "string", "format": "date" },
        "author": { "type": "string" }
    },
    "required": ["title", "doc_type", "version", "status"]
}
""";

var validator = new FrontmatterValidator(schemaJson);

// YAML frontmatter to validate
var yamlFrontmatter = """
title: API Design Specification
doc_type: spec
version: 1.0.0
status: draft
created: 2026-01-15
author: Engineering Team
""";

var result = validator.ValidateYamlFrontmatter(yamlFrontmatter);

if (result.IsValid)
{
    Console.WriteLine("Frontmatter is valid!");
}
else
{
    Console.WriteLine("Validation errors:");
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"  [{error.Path}]: {error.Message}");
    }
}
```

### Pros

- **Full Draft 2020-12 support** - Latest JSON Schema specification
- **MIT License** - No commercial restrictions
- **System.Text.Json based** - Modern, performant, no Newtonsoft dependency
- **Native AOT support** - Ready for modern deployment scenarios
- **Actively maintained** - Used by PowerShell, Microsoft Semantic Kernel
- **Comprehensive ecosystem** - Part of json-everything with related tools
- **27M+ downloads** - Battle-tested in production

### Cons

- Requires separate package (Yaml2JsonNode) for YAML support
- No direct YAML file loading API (need manual conversion step)
- Slightly more verbose API than NJsonSchema for simple cases

---

## 2. NJsonSchema (with NJsonSchema.Yaml)

**Recommendation: ALTERNATIVE - Good for OpenAPI/Swagger ecosystems**

### Package Information

| Package | Version | Purpose |
|---------|---------|---------|
| `NJsonSchema` | 11.5.2 | Core schema operations |
| `NJsonSchema.Yaml` | 11.5.2 | YAML schema support |

```xml
<PackageReference Include="NJsonSchema" Version="11.5.2" />
<PackageReference Include="NJsonSchema.Yaml" Version="11.5.2" />
```

### JSON Schema Draft Support

- Draft 4 (primary)
- Draft 6 (partial)
- Draft 7 (partial)
- **No Draft 2020-12 support** (critical limitation)

### API Example: YAML Validation

```csharp
using NJsonSchema;
using NJsonSchema.Yaml;
using YamlDotNet.Serialization;

public class NJsonSchemaValidator
{
    private readonly JsonSchema _schema;

    public static async Task<NJsonSchemaValidator> CreateAsync(string schemaJson)
    {
        var schema = await JsonSchema.FromJsonAsync(schemaJson);
        return new NJsonSchemaValidator(schema);
    }

    private NJsonSchemaValidator(JsonSchema schema)
    {
        _schema = schema;
    }

    public ValidationResult ValidateYaml(string yamlContent)
    {
        // Convert YAML to JSON
        var deserializer = new DeserializerBuilder().Build();
        var yamlObject = deserializer.Deserialize(new StringReader(yamlContent));

        var serializer = new SerializerBuilder()
            .JsonCompatible()
            .Build();
        var jsonString = serializer.Serialize(yamlObject);

        // Validate
        var errors = _schema.Validate(jsonString);

        return new ValidationResult
        {
            IsValid = !errors.Any(),
            Errors = errors.Select(e => new ValidationError
            {
                Path = e.Path,
                Message = $"{e.Kind}: {e.Property}"
            }).ToList()
        };
    }
}

// Usage
var validator = await NJsonSchemaValidator.CreateAsync(schemaJson);
var result = validator.ValidateYaml(yamlContent);
```

### Alternative: Load Schema from YAML

```csharp
using NJsonSchema.Yaml;

// Load schema defined in YAML format
var schema = await JsonSchemaYaml.FromFileAsync("schema.yaml");

// Or from YAML string
var schema = await JsonSchemaYaml.FromYamlAsync(yamlSchemaString);
```

### Pros

- **Dedicated NJsonSchema.Yaml package** - First-class YAML support
- **Mature ecosystem** - 89M+ downloads, heavily used in NSwag/OpenAPI
- **Schema generation** - Generate schemas from .NET types
- **Code generation** - Generate C#/TypeScript from schemas
- **MIT License** - No commercial restrictions

### Cons

- **No Draft 2020-12 support** - Critical limitation for modern schemas
- **Newtonsoft.Json dependency** - Older JSON library
- **Validation failures on newer drafts** - Known issues with 2020-12 features like `$defs`, `prefixItems`
- **Less strict compliance** - Fails significant portions of JSON Schema test suite for newer drafts

---

## 3. Json.NET Schema (Newtonsoft)

**Recommendation: NOT RECOMMENDED - Commercial licensing required**

### Package Information

```xml
<PackageReference Include="Newtonsoft.Json.Schema" Version="4.0.0" />
```

### Pricing (as of 2025)

| License Type | Scope | Limitations |
|--------------|-------|-------------|
| Free (AGPL) | Open source only | 1,000 validations/hour limit |
| Indie | Per developer | Commercial use allowed |
| Business | Per developer | Commercial use, priority support |
| Site | Entire company | Unlimited developers |

### JSON Schema Draft Support

- Draft 3, 4, 6, 7
- Draft 2019-09
- **Draft 2020-12** (full support)

### API Example

```csharp
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using YamlDotNet.Serialization;

public class NewtonsoftValidator
{
    private readonly JSchema _schema;

    public NewtonsoftValidator(string schemaJson)
    {
        _schema = JSchema.Parse(schemaJson);
    }

    public ValidationResult ValidateYaml(string yamlContent)
    {
        // Convert YAML to JSON
        var deserializer = new DeserializerBuilder().Build();
        var yamlObject = deserializer.Deserialize(new StringReader(yamlContent));

        var jToken = JToken.FromObject(yamlObject);

        var isValid = jToken.IsValid(_schema, out IList<string> errors);

        return new ValidationResult
        {
            IsValid = isValid,
            Errors = errors.Select(e => new ValidationError
            {
                Message = e
            }).ToList()
        };
    }
}
```

### Pros

- **100% JSON Schema Test Suite compliance** - Most accurate validation
- **Full Draft 2020-12 support**
- **Streaming validation** - Memory efficient for large documents
- **Mature and stable** - Long track record

### Cons

- **Commercial license required** - AGPL is restrictive; free tier has validation limits
- **Newtonsoft.Json dependency** - Legacy JSON library
- **No built-in YAML support** - Manual conversion required
- **Cost** - Pricing not publicly listed, contact required

---

## 4. Corvus.JsonSchema

**Recommendation: SPECIALIZED - Best for code generation scenarios**

### Package Information

```xml
<PackageReference Include="Corvus.Json.ExtendedTypes" Version="4.x" />
<!-- Or use the CLI tool -->
dotnet tool install --global Corvus.Json.JsonSchema.TypeGeneratorTool
```

### JSON Schema Draft Support

- Draft 4, 6, 7
- Draft 2019-09
- **Draft 2020-12** (full support)
- OpenAPI 3.0/3.1 variants

### Approach: Code Generation

Corvus.JsonSchema takes a different approach - it generates strongly-typed C# classes from JSON Schema at build time, with validation baked into the generated types.

```csharp
// Generated from schema - validation is built-in
public readonly partial struct FrontmatterDocument
{
    public string Title { get; }
    public string DocType { get; }
    public string Version { get; }
    public string Status { get; }

    public bool IsValid() => /* generated validation logic */;

    public ValidationContext Validate(
        ValidationContext context,
        ValidationLevel level = ValidationLevel.Flag)
    {
        // Generated validation code
    }
}

// Usage
var document = FrontmatterDocument.Parse(jsonString);
if (document.IsValid())
{
    // Use the typed properties
    Console.WriteLine(document.Title);
}
```

### Pros

- **Ultra-high performance** - Zero/low allocation validation
- **Strongly typed** - Compile-time safety from schemas
- **Full Draft 2020-12 support**
- **MIT License**
- **System.Text.Json based**

### Cons

- **No direct YAML support** - Must convert to JSON first
- **Code generation workflow** - Requires build-time generation
- **Overkill for simple validation** - Best when you need typed access to data
- **Learning curve** - Different paradigm than runtime validation

---

## Recommendation for Doc-Type YAML Frontmatter Validation

### Primary Recommendation: JsonSchema.Net + Yaml2JsonNode

For validating YAML frontmatter schemas, **JsonSchema.Net** combined with **Yaml2JsonNode** is the best choice because:

1. **Draft 2020-12 Support** - Required for modern schema features
2. **MIT License** - No commercial restrictions
3. **System.Text.Json** - Modern, performant, standard in .NET 8+
4. **Active Development** - Used by Microsoft (PowerShell, Semantic Kernel)
5. **Clean API** - Simple evaluate-and-check pattern

### Implementation Approach

```csharp
// Recommended package references
<PackageReference Include="JsonSchema.Net" Version="8.0.5" />
<PackageReference Include="Yaml2JsonNode" Version="2.4.0" />
<PackageReference Include="YamlDotNet" Version="16.3.0" />
```

### When to Choose Alternatives

| Scenario | Recommended Library |
|----------|-------------------|
| OpenAPI/Swagger toolchain integration | NJsonSchema |
| Need code generation from schemas | Corvus.JsonSchema |
| Require 100% spec compliance & have budget | Json.NET Schema |
| Simple Draft 4/7 validation, existing Newtonsoft codebase | NJsonSchema |

---

## References

### JsonSchema.Net
- [NuGet Package](https://www.nuget.org/packages/JsonSchema.Net)
- [Documentation](https://docs.json-everything.net/schema/basics/)
- [GitHub Repository](https://github.com/json-everything/json-everything)

### NJsonSchema
- [NuGet Package](https://www.nuget.org/packages/NJsonSchema)
- [NJsonSchema.Yaml NuGet](https://www.nuget.org/packages/NJsonSchema.Yaml)
- [GitHub Repository](https://github.com/RicoSuter/NJsonSchema)

### Json.NET Schema
- [Official Site](https://www.newtonsoft.com/jsonschema)
- [Pricing](https://www.newtonsoft.com/store)

### Corvus.JsonSchema
- [GitHub Repository](https://github.com/corvus-dotnet/Corvus.JsonSchema)
- [Getting Started Guide](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/docs/GettingStartedWithJsonSchemaCodeGeneration.md)

### JSON Schema Specification
- [Draft 2020-12](https://json-schema.org/draft/2020-12)
- [JSON Schema Implementations](https://json-schema.org/implementations)
