# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A Claude Code MCP plugin (`cd`) implementing a GraphRAG knowledge base for C#/.NET projects. It combines graph traversal (Amazon Neptune) with vector search (AWS OpenSearch Serverless) and LLM synthesis (Amazon Bedrock) to provide intelligent semantic Q&A over documentation with source attribution.

The MCP server exposes a single tool `rag_query` that orchestrates: embedding generation → KNN vector search → graph traversal for concept enrichment → LLM synthesis with sources.

## Build Commands

```bash
dotnet build                              # Build all projects (8 src + 3 test)
dotnet test                               # Run all tests (unit, integration, E2E)
dotnet test --filter "FullyQualifiedName~TestName"  # Run a specific test
dotnet run --project src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj  # Run MCP server
bash scripts/coverage-merge.sh            # Run tests + merge coverage + enforce 100% threshold
```

**Build and release scripts in `scripts/` (not part of the MCP server or plugin):**
- `coverage-merge.sh` — Run tests + merge coverage + enforce 100% threshold
- `release-prepare.sh` — Version bumps for releases
- `release-docker.sh` — Docker image build/push
- `release-helm.sh` — Helm chart packaging/push
- `release-docs.sh` — Documentation site build

## Architecture

**Solution:** `csharp-compounding-docs.sln` — 8 source projects, 3 test projects. .NET 9.0 with nullable reference types, `TreatWarningsAsErrors=true`, and enforced code style.

**Source projects (`src/`):**

| Project | Purpose |
|---------|---------|
| `CompoundDocs.McpServer` | Main MCP server app (HTTP transport, port 8080). Contains the `RagQueryTool`, document processing pipeline, resilience policies (Polly), and observability. |
| `CompoundDocs.Common` | Shared models (`DocumentNode`, `ChunkNode`, `ConceptNode`, etc.), graph relationships (`HAS_SECTION`, `HAS_CHUNK`, `MENTIONS`, `RELATES_TO`, `LINKS_TO`), config loading, Markdown/YAML parsing, logging with correlation IDs. |
| `CompoundDocs.GraphRag` | Orchestrates the full RAG pipeline: embed → vector search → graph traversal → LLM synthesis. Key interface: `IGraphRagPipeline`. |
| `CompoundDocs.Vector` | `IVectorStore` abstraction over AWS OpenSearch Serverless for KNN search. |
| `CompoundDocs.Graph` | `IGraphRepository` abstraction over Amazon Neptune (openCypher via AWS Neptunedata SDK). |
| `CompoundDocs.Bedrock` | `IBedrockEmbeddingService` (Titan Embed v2, 1024-dim) and `IBedrockLlmService` (Claude models). |
| `CompoundDocs.GitSync` | Git repository monitoring via LibGit2Sharp; triggers document re-indexing on changes. |
| `CompoundDocs.Worker` | Background service for async document processing. |

**Key patterns:** Dependency injection via `ServiceCollection` extensions, repository/service abstractions, pipeline pattern for RAG, `[McpServerToolType]`/`[McpServerTool]` attributes for MCP tool registration. HTTP transport via `MapMcp().RequireAuthorization()` on ASP.NET Core.

**Test projects (`tests/`):**
- `CompoundDocs.Tests.Unit` — xUnit + Moq + Shouldly
- `CompoundDocs.Tests.Integration` — Service integration tests (AWS tests skipped, mock equivalents runnable)
- `CompoundDocs.Tests.E2E` — End-to-end workflow and MCP protocol compliance tests

**Code coverage:** Uses `coverlet.collector` (not `coverlet.msbuild`) with configuration centralized in `coverlet.runsettings`. Per-project Cobertura reports are merged into a single report via ReportGenerator (local dotnet tool in `.config/dotnet-tools.json`). The merge script (`scripts/coverage-merge.sh`) enforces 100% line+branch thresholds.

Package versions are centrally managed in `Directory.Packages.props`. Global build settings are in `Directory.Build.props`.

## Critical Testing Rules

- **Mocking: Use Moq only.** Do NOT use NSubstitute. All mocks must use `Moq` (`Mock<T>`, `It.IsAny<T>()`, `.Setup()`, `.Returns()`, `.Verifiable()`, etc.).
- **Assertions: Use Shouldly only.** Do NOT use FluentAssertions. Use Shouldly's `ShouldBe()`, `ShouldNotBeNull()`, `ShouldContain()`, `ShouldThrow()`, etc.
- These rules apply to all test projects — unit, integration, and E2E.

## Test Isolation & Infrastructure

- **Every test method must be completely self-contained.** All mocks, SUTs, configs, and test data must be created inline within the test method body. You must be able to understand any test by reading only that test method.
- **No class-level fields** except `const` values (compile-time constants). No mocks, SUTs, config objects, test data arrays, or any other mutable/reference-type fields.
- **No constructors** for test setup.
- **No factory methods or helper methods.** No `CreateSut()`, no `SetupMocks()`, no `MakeTestData()`. These create single points of failure — if the factory has a bug, every test using it fails.
- **No test base classes.** Do not inherit from `TestBase`, `AsyncTestBase`, or any custom base class.
- **`IDisposable` test resources:** Use `try/finally` per test method. Create and clean up temp directories inline.
- **`[Theory]` data:** Use xUnit's built-in `[InlineData]`, `[MemberData]`, or `[ClassData]` for parameterized tests. This is the only mechanism for sharing test data across test methods.

