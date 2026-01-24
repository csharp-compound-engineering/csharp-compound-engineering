# Document Promotion Levels

> **Status**: [DRAFT]
> **Parent**: [../doc-types.md](../doc-types.md)

> **Background**: Document promotion builds on the compound engineering philosophy of making knowledge compound over time. The promotion system ensures critical learnings surface when needed. See [Compound Engineering Paradigm Research](../../research/compound-engineering-paradigm-research.md).

---

## Overview

Documents can be promoted to higher visibility tiers to ensure critical knowledge surfaces in future sessions. Promotion controls how readily documents appear in RAG queries and search results.

Promotion is stored both in the YAML frontmatter and in the vector database for query filtering.

---

## Promotion Level Enum

| Level | Value | Description |
|-------|-------|-------------|
| **Standard** | `standard` | Default level. Retrieved via normal RAG/search |
| **Important** | `important` | Higher relevance boost. Surfaces more readily in related queries |
| **Critical** | `critical` | **Required Reading**. Must be surfaced before code generation in related areas |

---

## When to Promote

### Promote to Important

Use `important` when:
- The knowledge prevents common mistakes
- The information is frequently referenced
- Understanding this is necessary for working in a specific area
- The solution saved significant debugging time

### Promote to Critical

Use `critical` when:
- The same mistake has been made 3+ times across different contexts
- The solution is non-obvious but must be followed every time
- Ignoring this knowledge leads to significant rework
- This represents a foundational pattern for the codebase
- Getting this wrong could cause production issues

### Do NOT Over-Promote

**If everything is critical, nothing is.** Reserve critical for truly essential patterns.

Signs of over-promotion:
- More than 10-15% of documents are marked critical
- Critical documents are rarely relevant to queries
- Team members start ignoring critical flags

---

## Promotion Workflow

### Promoting a Document

1. Identify the document to promote
2. Run `/cdocs:promote`
3. Select the target promotion level
4. The skill updates both:
   - YAML frontmatter (`promotion_level` field)
   - Vector database metadata

### Demoting a Document

Demotion is also supported (critical -> important -> standard):
1. Run `/cdocs:promote` on the document
2. Select a lower promotion level
3. Both frontmatter and database are updated

### Bulk Promotion Review

Periodically review promotion levels:
1. Query all critical documents
2. Assess if each still warrants critical status
3. Demote documents that no longer meet criteria

---

## Database Schema

The `promotion_level` field is part of the `CompoundDocument` and `DocumentChunk` models, managed by **Semantic Kernel's model-first approach**. The field is defined with `[VectorStoreData(IsFilterable = true)]` attributes, and tables are auto-created via `EnsureCollectionExistsAsync()`.

> **Background**: Semantic Kernel's PostgreSQL connector uses a model-first approach where C# attributes define the vector store schema. The `IsFilterable` attribute enables promotion-based query filtering. See [Microsoft Semantic Kernel Research](../../research/microsoft-semantic-kernel-research.md).

See [database-schema.md](../mcp-server/database-schema.md) for the full model definitions.

---

## MCP Tool Support

> **Background**: The MCP server exposes RAG and search capabilities as tools that Claude can invoke. The promotion level parameters integrate with Semantic Kernel's vector search API. See [MCP C# SDK Research](../../research/mcp-csharp-sdk-research.md).

The RAG and search tools support filtering by promotion level:

| Tool | Parameter | Behavior |
|------|-----------|----------|
| `rag_query` | `min_promotion_level` | Only return docs at or above this level |
| `rag_query` | `include_critical` | Boolean - prepend critical docs to context (default: true) |
| `semantic_search` | `promotion_level` | Filter to specific level(s) |

### Example: Query Critical Knowledge Only

```json
{
  "tool": "rag_query",
  "arguments": {
    "query": "What should I know about database migrations?",
    "min_promotion_level": "critical"
  }
}
```

### Example: Include All Levels But Boost Critical

