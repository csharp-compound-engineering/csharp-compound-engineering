---
name: cdocs:search
description: Performs semantic vector similarity search across project documentation
allowed-tools:
  - Read
preconditions:
  - Project activated via /cdocs:activate
---

# Search Documentation Skill

## Intake

This skill accepts a search query for finding relevant documents through semantic similarity. The user provides:

- **query** (required): The search query text for semantic matching
- **limit** (optional): Maximum number of results to return (1-100, default: 10)
- **doc_types** (optional): Comma-separated list of document types to filter by (spec, adr, research, doc, guide, api, changelog)
- **promotion_level** (optional): Minimum promotion level filter (standard, important, critical)
- **min_relevance** (optional): Minimum relevance score threshold (0.0 to 1.0, default: 0.0)

## Process

1. **Validate Input**: Ensure the query meets minimum length requirements (2+ characters)
2. **Call MCP Tool**: Invoke the `semantic_search` MCP tool with the provided parameters
3. **Process Response**: The search system will:
   - Convert the query to embeddings using the configured embedding model
   - Perform vector similarity search against the document store
   - Rank results by cosine similarity or configured distance metric
   - Apply filters for document type and promotion level
   - Return ranked list of matching documents
4. **Format Results**: Present documents with metadata and relevance scores
5. **Offer Follow-up**: Provide option to load selected documents for detailed reading

## Output Format

The skill returns a markdown-formatted response containing:

### Search Summary
- **Query**: The original search text
- **Results Found**: Total number of matching documents

### Documents Section
For each matching document:
- **Title**: Document name/title
- **Path**: File path (clickable link)
- **Type**: Document type classification
- **Promotion Level**: Importance level (standard, important, critical)
- **Relevance**: Similarity score (0.0-1.0)
- **Snippet**: Brief content preview

## Examples

### Example 1: Basic Search
```
User: "Find documents about vector embeddings"

Skill invocation:
- query: "vector embeddings"
- limit: 10

Output:
# Search Results

**Query**: vector embeddings
**Results Found**: 5

## Documents

### Semantic Kernel + Ollama RAG Research

- **Path**: [docs/research/semantic-kernel-ollama-rag-research.md](docs/research/semantic-kernel-ollama-rag-research.md)
- **Type**: research
- **Promotion Level**: important
- **Relevance**: 0.92

> This research explores implementing RAG with Semantic Kernel using Ollama for local
> embeddings generation. Covers embedding models, vector storage, and retrieval patterns.

### PostgreSQL pgvector Research

- **Path**: [docs/research/postgresql-pgvector-research.md](docs/research/postgresql-pgvector-research.md)
- **Type**: research
- **Promotion Level**: important
- **Relevance**: 0.88

> Research on PostgreSQL's pgvector extension for vector similarity search. Includes
> distance functions, indexing strategies, and performance considerations.

### ADR-005: Vector Database Selection

- **Path**: [docs/adr/adr-005.md](docs/adr/adr-005.md)
- **Type**: adr
- **Promotion Level**: critical
- **Relevance**: 0.85

> Decision record for selecting PostgreSQL with pgvector as the vector database solution.
```

### Example 2: Filtered Search
```
User: "Search for critical ADRs about database"

Skill invocation:
- query: "database architecture decisions"
- limit: 5
- doc_types: "adr"
- promotion_level: "critical"
- min_relevance: 0.7

Output:
# Search Results

**Query**: database architecture decisions
**Results Found**: 3

## Documents

### ADR-005: Vector Database Selection

- **Path**: [docs/adr/adr-005.md](docs/adr/adr-005.md)
- **Type**: adr
- **Promotion Level**: critical
- **Relevance**: 0.94

> Decision record for selecting PostgreSQL with pgvector as the vector database solution.

### ADR-008: Database Schema Design

- **Path**: [docs/adr/adr-008.md](docs/adr/adr-008.md)
- **Type**: adr
- **Promotion Level**: critical
- **Relevance**: 0.87

> Architecture decision for the document metadata schema and vector storage structure.

### ADR-012: Embedding Model Selection

- **Path**: [docs/adr/adr-012.md](docs/adr/adr-012.md)
- **Type**: adr
- **Promotion Level**: critical
- **Relevance**: 0.79

> Decision to use Ollama's nomic-embed-text model for local embeddings generation.
```

### Example 3: No Results
```
User: "Find documentation about quantum computing"

Skill invocation:
- query: "quantum computing algorithms"
- limit: 10

Output:
# Search Results

**Query**: quantum computing algorithms
**Results Found**: 0

_No documents matched your search query_
```

### Example 4: Decision Matrix Usage

**Use Semantic Search (this skill) when:**
- Query is specific: "Find the doc about X error"
- Looking for one specific document
- User explicitly asks to "find" or "search"
- Discovery/browsing rather than question answering
- Need to see document metadata and relevance scores

**Use RAG Query (/cdocs:query) when:**
- Question is open-ended: "How does X work?"
- Multiple documents likely relevant
- Synthesis needed across sources
- User wants an answer, not just document discovery

## Notes

- This is a manual-invocation skill (no auto-triggers)
- Requires active project context via `/cdocs:activate`
- Uses the `semantic_search` MCP tool for vector similarity search
- Results are ranked by semantic similarity (typically cosine distance)
- Can return up to 100 results but defaults to 10 for readability
- Supports filtering by document type and promotion level
- Minimum relevance threshold helps eliminate low-quality matches
- Content snippets provide quick context without opening documents
