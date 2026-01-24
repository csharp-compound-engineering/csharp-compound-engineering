# Query Skills

> **Status**: [DRAFT]
> **Parent**: [../skills.md](../skills.md)

---

## Overview

Query skills provide search and retrieval capabilities for captured documentation. They enable both semantic search and RAG-based question answering across local and external documentation sources.

> **Background**: For comprehensive coverage of RAG implementation patterns using Semantic Kernel with Ollama embeddings and PostgreSQL/pgvector, including complete code examples for the `rag_query` and `semantic_search` MCP tools referenced in this spec. See [Semantic Kernel + Ollama RAG Research](../../research/semantic-kernel-ollama-rag-research.md).

> **Background**: For MCP server implementation details including tool registration patterns, error handling, and the attribute-based tool definition approach used by the query tools. See [MCP C# SDK Research](../../research/mcp-csharp-sdk-research.md).

---

## `/cdocs:query`

**Purpose**: Query documentation using RAG.

**Invocation**: Manual - user asks a question about captured knowledge.

**Behavior**:
1. Take user's question
2. Call MCP `rag_query` tool
3. Present synthesized answer
4. Offer to load source documents if needed

**Decision Matrix for RAG vs Search**:
```
Use RAG (default) when:
- Question is open-ended ("How does X work?")
- Multiple documents likely relevant
- Synthesis needed across sources

Use Semantic Search when:
- Query is specific ("Find the doc about X error")
- Looking for one specific document
- User explicitly asks to "find" or "search"
```

---

## `/cdocs:search`

**Purpose**: Semantic search for specific documents.

> **Background**: Semantic search relies on embedding generation and vector similarity. For details on embedding model options (including Ollama's nomic-embed-text and OpenAI alternatives) and distance functions (cosine, L2, inner product). See [PostgreSQL pgvector Research](../../research/postgresql-pgvector-research.md).

**Behavior**:
1. Take search query
2. Call MCP `semantic_search` tool
3. Present ranked results with metadata
4. Offer to load selected documents

---

## `/cdocs:search-external`

**Purpose**: Search external project documentation (read-only).

**Precondition**: `external_docs` must be configured in `.csharp-compounding-docs/config.json`.

**Behavior**:
1. Check if external docs are configured
2. If not configured, inform user and offer to help configure
3. Take search query from user
4. Call MCP `search_external_docs` tool
5. Present ranked results with file paths and relevance scores
6. Offer to load selected documents

**Note**: This is **read-only search**. No skills are provided to create or modify external documentation. The assumption is that external docs are maintained via an external process.

---

## `/cdocs:query-external`

**Purpose**: Query external project documentation using RAG.

**Precondition**: `external_docs` must be configured in `.csharp-compounding-docs/config.json`.

**Invocation**: Manual - user asks a question about external documentation.

**Behavior**:
1. Check if external docs are configured
2. If not configured, inform user and offer to help configure
3. Take user's question
4. Call MCP `rag_query_external` tool
5. Present synthesized answer with source attribution
6. Offer to load source documents if needed

**Decision Matrix for External Docs**:
```
Use RAG Query External when:
- Question is open-ended ("How does the API authentication work?")
- Multiple external docs likely relevant
- Synthesis needed across sources

Use Search External when:
- Query is specific ("Find the API rate limits doc")
- Looking for one specific document
- User explicitly asks to "find" or "search"
```

**Note**: This is **read-only**. External docs cannot be modified through this plugin.

---

## Related Documentation

- [Capture Skills](./capture-skills.md) - Create new documentation
- [Meta Skills](./meta-skills.md) - Create custom doc-types and handle conflicts
- [Utility Skills](./utility-skills.md) - Delete, promote, and manage documentation

## Related Research

- [Semantic Kernel + Ollama RAG Research](../../research/semantic-kernel-ollama-rag-research.md) - Complete RAG pipeline implementation
- [MCP C# SDK Research](../../research/mcp-csharp-sdk-research.md) - MCP tool registration and implementation
- [PostgreSQL pgvector Research](../../research/postgresql-pgvector-research.md) - Vector database and similarity search
- [Microsoft.Extensions.AI RAG Research](../../research/microsoft-extensions-ai-rag-research.md) - Alternative lightweight AI abstraction layer
- [Building Claude Code Skills](../../research/building-claude-code-skills-research.md) - Skill authoring patterns and best practices
