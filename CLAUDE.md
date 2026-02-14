# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A Claude Code MCP plugin (`cd`) implementing a GraphRAG knowledge base for C#/.NET projects. It combines graph traversal (Amazon Neptune) with vector search (AWS OpenSearch Serverless) and LLM synthesis (Amazon Bedrock) to provide intelligent semantic Q&A over documentation with source attribution.

The MCP server exposes a single tool `rag_query` that orchestrates: embedding generation → KNN vector search → graph traversal for concept enrichment → LLM synthesis with sources.

## Build Commands

```bash
dotnet build                              # Build all 17 projects
dotnet test                               # Run all tests (unit, integration, E2E)
dotnet test --filter "FullyQualifiedName~TestName"  # Run a specific test
dotnet run --project src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj  # Run MCP server
bash scripts/coverage-merge.sh            # Run tests + merge coverage + enforce 100% threshold
```

**Infrastructure (Docker):**
```bash
docker compose up -d                      # Start PostgreSQL (pgvector) + Ollama
```

**PowerShell scripts in `scripts/`:**
- `build.ps1` — Build orchestration
- `start-infrastructure.ps1` — Docker compose startup
- `launch-mcp-server.ps1` — MCP server launcher
- `verify-release-readiness.ps1` — Release verification

## Architecture

**Solution:** `csharp-compounding-docs.sln` — 10 source projects, 7 test projects. .NET 9.0 with nullable reference types, `TreatWarningsAsErrors=true`, and enforced code style.

**Source projects (`src/`):**

| Project | Purpose |
|---------|---------|
| `CompoundDocs.McpServer` | Main MCP server app (stdio transport). Contains the `RagQueryTool`, document processing pipeline, resilience policies (Polly), and observability. |
| `CompoundDocs.Common` | Shared models (`DocumentNode`, `ChunkNode`, `ConceptNode`, etc.), graph relationships (`HAS_SECTION`, `HAS_CHUNK`, `MENTIONS`, `RELATES_TO`, `LINKS_TO`), config loading, Markdown/YAML parsing, logging with correlation IDs. |
| `CompoundDocs.GraphRag` | Orchestrates the full RAG pipeline: embed → vector search → graph traversal → LLM synthesis. Key interface: `IGraphRagPipeline`. |
| `CompoundDocs.Vector` | `IVectorStore` abstraction over AWS OpenSearch Serverless for KNN search. |
| `CompoundDocs.Graph` | `IGraphRepository` abstraction over Amazon Neptune (Gremlin). |
| `CompoundDocs.Bedrock` | `IBedrockEmbeddingService` (Titan Embed v2, 1024-dim) and `IBedrockLlmService` (Claude models). |
| `CompoundDocs.GitSync` | Git repository monitoring via LibGit2Sharp; triggers document re-indexing on changes. |
| `CompoundDocs.Worker` | Background service for async document processing. |
| `CompoundDocs.AppHost` | .NET Aspire orchestration for local dev. |
| `CompoundDocs.ServiceDefaults` | Common DI extension methods. |

**Key patterns:** Dependency injection via `ServiceCollection` extensions, repository/service abstractions, pipeline pattern for RAG, `[McpServerToolType]`/`[McpServerTool]` attributes for MCP tool registration.

**Test projects (`tests/`):**
- `CompoundDocs.Tests.Unit` — xUnit + Moq + Shouldly
- `CompoundDocs.Tests.Integration` — Service integration tests
- `CompoundDocs.Tests.E2E` — End-to-end workflow and MCP protocol compliance tests
- `CompoundDocs.Tests.AppHost` — Aspire host tests

**Code coverage:** Uses `coverlet.collector` (not `coverlet.msbuild`) with configuration centralized in `coverlet.runsettings`. Per-project Cobertura reports are merged into a single report via ReportGenerator (local dotnet tool in `.config/dotnet-tools.json`). The merge script (`scripts/coverage-merge.sh`) enforces 100% line+branch thresholds.

Package versions are centrally managed in `Directory.Packages.props`. Global build settings are in `Directory.Build.props`.

## Critical Testing Rules

- **Mocking: Use Moq only.** Do NOT use NSubstitute. All mocks must use `Moq` (`Mock<T>`, `It.IsAny<T>()`, `.Setup()`, `.Returns()`, `.Verifiable()`, etc.).
- **Assertions: Use Shouldly only.** Do NOT use FluentAssertions. Use Shouldly's `ShouldBe()`, `ShouldNotBeNull()`, `ShouldContain()`, `ShouldThrow()`, etc.
- These rules apply to all test projects — unit, integration, and E2E.

## CI/CD

GitHub Actions workflows in `.github/workflows/`:
- `ci.yml` — Build + test on Ubuntu (with coverage via coverlet.collector), Windows, macOS
- `docker.yml` — Docker image builds
- `release.yml` — Semantic versioning via `.releaserc.json` (conventional commits); uploads `coverage-report.tar.gz` (merged Cobertura XML + HTML) as a release asset
- `docs.yml` — Nextra documentation site
- `validate-plugin.yml` — Plugin validation

## npm vs pnpm

This repository uses pnpm for everything. So do not use `npm` commands.