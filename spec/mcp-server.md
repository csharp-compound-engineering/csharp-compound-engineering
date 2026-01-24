# MCP Server Specification

> **Status**: [DRAFT]
> **Parent**: [SPEC.md](../SPEC.md)

---

## Overview

The MCP server is a .NET Generic Host application that:
- Runs as a stdio server (launched per Claude Code instance)
- Provides RAG and semantic search tools
- Maintains vector database sync via file watching
- Uses Microsoft.SemanticKernel and/or Microsoft.Extensions.AI

> **Background**: Comprehensive SDK documentation covering server setup, tool registration, transport configuration, and Semantic Kernel integration patterns. See [MCP C# SDK Research](../research/mcp-csharp-sdk-research.md).

> **Background**: .NET Generic Host patterns including dependency injection, configuration, logging to stderr for stdio transport, and hosted services. See [.NET Generic Host MCP Research](../research/dotnet-generic-host-mcp-research.md).

---

## Transport

- **Type**: stdio
- **Protocol**: MCP (Model Context Protocol)
- **SDK**: `ModelContextProtocol` NuGet package (prerelease)
- **Framework**: .NET 10.0+

---

## Sub-Specifications

This specification is organized into the following sub-files:

| Document | Description |
|----------|-------------|
| [tools.md](./mcp-server/tools.md) | All 9 MCP tools with parameters, responses, and behaviors |
| [file-watcher.md](./mcp-server/file-watcher.md) | File watching, sync behavior, reconciliation, events |
| [chunking.md](./mcp-server/chunking.md) | Document chunking strategy, metadata, thresholds |
| [ollama-integration.md](./mcp-server/ollama-integration.md) | Embedding service, HttpClient config, circuit breaker |
| [database-schema.md](./mcp-server/database-schema.md) | PostgreSQL schema, multi-tenant architecture |
| [liquibase-changelog.md](./mcp-server/liquibase-changelog.md) | Database migration changesets |

> **Background**: Liquibase XML changelog format, include directives, sqlFile elements, rollback strategies, and PostgreSQL-specific considerations. See [Liquibase Changelog Format Research](../research/liquibase-changelog-format-research.md).

---

## Core Responsibilities

### Tool Endpoints

The server exposes 9 MCP tools. See [tools.md](./mcp-server/tools.md) for complete specifications.

| # | Tool | Purpose |
|---|------|---------|
| 1 | `rag_query` | Answer questions using RAG with compounding docs |
| 2 | `semantic_search` | Search compounding docs by semantic similarity |
| 3 | `index_document` | Manually index/re-index a document |
| 4 | `list_doc_types` | List available doc-types and their schemas |
| 5 | `search_external_docs` | Search external project documentation |
| 6 | `rag_query_external` | RAG query against external docs |
| 7 | `delete_documents` | Delete documents by tenant context |
| 8 | `update_promotion_level` | Update document visibility level |
| 9 | `activate_project` | Activate a project for the session |

### File Synchronization

The server maintains synchronization between disk and database. See [file-watcher.md](./mcp-server/file-watcher.md) for details.

- Watch `./csharp-compounding-docs/` recursively
- Debounce rapid changes (default 500ms)
- Handle create, modify, delete, rename events
- Reconcile on activation (startup)

> **Background**: FileSystemWatcher implementation patterns, debouncing strategies, background service integration, and embedding operation mapping. See [.NET FileSystemWatcher Embeddings Research](../research/dotnet-file-watcher-embeddings-research.md).

### Document Chunking

Large documents (>500 lines) are chunked for better search. See [chunking.md](./mcp-server/chunking.md) for details.

- Split at `##` and `###` markdown headers
- Database-only (source files unchanged)
- Chunks inherit parent promotion level

> **Background**: Markdig library for AST traversal, header extraction, YAML frontmatter parsing, and precise source location tracking. See [.NET Markdown Parser Research](../research/dotnet-markdown-parser-research.md).

### Ollama Integration

Embedding generation and RAG synthesis. See [ollama-integration.md](./mcp-server/ollama-integration.md) for details.

- Fixed embedding model: `mxbai-embed-large` (1024 dimensions)
- Configurable RAG model (default: `mistral`)
- Circuit breaker for resilience

> **Background**: Semantic Kernel Ollama connector, embedding generation, chat completion, and complete RAG pipeline implementation. See [Semantic Kernel Ollama RAG Research](../research/semantic-kernel-ollama-rag-research.md).

> **Background**: Running multiple models simultaneously, OLLAMA_KEEP_ALIVE settings, GPU memory management, and Docker configuration. See [Ollama Multi-Model Research](../research/ollama-multi-model-research.md).

---

## Semantic Kernel Integration

The MCP server uses Semantic Kernel abstractions where applicable.

### Embedding Service

Uses Semantic Kernel's `ITextEmbeddingGenerationService`:

```csharp
#pragma warning disable SKEXP0070  // Ollama connector is experimental

using Microsoft.SemanticKernel.Embeddings;

// Registration
kernelBuilder.AddOllamaTextEmbeddingGeneration(
    modelId: "mxbai-embed-large",
    endpoint: new Uri("http://localhost:11434")
);

// Usage
var embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
ReadOnlyMemory<float> embedding = await embeddingService.GenerateEmbeddingAsync(content, ct);
```

### Document Repository

Uses Semantic Kernel's PostgreSQL connector:

> **Package**: `Microsoft.SemanticKernel.Connectors.PgVector` (the old `Connectors.Postgres` package is deprecated)
> **Namespace**: `Microsoft.SemanticKernel.Connectors.Postgres` (unchanged despite package rename)
> **Status**: Preview/Experimental (`1.70.0-preview` as of January 2026)

```csharp
using Microsoft.SemanticKernel.Connectors.Postgres;

public class CompoundDocumentCollection : PostgresCollection<string, CompoundDocument>
{
    public CompoundDocumentCollection(NpgsqlDataSource dataSource)
        : base(dataSource, "documents") { }
}
```

### Transaction Support Limitation

Semantic Kernel's PostgreSQL connector does NOT support database transactions. A hybrid approach is used:
- **Writes**: Use raw Npgsql for transactional operations (document + chunks)
- **Reads**: Use Semantic Kernel for vector search

Reference: [Semantic Kernel Transaction Research](../research/semantic-kernel-postgresql-transaction-support.md)

---

## Service Interfaces

Custom interfaces wrap SK services for testability.

### IEmbeddingService

```csharp
/// <summary>
/// Wraps ITextEmbeddingGenerationService for testability.
/// </summary>
public interface IEmbeddingService
{
    Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string content,
        CancellationToken cancellationToken = default);
}
```

### IDocumentRepository

```csharp
/// <summary>
/// Repository abstraction for document CRUD and search.
/// </summary>
public interface IDocumentRepository
{
    Task<CompoundDocument?> GetByIdAsync(string id, CancellationToken ct);
    Task<CompoundDocument?> GetByPathAsync(string relativePath, TenantContext tenant, CancellationToken ct);
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        ReadOnlyMemory<float> embedding,
        int limit,
        float minRelevance,
        TenantContext tenant,
        CancellationToken ct);
    Task UpsertAsync(CompoundDocument document, CancellationToken ct);
    Task DeleteAsync(string id, CancellationToken ct);
    Task<int> DeleteByTenantAsync(TenantContext tenant, CancellationToken ct);
}

/// <summary>
/// Tenant isolation context.
/// </summary>
public record TenantContext(string ProjectName, string BranchName, string PathHash);

/// <summary>
/// Search result with relevance score.
/// </summary>
public record SearchResult(CompoundDocument Document, float RelevanceScore);
```

Reference: [Microsoft Semantic Kernel Research](../research/microsoft-semantic-kernel-research.md)

---

## Testable Service Wrappers

Production implementations wrap SK services:

```csharp
/// <summary>
/// Production implementation using Semantic Kernel.
/// </summary>
public class SemanticKernelEmbeddingService : IEmbeddingService
{
    private readonly ITextEmbeddingGenerationService _skService;

    public SemanticKernelEmbeddingService(ITextEmbeddingGenerationService skService)
        => _skService = skService;

    public Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string content, CancellationToken ct)
        => _skService.GenerateEmbeddingAsync(content, kernel: null, ct);
}
```

---

## Error Handling

### Error Response Format

All tools return errors in a standard format. See [tools.md - Error Handling](./mcp-server/tools.md#error-handling) for complete error codes.

```json
{
  "error": true,
  "code": "ERROR_CODE",
  "message": "Human-readable error description",
  "details": {}
}
```

### Resilience

Error recovery uses `Microsoft.Extensions.Http.Resilience` (built on Polly v8). See [ollama-integration.md - Resilience](./mcp-server/ollama-integration.md#resilience-and-circuit-breaker) for configuration details.

---

## Link Resolution

### In-Memory Graph

Uses QuikGraph (or similar) to track document links and detect circular references.

> **Background**: QuikGraph library evaluation, cycle detection algorithms, topological sort, and DAG enforcement patterns. See [.NET Graph Libraries Research](../research/dotnet-graph-libraries.md).

```csharp
// Build graph from document links
var linkGraph = new AdjacencyGraph<string, Edge<string>>();

// Add vertices (document paths)
foreach (var doc in documents)
    linkGraph.AddVertex(doc.RelativePath);

// Add edges (links between docs)
foreach (var link in parsedLinks)
    linkGraph.AddEdge(new Edge<string>(link.SourcePath, link.TargetPath));
```

### Link Depth Configuration

Configured per-project in `.csharp-compounding-docs/config.json`:

```json
{
  "link_resolution": {
    "max_depth": 2,
    "max_linked_docs": 5
  }
}
```

### Circular Reference Handling

- Track visited nodes during traversal
- Skip already-visited documents
- Log warning for circular references detected

---

## Concurrency Control

**Approach**: Last-write-wins (no file locking)

See [file-watcher.md - Concurrency Control](./mcp-server/file-watcher.md#concurrency-control) for details.

**Key Points**:
- No OS-level file locks
- File system is the source of truth
- File watcher reconciliation handles eventual consistency
- Chunks are always regenerated from current file content

---

## Resolved Decisions

| Decision | Resolution |
|----------|------------|
| Debounce interval | 500ms default (see [configuration.md](./configuration.md#file-watcher-settings)) |
| Large documents | Chunk by markdown headers (see [chunking.md](./mcp-server/chunking.md)) |
| Embedding dimensions | 1024 (mxbai-embed-large) |
| Default relevance threshold | 0.7 |
| Default max sources | 3 |

---

## Open Questions

1. Should the server expose a health check tool?
2. Should there be a "rebuild index" tool for full re-sync?
3. What's the optimal HNSW index configuration for expected data sizes?
