# Phase 138: Custom Doc-Type Registration

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Configuration System
> **Prerequisites**: Phase 095 (Create Type Skill)

---

## Spec References

This phase implements the configuration-based registration system for custom doc-types:

- **spec/doc-types/custom-types.md** - [Definition Structure](../spec/doc-types/custom-types.md#definition-structure-configjson) - Config registration format
- **spec/configuration.md** - [Custom Doc-Types Schema](../spec/configuration.md#schema) - JSON Schema definition for `custom_doc_types` array
- **spec/doc-types/custom-types.md** - [Schema File Structure](../spec/doc-types/custom-types.md#schema-file-structure) - Schema file path patterns

---

## Overview

Custom doc-types created via `/cdocs:create-type` (Phase 095) must be registered in the project's `config.json` file. This phase implements the runtime services that:

1. Parse and validate custom doc-type registrations from config
2. Resolve schema file paths to actual file locations
3. Locate and validate skill directories for custom types
4. Provide enumeration capabilities for listing all registered doc-types
5. Ensure registration integrity during project activation

This registration layer bridges the gap between the skill file (which defines capture behavior) and the MCP server (which needs to validate documents and manage storage folders).

---

## Goals

1. Implement custom doc-type registration parsing from `config.json`
2. Create schema file path resolution with existence validation
3. Build skill directory discovery for custom doc-types
4. Provide doc-type enumeration service combining built-in and custom types
5. Integrate registration validation into project activation workflow

---

## Deliverables

### 1. Custom Doc-Type Registration Models

**Location**: `src/CSharpCompoundingDocs.Core/DocTypes/`

#### 1.1 DocTypeRegistration Record

```csharp
/// <summary>
/// Unified representation of a registered doc-type (built-in or custom).
/// Used for enumeration and lookup operations.
/// </summary>
public sealed record DocTypeRegistration
{
    /// <summary>
    /// Unique identifier for the doc-type (e.g., "problem", "api-contract").
    /// Pattern: ^[a-z][a-z0-9-]*$
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable description of the doc-type's purpose.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Storage folder name relative to ./csharp-compounding-docs/
    /// </summary>
    public required string Folder { get; init; }

    /// <summary>
    /// Whether this is a built-in type or custom (project-defined).
    /// </summary>
    public required DocTypeSource Source { get; init; }

    /// <summary>
    /// Absolute path to the schema file (resolved at registration time).
    /// Null for built-in types that use embedded schemas.
    /// </summary>
    public string? SchemaFilePath { get; init; }

    /// <summary>
    /// Absolute path to the skill directory containing SKILL.md.
    /// For built-in: ${CLAUDE_PLUGIN_ROOT}/skills/cdocs-{name}/
    /// For custom: .claude/skills/cdocs-{name}/
    /// </summary>
    public string? SkillDirectoryPath { get; init; }

    /// <summary>
    /// Skill invocation name (e.g., "cdocs:problem", "cdocs:api-contract").
    /// </summary>
    public string SkillName => $"cdocs:{Name}";
}

/// <summary>
/// Indicates whether a doc-type is built-in or custom.
/// </summary>
public enum DocTypeSource
{
    /// <summary>Built-in doc-type shipped with the plugin.</summary>
    BuiltIn,

    /// <summary>Custom doc-type defined in project config.</summary>
    Custom
}
```

#### 1.2 ResolvedCustomDocType Record

```csharp
/// <summary>
/// A custom doc-type with all paths resolved to absolute locations.
/// Created during project activation after validation.
/// </summary>
public sealed record ResolvedCustomDocType
{
    /// <summary>
    /// Original registration from config.json.
    /// </summary>
    public required CustomDocType Registration { get; init; }

    /// <summary>
    /// Absolute path to the schema file.
    /// </summary>
    public required string ResolvedSchemaPath { get; init; }

    /// <summary>
    /// Absolute path to the skill directory.
    /// </summary>
    public required string ResolvedSkillPath { get; init; }

    /// <summary>
    /// Absolute path to the document storage folder.
    /// </summary>
    public required string ResolvedFolderPath { get; init; }

    /// <summary>
    /// Whether the schema file exists and is valid.
    /// </summary>
    public bool SchemaFileExists { get; init; }

    /// <summary>
    /// Whether the skill directory contains a SKILL.md file.
    /// </summary>
    public bool SkillFileExists { get; init; }
}
```

---

### 2. Doc-Type Registration Service

**Location**: `src/CSharpCompoundingDocs.Core/DocTypes/Services/`

#### 2.1 IDocTypeRegistrationService Interface

```csharp
/// <summary>
/// Service for managing doc-type registrations from configuration.
/// </summary>
public interface IDocTypeRegistrationService
{
    /// <summary>
    /// Gets all registered doc-types (built-in + custom) for the current project.
    /// </summary>
    /// <returns>Enumerable of all doc-type registrations.</returns>
    IEnumerable<DocTypeRegistration> GetAllDocTypes();

    /// <summary>
    /// Gets only built-in doc-types.
    /// </summary>
    IEnumerable<DocTypeRegistration> GetBuiltInDocTypes();

    /// <summary>
    /// Gets only custom doc-types from the current project config.
    /// </summary>
    IEnumerable<DocTypeRegistration> GetCustomDocTypes();

    /// <summary>
    /// Looks up a doc-type by name.
    /// </summary>
    /// <param name="name">Doc-type name (e.g., "problem", "api-contract")</param>
    /// <returns>Registration if found, null otherwise.</returns>
    DocTypeRegistration? GetDocType(string name);

    /// <summary>
    /// Checks if a doc-type name is registered (built-in or custom).
    /// </summary>
    bool IsRegistered(string name);

    /// <summary>
    /// Checks if a name conflicts with a built-in doc-type.
    /// </summary>
    bool IsBuiltInName(string name);

    /// <summary>
    /// Resolves and validates all custom doc-type registrations.
    /// Called during project activation.
    /// </summary>
    /// <param name="projectPath">Path to project root</param>
    /// <param name="config">Project configuration</param>
    /// <returns>Resolution results with validation status.</returns>
    Task<DocTypeResolutionResult> ResolveCustomDocTypesAsync(
        string projectPath,
        ProjectConfig config,
        CancellationToken cancellationToken = default);
}
```

#### 2.2 DocTypeRegistrationService Implementation

```csharp
public sealed class DocTypeRegistrationService : IDocTypeRegistrationService
{
    private static readonly IReadOnlyList<DocTypeRegistration> BuiltInDocTypes =
    [
        new DocTypeRegistration
        {
            Name = "problem",
            Description = "Problems encountered and their solutions",
            Folder = "problems",
            Source = DocTypeSource.BuiltIn
        },
        new DocTypeRegistration
        {
            Name = "insight",
            Description = "Discoveries and learnings about the codebase",
            Folder = "insights",
            Source = DocTypeSource.BuiltIn
        },
        new DocTypeRegistration
        {
            Name = "codebase",
            Description = "Architectural decisions and system design documentation",
            Folder = "codebase",
            Source = DocTypeSource.BuiltIn
        },
        new DocTypeRegistration
        {
            Name = "tool",
            Description = "Tool usage patterns and configurations",
            Folder = "tools",
            Source = DocTypeSource.BuiltIn
        },
        new DocTypeRegistration
        {
            Name = "style",
            Description = "Coding conventions and style guidelines",
            Folder = "styles",
            Source = DocTypeSource.BuiltIn
        }
    ];

    private const string DocsFolder = "csharp-compounding-docs";
    private const string SchemasFolder = "schemas";
    private const string CustomSkillsFolder = ".claude/skills";
    private const string SkillFileName = "SKILL.md";

    private readonly SwitchableProjectConfigurationProvider _configProvider;
    private readonly ILogger<DocTypeRegistrationService> _logger;

    private IReadOnlyList<ResolvedCustomDocType> _resolvedCustomTypes = [];
    private string? _resolvedProjectPath;

    public DocTypeRegistrationService(
        SwitchableProjectConfigurationProvider configProvider,
        ILogger<DocTypeRegistrationService> logger)
    {
        _configProvider = configProvider;
        _logger = logger;
    }

    public IEnumerable<DocTypeRegistration> GetAllDocTypes()
    {
        return GetBuiltInDocTypes().Concat(GetCustomDocTypes());
    }

    public IEnumerable<DocTypeRegistration> GetBuiltInDocTypes()
    {
        return BuiltInDocTypes;
    }

    public IEnumerable<DocTypeRegistration> GetCustomDocTypes()
    {
        return _resolvedCustomTypes.Select(r => new DocTypeRegistration
        {
            Name = r.Registration.Name,
            Description = r.Registration.Description,
            Folder = r.Registration.Folder,
            Source = DocTypeSource.Custom,
            SchemaFilePath = r.ResolvedSchemaPath,
            SkillDirectoryPath = r.ResolvedSkillPath
        });
    }

    public DocTypeRegistration? GetDocType(string name)
    {
        return GetAllDocTypes().FirstOrDefault(dt =>
            dt.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsRegistered(string name)
    {
        return GetDocType(name) is not null;
    }

    public bool IsBuiltInName(string name)
    {
        return BuiltInDocTypes.Any(dt =>
            dt.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<DocTypeResolutionResult> ResolveCustomDocTypesAsync(
        string projectPath,
        ProjectConfig config,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<DocTypeResolutionError>();
        var resolved = new List<ResolvedCustomDocType>();

        foreach (var customType in config.CustomDocTypes)
        {
            var resolution = await ResolveCustomTypeAsync(
                projectPath, customType, cancellationToken);

            if (resolution.Errors.Count > 0)
            {
                errors.AddRange(resolution.Errors);
            }

            if (resolution.Resolved is not null)
            {
                resolved.Add(resolution.Resolved);
            }
        }

        // Check for name collisions with built-in types
        foreach (var customType in config.CustomDocTypes)
        {
            if (IsBuiltInName(customType.Name))
            {
                errors.Add(new DocTypeResolutionError(
                    customType.Name,
                    DocTypeResolutionErrorKind.NameCollision,
                    $"Custom doc-type name '{customType.Name}' conflicts with built-in type"));
            }
        }

        // Check for duplicate custom type names
        var duplicates = config.CustomDocTypes
            .GroupBy(ct => ct.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicate in duplicates)
        {
            errors.Add(new DocTypeResolutionError(
                duplicate,
                DocTypeResolutionErrorKind.DuplicateName,
                $"Duplicate custom doc-type name: '{duplicate}'"));
        }

        if (errors.Count == 0)
        {
            _resolvedCustomTypes = resolved;
            _resolvedProjectPath = projectPath;
            _logger.LogInformation(
                "Resolved {Count} custom doc-types for project at {Path}",
                resolved.Count, projectPath);
        }
        else
        {
            _logger.LogWarning(
                "Custom doc-type resolution completed with {ErrorCount} errors",
                errors.Count);
        }

        return new DocTypeResolutionResult(resolved, errors);
    }

    private Task<SingleTypeResolution> ResolveCustomTypeAsync(
        string projectPath,
        CustomDocType customType,
        CancellationToken cancellationToken)
    {
        var errors = new List<DocTypeResolutionError>();

        // Resolve schema file path
        var schemaPath = ResolveSchemaPath(projectPath, customType.SchemaFile);
        var schemaExists = File.Exists(schemaPath);

        if (!schemaExists)
        {
            errors.Add(new DocTypeResolutionError(
                customType.Name,
                DocTypeResolutionErrorKind.SchemaFileNotFound,
                $"Schema file not found: {customType.SchemaFile} (resolved to: {schemaPath})"));
        }

        // Resolve skill directory path
        var skillPath = ResolveSkillPath(projectPath, customType.Name);
        var skillFilePath = Path.Combine(skillPath, SkillFileName);
        var skillExists = File.Exists(skillFilePath);

        if (!skillExists)
        {
            errors.Add(new DocTypeResolutionError(
                customType.Name,
                DocTypeResolutionErrorKind.SkillFileNotFound,
                $"Skill file not found at: {skillFilePath}"));
        }

        // Resolve storage folder path
        var folderPath = Path.Combine(projectPath, DocsFolder, customType.Folder);
        var folderExists = Directory.Exists(folderPath);

        // Folder not existing is a warning, not an error (it can be created)
        if (!folderExists)
        {
            _logger.LogDebug(
                "Storage folder for custom type '{Name}' does not exist yet: {Path}",
                customType.Name, folderPath);
        }

        var resolved = new ResolvedCustomDocType
        {
            Registration = customType,
            ResolvedSchemaPath = schemaPath,
            ResolvedSkillPath = skillPath,
            ResolvedFolderPath = folderPath,
            SchemaFileExists = schemaExists,
            SkillFileExists = skillExists
        };

        return Task.FromResult(new SingleTypeResolution(resolved, errors));
    }

    private string ResolveSchemaPath(string projectPath, string schemaFile)
    {
        // Schema files are relative to ./csharp-compounding-docs/
        // e.g., "./schemas/api-contract.schema.yaml" -> {projectPath}/csharp-compounding-docs/schemas/api-contract.schema.yaml

        var normalizedPath = schemaFile.TrimStart('.', '/', '\\');
        return Path.GetFullPath(
            Path.Combine(projectPath, DocsFolder, normalizedPath));
    }

    private string ResolveSkillPath(string projectPath, string docTypeName)
    {
        // Custom skills are in .claude/skills/cdocs-{name}/
        return Path.Combine(projectPath, CustomSkillsFolder, $"cdocs-{docTypeName}");
    }

    private sealed record SingleTypeResolution(
        ResolvedCustomDocType? Resolved,
        List<DocTypeResolutionError> Errors);
}
```

---

### 3. Resolution Result Types

**Location**: `src/CSharpCompoundingDocs.Core/DocTypes/`

#### 3.1 DocTypeResolutionResult

```csharp
/// <summary>
/// Result of resolving custom doc-type registrations.
/// </summary>
public sealed record DocTypeResolutionResult
{
    public IReadOnlyList<ResolvedCustomDocType> ResolvedTypes { get; }
    public IReadOnlyList<DocTypeResolutionError> Errors { get; }

    public bool IsSuccess => Errors.Count == 0;
    public int TotalCount => ResolvedTypes.Count;
    public int ValidCount => ResolvedTypes.Count(r => r.SchemaFileExists && r.SkillFileExists);

    public DocTypeResolutionResult(
        IReadOnlyList<ResolvedCustomDocType> resolvedTypes,
        IReadOnlyList<DocTypeResolutionError> errors)
    {
        ResolvedTypes = resolvedTypes;
        Errors = errors;
    }
}

/// <summary>
/// Error encountered during doc-type resolution.
/// </summary>
public sealed record DocTypeResolutionError(
    string DocTypeName,
    DocTypeResolutionErrorKind Kind,
    string Message);

/// <summary>
/// Categories of resolution errors.
/// </summary>
public enum DocTypeResolutionErrorKind
{
    /// <summary>Schema file does not exist at resolved path.</summary>
    SchemaFileNotFound,

    /// <summary>Skill directory or SKILL.md file not found.</summary>
    SkillFileNotFound,

    /// <summary>Custom type name conflicts with built-in type.</summary>
    NameCollision,

    /// <summary>Multiple custom types with same name.</summary>
    DuplicateName,

    /// <summary>Invalid doc-type name format.</summary>
    InvalidName,

    /// <summary>Schema file failed validation.</summary>
    SchemaInvalid
}
```

---

### 4. Registration Validation

**Location**: `src/CSharpCompoundingDocs.Core/DocTypes/Validation/`

#### 4.1 IDocTypeRegistrationValidator Interface

```csharp
/// <summary>
/// Validates custom doc-type registrations for completeness and correctness.
/// </summary>
public interface IDocTypeRegistrationValidator
{
    /// <summary>
    /// Validates a single custom doc-type registration.
    /// </summary>
    ValidationResult ValidateRegistration(CustomDocType registration);

    /// <summary>
    /// Validates all custom doc-types in a configuration.
    /// </summary>
    ValidationResult ValidateAll(IEnumerable<CustomDocType> registrations);

    /// <summary>
    /// Validates the doc-type name format.
    /// </summary>
    bool IsValidName(string name);
}
```

#### 4.2 DocTypeRegistrationValidator Implementation

```csharp
public sealed class DocTypeRegistrationValidator : IDocTypeRegistrationValidator
{
    // Matches: lowercase letter followed by lowercase letters, digits, or hyphens
    private static readonly Regex NamePattern = new(
        @"^[a-z][a-z0-9-]*$",
        RegexOptions.Compiled);

    private static readonly HashSet<string> BuiltInNames = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "problem", "insight", "codebase", "tool", "style"
    };

    private static readonly HashSet<string> ReservedNames = new(
        StringComparer.OrdinalIgnoreCase)
    {
        // Meta skills and special commands
        "create-type", "capture-select", "activate", "query",
        "search", "search-external", "query-external", "delete",
        "promote", "todo", "worktree"
    };

    public ValidationResult ValidateRegistration(CustomDocType registration)
    {
        var errors = new List<ValidationError>();

        // Validate name format
        if (string.IsNullOrWhiteSpace(registration.Name))
        {
            errors.Add(new ValidationError("name", "required", "Name is required"));
        }
        else if (!IsValidName(registration.Name))
        {
            errors.Add(new ValidationError(
                "name",
                "pattern",
                $"Name '{registration.Name}' must be kebab-case (lowercase letters, digits, hyphens)"));
        }
        else if (BuiltInNames.Contains(registration.Name))
        {
            errors.Add(new ValidationError(
                "name",
                "reserved",
                $"Name '{registration.Name}' conflicts with built-in doc-type"));
        }
        else if (ReservedNames.Contains(registration.Name))
        {
            errors.Add(new ValidationError(
                "name",
                "reserved",
                $"Name '{registration.Name}' is reserved for system skills"));
        }

        // Validate description
        if (string.IsNullOrWhiteSpace(registration.Description))
        {
            errors.Add(new ValidationError("description", "required", "Description is required"));
        }

        // Validate folder
        if (string.IsNullOrWhiteSpace(registration.Folder))
        {
            errors.Add(new ValidationError("folder", "required", "Folder is required"));
        }
        else if (registration.Folder.Contains("..") ||
                 Path.IsPathRooted(registration.Folder))
        {
            errors.Add(new ValidationError(
                "folder",
                "invalid",
                "Folder must be a simple name, not a path"));
        }

        // Validate schema file reference
        if (string.IsNullOrWhiteSpace(registration.SchemaFile))
        {
            errors.Add(new ValidationError("schema_file", "required", "Schema file path is required"));
        }
        else if (!registration.SchemaFile.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) &&
                 !registration.SchemaFile.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) &&
                 !registration.SchemaFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new ValidationError(
                "schema_file",
                "format",
                "Schema file must be .yaml, .yml, or .json"));
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failed(errors);
    }

    public ValidationResult ValidateAll(IEnumerable<CustomDocType> registrations)
    {
        var allErrors = new List<ValidationError>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var registration in registrations)
        {
            var result = ValidateRegistration(registration);
            allErrors.AddRange(result.Errors);

            // Check for duplicates
            if (!string.IsNullOrWhiteSpace(registration.Name))
            {
                if (!seenNames.Add(registration.Name))
                {
                    allErrors.Add(new ValidationError(
                        "name",
                        "duplicate",
                        $"Duplicate doc-type name: '{registration.Name}'"));
                }
            }
        }

        return allErrors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failed(allErrors);
    }

    public bool IsValidName(string name)
    {
        return !string.IsNullOrWhiteSpace(name) && NamePattern.IsMatch(name);
    }
}
```

---

### 5. Doc-Type Enumeration Tool Support

**Location**: `src/CSharpCompoundingDocs.Core/DocTypes/`

#### 5.1 DocTypeEnumerationResult

```csharp
/// <summary>
/// Result model for listing available doc-types.
/// Used by the list_doc_types MCP tool.
/// </summary>
public sealed record DocTypeEnumerationResult
{
    /// <summary>
    /// Built-in doc-types always available.
    /// </summary>
    public IReadOnlyList<DocTypeSummary> BuiltInTypes { get; init; } = [];

    /// <summary>
    /// Custom doc-types defined in project config.
    /// </summary>
    public IReadOnlyList<DocTypeSummary> CustomTypes { get; init; } = [];

    /// <summary>
    /// Total count of all doc-types.
    /// </summary>
    public int TotalCount => BuiltInTypes.Count + CustomTypes.Count;
}

/// <summary>
/// Summary information for a doc-type (used in listings).
/// </summary>
public sealed record DocTypeSummary
{
    /// <summary>Doc-type identifier.</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable description.</summary>
    public required string Description { get; init; }

    /// <summary>Storage folder name.</summary>
    public required string Folder { get; init; }

    /// <summary>Skill invocation command.</summary>
    public required string SkillCommand { get; init; }

    /// <summary>Whether registration is complete and valid.</summary>
    public bool IsValid { get; init; } = true;

    /// <summary>Validation error message if not valid.</summary>
    public string? ValidationError { get; init; }
}
```

#### 5.2 Extension Method for Enumeration

```csharp
public static class DocTypeRegistrationExtensions
{
    /// <summary>
    /// Creates an enumeration result suitable for the list_doc_types tool.
    /// </summary>
    public static DocTypeEnumerationResult ToEnumerationResult(
        this IDocTypeRegistrationService service)
    {
        var builtIn = service.GetBuiltInDocTypes()
            .Select(dt => new DocTypeSummary
            {
                Name = dt.Name,
                Description = dt.Description,
                Folder = dt.Folder,
                SkillCommand = $"/cdocs:{dt.Name}",
                IsValid = true
            })
            .ToList();

        var custom = service.GetCustomDocTypes()
            .Select(dt => new DocTypeSummary
            {
                Name = dt.Name,
                Description = dt.Description,
                Folder = dt.Folder,
                SkillCommand = $"/cdocs:{dt.Name}",
                IsValid = dt.SchemaFilePath is not null,
                ValidationError = dt.SchemaFilePath is null
                    ? "Schema file not found"
                    : null
            })
            .ToList();

        return new DocTypeEnumerationResult
        {
            BuiltInTypes = builtIn,
            CustomTypes = custom
        };
    }
}
```

---

### 6. Integration with Project Activation

**Location**: Update to `src/CSharpCompoundingDocs.Core/Services/ProjectActivationService.cs`

#### 6.1 Activation Integration

The project activation service (from Phase 079) should call the doc-type registration service:

```csharp
// In ProjectActivationService.ActivateAsync:

// ... existing activation logic ...

// Resolve and validate custom doc-types
var resolutionResult = await _docTypeService.ResolveCustomDocTypesAsync(
    projectPath, config, cancellationToken);

if (!resolutionResult.IsSuccess)
{
    // Log warnings but don't fail activation
    foreach (var error in resolutionResult.Errors)
    {
        _logger.LogWarning(
            "Custom doc-type '{Name}' has issues: {Error}",
            error.DocTypeName, error.Message);
    }
}

// Create missing folders for valid custom types
foreach (var resolved in resolutionResult.ResolvedTypes
    .Where(r => r.SchemaFileExists && r.SkillFileExists))
{
    if (!Directory.Exists(resolved.ResolvedFolderPath))
    {
        Directory.CreateDirectory(resolved.ResolvedFolderPath);
        _logger.LogInformation(
            "Created storage folder for custom doc-type '{Name}': {Path}",
            resolved.Registration.Name, resolved.ResolvedFolderPath);
    }
}

// ... continue with activation ...
```

---

### 7. Dependency Injection Registration

**Location**: `src/CSharpCompoundingDocs.Core/DocTypes/ServiceCollectionExtensions.cs`

```csharp
public static class DocTypeServiceCollectionExtensions
{
    public static IServiceCollection AddDocTypeServices(
        this IServiceCollection services)
    {
        services.AddSingleton<IDocTypeRegistrationService, DocTypeRegistrationService>();
        services.AddSingleton<IDocTypeRegistrationValidator, DocTypeRegistrationValidator>();

        return services;
    }
}
```

---

## Testing Requirements

### Unit Tests

1. **DocTypeRegistrationServiceTests**
   - Return all 5 built-in doc-types
   - Return empty list when no custom types configured
   - Resolve custom type schema paths correctly
   - Resolve custom type skill paths correctly
   - Detect missing schema files
   - Detect missing skill files
   - Detect name collisions with built-in types
   - Detect duplicate custom type names
   - Lookup doc-type by name (case-insensitive)

2. **DocTypeRegistrationValidatorTests**
   - Accept valid kebab-case names
   - Reject names with uppercase letters
   - Reject names with underscores
   - Reject names starting with numbers
   - Reject built-in type names
   - Reject reserved skill names
   - Require all mandatory fields
   - Validate schema file extension
   - Reject path traversal in folder names

3. **DocTypeEnumerationTests**
   - Include all built-in types in enumeration
   - Include valid custom types in enumeration
   - Mark invalid custom types appropriately
   - Generate correct skill commands

### Integration Tests

1. **Registration round-trip**: Add custom type to config, activate, verify enumeration
2. **Missing file handling**: Custom type with missing schema gracefully degrades
3. **Activation integration**: Project activation resolves custom types

---

## Acceptance Criteria

- [ ] Built-in doc-types enumerable without project activation
- [ ] Custom doc-types loaded from `config.json` `custom_doc_types` array
- [ ] Schema file paths resolved relative to `./csharp-compounding-docs/`
- [ ] Skill directories resolved to `.claude/skills/cdocs-{name}/`
- [ ] Name validation prevents collisions with built-in types
- [ ] Name validation prevents reserved skill names (create-type, capture-select, etc.)
- [ ] Missing schema files logged as warnings, don't block activation
- [ ] Missing skill files logged as warnings, don't block activation
- [ ] Storage folders auto-created for valid custom types during activation
- [ ] Enumeration includes validity status for each doc-type
- [ ] Case-insensitive doc-type name lookup
- [ ] Unit test coverage > 80% for all services

---

## File Structure

After implementation, the following files should exist:

```
src/CSharpCompoundingDocs.Core/
└── DocTypes/
    ├── DocTypeRegistration.cs
    ├── DocTypeSource.cs
    ├── ResolvedCustomDocType.cs
    ├── DocTypeResolutionResult.cs
    ├── DocTypeResolutionError.cs
    ├── DocTypeEnumerationResult.cs
    ├── DocTypeSummary.cs
    ├── ServiceCollectionExtensions.cs
    ├── Services/
    │   ├── IDocTypeRegistrationService.cs
    │   └── DocTypeRegistrationService.cs
    └── Validation/
        ├── IDocTypeRegistrationValidator.cs
        └── DocTypeRegistrationValidator.cs
```

---

## Dependencies

### Depends On
- **Phase 010**: Project Configuration System (ProjectConfig, CustomDocType models)
- **Phase 095**: Create Type Skill (generates the registrations this phase reads)
- **Phase 079**: Activate Project Tool (activation workflow integration point)

### Blocks
- **Phase 074**: List Doc-Types Tool (needs enumeration result model)
- Doc-type validation during document capture
- Custom schema loading for frontmatter validation

---

## Verification Steps

After completing this phase, verify:

1. **Built-in Types Available**:
   ```csharp
   var service = serviceProvider.GetRequiredService<IDocTypeRegistrationService>();
   var builtIn = service.GetBuiltInDocTypes().ToList();
   Assert.Equal(5, builtIn.Count);
   Assert.Contains(builtIn, dt => dt.Name == "problem");
   ```

2. **Custom Type Resolution**:
   ```csharp
   var result = await service.ResolveCustomDocTypesAsync(projectPath, config);
   Assert.True(result.IsSuccess);
   ```

3. **Name Validation**:
   ```csharp
   var validator = serviceProvider.GetRequiredService<IDocTypeRegistrationValidator>();
   Assert.False(validator.IsValidName("Problem"));  // Uppercase
   Assert.False(validator.IsValidName("api_contract"));  // Underscore
   Assert.True(validator.IsValidName("api-contract"));  // Valid
   ```

4. **Enumeration**:
   ```csharp
   var enumResult = service.ToEnumerationResult();
   Assert.Equal(5, enumResult.BuiltInTypes.Count);
   ```

---

## Notes

- Custom doc-types use project-scoped skill directories (`.claude/skills/`) not plugin directories
- Schema files use YAML format but support JSON as fallback
- Registration validation is lenient at activation time (warnings, not errors) to allow partial setups
- The enumeration result model supports the `list_doc_types` MCP tool's response format
- Name validation excludes reserved names to prevent conflicts with meta-skills
