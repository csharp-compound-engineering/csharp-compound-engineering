---
name: cdocs:search-external
description: Performs read-only semantic search against external project documentation
allowed-tools:
  - Read
preconditions:
  - Project activated via /cdocs:activate
  - external_docs configured in .csharp-compounding-docs/config.json
---

# Search External Documentation Skill

## Intake

This skill accepts a search query for finding relevant documents in external project documentation. The user provides:

- **query** (required): The search query text for semantic matching
- **limit** (optional): Maximum number of results to return (1-100, default: 10)
- **project_name** (optional): Specific external project to search (if multiple configured)
- **min_relevance** (optional): Minimum relevance score threshold (0.0 to 1.0, default: 0.0)

**Note**: This is a **read-only** skill. External documentation cannot be created or modified through this plugin. The assumption is that external docs are maintained via an external process.

## Process

1. **Check Configuration**: Verify that `external_docs` is configured in `.csharp-compounding-docs/config.json`
   - If not configured, inform user and offer guidance on configuration
   - Configuration should specify external project paths and embedding settings
2. **Validate Input**: Ensure the query meets minimum length requirements (2+ characters)
3. **Call MCP Tool**: Invoke the `search_external_docs` MCP tool with the provided parameters
4. **Process Response**: The search system will:
   - Convert the query to embeddings using the configured embedding model
   - Perform vector similarity search against the external document store
   - Rank results by cosine similarity or configured distance metric
   - Return ranked list of matching documents with file paths
5. **Format Results**: Present documents with paths, relevance scores, and snippets
6. **Offer Follow-up**: Provide option to read selected external documents

## Output Format

The skill returns a markdown-formatted response containing:

### Search Summary
- **Query**: The original search text
- **External Project**: Name of the external project searched
- **Results Found**: Total number of matching documents

### Documents Section
For each matching document:
- **Title**: Document name/title
- **Path**: External file path (absolute or relative to external project root)
- **Relevance**: Similarity score (0.0-1.0)
- **Snippet**: Brief content preview

### Configuration Warning (if applicable)
- Instructions for configuring external_docs if not set up

## Examples

### Example 1: Basic External Search
```
User: "Search external docs for API authentication patterns"

Skill invocation:
- query: "API authentication patterns"
- limit: 10

Output:
# Search Results - External Documentation

**Query**: API authentication patterns
**External Project**: MyCompany.Platform
**Results Found**: 4

## Documents

### Authentication Architecture

- **Path**: /external/platform-docs/architecture/authentication.md
- **Relevance**: 0.91

> The platform uses OAuth 2.0 with JWT tokens for API authentication. All requests
> must include a valid bearer token in the Authorization header.

### API Security Guidelines

- **Path**: /external/platform-docs/guides/api-security.md
- **Relevance**: 0.87

> Security best practices for API consumers including token management, refresh
> flows, and rate limiting strategies.

### OAuth Integration Guide

- **Path**: /external/platform-docs/guides/oauth-integration.md
- **Relevance**: 0.83

> Step-by-step guide for integrating with the platform's OAuth 2.0 authorization
> server and obtaining access tokens.

### API Reference - Auth Endpoints

- **Path**: /external/platform-docs/api/auth-endpoints.md
- **Relevance**: 0.79

> Complete reference for authentication-related API endpoints including /token,
> /authorize, and /revoke.
```

### Example 2: External Search - Not Configured
```
User: "Search external documentation for deployment process"

Output:
# External Documentation Not Configured

**Query**: deployment process

External documentation search is not currently configured for this project.

To enable external documentation search, add an `external_docs` section to your
`.csharp-compounding-docs/config.json`:

\`\`\`json
{
  "external_docs": {
    "enabled": true,
    "projects": [
      {
        "name": "MyCompany.Platform",
        "path": "/path/to/external/docs",
        "description": "Platform API documentation"
      }
    ]
  }
}
\`\`\`

Would you like help configuring external documentation access?
```

### Example 3: Filtered External Search
```
User: "Find external docs about error codes, minimum relevance 0.8"

Skill invocation:
- query: "error codes troubleshooting"
- limit: 5
- min_relevance: 0.8

Output:
# Search Results - External Documentation

**Query**: error codes troubleshooting
**External Project**: MyCompany.Platform
**Results Found**: 2

## Documents

### Error Code Reference

- **Path**: /external/platform-docs/reference/error-codes.md
- **Relevance**: 0.94

> Comprehensive reference of all API error codes, including HTTP status codes,
> error messages, and recommended resolution steps.

### Troubleshooting Guide

- **Path**: /external/platform-docs/guides/troubleshooting.md
- **Relevance**: 0.86

> Common issues and their solutions, organized by error code and symptom. Includes
> diagnostic steps and escalation procedures.
```

### Example 4: Decision Matrix Usage

**Use Search External (this skill) when:**
- Query is specific to external project: "Find the external API rate limits doc"
- Looking for specific documentation from dependency projects
- User explicitly asks to "search external docs"
- Discovery of external reference material
- Need to see what external documentation exists

**Use Query External (/cdocs:query-external) when:**
- Question is open-ended about external project: "How does their authentication work?"
- Multiple external docs likely relevant
- Synthesis needed across external sources
- User wants an answer from external docs, not just discovery

## Notes

- This is a manual-invocation skill (no auto-triggers)
- Requires active project context via `/cdocs:activate`
- Requires `external_docs` configuration in `.csharp-compounding-docs/config.json`
- Uses the `search_external_docs` MCP tool for vector similarity search
- **READ-ONLY**: Cannot create, modify, or delete external documentation
- External docs are assumed to be maintained by external processes/teams
- Results are ranked by semantic similarity
- Multiple external projects can be configured and searched
- Useful for discovering relevant documentation from dependencies or related projects
- External document paths may be absolute or relative to configured project root