```json
{
  "tool": "rag_query",
  "arguments": {
    "query": "How do we handle authentication?",
    "include_critical": true
  }
}
```

### Example: Search for Important Documents

```json
{
  "tool": "semantic_search",
  "arguments": {
    "query": "error handling patterns",
    "promotion_level": ["important", "critical"]
  }
}
```

---

## Promotion Guidelines by Doc-Type

### Problems (`problem`)

| Promote to | When |
|------------|------|
| Important | Solution required significant investigation |
| Critical | Bug caused production incident; root cause was non-obvious |

### Insights (`insight`)

| Promote to | When |
|------------|------|
| Important | Insight affects multiple features |
| Critical | Misunderstanding this leads to building wrong features |

### Codebase (`codebase`)

| Promote to | When |
|------------|------|
| Important | Architectural decision affects multiple modules |
| Critical | Violating this pattern breaks the system |

### Tools (`tool`)

| Promote to | When |
|------------|------|
| Important | Gotcha affects common use cases |
| Critical | Library bug causes data loss or security issues |

### Styles (`style`)

| Promote to | When |
|------------|------|
| Important | Convention improves code review efficiency |
| Critical | Style choice prevents security vulnerabilities |

---

## Promotion Frontmatter Example

### Standard Document (Default)

```yaml
---
doc_type: problem
title: Database connection timeout
date: 2025-01-15
summary: Connection pool exhaustion under load
significance: performance
# promotion_level defaults to standard when omitted
---
```

### Important Document

```yaml
---
doc_type: codebase
title: Repository pattern implementation
date: 2025-01-10
summary: How we implement repositories across all services
significance: architectural
promotion_level: important
---
```

### Critical Document

```yaml
---
doc_type: problem
title: Race condition in payment processing
date: 2025-01-08
summary: Critical race condition that causes double-charges
significance: correctness
promotion_level: critical
---
```

---

## Promotion Audit Trail

When a document is promoted or demoted:
1. The change is logged with timestamp and reason
2. Git history preserves the frontmatter changes
3. Database metadata is updated atomically

### Tracking Promotion History

The skill adds a comment to the frontmatter when promotion changes:

```yaml
---
doc_type: problem
title: Database migration ordering
promotion_level: critical
# Promoted to critical on 2025-01-20: Same issue hit 3 times this month
---
```

---

## Integration with RAG Pipeline

> **Background**: The RAG pipeline uses Semantic Kernel with Ollama for embeddings and PostgreSQL/pgvector for vector storage. Promotion-based filtering integrates with this existing semantic search infrastructure. See [Semantic Kernel + Ollama RAG Research](../../research/semantic-kernel-ollama-rag-research.md) and [PostgreSQL pgvector Research](../../research/postgresql-pgvector-research.md).

### Default Behavior

When `include_critical: true` (the default):
1. Query retrieves relevant documents via semantic search
2. Critical documents matching the query domain are prepended
3. Results are deduplicated if a critical doc was also semantically matched
4. Token budget is respected (critical docs count against limit)

### Critical Document Injection

Critical documents are injected based on:
- **Semantic relevance**: Document embedding matches query embedding
- **Tag overlap**: Document tags overlap with inferred query topics
- **File path context**: Query mentions files in the document's `files_involved`

### Relevance Boosting for Important

Important documents receive a relevance boost:
- 1.5x weight in semantic similarity ranking
- Higher position in result list when scores are similar
- Still filtered out if not semantically relevant

---

## Best Practices

### Regular Review Cadence

- **Monthly**: Review all critical documents for continued relevance
- **Quarterly**: Review important documents for promotion/demotion
- **On project milestones**: Reassess what knowledge is truly critical

### Team Consensus

For critical promotions:
1. Document the reason in the frontmatter comment
2. Discuss with team if the pattern is truly universal
3. Verify the knowledge has been validated multiple times

### Measuring Promotion Effectiveness

Good promotion practices show:
- Critical documents are frequently referenced in code reviews
- Important documents surface at relevant times
- Standard documents don't create noise in unrelated contexts
