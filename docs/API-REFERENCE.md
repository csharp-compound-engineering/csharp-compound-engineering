# API Reference

This document provides comprehensive documentation for all MCP tools provided by the CSharp Compound Docs server.

## Table of Contents

- [Overview](#overview)
- [Error Handling](#error-handling)
- [Tools Reference](#tools-reference)
  1. [rag_query](#1-rag_query)
  2. [semantic_search](#2-semantic_search)
  3. [index_document](#3-index_document)
  4. [list_doc_types](#4-list_doc_types)
  5. [search_external_docs](#5-search_external_docs)
  6. [rag_query_external](#6-rag_query_external)
  7. [delete_documents](#7-delete_documents)
  8. [update_promotion_level](#8-update_promotion_level)
  9. [activate_project](#9-activate_project)

---

## Overview

The CSharp Compound Docs MCP server provides 9 tools for managing and querying documentation:

| Tool | Category | Purpose |
|------|----------|---------|
| `rag_query` | Query | Answer questions using RAG with compounding docs |
| `semantic_search` | Query | Search compounding docs by semantic similarity |
| `index_document` | Management | Manually index/re-index a document |
| `list_doc_types` | Query | List available doc-types and their schemas |
| `search_external_docs` | Query | Search external project documentation |
| `rag_query_external` | Query | RAG query against external docs |
| `delete_documents` | Management | Delete documents by tenant context |
| `update_promotion_level` | Management | Update document visibility level |
| `activate_project` | Session | Activate a project for the session |

### Prerequisites

Before using any tool (except `activate_project`), ensure:

1. A project is activated via `activate_project`
2. Infrastructure is running (PostgreSQL, Ollama)
3. Required Ollama models are available

---

## Error Handling

### Error Response Format

All tools return errors in a consistent format:

```json
{
  "error": true,
  "code": "ERROR_CODE",
  "message": "Human-readable error description",
  "details": {}
}
```

### Standard Error Codes

| Code | HTTP Equivalent | Description |
|------|-----------------|-------------|
| `PROJECT_NOT_ACTIVATED` | 412 | No project is currently activated |
| `EXTERNAL_DOCS_NOT_CONFIGURED` | 400 | `external_docs` not configured in project config |
| `EXTERNAL_DOCS_NOT_PROMOTABLE` | 400 | Cannot promote external documentation |
| `DOCUMENT_NOT_FOUND` | 404 | Requested document does not exist |
| `INVALID_DOC_TYPE` | 400 | Unknown or invalid doc-type specified |
| `SCHEMA_VALIDATION_FAILED` | 400 | Document frontmatter doesn't match schema |
| `EMBEDDING_SERVICE_ERROR` | 503 | Ollama embedding generation failed |
| `DATABASE_ERROR` | 500 | PostgreSQL operation failed |
| `FILE_SYSTEM_ERROR` | 500 | File read/write operation failed |

---

## Tools Reference

---

### 1. rag_query

Answer questions using retrieved context from compounding documentation.

#### Description

Performs Retrieval-Augmented Generation (RAG) by:
1. Generating an embedding for the query
2. Finding semantically similar documents
3. Following linked documents up to configured depth
4. Synthesizing an answer using Ollama

#### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `query` | string | Yes | - | Natural language question |
| `doc_types` | array[string] | No | all | Filter to specific doc-types |
| `max_sources` | integer | No | 3 | Maximum documents to use (1-100) |
| `min_relevance_score` | float | No | 0.7 | Minimum relevance score (0.0-1.0) |
| `min_promotion_level` | enum | No | `standard` | Minimum visibility level |
| `include_critical` | boolean | No | true | Prepend critical docs regardless of relevance |

**Promotion Level Values**: `standard`, `important`, `critical`

#### Response

```json
{
  "answer": "The database connection pool exhaustion was caused by SqlConnection objects not being disposed in background jobs. The solution was to wrap all database operations in using statements...",
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

#### Example Usage

```json
{
  "tool": "rag_query",
  "arguments": {
    "query": "How do we handle database connection pool exhaustion?",
    "doc_types": ["problem", "codebase"],
    "max_sources": 5,
    "min_relevance_score": 0.8
  }
}
```

#### Error Codes

- `PROJECT_NOT_ACTIVATED` - No project activated
- `EMBEDDING_SERVICE_ERROR` - Ollama unavailable
- `DATABASE_ERROR` - PostgreSQL query failed

---

### 2. semantic_search

Search compounding documentation by semantic similarity without RAG synthesis.

#### Description

Returns ranked documents matching the query semantically. Use this when you need specific documents rather than synthesized answers.

#### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `query` | string | Yes | - | Search query |
| `doc_types` | array[string] | No | all | Filter to specific doc-types |
| `limit` | integer | No | 10 | Maximum results (1-100) |
| `min_relevance_score` | float | No | 0.5 | Minimum relevance threshold (0.0-1.0) |
| `promotion_levels` | array[enum] | No | all | Filter to specific promotion levels |

#### Response

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

#### Example Usage

```json
{
  "tool": "semantic_search",
  "arguments": {
    "query": "connection pool",
    "doc_types": ["problem"],
    "limit": 5,
    "promotion_levels": ["important", "critical"]
  }
}
```

---

### 3. index_document

Manually trigger indexing of a specific document.

#### Description

Forces re-indexing of a document. Typically unnecessary as the file watcher handles indexing automatically, but useful for:
- Immediate confirmation after document creation
- Re-indexing after manual file edits
- Troubleshooting indexing issues

#### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `path` | string | Yes | - | Relative path to document |

#### Response

```json
{
  "status": "indexed",
  "path": "./csharp-compounding-docs/problems/new-issue-20250122.md",
  "embedding_dimensions": 1024
}
```

#### Example Usage

```json
{
  "tool": "index_document",
  "arguments": {
    "path": "./csharp-compounding-docs/problems/new-issue-20250122.md"
  }
}
```

#### Error Codes

- `PROJECT_NOT_ACTIVATED` - No project activated
- `DOCUMENT_NOT_FOUND` - File does not exist
- `SCHEMA_VALIDATION_FAILED` - Invalid frontmatter

---

### 4. list_doc_types

List all available document types for the active project.

#### Description

Returns both built-in and custom doc-types with their metadata and document counts.

#### Parameters

None

#### Response

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
      "name": "insight",
      "description": "Product and project insights",
      "folder": "insights",
      "schema": "built-in",
      "doc_count": 5
    },
    {
      "name": "codebase",
      "description": "Codebase knowledge",
      "folder": "codebase",
      "schema": "built-in",
      "doc_count": 8
    },
    {
      "name": "tool",
      "description": "Tools and libraries",
      "folder": "tools",
      "schema": "built-in",
      "doc_count": 3
    },
    {
      "name": "style",
      "description": "Coding styles and preferences",
      "folder": "styles",
      "schema": "built-in",
      "doc_count": 2
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

#### Example Usage

```json
{
  "tool": "list_doc_types",
  "arguments": {}
}
```

---

### 5. search_external_docs

Search external project documentation (read-only).

#### Description

Searches documentation configured in `external_docs` section of project config. External docs are indexed separately and cannot be modified.

#### Prerequisites

- `external_docs` must be configured in project config

#### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `query` | string | Yes | - | Search query |
| `limit` | integer | No | 10 | Maximum results (1-100) |
| `min_relevance_score` | float | No | 0.7 | Minimum relevance threshold |

#### Response

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

#### Example Usage

```json
{
  "tool": "search_external_docs",
  "arguments": {
    "query": "database schema design",
    "limit": 5
  }
}
```

#### Error Codes

- `PROJECT_NOT_ACTIVATED` - No project activated
- `EXTERNAL_DOCS_NOT_CONFIGURED` - External docs not configured

---

### 6. rag_query_external

Answer questions using external documentation context.

#### Description

Performs RAG against external documentation. Unlike `rag_query`, this tool does not follow linked documents (external docs are assumed to be standalone reference material).

#### Prerequisites

- `external_docs` must be configured in project config

#### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `query` | string | Yes | - | Natural language question |
| `max_sources` | integer | No | 3 | Maximum documents to use |
| `min_relevance_score` | float | No | 0.7 | Minimum relevance score |

#### Response

```json
{
  "answer": "The API authentication uses JWT tokens with RSA-256 signing. Tokens are issued by the /auth/login endpoint and must be included in the Authorization header...",
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

#### Example Usage

```json
{
  "tool": "rag_query_external",
  "arguments": {
    "query": "How does API authentication work?",
    "max_sources": 5
  }
}
```

---

### 7. delete_documents

Delete compounding documents from the database.

#### Description

Removes documents matching the specified tenant context. Supports dry-run mode for previewing deletions.

#### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `project_name` | string | Yes | - | Project identifier |
| `branch_name` | string | No | all | Branch name filter |
| `path_hash` | string | No | all | Path hash filter |
| `dry_run` | boolean | No | false | Preview without deleting |

#### Response

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

**Dry Run Response**:
```json
{
  "status": "preview",
  "would_delete_count": 34,
  "would_delete_chunks": 12,
  "project_name": "my-project",
  "branch_name": "feature/old-branch",
  "path_hash": null,
  "dry_run": true
}
```

#### Example Usage

```json
{
  "tool": "delete_documents",
  "arguments": {
    "project_name": "my-project",
    "branch_name": "feature/old-branch",
    "dry_run": true
  }
}
```

#### Safety Notes

- Always run with `dry_run: true` first
- This operation is irreversible (no soft delete)
- Consider backing up before bulk deletions

---

### 8. update_promotion_level

Update the visibility level of a document.

#### Description

Changes a document's promotion level, which affects its visibility in search results and RAG context. Higher promotion levels (`important`, `critical`) are prioritized in retrieval.

#### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `document_path` | string | Yes | - | Relative path within `./csharp-compounding-docs/` |
| `promotion_level` | enum | Yes | - | New level: `standard`, `important`, `critical` |

#### Response

```json
{
  "status": "updated",
  "document_path": "problems/n-plus-one-query-20250120.md",
  "previous_level": "standard",
  "new_level": "critical"
}
```

#### Example Usage

```json
{
  "tool": "update_promotion_level",
  "arguments": {
    "document_path": "problems/n-plus-one-query-20250120.md",
    "promotion_level": "critical"
  }
}
```

#### Promotion Levels

| Level | Description | Behavior |
|-------|-------------|----------|
| `standard` | Default level | Normal search ranking |
| `important` | Elevated importance | Higher ranking in results |
| `critical` | Must-see documentation | Always included in RAG context |

#### Error Codes

- `DOCUMENT_NOT_FOUND` - Document does not exist
- `EXTERNAL_DOCS_NOT_PROMOTABLE` - Cannot promote external docs

---

### 9. activate_project

Activate a project for the current session.

#### Description

Establishes the project context required for all other tools. Must be called first before using any other tool.

This tool:
1. Reads the project configuration file
2. Computes tenant isolation keys (project, branch, path hash)
3. Starts the file watcher for automatic indexing
4. Performs initial synchronization

#### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `config_path` | string | Yes | - | Absolute path to `.csharp-compounding-docs/config.json` |
| `branch_name` | string | Yes | - | Current git branch name |

#### Response

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

#### Example Usage

```json
{
  "tool": "activate_project",
  "arguments": {
    "config_path": "/home/user/projects/my-project/.csharp-compounding-docs/config.json",
    "branch_name": "main"
  }
}
```

#### Notes

- Only one project can be active per MCP server instance
- Activating a new project deactivates the previous one
- The file watcher begins monitoring immediately after activation

---

## Common Patterns

### Query Workflow

```json
// 1. Activate project
{ "tool": "activate_project", "arguments": { "config_path": "...", "branch_name": "main" } }

// 2. Search for relevant documents
{ "tool": "semantic_search", "arguments": { "query": "authentication", "limit": 5 } }

// 3. Get detailed answer
{ "tool": "rag_query", "arguments": { "query": "How does authentication work in our system?" } }
```

### Document Management Workflow

```json
// 1. List available doc types
{ "tool": "list_doc_types", "arguments": {} }

// 2. Index a new document
{ "tool": "index_document", "arguments": { "path": "./csharp-compounding-docs/problems/new-bug.md" } }

// 3. Promote important document
{ "tool": "update_promotion_level", "arguments": { "document_path": "problems/new-bug.md", "promotion_level": "important" } }
```

### Cleanup Workflow

```json
// 1. Preview deletions
{ "tool": "delete_documents", "arguments": { "project_name": "my-project", "branch_name": "old-branch", "dry_run": true } }

// 2. Execute deletion (after confirming preview)
{ "tool": "delete_documents", "arguments": { "project_name": "my-project", "branch_name": "old-branch", "dry_run": false } }
```
