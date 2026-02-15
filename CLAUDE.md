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

**Bash scripts in `scripts/`:**
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
| `CompoundDocs.McpServer` | Main MCP server app (HTTP transport, port 3000). Contains the `RagQueryTool`, document processing pipeline, resilience policies (Polly), and observability. |
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

## npm vs pnpm

This repository uses pnpm for everything. So do not use `npm` commands.