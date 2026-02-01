# Architecture Documentation

This document provides a comprehensive overview of the CSharp Compound Docs MCP plugin architecture, including system design, component interactions, and key design decisions.

## Table of Contents

- [High-Level Overview](#high-level-overview)
- [System Architecture](#system-architecture)
- [Component Diagram](#component-diagram)
- [Data Flow](#data-flow)
- [Technology Stack](#technology-stack)
- [Design Decisions](#design-decisions)
- [Multi-Tenant Architecture](#multi-tenant-architecture)
- [Security Architecture](#security-architecture)

---

## High-Level Overview

CSharp Compound Docs is a Claude Code plugin that implements the "compound-engineering" paradigm for C#/.NET projects. It enables developers to capture and retrieve institutional knowledge through:

- **Disk-based storage** of markdown documentation
- **RAG and semantic search** via a bundled MCP server
- **PostgreSQL + pgvector** for vector storage
- **Ollama** for embeddings and RAG generation
- **Docker Compose** for shared infrastructure

### Key Principles

1. **File System as Source of Truth** - All documentation lives on disk; the database is a derived index
2. **Semantic Retrieval First** - RAG is the preferred retrieval method
3. **Shared Infrastructure** - Single Docker Compose stack serves all MCP server instances
4. **Multi-Tenant Isolation** - Documents isolated by project, branch, and path

---

## System Architecture

```
+------------------------------------------------------------------+
|                      Claude Code / Claude Desktop                  |
+------------------------------------------------------------------+
                                |
                          stdio transport
                                |
                                v
+------------------------------------------------------------------+
|                          MCP Server                               |
|                    (.NET 9.0 Generic Host)                        |
|                                                                    |
|  +--------------------+  +--------------------+  +---------------+ |
|  |   MCP Tools (9)    |  |  File Watcher     |  | Configuration | |
|  |                    |  |    Service        |  |    Provider   | |
|  | - activate_project |  |                    |  |               | |
|  | - rag_query        |  | - Watch ./csharp-  |  | - Global      | |
|  | - semantic_search  |  |   compounding-docs |  | - Project     | |
|  | - index_document   |  | - Debounce 500ms   |  | - Environment | |
|  | - list_doc_types   |  | - Sync to DB       |  +---------------+ |
|  | - search_external  |  +--------------------+                    |
|  | - rag_external     |                                            |
|  | - delete_documents |  +--------------------+  +---------------+ |
|  | - update_promotion |  |  Embedding Service |  | Document      | |
|  +--------------------+  |                    |  | Repository    | |
|                          | - mxbai-embed-large|  |               | |
|                          | - 1024 dimensions  |  | - CRUD ops    | |
|                          +--------------------+  | - Vector search|
|                                                  +---------------+ |
+------------------------------------------------------------------+
           |                              |
           | HTTP/REST                    | TCP/PostgreSQL
           |                              |
           v                              v
+---------------------+       +-------------------------+
|       Ollama        |       |      PostgreSQL         |
|                     |       |      + pgvector         |
| - mxbai-embed-large |       |                         |
| - mistral           |       | - documents table       |
|                     |       | - document_chunks table |
| Port: 11435         |       | - tenant isolation      |
+---------------------+       | - HNSW index            |
                              |                         |
                              | Port: 5433              |
                              +-------------------------+
           |                              |
           +-------------+----------------+
                         |
                   Docker Compose
             (~/.claude/.csharp-compounding-docs/)
```

---

## Component Diagram

```
                    +-------------------+
                    |   Claude Code     |
                    |   Plugin System   |
                    +--------+----------+
                             |
              +--------------+--------------+
              |                             |
              v                             v
    +-----------------+           +------------------+
    |     Skills      |           |     Agents       |
    |    (17 total)   |           |   (4 research)   |
    |                 |           |                  |
    | Capture:        |           | - best-practices |
    |  - problem      |           | - framework-docs |
    |  - insight      |           | - git-history    |
    |  - codebase     |           | - repo-analyst   |
    |  - tool         |           |                  |
    |  - style        |           +--------+---------+
    |                 |                    |
    | Query:          |                    |
    |  - query        |                    |
    |  - search       |                    |
    |  - search-ext   |                    |
    |  - query-ext    |                    |
    |                 |                    |
    | Meta:           |                    |
    |  - activate     |                    |
    |  - create-type  |                    |
    |  - capture-sel  |                    |
    |                 |                    |
    | Utility:        |                    |
    |  - delete       |                    |
    |  - promote      |                    |
    |  - todo         |                    |
    |  - worktree     |                    |
    |  - research ----+--------------------+
    +-----------------+
              |
              | MCP Protocol (stdio)
              v
    +------------------+
    |    MCP Server    |
    | (Generic Host)   |
    +--------+---------+
             |
    +--------+---------+--------+
    |        |         |        |
    v        v         v        v
+------+ +-------+ +-------+ +-------+
| Tool | | File  | | Embed | | Doc   |
| Hdlr | | Watch | | Svc   | | Repo  |
+------+ +-------+ +-------+ +-------+
    |        |         |        |
    |        v         |        |
    |    [Disk FS]     |        |
    |    ./csharp-     |        |
    |    compounding-  |        |
    |    docs/         |        |
    |                  |        |
    +--------+---------+--------+
             |
    +--------+---------+
    |                  |
    v                  v
+--------+       +-----------+
| Ollama |       | PostgreSQL|
|        |       | + pgvector|
+--------+       +-----------+
```

---

## Data Flow

### Document Capture Flow

```
User Conversation
       |
       v (trigger phrase detected)
+------------------+
|  Capture Skill   |
| (/cdocs:problem) |
+--------+---------+
         |
         | 1. Gather context
         | 2. Generate markdown
         v
+------------------+
|  Write to Disk   |
| ./csharp-compounding-docs/
| problems/xyz.md  |
+--------+---------+
         |
         | (file system event)
         v
+------------------+
|  File Watcher    |
| (500ms debounce) |
+--------+---------+
         |
         | 3. Parse document
         | 4. Validate schema
         v
+------------------+
| Embedding Service|
| (Ollama)         |
+--------+---------+
         |
         | 5. Generate embedding
         |    (1024 dimensions)
         v
+------------------+
|Document Repository|
| (PostgreSQL)     |
+--------+---------+
         |
         | 6. Upsert document
         |    + embedding
         v
    [Indexed in DB]
```

### Document Retrieval Flow

```
User Query: "/cdocs:query How do we handle pool exhaustion?"
       |
       v
+------------------+
|   Query Skill    |
| (/cdocs:query)   |
+--------+---------+
         |
         | 1. Call MCP tool
         v
+------------------+
|  rag_query Tool  |
+--------+---------+
         |
         | 2. Generate query embedding
         v
+------------------+
| Embedding Service|
+--------+---------+
         |
         | 3. Vector similarity search
         v
+------------------+
|Document Repository|
+--------+---------+
         |
         | 4. Retrieve top-k docs
         | 5. Parse linked docs
         v
+------------------+
| Link Resolution  |
| (QuikGraph)      |
+--------+---------+
         |
         | 6. Build context
         v
+------------------+
|   Ollama LLM     |
| (mistral)        |
+--------+---------+
         |
         | 7. Synthesize answer
         v
+------------------+
|  RAG Response    |
| + Source metadata|
+------------------+
```

---

## Technology Stack

### Core Components

| Component | Technology | Version | Purpose |
|-----------|------------|---------|---------|
| MCP Server | .NET Generic Host | 9.0+ | Application hosting, DI, logging |
| MCP Protocol | ModelContextProtocol | 0.6.0-preview | Claude Code integration |
| AI Abstraction | Microsoft.SemanticKernel | 1.70.0+ | Embeddings, vector operations |
| Vector Database | PostgreSQL + pgvector | 16+ / 0.5+ | Document storage, similarity search |
| Embeddings | Ollama | Latest | Local embedding generation |
| RAG Generation | Ollama | Latest | Answer synthesis |

### Supporting Libraries

| Library | Purpose |
|---------|---------|
| QuikGraph | In-memory graph for link resolution |
| Markdig | Markdown parsing, YAML frontmatter |
| JsonSchema.Net | Schema validation |
| Npgsql | PostgreSQL driver |
| Polly | Resilience (retry, circuit breaker) |

### Infrastructure

| Component | Technology | Port |
|-----------|------------|------|
| Container Orchestration | Docker Compose | - |
| Database | PostgreSQL (pgvector image) | 5433 |
| LLM Runtime | Ollama | 11435 |
| Scripting | PowerShell 7+ | - |

---

## Design Decisions

### Decision 1: File System as Source of Truth

**Choice**: Store documents on disk, use database as derived index

**Rationale**:
- Git operations (checkout, pull, merge) naturally trigger re-sync
- Documents are human-readable and editable
- No vendor lock-in to database format
- Enables offline access to documentation

**Trade-offs**:
- Requires synchronization between disk and database
- File watcher complexity

### Decision 2: Fixed Embedding Model

**Choice**: `mxbai-embed-large` (1024 dimensions), not configurable

**Rationale**:
- Changing embedding model invalidates all stored vectors
- Consistent dimensions simplify database schema
- High-quality open-source model

**Trade-offs**:
- Cannot use project-specific embedding models
- Requires re-indexing if model ever changes

### Decision 3: Distributed Capture Pattern

**Choice**: Each doc-type skill has its own auto-invoke triggers

**Rationale**:
- Built-in and custom doc-types behave identically
- No classification logic layer needed
- Direct trigger-to-skill mapping
- Easy to extend with new doc-types

**Trade-offs**:
- Multiple skills may trigger simultaneously (handled by capture-select meta-skill)

### Decision 4: Single PostgreSQL Schema with Tenant Isolation

**Choice**: Compound key (project_name, branch_name, path_hash) for isolation

**Rationale**:
- Simpler than schema-per-tenant
- Supports git worktrees with concurrent sessions
- Single database for backup/maintenance

**Trade-offs**:
- Query complexity with tenant filters
- Potential for data leakage if filters misconfigured

### Decision 5: Stdio Transport

**Choice**: MCP server uses stdio transport (not HTTP)

**Rationale**:
- One server instance per Claude Code session
- Simpler security model (no network exposure)
- Automatic lifecycle management

**Trade-offs**:
- Cannot share server across multiple clients
- Server restarts on each Claude Code session

---

## Multi-Tenant Architecture

### Tenant Isolation Model

```
+--------------------------------------------------+
|                    PostgreSQL                     |
|                                                   |
|  +----------------------------------------------+ |
|  |               documents table                | |
|  |----------------------------------------------| |
|  | project_name | branch_name | path_hash | ... | |
|  |--------------|-------------|-----------|-----| |
|  | my-app       | main        | a1b2c3d4  | ... | |
|  | my-app       | feature/x   | a1b2c3d4  | ... | |
|  | my-app       | main        | e5f6g7h8  | ... | <- worktree
|  | other-app    | main        | i9j0k1l2  | ... | |
|  +----------------------------------------------+ |
+--------------------------------------------------+
```

### Tenant Key Components

| Component | Source | Purpose |
|-----------|--------|---------|
| `project_name` | config.json | Identifies the project |
| `branch_name` | Git HEAD | Isolates branches |
| `path_hash` | SHA256(repo_root) | Distinguishes worktrees |

### Worktree Support

Multiple Claude Code sessions can work on the same project with different branches:

```
~/projects/my-app (main branch)         <- path_hash: a1b2c3d4
~/projects/my-app-feature (feature/x)   <- path_hash: e5f6g7h8

Both have project_name: "my-app" but different path_hash values
```

---

## Security Architecture

### Network Security

```
+-------------------+
|   Claude Code     |
|   (trusted)       |
+--------+----------+
         |
         | stdio (local only)
         v
+-------------------+
|    MCP Server     |
|   (localhost)     |
+--------+----------+
         |
         | localhost only (127.0.0.1)
         v
+-------------------+     +-------------------+
|    PostgreSQL     |     |      Ollama       |
| 127.0.0.1:5433    |     | 127.0.0.1:11435   |
+-------------------+     +-------------------+
```

### Security Boundaries

| Boundary | Protection |
|----------|------------|
| MCP Transport | stdio (no network exposure) |
| Database | localhost binding, authentication |
| Ollama | localhost binding |
| Docker | User namespace isolation |

### Data Security

| Data Type | Protection |
|-----------|------------|
| Documents | File system permissions |
| Embeddings | Database authentication |
| Configuration | User home directory permissions |
| Credentials | Environment variables (not stored in files) |

---

## Scalability Considerations

### Current Limits

| Metric | Limit | Notes |
|--------|-------|-------|
| Documents per project | ~10,000 | HNSW index performance |
| Concurrent sessions | 1 per MCP server | stdio limitation |
| Embedding batch size | 1 | Sequential processing |

### Future Scalability Options

1. **Connection pooling** for PostgreSQL
2. **Embedding batching** for bulk indexing
3. **Async file processing** for large document sets
4. **Index partitioning** for very large projects

---

## Deployment Architecture

### Development

```
Developer Machine
    |
    +-- Docker Desktop
    |       |
    |       +-- PostgreSQL container
    |       +-- Ollama container
    |
    +-- Claude Code
    |       |
    |       +-- MCP Server (dotnet run)
    |
    +-- Project Directory
            |
            +-- .csharp-compounding-docs/
            +-- csharp-compounding-docs/
```

### Production (Self-Hosted)

```
User Machine
    |
    +-- Docker Desktop
    |       |
    |       +-- PostgreSQL container (persistent volume)
    |       +-- Ollama container (model cache)
    |
    +-- Claude Code
    |       |
    |       +-- MCP Server (published binary)
    |
    +-- ~/.claude/.csharp-compounding-docs/
            |
            +-- docker-compose.yml
            +-- data/pgdata/
            +-- ollama/models/
```

---

## Monitoring and Observability

### Logging

| Component | Destination | Format |
|-----------|-------------|--------|
| MCP Server | stderr | Structured JSON |
| PostgreSQL | Docker logs | Standard PostgreSQL |
| Ollama | Docker logs | Standard Ollama |

### Metrics (Future)

| Metric | Type | Purpose |
|--------|------|---------|
| Document count | Gauge | Index size |
| Query latency | Histogram | Performance |
| Embedding time | Histogram | Ollama performance |
| Error rate | Counter | Reliability |

### Health Checks

| Check | Method | Frequency |
|-------|--------|-----------|
| PostgreSQL | pg_isready | Every 10s |
| Ollama | /api/tags | On demand |
| File watcher | Internal heartbeat | Every 30s |
