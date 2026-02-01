# Code Style and Conventions

## C# Conventions
- **File-scoped namespaces** - Use `namespace X;` instead of `namespace X { }`
- **Nullable reference types** - Enabled, use `?` for nullable types
- **Required properties** - Use `required` modifier for mandatory init properties
- **XML documentation** - All public APIs have `<summary>` docs
- **PascalCase** - Classes, methods, properties
- **camelCase with underscore** - Private fields (e.g., `_logger`)

## Interface Naming
- Prefix with `I` (e.g., `IDocumentProcessor`, `ISessionContext`)

## Async Methods
- Suffix async methods with `Async`
- Always pass `CancellationToken` as last parameter with default

## Dependency Injection
- Constructor injection with null guards
- Use `sealed` classes when inheritance is not intended
- Service registration extensions in `DependencyInjection/` folder

## Result Types
- Use `ToolResponse<T>` for MCP tool returns
- Use pattern like `IndexResult` with static factory methods `Success()`/`Failure()`

## MCP Tools
- Annotate with `[McpServerToolType]` and `[McpServerTool]`
- Include `[Description]` attribute
- Use JSON property names with snake_case via `[JsonPropertyName]`

## Directory Structure
- Documents stored in `.csharp-compounding-docs/` directory
- Use YAML frontmatter with underscore naming convention
