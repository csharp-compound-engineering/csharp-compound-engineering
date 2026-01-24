# Document Chunking

> **Status**: [DRAFT]
> **Parent**: [../mcp-server.md](../mcp-server.md)

---

## Overview

Large documents are split into chunks for more effective semantic search. Chunking is **database-only** - the source markdown file on disk is never modified.

---

## Strategy: Markdown Headers

Documents are chunked by markdown headers when they exceed size thresholds.

### Chunking Rules

1. **Threshold**: Documents > 500 lines are chunked
2. **Split Points**: `##` and `###` headers (H2 and H3)
3. **DB Storage**: Chunks are stored in the `document_chunks` table with references to the parent document
4. **Header Path**: Chunks store their header hierarchy (e.g., `## Section > ### Subsection`)
5. **Source File Unchanged**: The original markdown file remains untouched

### Why 500 Lines?

| Threshold | Pros | Cons |
|-----------|------|------|
| < 300 lines | More granular search | Too many small chunks, overhead |
| 300-500 lines | Good balance | - |
| > 500 lines | Fewer chunks | Large chunks reduce search precision |

The 500-line threshold balances search granularity with storage/processing overhead.

---

## Chunk Metadata (DB-Only)

When a document is chunked, the following metadata is stored in the database.

### Parent Document Record

Stored in `documents` table:

| Field | Type | Description |
|-------|------|-------------|
| `is_chunked` | boolean | `true` when document has chunks |
| `chunk_count` | integer | Number of chunks extracted |

### Chunk Records

Stored in `document_chunks` table:

```json
{
  "chunk_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "document_id": "parent-doc-guid",
  "header_path": "## Root Cause Analysis > ### Database Layer",
  "start_line": 45,
  "end_line": 120,
  "content": "The root cause was traced to...",
  "embedding": [...]
}
```

---

## Chunk Fields

Each chunk record contains:

| Field | Type | Purpose |
|-------|------|---------|
| `chunk_id` | GUID | Unique identifier for the chunk |
| `document_id` | GUID | Reference to parent document |
| `header_path` | string | Heading hierarchy for context |
| `start_line` | integer | Start line in source file |
| `end_line` | integer | End line in source file |
| `content` | text | The extracted text content |
| `embedding` | vector(1024) | Vector embedding for semantic search (mxbai-embed-large) |
| `promotion_level` | enum | Inherited from parent document |

### Header Path Format

The `header_path` provides context for where the chunk appears in the document:

```
## Root Cause Analysis > ### Database Layer
```

This format:
- Uses `>` as separator between heading levels
- Preserves the markdown heading markers (`##`, `###`)
- Enables hierarchical filtering in searches

---

## Chunking Algorithm

> **Background**: Markdig is the recommended .NET markdown parser, providing full AST traversal with source locations, built-in YAML frontmatter support, and methods like `Descendants<HeadingBlock>()` for extracting headers. See [Markdown Parser Research](../../research/dotnet-markdown-parser-research.md).

```
Input: Document with > 500 lines

1. Parse markdown with Markdig
2. Identify all H2 (##) and H3 (###) headers
3. For each header:
   a. Extract content until next header or EOF
   b. Build header_path from parent headers
   c. Record start_line and end_line
   d. Generate embedding for content
   e. Store chunk record

Output: List of chunk records with embeddings
```

### Edge Cases

| Case | Handling |
|------|----------|
| No H2/H3 headers | Single chunk for entire document |
| Very long section (> 1000 lines) | Chunk at paragraph boundaries within section |
| Empty section | Skip (no chunk created) |
| Code blocks | Include in chunk, do not split mid-block |

---

## Search Behavior

### Search Returns Chunks

When semantic search runs against chunked documents:
- Search returns **chunks** with relevance scores
- Results include parent document path for context
- Multiple chunks from same document may appear in results

### Agent Options

The agent can choose how to use chunk results:
- Load source document (full content)
- Load only relevant chunks (focused content)
- Load all chunks (complete but structured)

### RAG Behavior

For RAG queries:
- Source documents are always included in semantic search results
- Relevant chunks (meeting relevancy threshold) are included alongside parent docs
- RAG synthesis uses both parent context and specific chunk content

---

## External Docs Chunking

External documents > 500 lines are chunked using the same strategy:

- Split at `##` and `###` markdown headers
- Chunks stored in `external_document_chunks` collection with parent reference
- Search returns chunks with relevance scores; results include parent document path
- Source files on disk are never modified

### Differences from Compounding Docs

| Aspect | Compounding Docs | External Docs |
|--------|------------------|---------------|
| Chunk Table | `document_chunks` | `external_document_chunks` |
| Promotion Level | Inherited from parent | Not applicable |
| Write Access | Can update frontmatter | Read-only |

---

## Chunk Promotion

When a document's promotion level is updated:

1. Parent document `promotion_level` is updated in `documents` table
2. **All associated chunks** in `document_chunks` are updated atomically
3. Chunks inherit the parent document's promotion level
4. Chunks cannot be promoted independently

See [tools.md - Update Promotion Level](./tools.md#8-update-promotion-level-tool) for implementation details.

---

## Chunk Lifecycle

> **Background**: For implementation details on file monitoring, debouncing, and processing queues that trigger chunk creation/updates, see [FileSystemWatcher Embeddings Research](../../research/dotnet-file-watcher-embeddings-research.md).

### Creation

Chunks are created when:
- New document > 500 lines is indexed
- Modified document > 500 lines is re-indexed
- Reconciliation finds document > 500 lines needs indexing

### Update

Chunks are updated when:
- Parent document content changes (all chunks regenerated)
- Parent document promotion level changes (all chunks updated)

### Deletion

Chunks are deleted when:
- Parent document is deleted
- Parent document is modified to < 500 lines (chunks removed)
- `delete_documents` tool is called for parent document's tenant

---

## Performance Considerations

> **Background**: For embedding generation with Ollama and Semantic Kernel, including batch processing patterns and caching strategies, see [Semantic Kernel + Ollama RAG Research](../../research/semantic-kernel-ollama-rag-research.md). For vector storage with HNSW indexing and cosine distance operations, see [PostgreSQL pgvector Research](../../research/postgresql-pgvector-research.md).

### Embedding Generation

| Documents | Chunks (avg 5/doc) | Embedding Time (est.) |
|-----------|-------------------|----------------------|
| 10 | 50 | ~10 seconds |
| 100 | 500 | ~2 minutes |
| 500 | 2500 | ~10 minutes |

**Note**: Chunk embedding generation is parallelized where possible, limited by Ollama's `OLLAMA_NUM_PARALLEL` setting.

### Storage

| Field | Size (approx) |
|-------|--------------|
| chunk_id | 36 bytes |
| document_id | 36 bytes |
| header_path | ~100 bytes |
| content | ~5KB avg |
| embedding | 4KB (1024 floats) |
| **Total per chunk** | ~10KB |

For 1000 documents with average 5 chunks each: ~50MB chunk storage.

---

## Related Files

- [database-schema.md](./database-schema.md) - Schema for `document_chunks` table
- [file-watcher.md](./file-watcher.md) - Chunk regeneration on file changes
- [ollama-integration.md](./ollama-integration.md) - Embedding generation for chunks
- [tools.md](./tools.md) - Tools that interact with chunks
