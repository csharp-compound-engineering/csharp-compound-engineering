# Spec Structure

This document provides a navigational index to summaries of all specification files and their structural relationships. Detailed summaries are organized by major spec areas in `structure/`. When executing the plan creation process, sub agents responsible for individual phases should read this file and only load the elements of this spec structure doc and corresponding SPEC documents that are pertinent. The spec itself is quite large and so is this structure document. Conserve context.

---

## Root Specification

The root document defining the entire plugin: executive summary, core principles, high-level architecture, technology stack, repository structure, and document decomposition rules. Load only if you need the top-level overview or to understand how all specs relate.

See [structure/root.md](./structure/root.md)

---

## Doc-Types

Covers how the plugin categorizes institutional knowledge into doc-types (problem, insight, codebase, tool, style), including YAML frontmatter schemas, trigger phrase classification, promotion levels, and custom type creation. Load if working on document classification, capture skills, or schema validation.

**Includes:** `spec/doc-types.md`, `built-in-types.md`, `custom-types.md`, `promotion.md`

See [structure/doc-types.md](./structure/doc-types.md)

---

## MCP Server

The .NET Generic Host application serving as the core backend: 9 MCP tools (RAG query, semantic search, indexing, etc.), file watcher sync, document chunking, Ollama integration, PostgreSQL/pgvector schema, and Liquibase migrations. Load if implementing or debugging backend functionality.

**Includes:** `spec/mcp-server.md`, `tools.md`, `database-schema.md`, `chunking.md`, `file-watcher.md`, `ollama-integration.md`, `liquibase-changelog.md`

See [structure/mcp-server.md](./structure/mcp-server.md)

---

## Skills

All 17 `/cdocs:` prefixed skills organized into four categories: Capture (5 skills for creating docs), Query (4 skills for retrieval), Meta (3 system-level skills), and Utility (5 management skills). Includes the distributed capture pattern architecture and skill implementation patterns. Load if working on skill behavior or adding new skills.

**Includes:** `spec/skills.md`, `skill-patterns.md`, `capture-skills.md`, `query-skills.md`, `meta-skills.md`, `utility-skills.md`

See [structure/skills.md](./structure/skills.md)

---

## Infrastructure

Docker Compose stack with PostgreSQL (pgvector/Liquibase) and Ollama, PowerShell launcher scripts, directory structure, port assignments, and the cleanup console app for orphaned data removal. Load if working on Docker setup, deployment, or infrastructure configuration.

**Includes:** `spec/infrastructure.md`, `cleanup-app.md`

See [structure/infrastructure.md](./structure/infrastructure.md)

---

## Testing

Complete testing strategy: unit/integration/E2E test categories, xUnit/Moq/Shouldly stack, 100% coverage enforcement, test independence requirements, .NET Aspire fixtures, and GitHub Actions CI/CD pipeline. Load if writing tests or configuring test infrastructure.

**Includes:** `spec/testing.md`, `test-independence.md`, `ci-cd-pipeline.md`, `aspire-fixtures.md`

See [structure/testing.md](./structure/testing.md)

---

## Configuration

Two-tier configuration system: global config (`~/.claude/.csharp-compounding-docs/`) for infrastructure settings and project config (`.csharp-compounding-docs/config.json`) for project-specific RAG parameters, external docs, and custom doc-types. Load if working on configuration parsing or schema validation.

**Includes:** `spec/configuration.md`, `schema-files.md`

See [structure/configuration.md](./structure/configuration.md)

---

## Agents

Four research agents: best-practices-researcher, framework-docs-researcher, git-history-analyzer, and repo-research-analyst. All use Context7/Microsoft Docs/Sequential Thinking MCPs and can access the compound docs knowledge base via `rag_query`. Load if working on agent behavior or MCP integration.

**Includes:** `spec/agents.md`

See [structure/agents.md](./structure/agents.md)

---

## Marketplace

GitHub Pages-hosted plugin marketplace: manifest schema, plugin registry, Nextra landing page, installation flow, MCP configuration, external MCP prerequisites (check-and-warn), and release automation. Load if working on plugin distribution or installation.

**Includes:** `spec/marketplace.md`

See [structure/marketplace.md](./structure/marketplace.md)

---

## Observability

Logging architecture for MVP: stderr transport constraint, ILogger configuration, structured logging patterns, correlation IDs, sensitive data handling, and diagnostic scenarios. Metrics/tracing/alerting deferred to post-MVP. Load if implementing logging or debugging issues.

**Includes:** `spec/observability.md`

See [structure/observability.md](./structure/observability.md)

---

## Research Index

Index of 54 pre-research documents across 9 categories: Claude Code integration, Semantic Kernel/RAG, .NET development, infrastructure/database, testing, CI/CD, configuration/schema, git, and verification. Load if you need to find research backing a design decision.

**Includes:** `spec/research-index.md`

See [structure/research-index.md](./structure/research-index.md)
