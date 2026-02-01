# Phase 093: /cdocs:query-external Skill

> **Status**: PLANNED
> **Category**: Skills System
> **Estimated Effort**: S
> **Prerequisites**: Phase 081 (rag_query_external MCP Tool), Phase 076 (search_external_docs MCP Tool)

---

## Spec References

- [spec/skills/query-skills.md - /cdocs:query-external](../spec/skills/query-skills.md#cdocsquery-external) - Skill specification
- [spec/skills/skill-patterns.md](../spec/skills/skill-patterns.md) - Skill file structure and common patterns
- [spec/mcp-server/tools.md - rag_query_external](../spec/mcp-server/tools.md#6-rag-query-external-docs-tool) - Underlying MCP tool
- [spec/skills.md - Query Skills](../spec/skills.md#query-skills) - Query skill category overview

---

## Objectives

1. Create SKILL.md for `/cdocs:query-external` skill
2. Implement RAG-based Q&A workflow against external documentation
3. Invoke `rag_query_external` MCP tool for synthesis
4. Enforce read-only semantics (no document modification)
5. Present answers with external source citation and attribution
6. Handle `EXTERNAL_DOCS_NOT_CONFIGURED` error with helpful guidance
7. Implement decision matrix for RAG query vs search

---

## Acceptance Criteria

### Skill File Structure

- [ ] Skill directory created at `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-query-external/`
- [ ] SKILL.md file with correct YAML frontmatter
- [ ] Skill name: `cdocs:query-external`
- [ ] Skill description: "Query external project documentation using RAG"

### SKILL.md Frontmatter

- [ ] `name: cdocs:query-external`
- [ ] `description:` describes RAG querying external docs
- [ ] `allowed-tools:` includes Read, mcp__compounding_docs__rag_query_external
- [ ] `preconditions:` lists project activation and external_docs configuration
- [ ] No `auto-invoke` (manual invocation only per spec)

### External Docs Configuration Check

- [ ] Skill checks if `external_docs` is configured before proceeding
- [ ] If not configured, displays helpful error message
- [ ] Error message offers to help configure `external_docs.path` in config.json
- [ ] Links to configuration documentation

### RAG Query Workflow

- [ ] Skill accepts natural language question from user
- [ ] Calls `rag_query_external` MCP tool with query
- [ ] Supports optional `max_sources` parameter (default: 3)
- [ ] Supports optional `min_relevance_score` parameter (default: 0.7)
- [ ] Handles tool errors gracefully (embedding service, database)

### Answer Presentation

- [ ] Displays synthesized answer from RAG response
- [ ] Lists source documents with:
  - [ ] File path (relative to external_docs root)
  - [ ] Document title
  - [ ] Character count
  - [ ] Relevance score
- [ ] Shows `external_docs_path` for context
- [ ] Offers to load source documents if user needs more detail

### Read-Only Enforcement

- [ ] Skill is explicitly read-only
- [ ] No options to create, modify, or delete external documents
- [ ] Note in skill that external docs are maintained externally
- [ ] No links to capture skills from this skill

### Decision Matrix Implementation

- [ ] Skill includes guidance on when to use RAG query vs search:
  - Use RAG Query External when question is open-ended
  - Use RAG Query External when multiple docs likely relevant
  - Use RAG Query External when synthesis needed across sources
  - Use Search External when query is specific
  - Use Search External when looking for one document
  - Use Search External when user says "find" or "search"

---

## Implementation Notes

### Skill Directory Structure

```
${CLAUDE_PLUGIN_ROOT}/skills/cdocs-query-external/
├── SKILL.md              # Main skill definition
└── references/           # Optional reference materials
    └── decision-matrix.md
```

### SKILL.md Content

```yaml
---
name: cdocs:query-external
description: Query external project documentation using RAG to answer questions with synthesized responses and source attribution
allowed-tools:
  - Read
  - mcp__compounding_docs__rag_query_external
preconditions:
  - Project activated via /cdocs:activate
  - external_docs configured in .csharp-compounding-docs/config.json
---

# Query External Documentation Skill

## Purpose

Answer questions about external project documentation using RAG (Retrieval-Augmented Generation). This skill retrieves relevant external documents, synthesizes an answer, and provides source attribution.

**This is a read-only skill.** External documentation cannot be modified through this plugin.

## When to Use This Skill

Use `/cdocs:query-external` when:
- Question is open-ended ("How does the API authentication work?")
- Multiple external docs are likely relevant
- You need synthesis across multiple sources

Use `/cdocs:search-external` instead when:
- Query is specific ("Find the API rate limits doc")
- Looking for one specific document
- User explicitly asks to "find" or "search"

## Preconditions

1. **Project must be activated** via `/cdocs:activate`
2. **external_docs must be configured** in `.csharp-compounding-docs/config.json`

## Intake

This skill expects:
- A natural language question about external documentation
- Optional: number of sources to consider (default: 3)
- Optional: minimum relevance threshold (default: 0.7)

## Process

### Step 1: Verify External Docs Configuration

Check that external_docs is configured in the project:

```
Call: rag_query_external with query="test"
```

If error code is `EXTERNAL_DOCS_NOT_CONFIGURED`:

**External docs not configured.**

To enable external documentation querying, add to your `.csharp-compounding-docs/config.json`:

```json
{
  "external_docs": {
    "path": "./docs",
    "include_patterns": ["**/*.md"],
    "exclude_patterns": ["**/node_modules/**"]
  }
}
```

Then re-activate the project with `/cdocs:activate`.

**STOP** - Cannot proceed without external_docs configuration.

### Step 2: Gather Question

If user hasn't provided a question:

> What would you like to know about the external documentation?

**WAIT** for user's question before proceeding.

### Step 3: Execute RAG Query

Call the `rag_query_external` MCP tool:

```
Tool: rag_query_external
Parameters:
  query: "{user's question}"
  max_sources: 3
  min_relevance_score: 0.7
```

### Step 4: Present Results

Display the synthesized answer with source attribution:

---

**Answer:**

{synthesized answer from RAG response}

---

**Sources:**

| Document | Relevance |
|----------|-----------|
| [{title}]({path}) ({char_count} chars) | {relevance_score} |
| ... | ... |

*External docs path: {external_docs_path}*

---

### Step 5: Offer Follow-up Options

After presenting the answer:

**What's next?**
1. Load a source document for more detail
2. Ask another question
3. Search for specific documents (`/cdocs:search-external`)
4. Return to main workflow

## Error Handling

### EXTERNAL_DOCS_NOT_CONFIGURED

External documentation path is not configured. See Step 1 for configuration instructions.

### EMBEDDING_SERVICE_ERROR

Ollama embedding service unavailable. Ensure Ollama is running:
```bash
ollama serve
```

### PROJECT_NOT_ACTIVATED

No project is currently activated. Run `/cdocs:activate` first.

### No Results Found

If no relevant documents found (empty results):

> No external documents matched your question with sufficient relevance.
>
> Try:
> - Rephrasing your question
> - Lowering the relevance threshold
> - Using `/cdocs:search-external` for broader search

## Important Notes

- **Read-only**: This skill cannot modify external documentation
- **External maintenance**: External docs are assumed to be maintained via an external process
- **No linked docs**: Unlike `/cdocs:query`, this skill does not follow document links (external docs are standalone reference material)
- **Separate index**: External docs are indexed separately from compounding docs

## Examples

### Example 1: API Question

**User**: How does authentication work in the API?

**Skill calls**: `rag_query_external` with query "How does authentication work in the API?"

**Response**:

---

**Answer:**

The API uses JWT (JSON Web Tokens) for authentication. Clients must first obtain a token by calling the `/auth/login` endpoint with valid credentials. The token should then be included in the `Authorization` header as `Bearer {token}` for all subsequent requests. Tokens expire after 24 hours and can be refreshed using the `/auth/refresh` endpoint.

---

**Sources:**

| Document | Relevance |
|----------|-----------|
| [API Authentication Guide](./docs/api/authentication.md) (3421 chars) | 0.89 |
| [JWT Configuration](./docs/security/jwt-setup.md) (1876 chars) | 0.76 |

*External docs path: ./docs*

---

### Example 2: Configuration Question

**User**: What environment variables are required?

**Skill calls**: `rag_query_external` with query "What environment variables are required?"

**Response**:

---

**Answer:**

The following environment variables are required for the application:
- `DATABASE_URL`: PostgreSQL connection string
- `JWT_SECRET`: Secret key for JWT signing (min 32 chars)
- `REDIS_URL`: Redis connection for caching (optional in development)

Additional optional variables are documented in the configuration reference.

---

**Sources:**

| Document | Relevance |
|----------|-----------|
| [Environment Configuration](./docs/setup/environment.md) (2156 chars) | 0.92 |
| [Configuration Reference](./docs/reference/config.md) (4521 chars) | 0.71 |

*External docs path: ./docs*

---
```

### Decision Matrix Reference

Create `references/decision-matrix.md`:

```markdown
# Query vs Search Decision Matrix

## Use RAG Query External (`/cdocs:query-external`)

| Scenario | Example |
|----------|---------|
| Open-ended question | "How does X work?" |
| Multiple docs relevant | "What's our approach to authentication?" |
| Need synthesis | "Explain the deployment process" |
| Conceptual understanding | "What patterns do we use for error handling?" |

## Use Search External (`/cdocs:search-external`)

| Scenario | Example |
|----------|---------|
| Specific document | "Find the API rate limits doc" |
| Known title/topic | "Where's the database migration guide?" |
| User says "find"/"search" | "Search for Redis configuration" |
| Browsing available docs | "What docs do we have about testing?" |
```

### MCP Tool Integration

The skill invokes the `rag_query_external` MCP tool (Phase 081):

```json
{
  "tool": "rag_query_external",
  "parameters": {
    "query": "user's question",
    "max_sources": 3,
    "min_relevance_score": 0.7
  }
}
```

Expected response format:
```json
{
  "answer": "Synthesized answer...",
  "sources": [
    {
      "path": "./docs/api/authentication.md",
      "title": "API Authentication Guide",
      "char_count": 3421,
      "relevance_score": 0.89
    }
  ],
  "external_docs_path": "./docs"
}
```

### Error Response Handling

```json
{
  "error": true,
  "code": "EXTERNAL_DOCS_NOT_CONFIGURED",
  "message": "external_docs is not configured in project config...",
  "details": {
    "configFile": ".csharp-compounding-docs/config.json",
    "exampleConfig": { ... }
  }
}
```

---

## Dependencies

### Depends On

- **Phase 081**: rag_query_external MCP Tool - Underlying tool for RAG queries
- **Phase 076**: search_external_docs MCP Tool - Alternative for specific searches
- **Phase 009**: Plugin Directory Structure - Skill file locations
- **Phase 035**: Session State - Project context and external_docs config
- **Phase 029**: Embedding Service - For query embedding generation

### Blocks

- None (end-user skill)

---

## Testing Verification

### Manual Verification Steps

1. **Skill Discovery**
   ```bash
   # Verify skill file exists
   ls -la ${CLAUDE_PLUGIN_ROOT}/skills/cdocs-query-external/SKILL.md
   ```

2. **External Docs Not Configured**
   - Remove `external_docs` from config.json
   - Run `/cdocs:query-external`
   - Verify helpful error message with configuration instructions

3. **Successful Query**
   - Configure `external_docs` in config.json
   - Create test docs in external_docs path
   - Activate project with `/cdocs:activate`
   - Run `/cdocs:query-external` with a question
   - Verify synthesized answer and source attribution

4. **No Results Handling**
   - Query for topic not in external docs
   - Verify helpful "no results" message with suggestions

5. **Follow-up Options**
   - Complete a query
   - Verify decision menu appears
   - Test "load source document" option

### Skill Validation Checklist

- [ ] YAML frontmatter parses correctly
- [ ] `allowed-tools` includes required MCP tool
- [ ] `preconditions` are accurate
- [ ] No `auto-invoke` (manual only)
- [ ] All steps are clear and actionable
- [ ] Error handling covers all tool error codes
- [ ] Examples demonstrate realistic usage
- [ ] Read-only nature is emphasized

---

## Files Created

| File | Purpose |
|------|---------|
| `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-query-external/SKILL.md` | Main skill definition |
| `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-query-external/references/decision-matrix.md` | RAG vs Search guidance |

---

## Key Design Decisions

### 1. Manual Invocation Only

Unlike capture skills, query skills do not auto-invoke on conversation patterns. Users explicitly request queries with `/cdocs:query-external`.

**Rationale**: Queries are intentional actions, not incidental discoveries. Auto-invoking on every question would be disruptive.

### 2. Read-Only Emphasis

The skill repeatedly emphasizes read-only semantics:
- In the description
- In the purpose section
- In the important notes
- No links to capture skills

**Rationale**: External docs are maintained externally. Providing modification options would create confusion about ownership.

### 3. No Linked Document Following

Unlike `/cdocs:query` which follows inter-document links, this skill treats external docs as standalone.

**Rationale**: External docs may not follow the same linking conventions. Attempting to follow links could fail or produce unexpected results.

### 4. Decision Matrix Integration

The skill includes explicit guidance on when to use RAG query vs search.

**Rationale**: Users may not know which tool is appropriate. Clear decision criteria improve the experience.

### 5. Configuration Error Handling

When `external_docs` is not configured, the skill provides:
- Clear explanation of what's missing
- Exact JSON to add to config
- Next steps (re-activate project)

**Rationale**: Configuration errors should be self-service recoverable.

---

## Notes

- This skill complements `/cdocs:search-external` (Phase 092) for specific document lookups
- External docs indexing happens during project activation (Phase 057)
- The `external_docs_path` in responses helps users understand file locations
- Skill assumes Ollama is running for embedding generation
- Min relevance score can be overridden by project config (`semantic_search.min_relevance_score`)
