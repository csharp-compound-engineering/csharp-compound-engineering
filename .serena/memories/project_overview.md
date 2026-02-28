# Project Overview

## Purpose
A Claude Code MCP plugin (`cd`) implementing a GraphRAG knowledge base for C#/.NET projects. Combines graph traversal (Amazon Neptune), vector search (AWS OpenSearch Serverless), and LLM synthesis (Amazon Bedrock) for semantic Q&A over documentation with source attribution.

## Tech Stack
- **.NET 10.0 LTS** (SDK 10.0.101, `rollForward: disable`)
- **C# latest** with nullable reference types, `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`
- **MCP SDK**: `ModelContextProtocol` v1.0.0 GA (HTTP transport, not stdio; stateless mode for Lambda)
- **AWS**: Neptune provisioned `db.t4g.medium` (openCypher via `AWSSDK.Neptunedata`), OpenSearch Serverless, Bedrock (Titan Embed v2, Claude), Lambda (`Amazon.Lambda.AspNetCoreServer.Hosting`)
- **Polly** for resilience patterns
- **LibGit2Sharp** for Git operations
- **Markdig** for Markdown parsing, **YamlDotNet** for YAML, **NJsonSchema** for validation
- **pnpm** for JS tooling (semantic-release, docs site). **Not npm.**
- Central package management via `Directory.Packages.props`
- Global build settings in `Directory.Build.props`

## Solution Structure
Solution file: `csharp-compounding-docs.sln` — 9 source projects, 3 test projects (+TestFixtures).

### Source Projects (`src/`)
| Project | Purpose |
|---------|---------|
| `CompoundDocs.McpServer` | Main MCP server app (HTTP, port 8080). Dual-mode: K8s (Kestrel) or Lambda (stateless). RagQueryTool, document processing, resilience (Polly), health checks (K8s only), auth, observability. |
| `CompoundDocs.Common` | Shared models (DocumentNode, ChunkNode, ConceptNode), graph relationships, config loading, Markdown/YAML parsing, logging with correlation IDs. |
| `CompoundDocs.GraphRag` | Full RAG pipeline orchestration: embed → vector search → graph traversal → LLM synthesis. Entity extraction, cross-repo entity resolution, document ingestion. |
| `CompoundDocs.Vector` | `IVectorStore` over AWS OpenSearch Serverless for KNN search. |
| `CompoundDocs.Graph` | `IGraphRepository` over Amazon Neptune (openCypher). `NeptuneClient` wraps AWS SDK. |
| `CompoundDocs.Bedrock` | `IBedrockEmbeddingService` (Titan Embed v2, 1024-dim), `IBedrockLlmService` (Claude). Model tier abstraction. |
| `CompoundDocs.GitSync` | Git repository monitoring library via LibGit2Sharp; clone/update, change detection, file reading. |
| `CompoundDocs.GitSync.Job` | Standalone run-once console app for K8s CronJob / Fargate. Iterates configured repos and runs `GitSyncRunner`. |
| `CompoundDocs.Worker` | CLI tool for async document processing. Takes repo name as arg. |

### Test Projects (`tests/`)
| Project | Purpose |
|---------|---------|
| `CompoundDocs.Tests.Unit` | xUnit + Moq + Shouldly. Extensive coverage across all source projects. |
| `CompoundDocs.Tests.Integration` | Service integration tests. AWS tests skipped, mock equivalents runnable. |
| `CompoundDocs.Tests.E2E` | MCP protocol compliance. `McpE2ETests` (mocked), `McpE2EAwsTests` (skipped, real AWS). |
| `TestFixtures` | Shared test fixture data. |

## Key Patterns
- **Dependency injection** via per-project `ServiceCollectionExtensions` classes
- **Repository/service abstractions** with interfaces (e.g., `IGraphRepository`, `IVectorStore`, `IBedrockEmbeddingService`)
- **Pipeline pattern** for RAG (`IGraphRagPipeline`)
- **MCP tool registration** via `[McpServerToolType]`/`[McpServerTool]` attributes
- **HTTP transport** for MCP: `.WithHttpTransport()`, `MapMcp()` on ASP.NET Core. Stateless mode for Lambda via `HttpServerTransportOptions.Stateless`.
- **Lambda dual-mode**: `AddAWSLambdaHosting(LambdaEventSource.HttpApi)` conditional on `AWS_LAMBDA_FUNCTION_NAME` env var. Auth via existing `ApiKeyAuthenticationHandler` (Function URL with `auth_type=NONE`).
- **openCypher** for Neptune queries (not Gremlin), via `AWSSDK.Neptunedata`
- **GitSync as CronJob**: `CompoundDocs.GitSync.Job` runs as K8s CronJob with EFS PVC for shared git repo storage
- **Pre-commit hooks**: gitleaks for secret scanning

## CI/CD
- Single GitHub Actions workflow: `.github/workflows/ci.yml` ("Release")
- PR: semantic-release dry-run; Push to main/master: full semantic-release
- Docker (GHCR), Helm chart (GHCR), docs (gh-pages), changelog, GitHub release
- Conventional Commits enforced by CI (`pr-title.yml`)

## Versioning
Current version: **3.0.0** (managed by semantic-release, set in `Directory.Build.props`) — 3.0.0 due to BREAKING CHANGE in Neptune config + GitSync separation
