# MCP Server Structure Summary

This file contains summaries for the MCP server specification and its children.

---

## spec/mcp-server.md

### What This File Covers

The MCP Server specification defines a .NET Generic Host application that serves as the core backend for the compound docs plugin. Key areas covered:

- **Transport**: stdio-based MCP protocol using the `ModelContextProtocol` NuGet package on .NET 10.0+
- **9 MCP Tools**: RAG query, semantic search, document indexing, doc-type listing, external docs search/query, document deletion, promotion level updates, and project activation
- **File Synchronization**: File watcher service that maintains sync between disk files and the vector database with 500ms debounce
- **Document Chunking**: Large documents (>500 lines) are chunked by markdown headers for better search (database-only, source files unchanged)
- **Ollama Integration**: Embedding generation (`mxbai-embed-large`, 1024 dimensions) and RAG synthesis (`mistral` default)
- **Semantic Kernel Integration**: Uses SK for embeddings and PostgreSQL vector storage, with custom interfaces for testability
- **Link Resolution**: In-memory graph (QuikGraph) for document link tracking and circular reference detection
- **Concurrency**: Last-write-wins approach with file system as source of truth
- **Error Handling**: Standardized error response format with circuit breaker resilience

### Structural Relationships

- **Parent**: [SPEC.md](../SPEC.md) - The root specification document
- **Children** (6 sub-specifications):
  - `mcp-server/tools.md` - All 9 MCP tools with parameters and behaviors
  - `mcp-server/file-watcher.md` - File watching, sync, and reconciliation
  - `mcp-server/chunking.md` - Document chunking strategy
  - `mcp-server/ollama-integration.md` - Embedding service and circuit breaker
  - `mcp-server/database-schema.md` - PostgreSQL schema and multi-tenant architecture
  - `mcp-server/liquibase-changelog.md` - Database migration changesets
- **Siblings** (other top-level specs under spec/):
  - `doc-types.md` - Document type architecture
  - `skills.md` - Skills and commands
  - `infrastructure.md` - Docker infrastructure
  - `configuration.md` - Plugin configuration
  - `testing.md` - Testing approach
  - `agents.md` - Research agents
  - `marketplace.md` - Plugin marketplace
  - `observability.md` - Observability concerns
  - `research-index.md` - Research document index

---

## spec/mcp-server/tools.md

### What This File Covers

The `spec/mcp-server/tools.md` file specifies all 9 MCP tools provided by the server:

1. **rag_query** - RAG-based Q&A using compounding docs with source attribution
2. **semantic_search** - Vector similarity search returning ranked documents
3. **index_document** - Manual document indexing trigger
4. **list_doc_types** - Enumerate available doc-types and their schemas
5. **search_external_docs** - Search external project documentation (read-only)
6. **rag_query_external** - RAG queries against external docs
7. **delete_documents** - Delete documents by tenant context (with dry-run support)
8. **update_promotion_level** - Update document visibility (standard/important/critical)
9. **activate_project** - Activate a project for the session

Each tool includes parameter definitions, response schemas, behavioral descriptions, and error handling. The file also documents:
- Standard error response format and error codes
- Intentional exclusion of `list_all_documents` (token consumption concern)
- Chunk promotion behavior (chunks inherit parent document promotion level)

### Structural Relationships

- **Parent**: `spec/mcp-server.md` - The main MCP server specification that provides overview and references this file
- **Siblings** (other mcp-server sub-specs):
  - `file-watcher.md` - File watching and sync behavior
  - `chunking.md` - Document chunking strategy
  - `ollama-integration.md` - Embedding and RAG generation
  - `database-schema.md` - PostgreSQL schema details
  - `liquibase-changelog.md` - Database migration changesets
- **Cross-references**:
  - `spec/skills.md` - Referenced for excluded skills rationale
  - Multiple research documents for implementation patterns

---

## spec/mcp-server/database-schema.md

### What This File Covers

This specification defines the PostgreSQL database schema for the MCP server's multi-tenant document storage system with pgvector support. Key topics include:

