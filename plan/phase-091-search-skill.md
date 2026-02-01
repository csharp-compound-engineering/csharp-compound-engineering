# Phase 091: /cdocs:search Skill

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Skills System
> **Prerequisites**: Phase 081 (Query Skill Framework), Phase 072 (Skill Framework Base)

---

## Spec References

This phase implements the `/cdocs:search` skill defined in:

- **spec/skills/query-skills.md** - [/cdocs:search](../spec/skills/query-skills.md#cdocssearch) - Skill specification and decision matrix
- **spec/skills.md** - [Query Skills](../spec/skills.md#query-skills) - Skill categorization and naming conventions
- **spec/mcp-server/tools.md** - [Semantic Search Tool](../spec/mcp-server/tools.md#2-semantic-search-tool) - MCP tool parameters and response format
- **research/building-claude-code-skills-research.md** - Skill authoring patterns and SKILL.md structure
- **research/postgresql-pgvector-research.md** - Vector search and relevance scoring

---

## Objectives

1. Create the `/cdocs:search` skill SKILL.md file with complete YAML frontmatter
2. Implement semantic search workflow via `semantic_search` MCP tool invocation
3. Provide response formatting with relevance scores for ranked results
4. Include decision matrix guidance (when to use search vs query)
5. Design skill for finding specific documents rather than synthesized answers
6. Support optional filtering by doc-type and promotion level

---

## Acceptance Criteria

### SKILL.md Structure

- [ ] Skill file created at `skills/cdocs-search/SKILL.md`
- [ ] YAML frontmatter includes all required fields:
  - [ ] `name: cdocs:search`
  - [ ] `description:` clear description of semantic search purpose
- [ ] Frontmatter includes optional fields as appropriate:
  - [ ] `allowed-tools:` restricted to `mcp__cdocs__semantic_search` MCP tool
- [ ] Skill body contains comprehensive instructions for Claude

### Skill Behavior

- [ ] Skill takes a search query from user input
- [ ] Skill invokes MCP `semantic_search` tool with appropriate parameters
- [ ] Default limit of 10 results (configurable via user input)
- [ ] Default relevance threshold of 0.5 (lower than RAG for broader discovery)
- [ ] Results presented in ranked order by relevance score
- [ ] Each result displays:
  - [ ] Document title
  - [ ] Relative path
  - [ ] Doc-type
  - [ ] Relevance score (percentage or decimal)
  - [ ] Summary (if available)
  - [ ] Promotion level (if non-standard)

### Response Formatting

- [ ] Results formatted as a clear, scannable list
- [ ] Relevance scores displayed prominently (e.g., "92% match")
- [ ] Doc-type shown for context (e.g., "[problem]", "[insight]")
- [ ] Promotion level indicated for important/critical docs
- [ ] Offer to load/display selected documents after presenting results
- [ ] Empty result handling with helpful suggestions

### Decision Matrix Integration

- [ ] Skill instructions include decision matrix guidance:
  - [ ] When to use `/cdocs:search` (specific document lookup, "find the doc about X")
  - [ ] When to suggest `/cdocs:query` instead (open-ended questions needing synthesis)
- [ ] Skill can recognize when user intent better suits RAG query
- [ ] Skill offers to redirect to `/cdocs:query` when appropriate

### MCP Tool Invocation

- [ ] Skill correctly formats `semantic_search` tool call
- [ ] Skill handles tool response JSON parsing
- [ ] Skill handles error responses gracefully (project not activated, etc.)
- [ ] Skill passes optional filters when user specifies:
  - [ ] `doc_types` filter for specific document types
  - [ ] `promotion_levels` filter for visibility levels

---

## Implementation Notes

### SKILL.md File Content

```markdown
---
name: cdocs:search
description: Semantic search for specific documents in compounding docs. Use when looking for a particular document, finding "the doc about X", or when the user explicitly asks to "find" or "search" documentation. Best for specific document lookup rather than synthesized answers.
allowed-tools: mcp__cdocs__semantic_search
---

# Semantic Search for Compounding Docs

You are helping the user find specific documents in their compounding documentation using semantic search.

## When to Use This Skill vs /cdocs:query

**Use THIS skill (/cdocs:search) when:**
- User asks to "find" or "search" for something specific
- Looking for one particular document
- Query is specific: "Find the doc about the N+1 query fix"
- User wants to browse/review documents on a topic
- User wants to see what documentation exists

**Suggest /cdocs:query instead when:**
- Question is open-ended: "How does X work?"
- Multiple documents likely need synthesis
- User needs an answer, not a document reference
- Question spans multiple topics

## Search Workflow

### Step 1: Understand the Search Intent

Ask clarifying questions if needed:
- What specific topic or problem are they looking for?
- Do they need a particular doc-type (problem, insight, codebase, tool, style)?
- Are they looking for critical/important documents specifically?

### Step 2: Execute Search

Call the `semantic_search` MCP tool with the user's query:

```json
{
  "query": "<user's search query>",
  "limit": 10,
  "min_relevance_score": 0.5
}
```

Optional filters based on user input:
- `doc_types`: ["problem", "insight", "codebase", "tool", "style"] - filter to specific types
- `promotion_levels`: ["standard", "important", "critical"] - filter by visibility

### Step 3: Present Results

Format results as a clear, scannable list:

**Search Results for: "<query>"**

1. **[title]** - [relevance score]% match
   - Path: `[relative_path]`
   - Type: [doc_type]
   - Summary: [summary if available]
   - [CRITICAL] or [IMPORTANT] if non-standard promotion

2. **[title]** - [relevance score]% match
   ...

**[N] results found** (showing top [limit])

### Step 4: Offer Follow-up Actions

After presenting results, offer:
- "Would you like me to open any of these documents?"
- "Should I search with different terms?"
- "Would a RAG query (`/cdocs:query`) be more helpful to synthesize an answer?"

## Handling Empty Results

If no results match (or all below threshold):

> I couldn't find any documents matching "[query]" with sufficient relevance.
>
> **Suggestions:**
> - Try different search terms
> - Check if the compounding docs folder has been indexed
> - Consider if this knowledge has been captured yet
> - Use `/cdocs:query` for broader exploration

## Handling Errors

If the MCP tool returns an error:

**PROJECT_NOT_ACTIVATED**: "Please activate a project first. The compounding docs system needs to know which project's documentation to search."

**Other errors**: Present the error message and suggest troubleshooting steps.

## Search Tips

- Be specific: "database connection pool exhaustion" works better than "database issues"
- Use domain terms: Include specific technology names, error codes, or patterns
- Filter when possible: If user knows the doc-type, filter to reduce noise
```

### Skill Directory Structure

```
skills/
└── cdocs-search/
    └── SKILL.md
```

### Response Formatting Example

When the skill receives search results from the MCP tool:

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

The skill formats this as:

```
**Search Results for: "database connection pool"**

1. **Database Connection Pool Exhaustion** - 92% match [CRITICAL]
   - Path: `problems/db-pool-exhaustion-20250115.md`
   - Type: problem
   - Summary: Connection pool exhaustion caused by missing disposal in background jobs
   - Date: 2025-01-15

**1 result found**

Would you like me to open this document or search with different terms?
```

### Decision Matrix for RAG vs Search

The skill includes embedded logic to help Claude determine when to suggest the alternative skill:

| User Intent | Indicators | Recommended Skill |
|-------------|------------|-------------------|
| Find specific doc | "find", "search", "where is", "show me" | `/cdocs:search` |
| Get an answer | "how", "why", "what", "explain" | `/cdocs:query` |
| Browse topic | "what docs do we have about" | `/cdocs:search` |
| Synthesize knowledge | "summarize our understanding of" | `/cdocs:query` |
| Debug with context | "I'm seeing this error" | `/cdocs:query` |
| Locate reference | "the doc that mentions" | `/cdocs:search` |

---

## Dependencies

### Depends On

- **Phase 081**: Query Skill Framework - shared patterns for query-type skills
- **Phase 072**: Skill Framework Base - SKILL.md infrastructure and skill loading
- **Phase 050**: Vector Search Service - provides semantic search capability
- **Phase 009**: Plugin Directory Structure - skill file locations

### Blocks

- **Phase 092+**: Search External skill (uses similar patterns)
- **Phase 095+**: Skill testing infrastructure

---

## Verification Steps

After completing this phase, verify:

1. **Skill File Exists**: `skills/cdocs-search/SKILL.md` created with proper structure
2. **YAML Frontmatter Valid**: Frontmatter parses correctly with required fields
3. **Tool Reference Correct**: `mcp__cdocs__semantic_search` matches actual MCP tool name
4. **Decision Matrix Clear**: Instructions clearly differentiate search vs query use cases
5. **Response Format Clear**: Example formatting matches actual MCP tool response structure
6. **Error Handling Complete**: All standard error codes have handling instructions

### Manual Verification

```bash
# 1. Verify skill file exists
ls -la skills/cdocs-search/SKILL.md

# 2. Validate YAML frontmatter
head -20 skills/cdocs-search/SKILL.md

# 3. Test skill invocation (in Claude Code)
/cdocs:search database connection pool

# 4. Verify skill appears in help
/help cdocs:search
```

### Skill Invocation Test Cases

| Test Case | Input | Expected Behavior |
|-----------|-------|-------------------|
| Basic search | `/cdocs:search connection pool` | Returns ranked results |
| Filtered search | `/cdocs:search api design --type codebase` | Filters to codebase docs only |
| No results | `/cdocs:search xyznonexistent123` | Shows helpful suggestions |
| Empty query | `/cdocs:search` | Prompts for search query |
| Decision redirect | `/cdocs:search how does caching work` | Suggests using `/cdocs:query` |

---

## Files to Create/Modify

### New Files

| File | Purpose |
|------|---------|
| `skills/cdocs-search/SKILL.md` | Main skill definition with frontmatter and instructions |

### Related Files (Reference Only)

| File | Relationship |
|------|--------------|
| `skills/cdocs-query/SKILL.md` | Sister skill for RAG queries (decision matrix counterpart) |
| `src/CompoundDocs.McpServer/Tools/SearchTools.cs` | MCP tool implementation this skill invokes |

---

## Notes

- The skill is intentionally simple - it's a thin wrapper around the MCP `semantic_search` tool
- The value comes from Claude's natural language processing of the query and result formatting
- Decision matrix is critical for guiding users to the right tool for their needs
- Response formatting should be consistent with how Claude typically presents lists
- Relevance scores are converted to percentages for readability (0.92 -> 92%)
- The skill should feel conversational, not robotic - Claude adds context and offers follow-ups
- Promotion level indicators (CRITICAL, IMPORTANT) help users prioritize results
- Empty result handling prevents user confusion when no docs match

---

## Open Questions

1. Should the skill support multi-term queries with AND/OR logic? (Currently relies on embedding similarity)
2. Should results include character count for context on document length?
3. Should the skill auto-expand results if initial search returns few matches?