## Integration & E2E Test Patterns

- **No shared fixtures.** Do not use `IClassFixture<T>` or `ICollectionFixture<T>`. Each test creates its own `WebApplicationFactory<Program>` inline. The in-memory `TestServer` is cheap (~10-50ms, no TCP ports, no network).
- **Per-test factory pattern:**
  ```csharp
  await using var factory = new WebApplicationFactory<Program>()
      .WithWebHostBuilder(builder =>
      {
          builder.UseEnvironment("Testing");
          builder.ConfigureTestServices(services =>
          {
              // Register mocks inline
              services.AddSingleton(myMock.Object);
              services.PostConfigure<ApiKeyAuthenticationOptions>(opts => { ... });
          });
      });
  using var httpClient = factory.CreateClient();
  ```
- **MCP protocol tests must use `HttpClientTransport`.** Pass the factory's `HttpClient` to `HttpClientTransport` with `ownsHttpClient: false`. The factory owns the `HttpClient` lifecycle.
  ```csharp
  var transport = new HttpClientTransport(
      new HttpClientTransportOptions
      {
          Endpoint = new Uri(httpClient.BaseAddress!, "/"),
          AdditionalHeaders = new Dictionary<string, string> { ["X-API-Key"] = "test-key" },
          TransportMode = HttpTransportMode.StreamableHttp
      },
      httpClient,
      ownsHttpClient: false);
  await using var mcpClient = await McpClient.CreateAsync(transport, options);
  ```
- **Disposal chain:** `McpClient` → `HttpClient` → `WebApplicationFactory` (use `await using` / `using`).
- **Auth in tests:** Use `PostConfigure<ApiKeyAuthenticationOptions>` to inject test API keys. Set the key via `AdditionalHeaders` on `HttpClientTransportOptions` or `httpClient.DefaultRequestHeaders`.

## Commit Convention

This repository uses [Conventional Commits](https://www.conventionalcommits.org/). PR titles and single-commit messages are validated by CI (`pr-title.yml`).

**Format:** `type(scope): lowercase description`

| Type | Release | Description |
|------|---------|-------------|
| `feat` | minor | New feature |
| `fix` | patch | Bug fix |
| `perf` | minor | Performance improvement |
| `revert` | patch | Reverts a previous commit |
| `refactor` | **major** | Code restructuring (no behavior change) |
| `docs` | none | Documentation only |
| `style` | none | Formatting, whitespace |
| `test` | none | Adding or updating tests |
| `build` | none | Build system or dependencies |
| `ci` | none | CI configuration |
| `chore` | none | Tooling, maintenance |

**Rules:**
- Subject must start with **lowercase** (enforced by CI)
- Append `!` after type/scope for breaking changes: `feat(mcp)!: remove legacy endpoint`
- Or use a `BREAKING CHANGE:` footer
- Breaking changes always trigger a **major** release regardless of type
- Scopes are optional; common scopes: `mcp`, `tools`, `db`, `docker`, `docs`, `tests`

## CI/CD

Single unified GitHub Actions workflow: `.github/workflows/ci.yml` (named "Release").
- **PR** → semantic-release dry-run (validates commits, build, test, pack)
- **Push to main/master** → full semantic-release with conventional commits
- Handles: Docker build+push (GHCR), Helm chart publish (GHCR), docs deploy (gh-pages), changelog, GitHub release
- Release assets: Helm chart (`.tgz`), coverage report (`coverage-report.tar.gz` — merged Cobertura XML + HTML)
- Config in `.releaserc.json`

## Agent Teams (MANDATORY)

**Agent teams MUST be used for all plan execution and for any task of sufficient complexity.** This is not optional. Executing a plan without agent teams is **FORBIDDEN**.

### Rules

- **Plans MUST be written to use agent teams.** Every plan file MUST specify team composition, task breakdown, and teammate assignments. A plan that does not use agent teams is invalid and MUST be rewritten.
- **Executing a plan without agent teams is FORBIDDEN.** Do NOT execute plan steps sequentially in a single session. Always create a team, spawn teammates, and distribute work across them.
- **Any task touching 3+ files, 2+ projects, or requiring parallel workstreams MUST use agent teams.** This includes refactors, new features, cross-cutting changes, and multi-project test updates.
- **You are NOT necessarily the team lead.** Spawn a dedicated lead agent to orchestrate the team. You may do other work or monitor progress while the lead coordinates teammates.
- **One teammate per file.** No two teammates may edit the same file. The lead (or you) must assign clear file ownership before work begins.
- **Use appropriate agent types.** Match `subagent_type` to the work: `backend-architect` for service design, `quality-engineer` for tests, `security-engineer` for auth/security, `refactoring-expert` for refactors, `frontend-architect` for UI, etc.
- **Task lists are mandatory.** All team work MUST be tracked via `TaskCreate`/`TaskUpdate`/`TaskList`. Every teammate MUST mark tasks completed when done.
- **Graceful shutdown is required.** When work is complete, send `shutdown_request` to all teammates and call `TeamDelete` to clean up.

### When single-session work is acceptable

Agent teams are NOT required for:
- Single-file edits (typo fixes, small bug fixes, config changes)
- Pure research/exploration with no code changes
- Answering questions about the codebase
- Tasks the user explicitly asks to be done without a team

Everything else uses agent teams. When in doubt, use a team.

## npm vs pnpm

This repository uses pnpm for everything. So do not use `npm` commands.