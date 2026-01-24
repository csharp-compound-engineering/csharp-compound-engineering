# JSON Schema Validation Library Research for .NET

**Date:** January 2026
**Purpose:** Evaluate JSON Schema validation libraries for validating YAML doc-type schemas in the CSharp Compound Docs plugin

## Executive Summary

This research compares JSON Schema validation libraries for .NET with a focus on Draft 2020-12 support and YAML integration. After thorough evaluation, **JsonSchema.Net combined with YamlDotNet** is the recommended solution, with important licensing considerations noted.

---

## Libraries Evaluated

### 1. NJsonSchema + NJsonSchema.Yaml

**Repository:** [RicoSuter/NJsonSchema](https://github.com/RicoSuter/NJsonSchema)
**NuGet:** `NJsonSchema` (v11.5.2), `NJsonSchema.Yaml` (v11.5.2)
**License:** MIT

#### Overview

NJsonSchema is a well-established .NET library for reading, generating, and validating JSON Schema. It is heavily used in [NSwag](https://github.com/RicoSuter/NSwag), a popular Swagger API toolchain for .NET.

#### JSON Schema Draft Support

| Draft | Support Status |
|-------|----------------|
| Draft 4 | Full |
| Draft 6 | Partial |
| Draft 7 | Partial |
| Draft 2019-09 | Not Supported |
| Draft 2020-12 | Not Supported |

**Critical Limitation:** NJsonSchema does NOT support Draft 2020-12. Multiple GitHub issues ([#1536](https://github.com/RicoSuter/NJsonSchema/issues/1536), [#1667](https://github.com/RicoSuter/NJsonSchema/issues/1667), [#1691](https://github.com/RicoSuter/NJsonSchema/issues/1691)) document validation failures with newer drafts. Issue #574 has tracked draft-06+ support progress for years without resolution.

#### YAML Integration

Native YAML support via `NJsonSchema.Yaml` package:

```csharp
using NJsonSchema;
using NJsonSchema.Yaml;

// Load schema from YAML
var schema = await JsonSchemaYaml.FromYamlAsync(yamlSchemaString);

// Validate JSON data
var errors = schema.Validate(jsonData);

foreach (var error in errors)
{
    Console.WriteLine($"{error.Path}: {error.Kind}");
}
```

#### .NET Compatibility

- .NET 8.0
- .NET Standard 2.0
- .NET Framework 4.6.2

#### Error Messages

Provides `ValidationError` class with:
- `Path` - JSON path to the error location
- `Kind` - `ValidationErrorKind` enum (e.g., `StringExpected`, `NumberExpected`, `IntegerExpected`)
- `ToString()` override for human-readable output

```csharp
var errors = schema.Validate(jsonData);
foreach (var error in errors)
{
    Console.WriteLine(error.Path + ": " + error.Kind);
    // Output: "age: IntegerExpected"
}
```

#### Strengths
- Native YAML support (NJsonSchema.Yaml)
- Mature, well-tested codebase
- Extensive code generation capabilities (C#, TypeScript)
- No licensing concerns (MIT)
- Large community (NSwag ecosystem)

#### Weaknesses
- No Draft 2020-12 support (deal-breaker for this use case)
- Uses Newtonsoft.Json (older serialization library)
- Less detailed error messages compared to JsonSchema.Net

---

### 2. JsonSchema.Net (JSON Everything)

**Repository:** [json-everything/json-everything](https://github.com/json-everything/json-everything)
**Documentation:** [docs.json-everything.net](https://docs.json-everything.net/schema/basics/)
**NuGet:** `JsonSchema.Net` (v8.0.5)
**License:** MIT (with commercial licensing change effective February 2026)

#### Overview

JsonSchema.Net is a modern, System.Text.Json-based JSON Schema implementation. It is actively maintained and used by PowerShell's `Test-Json` cmdlet.

#### JSON Schema Draft Support

| Draft | Support Status |
|-------|----------------|
| Draft 4 | Not Supported |
| Draft 6 | Full |
| Draft 7 | Full |
| Draft 2019-09 | Full |
| Draft 2020-12 | Full |
| v1/2026 (upcoming) | In Development |

#### YAML Integration

Requires bridge via `YamlDotNet.System.Text.Json`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using YamlDotNet.Serialization;
using YamlDotNet.System.Text.Json;

// Configure YamlDotNet with System.Text.Json support
var deserializer = new DeserializerBuilder()
    .WithNodeTypeResolver(new SystemTextJsonNodeTypeResolver())
    .Build();

// Parse YAML to JsonNode
var yamlContent = File.ReadAllText("schema.yaml");
var jsonNode = deserializer.Deserialize<JsonNode>(new StringReader(yamlContent));

// Build schema from JsonNode
var schema = JsonSchema.FromText(jsonNode.ToJsonString());

// Validate
var result = schema.Evaluate(JsonNode.Parse(dataJson));
```

#### .NET Compatibility

- .NET 8.0
- .NET Standard 2.0

#### Error Messages

Three output formats available via `EvaluationOptions.OutputFormat`:

1. **Flag** (default) - Boolean pass/fail only, fastest performance
2. **List** - Flat error list with metadata
3. **Hierarchy** - Tree structure mirroring schema organization

```csharp
var options = new EvaluationOptions
{
    OutputFormat = OutputFormat.List
};

var result = schema.Evaluate(jsonData, options);

if (!result.IsValid)
{
    foreach (var detail in result.Details)
    {
        Console.WriteLine($"Path: {detail.InstanceLocation}");
        Console.WriteLine($"Error: {detail.Errors}");
    }
}
```

Example error output:
```json
{
  "valid": false,
  "details": [
    {
      "valid": false,
      "evaluationPath": "",
      "schemaLocation": "https://example.com/schema#",
      "instanceLocation": "",
      "errors": {
        "required": "Required properties [\"foo\"] were not present"
      }
    }
  ]
}
```

#### Localization

Supports 8 languages via separate NuGet packages:
- Italian, Norwegian, Polish, Russian, Spanish, German, Swedish, Turkish

#### Strengths
- Full Draft 2020-12 support
- Uses System.Text.Json (modern, performant)
- Excellent error message quality with configurable output
- Localization support
- Active development and maintenance
- Used by PowerShell (high credibility)
- 26.9M+ downloads on NuGet

#### Weaknesses
- No native YAML support (requires YamlDotNet bridge)
- **IMPORTANT: Licensing change effective February 1, 2026**

---

### 3. YamlDotNet

**Repository:** [aaubry/YamlDotNet](https://github.com/aaubry/YamlDotNet)
**NuGet:** `YamlDotNet` (v16.3.0+)
**License:** MIT

#### Overview

YamlDotNet is the de-facto standard YAML library for .NET. It provides both low-level parsing/emitting and high-level serialization.

#### System.Text.Json Integration

The `YamlDotNet.System.Text.Json` package (v1.7.1) bridges YamlDotNet with System.Text.Json types:

```csharp
// Package: YamlDotNet.System.Text.Json
using YamlDotNet.Serialization;
using YamlDotNet.System.Text.Json;

var deserializer = new DeserializerBuilder()
    .WithNodeTypeResolver(new SystemTextJsonNodeTypeResolver())
    .Build();

// Deserialize YAML directly to JsonNode
var jsonNode = deserializer.Deserialize<JsonNode>(yamlReader);
```

Supported types:
- `System.Text.Json.Nodes.JsonNode`
- `System.Text.Json.Nodes.JsonArray`
- `System.Text.Json.Nodes.JsonObject`
- `System.Text.Json.Nodes.JsonValue`
- `System.Text.Json.JsonElement`
- `System.Text.Json.JsonDocument`

---

## Comparison Matrix

| Criteria | NJsonSchema | JsonSchema.Net |
|----------|-------------|----------------|
| **Draft 2020-12 Support** | No | Yes |
| **Draft 2019-09 Support** | No | Yes |
| **Draft 7 Support** | Partial | Yes |
| **YAML Native Support** | Yes (NJsonSchema.Yaml) | No (requires bridge) |
| **.NET 8+ Compatible** | Yes | Yes |
| **JSON Library** | Newtonsoft.Json | System.Text.Json |
| **Error Message Quality** | Good | Excellent |
| **Localization** | No | Yes (8 languages) |
| **License** | MIT | MIT + Fee (Feb 2026) |
| **Active Maintenance** | Yes | Yes |
| **NuGet Downloads** | ~45M (NJsonSchema) | ~27M |

---

## Licensing Considerations

### JsonSchema.Net Commercial License Change

**Effective Date:** February 1, 2026

The json-everything project (including JsonSchema.Net) is introducing a monthly maintenance fee for commercial use:

> "To ensure the long-term sustainability of this project, I will be introducing a monthly maintenance fee, required to be paid by all organizations or users of any library from this project who generate revenue."

**Key Points:**
- An EULA has been added to binary releases and NuGet packages
- Payment is required via GitHub Sponsors
- Affects revenue-generating organizations and users
- Currently still MIT licensed (until Feb 2026)

**Recommendation:** Organizations should:
1. Budget for the maintenance fee if choosing JsonSchema.Net
2. Review the EULA terms on the GitHub repository
3. Consider contribution to the project as an alternative

---

## Alternative Considerations

### Corvus.JsonSchema

**Repository:** [corvus-dotnet/Corvus.JsonSchema](https://github.com/corvus-dotnet/Corvus.JsonSchema)

Another option supporting Draft 2020-12 with .NET 8 packages. Worth evaluating if the JsonSchema.Net licensing is a concern.

### Json.NET Schema (Newtonsoft)

**Website:** [newtonsoft.com/jsonschema](https://www.newtonsoft.com/jsonschema)

Commercial library with Draft 2020-12 support. Requires a license purchase.

---

## Recommended Solution

### Primary Recommendation: JsonSchema.Net + YamlDotNet

For the CSharp Compound Docs plugin requiring Draft 2020-12 support:

```
Packages Required:
- JsonSchema.Net (v8.0.5+)
- YamlDotNet (v16.3.0+)
- YamlDotNet.System.Text.Json (v1.7.1+)
```

#### Implementation Pattern

```csharp
using System.Text.Json.Nodes;
using Json.Schema;
using YamlDotNet.Serialization;
using YamlDotNet.System.Text.Json;

public class DocTypeSchemaValidator
{
    private readonly IDeserializer _yamlDeserializer;

    public DocTypeSchemaValidator()
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNodeTypeResolver(new SystemTextJsonNodeTypeResolver())
            .Build();
    }

    public JsonSchema LoadSchemaFromYaml(string yamlContent)
    {
        var jsonNode = _yamlDeserializer.Deserialize<JsonNode>(
            new StringReader(yamlContent));
        return JsonSchema.FromText(jsonNode.ToJsonString());
    }

    public ValidationResult ValidateDocument(JsonSchema schema, string jsonDocument)
    {
        var options = new EvaluationOptions
        {
            OutputFormat = OutputFormat.List
        };

        var result = schema.Evaluate(JsonNode.Parse(jsonDocument), options);

        return new ValidationResult
        {
            IsValid = result.IsValid,
            Errors = ExtractErrors(result)
        };
    }

    private List<ValidationError> ExtractErrors(EvaluationResults result)
    {
        var errors = new List<ValidationError>();

        if (!result.IsValid && result.Details != null)
        {
            foreach (var detail in result.Details.Where(d => !d.IsValid))
            {
                if (detail.Errors != null)
                {
                    foreach (var error in detail.Errors)
                    {
                        errors.Add(new ValidationError
                        {
                            Path = detail.InstanceLocation?.ToString() ?? "",
                            Keyword = error.Key,
                            Message = error.Value
                        });
                    }
                }
            }
        }

        return errors;
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
    public string Keyword { get; init; } = "";
    public string Message { get; init; } = "";
}
```

### Fallback Recommendation: NJsonSchema.Yaml

If Draft 2020-12 support is not strictly required and licensing simplicity is preferred:

```
Packages Required:
- NJsonSchema (v11.5.2+)
- NJsonSchema.Yaml (v11.5.2+)
```

This option provides simpler YAML integration but is limited to Draft 7 and earlier.

---

## Decision Matrix

| Requirement | Weight | NJsonSchema Score | JsonSchema.Net Score |
|-------------|--------|-------------------|---------------------|
| Draft 2020-12 Support | Critical | 0 | 10 |
| YAML Integration | High | 10 | 7 |
| .NET 8+ Compatible | Critical | 10 | 10 |
| Error Message Quality | Medium | 7 | 10 |
| Maintenance Status | High | 8 | 9 |
| Licensing Clarity | Medium | 10 | 6 |
| **Weighted Total** | - | **45** | **52** |

---

## Conclusion

**Recommended:** JsonSchema.Net + YamlDotNet

The requirement for Draft 2020-12 support makes JsonSchema.Net the only viable option among the evaluated libraries. While it requires a YamlDotNet bridge for YAML parsing, this is a straightforward integration pattern.

**Action Items:**
1. Add required NuGet packages to the project
2. Implement the YAML-to-JSON conversion utility
3. Create a schema validation service using the pattern above
4. Monitor the JsonSchema.Net licensing situation before February 2026
5. Consider evaluating Corvus.JsonSchema as a backup option

---

## Sources

- [NJsonSchema GitHub Repository](https://github.com/RicoSuter/NJsonSchema)
- [JsonSchema.Net Documentation](https://docs.json-everything.net/schema/basics/)
- [json-everything GitHub Repository](https://github.com/json-everything/json-everything)
- [YamlDotNet GitHub Repository](https://github.com/aaubry/YamlDotNet)
- [YamlDotNet.System.Text.Json NuGet](https://www.nuget.org/packages/YamlDotNet.System.Text.Json)
- [JSON Schema Draft 2020-12 Specification](https://json-schema.org/draft/2020-12)
- [PowerShell Test-Json Migration PR](https://github.com/PowerShell/PowerShell/pull/18141)
