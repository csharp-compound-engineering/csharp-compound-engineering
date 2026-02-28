# Code Style & Conventions

## .editorconfig Rules (enforced)
- **Charset**: UTF-8, LF line endings
- **Indent**: 4 spaces for C#/Dockerfile, 2 spaces for XML/JSON/YAML
- **Trailing whitespace**: trimmed (except Markdown)
- **Final newline**: always inserted

## C# Conventions
- **File-scoped namespaces** (`csharp_style_namespace_declarations = file_scoped:warning`)
- **`var` preferred** everywhere (built-in types, apparent types, and elsewhere)
- **Expression-bodied members**: allowed on single-line for methods, properties, accessors, lambdas, local functions
- **Constructors**: block-bodied (not expression-bodied)
- **Braces**: preferred (`csharp_prefer_braces = true:suggestion`)
- **Simple using statements**: preferred
- **Allman-style braces**: open brace on new line for all constructs
- **Sort system usings first**, no separate import groups
- **Pattern matching** preferred over `is`/`as` with casts/null checks

## Naming Conventions (warning severity)
| Element | Style | Example |
|---------|-------|---------|
| Interfaces | PascalCase with `I` prefix | `IGraphRepository` |
| Types (class, struct, enum) | PascalCase | `DocumentNode` |
| Public/internal members | PascalCase | `ExecuteQueryAsync` |
| Private fields | `_camelCase` (underscore prefix) | `_logger` |

## Testing Rules (CRITICAL)
- **Mocking: Moq ONLY** — `Mock<T>`, `It.IsAny<T>()`, `.Setup()`, `.Returns()`, `.Verifiable()`
- **Assertions: Shouldly ONLY** — `ShouldBe()`, `ShouldNotBeNull()`, `ShouldContain()`, `ShouldThrow()`
- **NO NSubstitute. NO FluentAssertions.**
- These rules apply to ALL test projects (unit, integration, E2E)
- xUnit test framework

## Commit Convention (Conventional Commits)
Format: `type(scope): lowercase description`
- Types: `feat`, `fix`, `perf`, `revert`, `refactor`, `docs`, `style`, `test`, `build`, `ci`, `chore`
- Subject must start with **lowercase**
- `refactor` → major release; `feat`/`perf` → minor; `fix`/`revert` → patch
- Breaking changes: `type!:` suffix or `BREAKING CHANGE:` footer → major
- Scopes optional; common: `mcp`, `tools`, `db`, `docker`, `docs`, `tests`

## Design Patterns
- DI registration in per-project `DependencyInjection/` folders with `*ServiceCollectionExtensions` classes
- Interface-driven design: every service has an `I*` interface
- Repository pattern for data access
- Pipeline pattern for RAG orchestration
- Options pattern for configuration (`IOptions<T>`, `PostConfigure<T>`)
- MCP tool discovery via `WithToolsFromAssembly()` (auto-discovers `[McpServerToolType]` classes)
- Lambda dual-mode: `AWS_LAMBDA_FUNCTION_NAME` env var detection, conditional service registration

## Pre-commit
- gitleaks for secret scanning (v8.24.2)

## Build Strictness
- `TreatWarningsAsErrors=true`
- `EnforceCodeStyleInBuild=true`
- `AnalysisLevel=latest`
- `Nullable=enable`