- **Multi-tenant architecture**: Single schema with tenant isolation via compound keys (project_name, branch_name, path_hash) supporting multiple projects, git worktrees, and concurrent branches
- **Schema management split**: Liquibase manages `tenant_management` schema (relational tables); Semantic Kernel manages `compounding` schema (vector collections via `EnsureCollectionExistsAsync()`)
- **Four Semantic Kernel models**:
  - `CompoundDocument`: Main document storage with metadata and 1024-dimension embeddings
  - `DocumentChunk`: Chunks for large documents (>500 lines)
  - `ExternalDocument`: Read-only reference material in separate collection
  - `ExternalDocumentChunk`: Chunks for large external documents
- **HNSW index configuration**: Medium tier (m=32, ef_construction=128, ef_search=64) for 1K-10K documents per tenant
- **Startup validation**: Verifies embedding dimensions match mxbai-embed-large (1024) at startup
- **Query filtering**: Tenant isolation via VectorSearchFilter on filterable columns

### Structural Relationships

| Relationship | File |
|--------------|------|
| **Parent** | [mcp-server.md](../spec/mcp-server.md) |
| **Siblings** | [liquibase-changelog.md](../spec/mcp-server/liquibase-changelog.md), [ollama-integration.md](../spec/mcp-server/ollama-integration.md), [chunking.md](../spec/mcp-server/chunking.md), [file-watcher.md](../spec/mcp-server/file-watcher.md), [tools.md](../spec/mcp-server/tools.md) |
| **Children** | None |
| **References** | Research docs: postgresql-pgvector-research.md, liquibase-semantic-kernel-schema-research.md, liquibase-pgvector-docker-init.md, semantic-kernel-pgvector-package-update.md |

---

## spec/mcp-server/chunking.md

### What This File Covers

The `chunking.md` specification defines how large documents (>500 lines) are split into smaller chunks for more effective semantic search. Key aspects include:

- **Strategy**: Documents are chunked at H2 (`##`) and H3 (`###`) markdown headers using the Markdig parser
- **Database-only**: Chunking is stored in the database; source markdown files are never modified
- **Chunk metadata**: Each chunk stores `chunk_id`, `document_id`, `header_path`, line ranges, content, and vector embeddings (1024-dim via mxbai-embed-large)
- **Search behavior**: Semantic search returns chunks with relevance scores; agents can load full documents, specific chunks, or all chunks
- **External docs**: External documents use the same chunking strategy but are stored in a separate `external_document_chunks` table
- **Promotion inheritance**: Chunks inherit their parent document's promotion level and are updated atomically when the parent changes
- **Lifecycle**: Chunks are created/updated when documents are indexed and deleted when parent documents are removed or fall below the 500-line threshold

### Structural Relationships

| Relationship | File |
|--------------|------|
| **Parent** | `spec/mcp-server.md` - MCP Server Specification |
| **Siblings** | `spec/mcp-server/tools.md` - MCP tools that interact with chunks |
| | `spec/mcp-server/file-watcher.md` - Triggers chunk regeneration on file changes |
| | `spec/mcp-server/ollama-integration.md` - Embedding generation for chunks |
| | `spec/mcp-server/database-schema.md` - Schema for `document_chunks` table |
| | `spec/mcp-server/liquibase-changelog.md` - Database migrations |

---

## spec/mcp-server/file-watcher.md

### What This File Covers

The `file-watcher.md` specification defines the File Watcher Service, which maintains synchronization between markdown documentation files on disk (`./csharp-compounding-docs/`) and a vector database used for RAG (Retrieval-Augmented Generation) operations.

**Key Topics:**
- **Technology**: Uses `System.IO.FileSystemWatcher` with recursive watching and 500ms debouncing
- **Lifecycle**: Starts on project activation via `activate_project` tool; stops when switching projects
- **Events**: Handles file create, modify, delete, and rename events with corresponding database operations
- **Startup Reconciliation**: Full disk-to-database comparison on project activation to detect drift
- **Crash Recovery**: Uses reconciliation approach (no persistent queue needed) since file system is source of truth
- **External Docs**: Same reconciliation logic applies to configurable external documentation paths
- **Concurrency**: Last-write-wins strategy without OS-level file locking
- **Error Handling**: Detailed handling for file system errors and processing errors with retry strategies

