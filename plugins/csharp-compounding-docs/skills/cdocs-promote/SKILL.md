---
name: cdocs:promote
description: Change document's visibility level to increase or decrease relevance in RAG search results
allowed-tools:
  - Read
  - Write
  - Bash
preconditions:
  - Project activated via /cdocs:activate
  - Document exists in ./csharp-compounding-docs/ (external docs cannot be promoted)
---

# Promote Documentation Skill

## Purpose

Promote or demote a document's visibility level to control its relevance boost in RAG search results. This affects how prominently a document appears when queried.

## Invocation

**Manual only** - User wants to increase or decrease a document's visibility.

**Use Cases**:
- Mark critical architecture decisions for high visibility
- Boost frequently needed troubleshooting guides
- Reduce visibility of outdated but retained documentation
- Ensure important patterns appear in search results

## Process

### Step 1: Identify Document

If document path not provided by user:

1. Prompt user for document path or search criteria
2. Optionally search via `/cdocs:search` to help user locate document
3. Validate document exists in `./csharp-compounding-docs/`

**Validation**:
- Document must exist
- Document must be in project docs (not external)
- Path must be relative to project root

### Step 2: Display Current Status

Load document metadata and display:

```
Current Document Status
=======================

Path: ./csharp-compounding-docs/problems/n-plus-one-query-20250120.md
Title: Resolved N+1 Query Performance Issue
Current Level: standard (1.0x boost)
Created: 2025-01-20
Doc Type: problem
```

### Step 3: Present Promotion Options

Show available promotion levels:

```
Promotion Levels
================

1. standard (1.0x) - Normal relevance scoring
2. important (1.5x) - Boosted in search results
3. critical (2.0x) - Maximum visibility

Current level: standard

Select new level (1-3):
```

**Level Descriptions**:
- **standard**: Default visibility, normal relevance scoring
- **important**: 1.5x relevance boost, appears higher in search results
- **critical**: 2.0x relevance boost, maximum visibility, treated as required reading

### Step 4: Confirm Change

Display proposed change:

```
Confirm Promotion
=================

Document: Resolved N+1 Query Performance Issue
Change: standard (1.0x) → critical (2.0x)

This will:
1. Update YAML frontmatter in the markdown file
2. Update promotion level in the database
3. Apply 2.0x boost to future RAG queries

Proceed? (y/N)
```

**BLOCKING**: Wait for explicit user confirmation.

### Step 5: Update File Frontmatter

Update the document's YAML frontmatter:

**Before**:
```yaml
---
doc_type: problem
title: Resolved N+1 Query Performance Issue
created: 2025-01-20
---
```

**After**:
```yaml
---
doc_type: problem
title: Resolved N+1 Query Performance Issue
created: 2025-01-20
promotion_level: critical
---
```

Use **Edit** tool to update the frontmatter.

### Step 6: Update Database

Call MCP `update_promotion_level` tool to update the database record.

**Parameters**:
- `document_path`: string (required) - Relative path to document
- `promotion_level`: enum (required) - `standard`, `important`, or `critical`

**Response**:
```json
{
  "status": "updated",
  "document_path": "problems/n-plus-one-query-20250120.md",
  "previous_level": "standard",
  "new_level": "critical",
  "boost_factor": 2.0
}
```

### Step 7: Report Success

Display success message:

```
Document Promoted
=================

✓ File updated: ./csharp-compounding-docs/problems/n-plus-one-query-20250120.md
✓ Database updated

Document: Resolved N+1 Query Performance Issue
Previous Level: standard (1.0x)
New Level: critical (2.0x)

This document will now receive a 2.0x boost in RAG search results.
```

### Step 8: Post-Promotion Options

Present decision menu:

```
What's next?
============

1. Continue workflow
2. Promote another document
3. View document
4. Search for related documents
5. Done
```

## Restrictions

**Cannot Promote**:
- External documentation (read-only)
- Documents outside `./csharp-compounding-docs/`
- Non-existent documents

**Error Handling**:
- If external doc: "Cannot promote external documentation. External docs are read-only."
- If not found: "Document not found. Use /cdocs:search to locate documents."
- If database error: Display error and suggest retry

## MCP Tool Reference

**Tool**: `update_promotion_level`

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `document_path` | string | Yes | Relative path to document from project root |
| `promotion_level` | enum | Yes | `standard`, `important`, or `critical` |

## Promotion Level Guidelines

**When to use standard**:
- Default for most documentation
- General reference materials
- Non-critical how-to guides

**When to use important**:
- Frequently referenced patterns
- Common troubleshooting guides
- Key architectural decisions

**When to use critical**:
- Core system architecture
- Critical security considerations
- Must-read onboarding documentation
- Breaking change announcements

## Example Workflow

```
User: /cdocs:promote