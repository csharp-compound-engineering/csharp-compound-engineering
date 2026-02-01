# Phase 092: /cdocs:search-external Skill

> **Status**: NOT_STARTED
> **Effort Estimate**: 3-4 hours
> **Category**: Skills System
> **Prerequisites**: Phase 081 (Skills Infrastructure), Phase 075 (search_external_docs MCP Tool)

---

## Spec References

This phase implements the `/cdocs:search-external` skill defined in:

- **spec/skills/query-skills.md** - [/cdocs:search-external](../spec/skills/query-skills.md#cdocssearch-external) - Skill specification
- **spec/skills.md** - [Query Skills](../spec/skills.md#query-skills) - Skill categorization
- **spec/skills/skill-patterns.md** - Common skill implementation patterns
- **spec/mcp-server/tools.md** - [5. Search External Docs Tool](../spec/mcp-server/tools.md#5-search-external-docs-tool) - MCP tool parameters

---

## Objectives

1. Create SKILL.md file for the `/cdocs:search-external` skill
2. Implement external documentation search capability via MCP tool invocation
3. Enforce `external_docs` configuration precondition
4. Emphasize read-only behavior (no modification of external docs)
5. Provide clear external source attribution in search results
6. Guide users to configure external docs if not already set up

---

## Acceptance Criteria

### Skill File Structure

- [ ] Skill directory created at `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-search-external/`
- [ ] SKILL.md file contains valid YAML frontmatter
- [ ] Skill name is `cdocs:search-external`
- [ ] Description clearly states purpose: "Search external project documentation (read-only)"

### YAML Frontmatter

- [ ] `name: cdocs:search-external`
- [ ] `description` clearly describes read-only external doc search
- [ ] `allowed-tools` includes MCP tool reference: `mcp__compounding-docs__search_external_docs`
- [ ] No `auto-invoke` configured (manual invocation only)
- [ ] Precondition documented: `external_docs` must be configured

### Skill Behavior

- [ ] Step 1: Check for external docs configuration
  - [ ] If not configured, inform user and offer help to configure
  - [ ] If configured, proceed to search
- [ ] Step 2: Accept search query from user
  - [ ] Support natural language queries
  - [ ] Allow optional limit parameter
- [ ] Step 3: Invoke MCP `search_external_docs` tool
  - [ ] Pass query, limit, and min_relevance_score parameters
- [ ] Step 4: Present search results
  - [ ] Display file paths (relative to external_docs root)
  - [ ] Show relevance scores
  - [ ] Show document titles and summaries
  - [ ] Indicate total matches found
- [ ] Step 5: Offer to load selected documents
  - [ ] Allow user to select document(s) to view in full

### Read-Only Emphasis

- [ ] SKILL.md explicitly states external docs are read-only
- [ ] No tools for modifying external documentation included in `allowed-tools`
- [ ] Clear note that external docs are maintained via external processes
- [ ] No capture, create, or edit operations available

### External Source Attribution

- [ ] Search results include `external_docs_path` from response
- [ ] Each result clearly shows source path
- [ ] Results differentiated from compounding docs (not promotable)

### Error Handling

- [ ] Handle `EXTERNAL_DOCS_NOT_CONFIGURED` error gracefully
- [ ] Provide configuration instructions when external docs not set up
- [ ] Handle empty search results with helpful messaging

---

## Implementation Notes

### SKILL.md Content

```yaml
---
name: cdocs:search-external
description: Search external project documentation using semantic similarity. Read-only - external docs cannot be modified through this plugin. Requires external_docs to be configured in project config.
allowed-tools:
  - mcp__compounding-docs__search_external_docs
  - Read
preconditions:
  - Project activated via /cdocs:activate
  - external_docs configured in .csharp-compounding-docs/config.json
---

# Search External Documentation

This skill searches external project documentation that has been indexed for semantic search. External documentation is **read-only** - it cannot be created or modified through compounding docs.

## Intake

You need a search query to find relevant external documentation.

**Ask the user**:
> What would you like to search for in the external documentation?

## Process

### Step 1: Verify External Docs Configuration

Before searching, verify that external docs are configured:

**If external_docs is NOT configured**:
> External documentation is not configured for this project.
>
> To configure external docs, add the following to `.csharp-compounding-docs/config.json`:
> ```json
> {
>   "external_docs": {
>     "path": "./docs",
>     "include_patterns": ["**/*.md"],
>     "exclude_patterns": ["**/node_modules/**"]
>   }
> }
> ```
>
> Would you like help configuring external documentation?

**If configured, proceed to Step 2.**

### Step 2: Execute Search

Call the MCP tool with the user's query:

```
Tool: search_external_docs
Parameters:
  - query: [user's search query]
  - limit: 10 (or user-specified)
  - min_relevance_score: 0.7 (or from config)
```

### Step 3: Present Results

Display search results with clear attribution:

```
## External Documentation Search Results

**Source**: `{external_docs_path}`
**Query**: "{query}"
**Found**: {total_matches} matches

| # | Document | Relevance | Summary |
|---|----------|-----------|---------|
| 1 | [Title](path) | 0.92 | Brief summary... |
| 2 | [Title](path) | 0.85 | Brief summary... |
```

### Step 4: Offer Document Loading

After presenting results:

> Would you like to view any of these documents in full?
> Enter a number (1-N) or "none" to continue.

If user selects a document:
- Use Read tool to load the full document
- Display content with clear "External Documentation" header

## Important Notes

1. **Read-Only**: External documentation cannot be modified through this skill. Changes to external docs must be made through your normal documentation workflow.

2. **No Promotion**: External documents cannot be promoted (standard/important/critical levels apply only to compounding docs).

3. **Separate Index**: External docs are indexed separately from compounding docs. The index refreshes when:
   - Project is activated
   - File changes detected in external docs folder

4. **Chunking**: Large external documents (>500 lines) are automatically chunked for better search relevance.

## Decision Matrix

Use this skill when:
- Looking for specific external documentation
- Need to find reference material by topic
- User says "find", "search", "locate" regarding project docs

Use `/cdocs:query-external` instead when:
- Question is open-ended
- Synthesis across multiple docs needed
- User wants an answer, not a document list
```

### Skill Directory Structure

```
${CLAUDE_PLUGIN_ROOT}/skills/cdocs-search-external/
├── SKILL.md              # Main skill definition (above)
└── references/
    └── config-example.md # Example external_docs configuration
```

### Example Configuration Reference

Create `references/config-example.md`:

```markdown
# External Docs Configuration Examples

## Basic Configuration

```json
{
  "external_docs": {
    "path": "./docs"
  }
}
```

## With Include/Exclude Patterns

```json
{
  "external_docs": {
    "path": "./documentation",
    "include_patterns": [
      "**/*.md",
      "**/*.mdx",
      "**/README"
    ],
    "exclude_patterns": [
      "**/node_modules/**",
      "**/.git/**",
      "**/vendor/**"
    ]
  }
}
```

## Multiple Documentation Roots (Future)

Note: Multiple external_docs paths are not currently supported. Configure a single root path that contains all external documentation.
```

### MCP Tool Integration

The skill invokes the `search_external_docs` MCP tool with these parameters:

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `query` | string | Yes | - | Semantic search query |
| `limit` | integer | No | 10 | Maximum results to return |
| `min_relevance_score` | float | No | 0.7 | Minimum similarity threshold |

Response format handled by the skill:

```json
{
  "results": [
    {
      "path": "./docs/architecture/database-design.md",
      "title": "Database Design Guide",
      "summary": "Overview of database schema...",
      "char_count": 4521,
      "relevance_score": 0.85
    }
  ],
  "total_matches": 1,
  "external_docs_path": "./docs"
}
```

### Error Response Handling

Handle the `EXTERNAL_DOCS_NOT_CONFIGURED` error:

```json
{
  "error": true,
  "code": "EXTERNAL_DOCS_NOT_CONFIGURED",
  "message": "external_docs not configured in project config",
  "details": {
    "config_path": ".csharp-compounding-docs/config.json"
  }
}
```

When this error is received, the skill should:
1. Present the error message to the user
2. Offer configuration guidance
3. Optionally help edit the config file

---

## Dependencies

### Depends On

- Phase 081: Skills Infrastructure (skill directory structure, discovery)
- Phase 075: search_external_docs MCP Tool (backend search capability)
- Phase 010: Project Configuration (config.json structure)
- Phase 044: External Document Model (external docs data model)
- Phase 049: External Repository (external docs storage)

### Blocks

- Phase 093: /cdocs:query-external Skill (uses similar patterns)

---

## Verification Steps

After completing this phase, verify:

1. **Skill Discovery**: `/cdocs:search-external` appears in available skills
2. **Precondition Check**: Skill reports missing external_docs configuration appropriately
3. **Search Execution**: Skill successfully invokes MCP tool and displays results
4. **Source Attribution**: Results clearly show external_docs_path
5. **Read-Only**: No modification capabilities present
6. **Document Loading**: Can load and display selected external documents

### Manual Verification

```bash
# Verify skill is discoverable
claude-code /help

# Test without external_docs configured (should show setup guidance)
/cdocs:search-external

# Configure external_docs, then test search
/cdocs:search-external "API authentication"
```

### Test Scenarios

1. **No External Docs Configured**
   - Invoke skill
   - Verify configuration guidance is displayed
   - Verify offer to help configure

2. **Search with Results**
   - Configure external_docs with sample docs
   - Search for known content
   - Verify results include path, title, summary, relevance
   - Verify external_docs_path attribution

3. **Search with No Results**
   - Search for non-existent term
   - Verify helpful "no results" message

4. **Document Loading**
   - Search and select a document
   - Verify full document displayed with external attribution

---

## Files to Create

### New Files

| File | Purpose |
|------|---------|
| `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-search-external/SKILL.md` | Main skill definition |
| `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-search-external/references/config-example.md` | Configuration examples |

### Modified Files

| File | Changes |
|------|---------|
| None | This is a new skill with no modifications to existing files |

---

## Notes

- This skill is intentionally read-only to maintain clear separation between compounding docs (managed by this plugin) and external docs (managed externally)
- The skill does not auto-invoke - users must explicitly request external doc search
- External docs cannot be promoted because the promotion system is for compounding docs only
- If users need to modify external docs, they should use their normal documentation workflow outside this plugin
- The skill pairs with `/cdocs:query-external` which provides RAG synthesis instead of simple search
