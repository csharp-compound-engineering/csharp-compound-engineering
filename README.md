# CompoundDocs — Claude Code Plugin

A [Claude Code](https://docs.anthropic.com/en/docs/claude-code) plugin (`cd`) that implements a GraphRAG knowledge base for C#/.NET documentation. It bundles an HTTP-based [MCP](https://modelcontextprotocol.io/) server backed by graph traversal (Amazon Neptune), vector search (AWS OpenSearch Serverless), and LLM synthesis (Amazon Bedrock) to provide semantic Q&A over documentation with source attribution.

## How It Works

The plugin exposes a single MCP tool, `rag_query`, that orchestrates a multi-stage retrieval pipeline:

```
Query → Embedding Generation → KNN Vector Search → Graph Traversal → LLM Synthesis → Answer + Sources
```

1. **Embed** the user query using Amazon Bedrock Titan Embed v2 (1024-dim vectors)
2. **Search** for relevant document chunks via KNN in OpenSearch Serverless
3. **Enrich** results by traversing concept relationships in Neptune
4. **Synthesize** a final answer with source citations using Claude on Bedrock

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- AWS credentials configured for Neptune, OpenSearch Serverless, and Bedrock access
- Docker (optional, for local infrastructure)

## Quick Start

```bash
# Build
dotnet build

# Run the MCP server
dotnet run --project src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj

# Run all tests
dotnet test
```

## Project Structure

```
src/
├── CompoundDocs.McpServer     # MCP server app (HTTP transport), RagQueryTool, processing pipeline, resilience
├── CompoundDocs.Common        # Shared models, graph relationships, config loading, logging
├── CompoundDocs.GraphRag      # RAG pipeline orchestration (IGraphRagPipeline)
├── CompoundDocs.Vector        # IVectorStore — AWS OpenSearch Serverless KNN search
├── CompoundDocs.Graph         # IGraphRepository — Amazon Neptune (openCypher)
├── CompoundDocs.Bedrock       # IBedrockEmbeddingService + IBedrockLlmService
├── CompoundDocs.GitSync       # Git repository monitoring via LibGit2Sharp
└── CompoundDocs.Worker        # Background document processing service

tests/
├── CompoundDocs.Tests.Unit         # xUnit + Moq + Shouldly
├── CompoundDocs.Tests.Integration  # Service integration tests
└── CompoundDocs.Tests.E2E          # End-to-end workflow and MCP protocol compliance tests
```

## MCP Tool

### `rag_query`

Performs RAG-based question answering with source attribution.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `query` | string | *(required)* | The question to answer |
| `maxResults` | int | 5 | Max source documents to use (1–20) |

**Response:**

```json
{
  "success": true,
  "data": {
    "query": "How does dependency injection work?",
    "answer": "...",
    "sources": [
      {
        "documentId": "...",
        "chunkId": "...",
        "filePath": "docs/dependency-injection.md",
        "relevanceScore": 0.95
      }
    ],
    "relatedConcepts": ["service lifetime", "constructor injection"],
    "confidenceScore": 0.87
  }
}
```

## Configuration

Configuration is loaded from multiple sources (highest priority wins):

1. **Code defaults** — `CompoundDocsServerOptions` provides sensible defaults
2. **Global config** — `~/.claude/.csharp-compounding-docs/global-config.json`
3. **Project config** — `.csharp-compounding-docs/config.json` (per-repository)
4. **Environment variables** — override any config file values

Config loading is handled by `ConfigurationLoader` in `CompoundDocs.Common`.

### Key Sections

| Section | Description |
|---------|-------------|
| `Authentication` | API key auth — keys (comma-separated), header name, enabled flag |
| `EmbeddingCache` | In-memory cache — max items (10,000), TTL (24h), optional disk persistence |
| `Resilience` | Polly policies — retry, circuit breaker, timeout settings |
| `Chunking` | Document chunking — chunk size (1000), overlap (200), paragraph boundaries |

### Environment Variables

#### Local Development Mode

These override the local `GlobalConfig` defaults (PostgreSQL + Ollama) used when running without AWS services:

| Variable | Description | Default |
|----------|-------------|---------|
| `COMPOUNDING_POSTGRES_HOST` | PostgreSQL host | `127.0.0.1` |
| `COMPOUNDING_POSTGRES_PORT` | PostgreSQL port | `5433` |
| `COMPOUNDING_POSTGRES_DATABASE` | PostgreSQL database name | `compounding_docs` |
| `COMPOUNDING_POSTGRES_USERNAME` | PostgreSQL username | `compounding` |
| `COMPOUNDING_POSTGRES_PASSWORD` | PostgreSQL password | `compounding` |
| `COMPOUNDING_OLLAMA_HOST` | Ollama endpoint host | `127.0.0.1` |
| `COMPOUNDING_OLLAMA_PORT` | Ollama endpoint port | `11435` |
| `COMPOUNDING_OLLAMA_MODEL` | Ollama generation model | `mistral` |

#### Cloud / Container Deployment

| Variable | Description |
|----------|-------------|
| `ASPNETCORE_ENVIRONMENT` | Runtime environment (`Production`, `Development`) |
| `COMPOUNDDOCS_LOG_LEVEL` | Log level (set in the Dockerfile) |
| `COMPOUNDDOCS_API_KEYS` | API keys (used in the Helm chart deployment template) |

## Authentication

The server uses API key authentication by default. Keys are validated from the `X-API-Key` header or `Authorization: Bearer` header.

```json
{
  "Authentication": {
    "Enabled": true,
    "HeaderName": "X-API-Key",
    "ApiKeys": "key1,key2,key3"
  }
}
```

Set `Enabled` to `false` to disable authentication for local development. The `/health` endpoint is always anonymous.

## Docker

```bash
# Build the image
docker build -t compound-docs-mcp .
```

The image uses a multi-stage Ubuntu Chiseled build with a non-root user (UID 1654). The MCP server listens on HTTP port 3000. Pre-built multi-arch images are published to GitHub Container Registry on release.

## Infrastructure

### AWS Services

| Service | Purpose |
|---------|---------|
| Amazon Neptune | Graph database for concept relationships (openCypher) |
| AWS OpenSearch Serverless | Vector store for KNN semantic search |
| Amazon Bedrock | Embedding generation (Titan Embed v2) and LLM synthesis (Claude) |

### Infrastructure as Code

- **OpenTofu** (`opentofu/`) — Reference IaC configuration for provisioning the full AWS infrastructure stack across 4 phases:
  - `00-prereqs` — IAM roles and policies (Crossplane provider, ESO, ExternalDNS) + Secrets Manager
  - `01-network` — VPC and subnets
  - `02-cluster` — EKS cluster + Pod Identity associations
  - `03-platform` — Helm releases and Crossplane DeploymentRuntimeConfig
- **Helm** (`charts/compound-docs/`) — Kubernetes deployment with Crossplane, External Secrets, and IRSA support

## CI/CD

A single unified GitHub Actions workflow (`ci.yml`, named "Release") handles everything:

- **PR** → semantic-release dry-run (validates commits, build, test, pack)
- **Push to main/master** → full semantic-release: Docker build+push (GHCR), Helm chart publish (GHCR), docs deploy (gh-pages), changelog, GitHub release

Release assets include the Helm chart (`.tgz`) and a coverage report (`coverage-report.tar.gz` with merged Cobertura XML + HTML). Config in `.releaserc.json`.

Releases follow [Conventional Commits](https://www.conventionalcommits.org/) with automatic changelog generation.

## Architecture

The project uses a layered architecture with clear abstractions:

- **Dependency injection** via `IServiceCollection` extension methods
- **Repository pattern** for data access (`IVectorStore`, `IGraphRepository`)
- **Pipeline pattern** for RAG orchestration (`IGraphRagPipeline`)
- **Resilience** via Polly (retry with exponential backoff, circuit breaker, timeout)
- **Caching** for embeddings (configurable max items and TTL)
- **Structured logging** with Serilog and correlation IDs

### Graph Model

Documents are represented as a property graph with typed relationships:

```
DocumentNode ──HAS_SECTION──▶ SectionNode
     │                            │
     └──────HAS_CHUNK──────▶ ChunkNode ──MENTIONS──▶ ConceptNode
                                  │                       │
                            HAS_EXAMPLE             RELATES_TO
                                  │                       │
                          CodeExampleNode            ConceptNode
```

## Scripts

Bash scripts in `scripts/`:

| Script | Purpose |
|--------|---------|
| `coverage-merge.sh` | Run tests, merge coverage with ReportGenerator, enforce 100% threshold |
| `release-prepare.sh` | Update version in `Directory.Build.props` and `Chart.yaml` |
| `release-docker.sh` | Build and push multi-arch Docker image to GHCR |
| `release-helm.sh` | Package and push Helm chart to GHCR |
| `release-docs.sh` | Build Nextra documentation site |

## Testing

```bash
# Run all tests
dotnet test

# Run a specific test
dotnet test --filter "FullyQualifiedName~TestName"
```

Tests use **xUnit** as the framework, **Moq** for mocking, and **Shouldly** for assertions. Integration tests that require AWS infrastructure will skip automatically when services are unavailable.

## License

MIT

