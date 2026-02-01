# Phase 090: /cdocs:query Skill

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Skills System
> **Prerequisites**: Phase 081 (Skills Framework), Phase 071 (RAG Query Tool)

---

## Spec References

This phase implements the `/cdocs:query` skill as defined in:

- **spec/skills/query-skills.md** - [/cdocs:query specification](../spec/skills/query-skills.md#cdocsquery)
- **spec/skills.md** - [Query Skills overview](../spec/skills.md#query-skills)
- **spec/mcp-server/tools.md** - [RAG Query Tool](../spec/mcp-server/tools.md#1-rag-query-tool) parameters and response format
- **research/building-claude-code-skills-research.md** - Skill authoring patterns and YAML frontmatter
- **research/claude-code-skills-research.md** - MCP tool integration and best practices

---

## Objectives

1. Create the `/cdocs:query` skill SKILL.md file with proper YAML frontmatter
2. Define instructions for RAG-based question answering over compounding docs
3. Document MCP `rag_query` tool invocation patterns and parameter guidance
4. Specify response formatting for synthesized answers with source attribution
5. Include decision matrix guidance for when to use RAG vs semantic search
6. Position as the primary skill for open-ended questions about captured knowledge

---

## Acceptance Criteria

### SKILL.md Structure

- [ ] Skill file created at `.claude/skills/cdocs-query/SKILL.md`
- [ ] YAML frontmatter includes all required fields:
  - [ ] `name: cdocs-query` (matches `/cdocs:query` invocation)
  - [ ] `description` clearly states purpose and when to use
  - [ ] Description written in third person
  - [ ] Description mentions RAG, question answering, and knowledge retrieval
- [ ] Skill body uses clear markdown formatting with sections
- [ ] Skill body under 500 lines (targeting ~100-150 lines for conciseness)

### Invocation Configuration

- [ ] `disable-model-invocation: false` (allow Claude to auto-invoke)
- [ ] `user-invocable: true` (user can invoke via `/cdocs:query`)
- [ ] `argument-hint: [question]` for autocomplete guidance
- [ ] No tool restrictions (needs MCP tool access)

### MCP Tool Integration

- [ ] Instructions reference `rag_query` MCP tool explicitly
- [ ] Parameter guidance documented:
  - [ ] `query` - the user's natural language question
  - [ ] `doc_types` - optional filter by doc-type (problem, insight, etc.)
  - [ ] `max_sources` - default 3, guidance on when to increase
  - [ ] `min_relevance_score` - default 0.7, guidance on adjusting
  - [ ] `min_promotion_level` - optional filter for important/critical docs
  - [ ] `include_critical` - default true, always include critical docs
- [ ] Tool response handling documented:
  - [ ] Extract `answer` field for the synthesized response
  - [ ] Format `sources` array with paths, titles, and relevance scores
  - [ ] Handle `linked_docs` array when present

### Response Formatting

- [ ] Instructions specify output format for answers
- [ ] Source attribution format defined (document paths, titles)
- [ ] Relevance scores displayed (percentage or decimal)
- [ ] Offer to load source documents when user wants more detail
- [ ] Handle "no relevant documents found" case gracefully

### Decision Matrix

- [ ] Clear guidance on when to use `/cdocs:query` vs `/cdocs:search`:
  - [ ] Use RAG query for open-ended questions ("How does X work?")
  - [ ] Use RAG query when multiple documents likely relevant
  - [ ] Use RAG query when synthesis needed across sources
  - [ ] Use search when looking for one specific document
  - [ ] Use search when user explicitly asks to "find" or "search"
- [ ] Examples of good query questions included

### Content Guidelines

- [ ] Instructions emphasize answering from captured knowledge
- [ ] Guidance on handling insufficient context in docs
- [ ] Mention that critical docs are always included in context
- [ ] Explain promotion level filtering for specialized queries

---

## Implementation Notes

### SKILL.md File Location

Create the skill at `.claude/skills/cdocs-query/SKILL.md`:

```
.claude/
  skills/
    cdocs-query/
      SKILL.md
```

### Complete SKILL.md Content

```markdown
---
name: cdocs-query
description: |
  Query compounding documentation using RAG (Retrieval-Augmented Generation).
  Synthesizes answers from captured knowledge with source attribution.
  Use when asking open-ended questions about the codebase, solved problems,
  architectural decisions, tools, or coding standards.
argument-hint: [question]
---

# /cdocs:query - RAG Query for Compounding Docs

Query the captured compounding documentation to answer questions using RAG synthesis.

## When to Use This Skill

Use `/cdocs:query` when:
- Question is open-ended ("How does the authentication system work?")
- Multiple documents are likely relevant to the answer
- Synthesis across multiple sources would be valuable
- User wants an explanation, not just document references

Use `/cdocs:search` instead when:
- Looking for one specific document
- User explicitly asks to "find" or "search"
- Need a list of matching documents without synthesis

## How to Use

1. **Take the user's question** - ensure it's a clear, answerable question
2. **Call the MCP `rag_query` tool** with appropriate parameters
3. **Present the synthesized answer** with source attribution
4. **Offer to load source documents** if user wants more detail

## MCP Tool: `rag_query`

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `query` | string | (required) | The user's natural language question |
| `doc_types` | array | all | Filter to specific doc-types: `problem`, `insight`, `codebase`, `tool`, `style`, or custom types |
| `max_sources` | integer | 3 | Maximum documents to use for context |
| `min_relevance_score` | float | 0.7 | Minimum semantic similarity threshold (0.0-1.0) |
| `min_promotion_level` | enum | `standard` | Only include docs at or above: `standard`, `important`, `critical` |
| `include_critical` | boolean | true | Always prepend critical docs regardless of relevance score |

### Parameter Guidance

- **Increase `max_sources`** to 5-7 for broad questions spanning multiple concerns
- **Decrease `min_relevance_score`** to 0.5-0.6 if getting too few results
- **Filter `doc_types`** when the question clearly relates to one category:
  - Problems/bugs/errors: `["problem"]`
  - Architecture/design: `["codebase", "insight"]`
  - Tool/library usage: `["tool"]`
  - Coding conventions: `["style"]`
- **Set `min_promotion_level: important`** to focus on high-value documentation

### Example Tool Invocation

```
Call rag_query with:
- query: "How do we handle database connection pooling?"
- doc_types: ["problem", "codebase", "tool"]
- max_sources: 5
```

## Response Formatting

Present the answer in this format:

```
## Answer

[Synthesized answer from the RAG response]

## Sources

| Document | Relevance |
|----------|-----------|
| [Title](path) | 92% |
| [Title](path) | 78% |

Would you like me to load any of these source documents for more detail?
```

### Handling Linked Documents

If the response includes `linked_docs`, mention them:

```
## Related Documents

The answer also references these linked documents:
- [Title](path) - linked from [Source Title]
```

### Handling No Results

If no relevant documents are found:

```
I searched the compounding documentation but couldn't find relevant information about [topic].

This could mean:
- No documentation has been captured for this topic yet
- Try rephrasing the question
- Consider using `/cdocs:search` with broader terms

Would you like to capture new documentation about this topic?
```

## Example Questions (Good for RAG)

These types of questions work well with `/cdocs:query`:

- "How does our authentication flow work?"
- "What problems have we encountered with the database?"
- "What are our coding conventions for error handling?"
- "How should I configure the caching layer?"
- "What architectural decisions have we made about the API?"

## Integration Notes

- **Project must be activated** via `/cdocs:activate` before querying
- **Critical documents** are always included in context when `include_critical: true`
- **Promotion levels** allow filtering to high-value documentation
- **Source attribution** enables verification of synthesized answers
```

### Skill Directory Structure

```
.claude/
  skills/
    cdocs-query/
      SKILL.md         # Main skill file (above content)
```

No additional files needed for this skill - the MCP tool does the heavy lifting.

### Key Design Decisions

1. **No `allowed-tools` restriction**: The skill needs access to MCP tools and potentially file reading for follow-up actions.

2. **Auto-invocation enabled**: Questions about documentation should trigger this skill automatically based on semantic matching.

3. **Concise instructions**: Claude already understands RAG and tool invocation - focus on parameter guidance and formatting.

4. **Decision matrix included**: Helps Claude and users understand when to use query vs search.

5. **Source attribution emphasized**: Critical for trust and verification of synthesized answers.

---

## Dependencies

### Depends On

- **Phase 081**: Skills Framework - Provides skill directory structure and discovery mechanism
- **Phase 071**: RAG Query Tool - Provides the `rag_query` MCP tool implementation
- **Phase 032**: RAG Generation Service - Backend for answer synthesis
- **Phase 050**: Vector Search Service - Backend for document retrieval

### Blocks

- User documentation for query skill
- Integration testing with real compounding docs
- `/cdocs:query-external` skill (similar pattern for external docs)

---

## Verification Steps

After completing this phase, verify:

1. **Skill Discovery**
   ```bash
   # Check skill file exists
   ls -la .claude/skills/cdocs-query/SKILL.md

   # Verify YAML frontmatter is valid
   # (Claude Code will report parse errors on startup)
   ```

2. **Skill Invocation**
   - Start Claude Code in a project with compounding docs
   - Run `/cdocs:query How does X work?`
   - Verify skill instructions are loaded
   - Verify MCP tool is invoked correctly

3. **Response Formatting**
   - Answer is presented with clear formatting
   - Sources are listed with titles and relevance scores
   - Follow-up offer to load source documents

4. **Parameter Handling**
   - Test with doc_type filter
   - Test with increased max_sources
   - Test with min_promotion_level filter

5. **Edge Cases**
   - Query with no matching documents
   - Query before project activation
   - Query with very broad terms (many results)

---

## Testing Notes

### Manual Testing Checklist

- [ ] Invoke via `/cdocs:query` command
- [ ] Verify auto-invocation on question about docs
- [ ] Check response formatting matches spec
- [ ] Test with various doc_type filters
- [ ] Test with promoted documents
- [ ] Verify source attribution accuracy
- [ ] Test "no results" handling

### Integration Test Scenarios

```gherkin
Feature: /cdocs:query skill

  Scenario: Query about solved problems
    Given the project has compounding docs with problem documents
    When I run "/cdocs:query What database issues have we solved?"
    Then the skill invokes rag_query with query parameter
    And the response includes synthesized answer
    And the response includes source attribution

  Scenario: Query with doc-type filter
    Given the project has multiple doc-types
    When I run "/cdocs:query What are our coding conventions?" with context about styles
    Then the skill filters to style and insight doc-types
    And the response focuses on convention-related docs

  Scenario: Query with no matching documents
    Given the project has no documentation about topic X
    When I run "/cdocs:query How does topic X work?"
    Then the response indicates no relevant documents found
    And suggests capturing new documentation
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `.claude/skills/cdocs-query/SKILL.md` | Create | Main skill file with instructions |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Skill not auto-invoking | Clear, keyword-rich description for semantic matching |
| Poor answer quality | Guidance on parameter tuning for better results |
| Missing source attribution | Explicit formatting instructions in skill |
| Confusion with search | Decision matrix clearly distinguishes use cases |
| MCP tool errors | Instructions to handle error responses gracefully |

---

## Notes

- The skill is intentionally concise - Claude understands RAG and doesn't need verbose explanations
- Parameter guidance is the most valuable content - helps users tune queries
- Response formatting ensures consistent, professional output
- Decision matrix prevents confusion between query and search skills
- Source attribution builds trust in synthesized answers
- Follow-up offers (load source docs) improve user experience
