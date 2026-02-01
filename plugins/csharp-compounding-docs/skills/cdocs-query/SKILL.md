---
name: cdocs:query
description: Performs RAG-based question answering against project documentation with source attribution
allowed-tools:
  - Read
preconditions:
  - Project activated via /cdocs:activate
---

# Query Documentation Skill

## Intake

This skill accepts a natural language question about the project's captured documentation. The user provides:

- **query** (required): The question or query to answer from the documentation
- **limit** (optional): Maximum number of source documents to retrieve (1-20, default: 5)
- **doc_types** (optional): Comma-separated list of document types to filter by (spec, adr, research, doc, guide, api, changelog)
- **promotion_level** (optional): Minimum promotion level filter (standard, important, critical)
- **include_chunks** (optional): Whether to include individual content chunks in the response (default: false)

## Process

1. **Validate Input**: Ensure the query meets minimum length requirements (3+ characters)
2. **Call MCP Tool**: Invoke the `rag_query` MCP tool with the provided parameters
3. **Process Response**: The RAG system will:
   - Convert the query to embeddings
   - Retrieve semantically relevant document chunks from the vector store
   - Use LLM to synthesize a coherent answer from the retrieved chunks
   - Return the answer with source attribution and confidence scoring
4. **Format Results**: Present the answer with sources and metadata
5. **Offer Follow-up**: Provide option to load source documents for deeper investigation

## Output Format

The skill returns a markdown-formatted response containing:

### Query Results Section
- **Query**: The original question asked
- **Answer**: Synthesized answer from the RAG system

### Sources Section
- List of source documents with:
  - Document title
  - File path (clickable link)
  - Document type
  - Relevance score

### Content Chunks Section (optional)
- Individual text chunks retrieved from the vector store
- Only included if `include_chunks=true`

### Metadata
- **Confidence Score**: RAG system's confidence in the answer (0.0-1.0)

## Examples

### Example 1: Basic Question
```
User: "How does the compound engineering workflow work?"

Skill invocation:
- query: "How does the compound engineering workflow work?"
- limit: 5

Output:
# Query Results

**Query**: How does the compound engineering workflow work?

## Answer

The compound engineering workflow is a documentation-driven development process that
captures knowledge incrementally. It follows these steps: 1) Capture requirements and
decisions as structured documents, 2) Store documents in a vector database for semantic
search, 3) Query documentation using RAG for synthesis, 4) Evolve documentation alongside code.

## Sources

- [Compound Engineering Overview](docs/spec/overview.md) (spec, relevance: 0.95)
- [Workflow Patterns](docs/spec/workflow.md) (spec, relevance: 0.87)
- [ADR-001: Documentation Strategy](docs/adr/adr-001.md) (adr, relevance: 0.82)

---
**Confidence**: 0.91
```

### Example 2: Filtered Query with Chunks
```
User: "What are the critical decisions about the database?"

Skill invocation:
- query: "What are the critical decisions about the database?"
- limit: 3
- doc_types: "adr,spec"
- promotion_level: "critical"
- include_chunks: true

Output:
# Query Results

**Query**: What are the critical decisions about the database?

## Answer

The critical database decisions include: 1) PostgreSQL with pgvector for vector storage,
2) Ollama for local embeddings generation, 3) Semantic Kernel for RAG orchestration.

## Sources

- [ADR-005: Vector Database Selection](docs/adr/adr-005.md) (adr, relevance: 0.98)
- [Database Architecture](docs/spec/database.md) (spec, relevance: 0.89)

## Content Chunks

### Chunk 0
We chose PostgreSQL with the pgvector extension because it provides production-ready
vector similarity search while maintaining ACID compliance for structured metadata.

### Chunk 1
Ollama was selected for embeddings generation to enable fully local, offline operation
without dependency on external API services.

---
**Confidence**: 0.94
```

### Example 3: Decision Matrix Usage

**Use RAG (this skill) when:**
- Question is open-ended: "How does X work?"
- Multiple documents likely relevant
- Synthesis needed across sources
- User wants an answer, not just document discovery

**Use Semantic Search (/cdocs:search) when:**
- Query is specific: "Find the doc about X error"
- Looking for one specific document
- User explicitly asks to "find" or "search"
- Discovery/browsing rather than question answering

## Notes

- This is a manual-invocation skill (no auto-triggers)
- Requires active project context via `/cdocs:activate`
- Uses the `rag_query` MCP tool for retrieval-augmented generation
- Sources are ranked by semantic relevance to the query
- Confidence scores indicate RAG system's certainty in the answer
- Can be filtered by document type and promotion level for targeted queries
