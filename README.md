# CompoundDocs MCP Server

A [Model Context Protocol](https://modelcontextprotocol.io/)  (MCP) server that implements a GraphRAG knowledge base for C#/.NET documentation. It combines graph traversal (Amazon Neptune), vector search (AWS OpenSearch Serverless), and LLM synthesis (Amazon Bedrock) to provide semantic Q&A over documentation with source attribution.

## How It Works

The server exposes a single MCP tool, `rag_query`, that orchestrates a multi-stage retrieval pipeline:

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
├── CompoundDocs.McpServer     # MCP server app (stdio transport), RagQueryTool, processing pipeline, resilience
├── CompoundDocs.Common        # Shared models, graph relationships, config loading, logging
├── CompoundDocs.GraphRag      # RAG pipeline orchestration (IGraphRagPipeline)
├── CompoundDocs.Vector        # IVectorStore — AWS OpenSearch Serverless KNN search
├── CompoundDocs.Graph         # IGraphRepository — Amazon Neptune (Gremlin)
├── CompoundDocs.Bedrock       # IBedrockEmbeddingService + IBedrockLlmService
├── CompoundDocs.GitSync       # Git repository monitoring via LibGit2Sharp
└── CompoundDocs.Worker        # Background document processing service

tests/
├── CompoundDocs.Tests.Unit         # xUnit + Moq + Shouldly
├── CompoundDocs.Tests.Integration  # Service integration tests
└── CompoundDocs.Tests              # Additional unit tests
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

Configuration is loaded from `appsettings.json`, environment variables, and command-line args.

### Key Sections

| Section | Description |
|---------|-------------|
| `McpServer` | Server name, description, port (default: 3000) |
| `Authentication` | API key auth — keys (comma-separated), header name, enabled flag |
| `EmbeddingCache` | In-memory cache — max items (10,000), TTL (24h), optional disk persistence |
| `Resilience` | Polly policies — retry, circuit breaker, timeout settings |
| `Chunking` | Document chunking — chunk size (1000), overlap (200), paragraph boundaries |

### Environment Variables

| Variable | Description |
|----------|-------------|
| `ASPNETCORE_ENVIRONMENT` | Runtime environment |
| `COMPOUNDDOCS_CONNECTION_STRING` | Database connection string |
| `COMPOUNDDOCS_OLLAMA_URL` | Ollama endpoint for local embedding |
| `COMPOUNDDOCS_EMBEDDING_MODEL` | Embedding model name |
| `COMPOUNDDOCS_CHAT_MODEL` | Chat/LLM model name |
| `COMPOUNDDOCS_LOG_LEVEL` | Log level (default: Information) |
| `COMPOUNDDOCS_TRANSPORT` | Transport mode |
| `COMPOUNDDOCS_PORT` | Server port |

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

# Run
docker run -p 3000:3000 compound-docs-mcp
```

The image uses a multi-stage Alpine build with a non-root user. Pre-built images are published to GitHub Container Registry on release.

## Infrastructure

### AWS Services

| Service | Purpose |
|---------|---------|
| Amazon Neptune | Graph database for concept relationships (Gremlin) |
| AWS OpenSearch Serverless | Vector store for KNN semantic search |
| Amazon Bedrock | Embedding generation (Titan Embed v2) and LLM synthesis (Claude) |

### Infrastructure as Code

- **OpenTofu** (`opentofu/`) — Modular AWS infrastructure provisioning (VPC, EKS, platform)
- **Helm** (`charts/compound-docs/`) — Kubernetes deployment with Crossplane, External Secrets, and IRSA support

## CI/CD

GitHub Actions workflows:

| Workflow | Trigger | Description |
|----------|---------|-------------|
| `ci.yml` | Push / PR | Build and test on Ubuntu, Windows, macOS with coverage |
| `docker.yml` | Release / push to main | Multi-arch Docker image build and push to GHCR |
| `release.yml` | Push to main | Semantic versioning via conventional commits |
| `docs.yml` | Push | Nextra documentation site deployment |

Releases follow [Conventional Commits](https://www.conventionalcommits.org/) with automatic changelog generation and GitHub releases.

## Architecture

The project uses a layered architecture with clear abstractions:

- **Dependency injection** via `IServiceCollection` extension methods
- **Repository pattern** for data access (`IVectorStore`, `IGraphRepository`)
- **Pipeline pattern** for RAG orchestration (`IGraphRagPipeline`)
- **Resilience** via Polly (retry with exponential backoff, circuit breaker, timeout)
- **Caching** for embeddings (configurable max items and TTL)
- **Structured logging** with Serilog and correlation IDs
- **Structured logging** and observability

### Graph Model

Documents are represented as a property graph with typed relationships:

```
DocumentNode ──HAS_SECTION──▶ SectionNode
     │                            │
     └──────HAS_CHUNK──────▶ ChunkNode ──MENTIONS──▶ ConceptNode
                                                          │
                                                    RELATES_TO
                                                          │
                                                    ConceptNode
```

## Scripts

PowerShell scripts in `scripts/`:

| Script | Purpose |
|--------|---------|
| `build.ps1` | Build orchestration with coverage reporting |
| `launch-mcp-server.ps1` | MCP server launcher with transport mode options |
| `start-infrastructure.ps1` | Docker infrastructure management |
| `verify-release-readiness.ps1` | Pre-release verification |

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