### Structural Relationships

**Parent:**
- [mcp-server.md](../spec/mcp-server.md) - The main MCP Server specification

**Siblings (other children of mcp-server.md):**
- [tools.md](../spec/mcp-server/tools.md) - MCP tools including `activate_project`
- [chunking.md](../spec/mcp-server/chunking.md) - Document chunking for large files
- [ollama-integration.md](../spec/mcp-server/ollama-integration.md) - Embedding generation service
- [database-schema.md](../spec/mcp-server/database-schema.md) - Database records managed by file watcher
- [liquibase-changelog.md](../spec/mcp-server/liquibase-changelog.md) - Database migrations

**Cross-References:**
- [configuration.md](../spec/configuration.md) - File watcher configuration options (debounce interval, etc.)

---

## spec/mcp-server/ollama-integration.md

### What This File Covers

This specification defines how the MCP server integrates with Ollama for embedding generation and RAG (Retrieval-Augmented Generation) synthesis. Key topics include:

- **Embedding Model**: Uses `mxbai-embed-large` (1024 dimensions) with auto-pull capability
- **RAG Generation Model**: Configurable via JSON config, defaults to `mistral`
- **Connection Configuration**: Receives Ollama host:port via command-line argument
- **HttpClient Setup**: Configured with 5-minute timeout for embedding operations
- **Resilience Patterns**: Implements rate limiting, exponential backoff retry (3 attempts), and circuit breaker (50% failure ratio triggers 30s break)
- **Graceful Degradation**: Error responses for RAG queries and semantic search when Ollama unavailable; file watcher queues documents for later indexing
- **Apple Silicon Handling**: Detects macOS ARM64 and expects native Ollama for Metal acceleration
- **Semantic Kernel Integration**: Uses `ITextEmbeddingGenerationService` with a testable wrapper interface
- **Rate Limiting Alignment**: HttpClient concurrency matches `OLLAMA_NUM_PARALLEL` setting with hardware-specific recommendations

### Structural Relationships

- **Parent**: `spec/mcp-server.md`
- **Siblings**:
  - `spec/mcp-server/chunking.md`
  - `spec/mcp-server/database-schema.md`
  - `spec/mcp-server/file-watcher.md`
  - `spec/mcp-server/liquibase-changelog.md`
  - `spec/mcp-server/tools.md`
- **Children**: None
- **Cross-references**:
  - `spec/infrastructure.md` (Apple Silicon deployment)
  - `research/semantic-kernel-ollama-rag-research.md`
  - `research/ollama-multi-model-research.md`
  - `research/ollama-docker-gpu-research.md`

---

## spec/mcp-server/liquibase-changelog.md

### What This File Covers

This specification defines the Liquibase-based database migration system for the `tenant_management` schema. Key areas include:

- **Directory structure**: XML-based master changelog with individual change directories containing `change.xml`, `change.sql`, and `rollback.sql` files
- **Changeset format**: Template and conventions for defining migrations using `sqlFile` references
- **Initial migrations**: Four changesets that create the schema, `repo_paths` table, `branches` table, and indexes
- **Migration workflow**: Docker init script execution and manual Liquibase commands for applying/rolling back changes
- **Best practices**: Guidelines for changeset design, SQL conventions, file organization, and PostgreSQL-specific considerations
- **pgvector handling**: Extension enabled via Docker init script (`init-db.sh`) before Liquibase runs, not managed by Liquibase itself

### Structural Relationships

| Relationship | File |
|--------------|------|
| **Parent** | `spec/mcp-server/database-schema.md` |
| **Grandparent** | `spec/mcp-server.md` |
| **Siblings** | `chunking.md`, `file-watcher.md`, `ollama-integration.md`, `tools.md` (all in `spec/mcp-server/`) |
| **Research** | `research/liquibase-changelog-format-research.md`, `research/liquibase-pgvector-docker-init.md` |
| **Related** | `spec/infrastructure.md` (Docker/init script details) |

This spec is a leaf node focusing on implementation details of the migration system defined in the parent database schema spec.
