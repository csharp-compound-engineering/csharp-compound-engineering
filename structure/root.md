# SPEC.md Structure Summary

## What This File Covers

SPEC.md is the **root specification document** for the CSharp Compound Docs plugin (`csharp-compounding-docs`). It defines:

- **Executive Summary**: A Claude Code plugin implementing "compound-engineering" for C#/.NET projects with disk-based markdown storage, RAG/semantic search via MCP server, PostgreSQL+pgvector for vectors, and Ollama for embeddings
- **Complete Workflow Example**: Day 1 knowledge capture (auto-triggered problem documentation) and Day 30 retrieval (RAG query returning synthesized answers with sources)
- **Core Principles**: Capture significant insights only, file system as source of truth, semantic retrieval first, shared Docker infrastructure, multi-tenant isolation, external MCP check-and-warn policy
- **High-Level Architecture**: 17 skills, 4 research agents, MCP server (.NET Generic Host), PostgreSQL+pgvector, Ollama
- **Technology Stack**: C#/.NET, Semantic Kernel, Markdig, QuikGraph, xUnit/Shouldly/Moq, .NET Aspire
- **Repository Structure**: Full project layout including src/, plugins/, spec/, tests/
- **Resolved Decisions**: Embedding model, test frameworks, markdown parser, coverage requirements
- **Excluded Components**: Intentionally omitted agents (review, design, workflow), skills (frontend, Ruby-specific), and utility commands
- **Document Structure Rules**: 500-line limit per file, recursive decomposition into spec/ subdirectories

## Structural Relationships

### Position in Hierarchy
- **Role**: Root/parent document for the entire specification tree
- **Parent**: None (this is the top-level entry point)

### Child Documents (spec/)
Direct children linked from the Sub-Topic Index:
- `spec/doc-types.md` - Documentation type architecture
- `spec/mcp-server.md` - MCP server implementation
- `spec/infrastructure.md` - Docker/infrastructure setup
- `spec/skills.md` - Skills and commands
- `spec/agents.md` - Research agents
- `spec/marketplace.md` - Plugin marketplace
- `spec/configuration.md` - Configuration system
- `spec/testing.md` - Testing strategy
- `spec/observability.md` - Observability/monitoring
- `spec/research-index.md` - Research documentation index

### Grandchild Documents (spec/*/):
- `spec/mcp-server/database-schema.md`, `chunking.md`, `file-watcher.md`, `tools.md`, `ollama-integration.md`, `liquibase-changelog.md`
- `spec/skills/skill-patterns.md`, `capture-skills.md`, `query-skills.md`, `meta-skills.md`, `utility-skills.md`
- `spec/doc-types/built-in-types.md`, `custom-types.md`, `promotion.md`
- `spec/infrastructure/cleanup-app.md`
- `spec/configuration/schema-files.md`
- `spec/testing/aspire-fixtures.md`, `test-independence.md`, `ci-cd-pipeline.md`

### Siblings
- None (root document)
