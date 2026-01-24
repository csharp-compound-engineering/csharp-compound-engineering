# MCP Server Tools

> **Status**: [DRAFT]
> **Parent**: [../mcp-server.md](../mcp-server.md)

> **Background**: Implementation patterns for MCP tools in C#, including attribute-based registration, dependency injection, and error handling. See [MCP C# SDK Research](../../research/mcp-csharp-sdk-research.md).

---

## Overview

This document details all 9 MCP tools provided by the server. For error response format and standard error codes, see the [Error Handling](#error-handling) section.

---

## Tool Summary

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

**Intentionally Excluded**: A `list_all_documents` tool is explicitly not provided. Listing all documents could consume excessive tokens for large document sets. Use `semantic_search` with specific queries instead. See also: [skills.md - Excluded Skills](../skills.md#excluded-skills).

---

## 1. RAG Query Tool

> **Background**: Complete RAG pipeline implementation with Ollama for embeddings and chat completion, including context building and response generation patterns. See [Semantic Kernel + Ollama RAG Research](../../research/semantic-kernel-ollama-rag-research.md).

**Tool Name**: `rag_query`

**Purpose**: Answer questions using retrieved context, returning synthesized response with source metadata.

**Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `query` | string | Yes | Natural language question |
| `doc_types` | array[string] | No | Filter to specific doc-types (default: all) |
| `max_sources` | integer | No | Maximum documents to use (default: 3) |
| `min_relevance_score` | float | No | Minimum relevance score (default: 0.7) |
| `min_promotion_level` | enum | No | Only return docs at or above this level: `standard`, `important`, `critical` (default: `standard`) |
| `include_critical` | boolean | No | Prepend critical docs to context regardless of relevance (default: true) |

**Response**:
```json
{
  "answer": "The database connection pool exhaustion was caused by...",
  "sources": [
    {
      "path": "./csharp-compounding-docs/problems/db-pool-exhaustion-20250115.md",
      "title": "Database Connection Pool Exhaustion",
      "char_count": 2847,
      "relevance_score": 0.92
    },
    {
      "path": "./csharp-compounding-docs/tools/npgsql-configuration-20250110.md",
      "title": "Npgsql Connection Configuration",
      "char_count": 1523,
      "relevance_score": 0.78
    }
  ],
  "linked_docs": [
    {
      "path": "./csharp-compounding-docs/codebase/connection-management-20250108.md",
      "title": "Connection Management Architecture",
      "char_count": 3102,
      "linked_from": "./csharp-compounding-docs/problems/db-pool-exhaustion-20250115.md"
    }
  ]
}
```

**Behavior**:
1. Generate embedding for query using Ollama
2. Perform vector similarity search in PostgreSQL
3. Retrieve top-k documents
4. Parse documents with Markdig, extract linked docs (see [.NET Markdown Parser Research](../../research/dotnet-markdown-parser-research.md))
5. Send context + query to Ollama for synthesis
6. Return synthesized answer with metadata

---

## 2. Semantic Search Tool

> **Background**: PostgreSQL pgvector operations for vector similarity search, including HNSW indexing, distance functions, and query optimization. See [PostgreSQL pgvector Research](../../research/postgresql-pgvector-research.md).

**Tool Name**: `semantic_search`

**Purpose**: Return ranked documents without RAG synthesis (for specific queries).

**Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `query` | string | Yes | Search query |
| `doc_types` | array[string] | No | Filter to specific doc-types (default: all) |
| `limit` | integer | No | Maximum results (default: 10) |
| `min_relevance_score` | float | No | Minimum relevance threshold (default: 0.5) |
| `promotion_levels` | array[enum] | No | Filter to specific levels: `standard`, `important`, `critical` (default: all) |

**Response**:
```json
{
  "results": [
    {
      "path": "./csharp-compounding-docs/problems/db-pool-exhaustion-20250115.md",
      "title": "Database Connection Pool Exhaustion",
      "summary": "Connection pool exhaustion caused by missing disposal in background jobs",
      "char_count": 2847,
      "relevance_score": 0.92,
      "doc_type": "problem",
      "date": "2025-01-15",
      "promotion_level": "critical"
    }
  ],
  "total_matches": 1
}
```

---

## 3. Index Document Tool

**Tool Name**: `index_document`

**Purpose**: Manually trigger indexing of a specific document (used by skills after creating docs).

**Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | Yes | Relative path to document |

**Response**:
```json
{
  "status": "indexed",
  "path": "./csharp-compounding-docs/problems/new-issue-20250122.md",
  "embedding_dimensions": 1024
}
```

**Note**: This is a fallback. File watcher should handle most cases, but skills may call this for immediate confirmation.

---

## 4. List Doc-Types Tool

**Tool Name**: `list_doc_types`

**Purpose**: Return available doc-types for the active project.

**Parameters**: None

**Response**:
```json
{
  "doc_types": [
    {
      "name": "problem",
      "description": "Problems and solutions",
      "folder": "problems",
      "schema": "built-in",
      "doc_count": 12
    },
    {
      "name": "api-contract",
      "description": "API design decisions",
      "folder": "api-contracts",
      "schema": "./schemas/api-contract.schema.yaml",
      "doc_count": 4,
      "custom": true
    }
  ]
}
```

---

## 5. Search External Docs Tool

**Tool Name**: `search_external_docs`

**Purpose**: Search external project documentation (read-only). Requires `external_docs` to be configured in project config.

**Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `query` | string | Yes | Search query |
| `limit` | integer | No | Maximum results (default: 10) |
| `min_relevance_score` | float | No | Minimum relevance threshold (default: 0.7, overridden by `semantic_search.min_relevance_score` in project config) |

**Response**:
```json
{
  "results": [
    {
      "path": "./docs/architecture/database-design.md",
      "title": "Database Design Guide",
      "summary": "Overview of database schema and design decisions",
      "char_count": 4521,
      "relevance_score": 0.85
    }
  ],
  "total_matches": 1,
  "external_docs_path": "./docs"
}
```

**Behavior**:
1. Check if `external_docs` is configured in project config
2. If not configured, return error with instructions
3. Generate embedding for query
4. Search external docs vector index
5. Return ranked results (read-only, no RAG synthesis)

**Note**: External docs are indexed separately from compounding docs. The index is rebuilt when:
- Project is activated
- External docs path changes
- File watcher detects changes in external docs folder

**Chunking**: External documents >500 lines are chunked using the same strategy as compounding docs. See [chunking.md](./chunking.md) for details.

---

## 6. RAG Query External Docs Tool

**Tool Name**: `rag_query_external`

**Purpose**: Answer questions using external documentation context, returning synthesized response with source metadata. Requires `external_docs` to be configured in project config.

**Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `query` | string | Yes | Natural language question |
| `max_sources` | integer | No | Maximum documents to use (default: 3) |
| `min_relevance_score` | float | No | Minimum relevance score (default: 0.7, overridden by `semantic_search.min_relevance_score` in project config) |

**Response**:
```json
{
  "answer": "The API authentication uses JWT tokens with...",
  "sources": [
    {
      "path": "./docs/api/authentication.md",
      "title": "API Authentication Guide",
      "char_count": 3421,
      "relevance_score": 0.89
    },
    {
      "path": "./docs/security/jwt-setup.md",
      "title": "JWT Configuration",
      "char_count": 1876,
      "relevance_score": 0.76
    }
  ],
  "external_docs_path": "./docs"
}
```

**Behavior**:
1. Check if `external_docs` is configured in project config
2. If not configured, return error with instructions
3. Generate embedding for query using Ollama
4. Perform vector similarity search in external docs collection
5. Retrieve top-k documents
6. Send documents + query to Ollama for RAG synthesis
7. Return synthesized answer with source attribution

**Note**: Unlike `rag_query` for compounding docs, this tool does not follow linked docs (external docs are assumed to be standalone reference material).

---

## 7. Delete Documents Tool

**Tool Name**: `delete_documents`

**Purpose**: Delete compounding docs from the database by project, branch, or path.

**Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `project_name` | string | Yes | Project identifier |
| `branch_name` | string | No | Branch name (if omitted, deletes all branches) |
| `path_hash` | string | No | Path hash (if omitted, deletes all paths) |
| `dry_run` | boolean | No | If true, return counts without deleting (default: false) |

**Response**:
```json
{
  "status": "deleted",
  "deleted_count": 34,
  "deleted_chunks": 12,
  "project_name": "my-project",
  "branch_name": "feature/old-branch",
  "path_hash": null,
  "dry_run": false
}
```

**Behavior**:
1. Validate `project_name` is provided
2. Build filter based on provided parameters
3. If `dry_run=true`: Count matching documents/chunks, return counts with `status: "preview"`
4. If `dry_run=false`: Delete matching documents from `documents` collection
5. Delete matching chunks from `document_chunks` collection
6. Optionally clean up orphaned tenant_management records
7. Return deletion counts

**Safety Notes**:
- This is a destructive operation
- Skills should call with `dry_run=true` first to show counts, then confirm with user, then call with `dry_run=false`
- Consider soft-delete with TTL for recovery window (future enhancement)

---

## 8. Update Promotion Level Tool

**Tool Name**: `update_promotion_level`

**Purpose**: Update the promotion level of a document (standard, important, critical).

**Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `document_path` | string | Yes | Relative path to document within `./csharp-compounding-docs/` |
| `promotion_level` | enum | Yes | New level: `standard`, `important`, or `critical` |

**Response**:
```json
{
  "status": "updated",
  "document_path": "problems/n-plus-one-query-20250120.md",
  "previous_level": "standard",
  "new_level": "critical"
}
```

**Behavior**:
1. Validate document exists in `./csharp-compounding-docs/`
2. Read current document content
3. Update `promotion_level` in YAML frontmatter
4. Write updated content back to file
5. Update `promotion_level` in database record for the document
6. **Atomically update all associated chunks** in `document_chunks` collection to the same promotion level
7. Return previous and new levels

**Chunk Promotion**: When a document is promoted, all its chunks are promoted together in a single transaction. Chunks inherit the parent document's promotion level and cannot be promoted independently.

**Restrictions**:
- Cannot promote external docs (returns `EXTERNAL_DOCS_NOT_PROMOTABLE` error)
- Document must exist (returns `DOCUMENT_NOT_FOUND` error)

---

## 9. Activate Project Tool

> **Background**: Dynamic configuration loading for runtime-discovered project paths using custom IConfigurationProvider. See [IOptionsMonitor with Dynamic Paths Research](../../research/ioptions-monitor-dynamic-paths.md).

**Tool Name**: `activate_project`

**Purpose**: Activate a project for the session, establishing context for all other tools.

**Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `config_path` | string | Yes | Absolute path to `.csharp-compounding-docs/config.json` |
| `branch_name` | string | Yes | Current git branch name (see [Git Branch Detection Research](../../research/git-current-branch-detection.md)) |

**Behavior**:
1. Read the config file at `config_path`
2. Compute `repo_root` from config path (parent of `.csharp-compounding-docs/`)
3. Compute `path_hash` from `repo_root` (SHA256, truncated)
4. Store `project_name`, `branch_name`, `path_hash` in `IOptionsMonitor<>` configuration
5. Register path and branch in tenant management tables
6. Start file watcher on `{repo_root}/csharp-compounding-docs/` (see [FileSystemWatcher Research](../../research/dotnet-file-watcher-embeddings-research.md))
7. Perform initial sync (ensure all files are in vector DB for this tenant)
8. Return activation status with doc-type list

**Response**:
```json
{
  "status": "activated",
  "project_name": "my-project",
  "branch_name": "feature/new-feature",
  "path_hash": "a1b2c3d4",
  "doc_types": [
    { "name": "problem", "doc_count": 12 },
    { "name": "insight", "doc_count": 5 },
    { "name": "codebase", "doc_count": 8 },
    { "name": "tool", "doc_count": 3 },
    { "name": "style", "doc_count": 2 },
    { "name": "api-contract", "doc_count": 4, "custom": true }
  ],
  "total_docs": 34
}
```

**Single Project Constraint**: Since the server runs as stdio (one per Claude Code instance), it supports only one active project at a time. Activating a new project deactivates the previous one.

---

## Error Handling

### Error Response Format

All tools return errors in a standard format:

```json
{
  "error": true,
  "code": "ERROR_CODE",
  "message": "Human-readable error description",
  "details": {}
}
```

### Standard Error Codes

| Code | Description |
|------|-------------|
| `PROJECT_NOT_ACTIVATED` | No project is currently activated |
| `EXTERNAL_DOCS_NOT_CONFIGURED` | `external_docs` not configured in project config |
| `EXTERNAL_DOCS_NOT_PROMOTABLE` | Cannot promote external documentation |
| `DOCUMENT_NOT_FOUND` | Requested document does not exist |
| `INVALID_DOC_TYPE` | Unknown or invalid doc-type specified |
| `SCHEMA_VALIDATION_FAILED` | Document frontmatter doesn't match schema |
| `EMBEDDING_SERVICE_ERROR` | Ollama embedding generation failed |
| `DATABASE_ERROR` | PostgreSQL operation failed |
| `FILE_SYSTEM_ERROR` | File read/write operation failed |

### Error Response Behaviors

| Scenario | Behavior |
|----------|----------|
| Project not activated | Return error with activation instructions |
| Ollama unavailable | Retry with circuit breaker, then return error |
| PostgreSQL unavailable | Retry with exponential backoff, then return error |
| Invalid document (parse error) | Log warning, skip document, continue |
| Schema validation failure | Return validation errors, don't index |

For resilience implementation details including retry policies and circuit breaker configuration, see [ollama-integration.md](./ollama-integration.md#resilience-and-circuit-breaker).

---

## Related Files

- [file-watcher.md](./file-watcher.md) - File watching and sync behavior
- [chunking.md](./chunking.md) - Document chunking strategy
- [ollama-integration.md](./ollama-integration.md) - Embedding and RAG generation
- [database-schema.md](./database-schema.md) - PostgreSQL schema details
