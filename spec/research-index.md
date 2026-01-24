# Research Documentation Index

> **Status**: [DRAFT]
> **Parent**: [SPEC.md](../SPEC.md)
> **Last Updated**: 2025-01-24

---

## Overview

The `/research/` folder contains pre-research documents that inform the design decisions and implementation choices for the csharp-compounding-docs plugin. These documents capture findings from investigating technologies, frameworks, patterns, and best practices before they are incorporated into the formal specification.

Research documents are created during the discovery phase to:
- Evaluate technology options and make informed decisions
- Document API patterns and integration approaches
- Capture best practices from official documentation and community resources
- Provide reference material for implementation

---

## Research Categories

### Claude Code Integration

Research covering Claude Code's plugin architecture, skills system, agents, hooks, and marketplace ecosystem.

| Document | Description |
|----------|-------------|
| [building-claude-code-agents-research.md](../research/building-claude-code-agents-research.md) | Comprehensive guide to building custom Claude Code agents (subagents), covering file structure, YAML frontmatter, agent capabilities, invocation patterns, multi-agent systems, and MCP integration. |
| [building-claude-code-skills-research.md](../research/building-claude-code-skills-research.md) | Complete guide to creating Claude Code skills, including SKILL.md file structure, YAML frontmatter reference, triggers, tool permissions, bundled resources, and distribution patterns. |
| [claude-code-agents-research.md](../research/claude-code-agents-research.md) | Research on Claude Code agent types, spawning mechanisms, configuration, communication, coordination patterns, and built-in agents like Explore and Plan. |
| [claude-code-hooks-skill-invocation.md](../research/claude-code-hooks-skill-invocation.md) | Research on using Claude Code hooks (SessionStart, UserPromptSubmit, etc.) for automatic skill invocation and project context injection at session start. |
| [claude-code-plugin-architecture-research.md](../research/claude-code-plugin-architecture-research.md) | Overview of Claude Code's plugin system including MCP architecture, plugin configuration files, skills, hooks, and extension mechanisms. |
| [claude-code-plugin-installation-mechanism.md](../research/claude-code-plugin-installation-mechanism.md) | Research on Claude Code's native plugin installation including discovery flow, manifest format (plugin.json), MCP server registration, and marketplace integration. |
| [claude-code-skills-research.md](../research/claude-code-skills-research.md) | Guide to Claude Code skill structure, YAML frontmatter, auto-invoke mechanisms, skill hooks, MCP tool integration, and common patterns. |
| [claude-plugin-marketplace-ecosystem-research.md](../research/claude-plugin-marketplace-ecosystem-research.md) | Research on the Claude/MCP plugin marketplace ecosystem including official Anthropic directories, MCP server registries, and community marketplaces. |
| [claude-skills-yaml-frontmatter-research.md](../research/claude-skills-yaml-frontmatter-research.md) | Detailed reference for Claude Code skills YAML frontmatter including core fields, tool configuration, execution control, triggers, and agent-specific fields. |
| [compound-engineering-paradigm-research.md](../research/compound-engineering-paradigm-research.md) | Analysis of the compound-engineering paradigm from the original Ruby/Python plugin, covering the four pillars (Plan, Delegate, Assess, Compound) and implementation patterns. |
| [custom-claude-plugin-marketplace-research.md](../research/custom-claude-plugin-marketplace-research.md) | Comprehensive guide for building a custom Claude Code plugin marketplace using static-first approaches with GitHub Pages and marketplace.json format. |

### Semantic Kernel & RAG

Research on Microsoft Semantic Kernel, embeddings, RAG (Retrieval Augmented Generation) patterns, and AI service integration.

| Document | Description |
|----------|-------------|
| [microsoft-semantic-kernel-research.md](../research/microsoft-semantic-kernel-research.md) | Comprehensive research on Microsoft Semantic Kernel SDK covering core concepts, Ollama embeddings integration, PostgreSQL vector store, and RAG implementation patterns. |
| [microsoft-extensions-ai-rag-research.md](../research/microsoft-extensions-ai-rag-research.md) | Research on Microsoft.Extensions.AI unified abstraction layer for AI services, covering IChatClient, IEmbeddingGenerator interfaces, and relationship with Semantic Kernel. |
| [semantic-kernel-claude-rag-integration-research.md](../research/semantic-kernel-claude-rag-integration-research.md) | Research on integrating Anthropic Claude with Microsoft Semantic Kernel for RAG, covering Anthropic.SDK, Amazon Bedrock connector, and embedding options. |
| [semantic-kernel-ollama-rag-research.md](../research/semantic-kernel-ollama-rag-research.md) | Research on using Semantic Kernel with Ollama for local RAG systems, covering embedding models (mxbai-embed-large), chat models, and PostgreSQL integration. |
| [semantic-kernel-pgvector-package-update.md](../research/semantic-kernel-pgvector-package-update.md) | Documents the January 2026 API changes for Semantic Kernel PostgreSQL connector including package rename to Microsoft.SemanticKernel.Connectors.PgVector. |
| [semantic-kernel-postgresql-transaction-support.md](../research/semantic-kernel-postgresql-transaction-support.md) | Research confirming Semantic Kernel's PostgreSQL connector does NOT support database transactions, with recommended hybrid architecture patterns. |
| [semantic-kernel-request-queuing-research.md](../research/semantic-kernel-request-queuing-research.md) | Research on managing concurrency with Semantic Kernel and Ollama, covering rate limiting patterns using .NET primitives and Polly resilience library. |
| [claude-api-rag-integration.md](../research/claude-api-rag-integration.md) | Guide for integrating Claude API into RAG applications including authentication, API access models (direct, Bedrock, Vertex AI), and Claude Opus specifications. |
| [claude-embeddings-rag-research.md](../research/claude-embeddings-rag-research.md) | Definitive research confirming Anthropic does NOT provide an embeddings API, with recommended alternatives (Voyage AI, OpenAI, Ollama) for RAG implementations. |
| [liquibase-semantic-kernel-schema-research.md](../research/liquibase-semantic-kernel-schema-research.md) | Research on using Semantic Kernel's built-in schema management (EnsureCollectionExistsAsync) for PostgreSQL with pgvector, eliminating need for Liquibase. |

### .NET Development

Research on .NET Generic Host, dependency injection, configuration, background services, and general .NET development patterns.

| Document | Description |
|----------|-------------|
| [dotnet-generic-host-mcp-research.md](../research/dotnet-generic-host-mcp-research.md) | Research on using .NET Generic Host with MCP C# SDK for stdio servers, covering host configuration, DI, logging (stderr for stdio), and hosted services. |
| [dotnet-dependency-injection-patterns.md](../research/dotnet-dependency-injection-patterns.md) | Comprehensive guide to .NET dependency injection covering service lifetimes, keyed services (.NET 8+), IOptions pattern, and common pitfalls. |
| [dotnet-runtime-configuration-loading-research.md](../research/dotnet-runtime-configuration-loading-research.md) | Research on loading configuration files dynamically at runtime in .NET Generic Host applications, including IOptions pattern and change notification. |
| [dotnet-file-watcher-embeddings-research.md](../research/dotnet-file-watcher-embeddings-research.md) | Comprehensive research on FileSystemWatcher for monitoring document directories, covering debouncing, background service patterns, and embedding sync. |
| [dotnet-graph-libraries.md](../research/dotnet-graph-libraries.md) | Analysis of .NET graph libraries (QuikGraph, MSAGL, Advanced.Algorithms) with focus on cycle detection for DAG enforcement in link tracking. |
| [dotnet-markdown-parser-research.md](../research/dotnet-markdown-parser-research.md) | Evaluation of .NET markdown parsers recommending Markdig for YAML frontmatter extraction, link parsing, and full AST traversal capabilities. |
| [dotnet-multiplatform-publishing-research.md](../research/dotnet-multiplatform-publishing-research.md) | Research on compiling .NET applications for multiple platforms (Windows, macOS, Linux) with self-contained single-file executables for MCP server distribution. |
| [environment-variables-iconfiguration.md](../research/environment-variables-iconfiguration.md) | Guide to using environment variables with IConfiguration in .NET Generic Host, covering provider precedence and configuration source hierarchy. |
| [hosted-services-background-tasks.md](../research/hosted-services-background-tasks.md) | Research on IHostedService and BackgroundService patterns for long-running periodic tasks including timer-based execution and .NET 8+ improvements. |
| [ioptions-monitor-dynamic-paths.md](../research/ioptions-monitor-dynamic-paths.md) | Research on implementing IOptionsMonitor with dynamically discovered configuration paths for runtime config switching in MCP servers. |
| [mcp-csharp-sdk-research.md](../research/mcp-csharp-sdk-research.md) | Comprehensive research on the MCP C# SDK covering server implementation, tools, resources, prompts, transport layer, and Semantic Kernel integration. |

### Infrastructure & Database

Research on Docker Compose, PostgreSQL, pgvector, Ollama, and Liquibase for database schema management.

| Document | Description |
|----------|-------------|
| [postgresql-pgvector-research.md](../research/postgresql-pgvector-research.md) | Comprehensive research on PostgreSQL with pgvector for RAG including vector indexing (HNSW), SQL operations, schema design, and performance optimization. |
| [postgresql-docker-data-mounting-research.md](../research/postgresql-docker-data-mounting-research.md) | Research on PostgreSQL Docker image configuration including data persistence, volume mounting, initialization scripts, and the PostgreSQL 18 PGDATA change. |
| [docker-compose-postgresql-host-mcp-research.md](../research/docker-compose-postgresql-host-mcp-research.md) | Research on running PostgreSQL with pgvector in Docker while MCP server runs on host, covering connectivity, startup orchestration, and development workflow. |
| [ollama-docker-gpu-research.md](../research/ollama-docker-gpu-research.md) | Research on running Ollama in Docker with GPU acceleration (NVIDIA CUDA, AMD ROCm), model management, health checks, and Docker Compose configuration. |
| [ollama-multi-model-research.md](../research/ollama-multi-model-research.md) | Research on running multiple Ollama models simultaneously for RAG (embedding + LLM), covering GPU memory management and OLLAMA_NUM_PARALLEL settings. |
| [liquibase-changelog-format-research.md](../research/liquibase-changelog-format-research.md) | Research on Liquibase XML changelog format including include directives, sqlFile elements, rollback strategies, and PostgreSQL-specific considerations. |
| [liquibase-pgvector-docker-init.md](../research/liquibase-pgvector-docker-init.md) | Research on using Liquibase for PostgreSQL/pgvector schema management in Docker including custom Dockerfile, changelog organization, and tenant schema design. |

### Testing & Quality

Research on testing frameworks, code coverage, and quality assurance patterns for .NET projects.

| Document | Description |
|----------|-------------|
| [unit-testing-xunit-moq-shouldly.md](../research/unit-testing-xunit-moq-shouldly.md) | Research report on xUnit (v2.9.3/v3), Moq, and Shouldly for unit testing .NET 8+ applications including async patterns and MCP server testing strategies. |
| [coverlet-code-coverage-research.md](../research/coverlet-code-coverage-research.md) | Research on Coverlet for .NET code coverage including 100% threshold enforcement, MSBuild configuration, and exclusion patterns for MCP server projects. |
| [csharp-code-coverage-exclusions.md](../research/csharp-code-coverage-exclusions.md) | Guide to properly excluding code from coverage using ExcludeFromCodeCoverage attribute, Coverlet configuration, and justification documentation. |
| [reportgenerator-coverage-visualization.md](../research/reportgenerator-coverage-visualization.md) | Research on ReportGenerator for visualizing code coverage reports including HTML output, SVG badges, history tracking, and GitHub Actions integration. |
| [aspire-testing-mcp-client.md](../research/aspire-testing-mcp-client.md) | Research on using .NET Aspire (Aspire.Hosting.Testing) for integration and E2E testing with MCP client SDK, covering test infrastructure and database isolation. |
| [aspire-development-orchestrator.md](../research/aspire-development-orchestrator.md) | Research on .NET Aspire as development orchestrator for MCP server, PostgreSQL, and Ollama with code-first configuration and built-in observability dashboard. |

### CI/CD & Marketplace

Research on GitHub Actions, GitHub Pages, and plugin marketplace hosting infrastructure.

| Document | Description |
|----------|-------------|
| [github-actions-dotnet-cicd-research.md](../research/github-actions-dotnet-cicd-research.md) | Comprehensive research on GitHub Actions CI/CD for .NET including multi-platform builds, Docker builds, GitHub Container Registry, and testing strategies. |
| [github-actions-cache-limits.md](../research/github-actions-cache-limits.md) | Research on GitHub Actions cache limits (10GB default) for caching Ollama models in CI, including alternatives like custom Docker images or S3-backed caching. |
| [github-pages-plugin-marketplace-research.md](../research/github-pages-plugin-marketplace-research.md) | Research on using GitHub Pages to host plugin marketplace JSON index with GitHub Actions CI/CD for automated plugin publishing. |
| [static-site-generator-marketplace-research.md](../research/static-site-generator-marketplace-research.md) | Evaluation of static site generators (Jekyll, Hugo, Nextra) for plugin marketplace hosting, recommending Nextra for modern React/Next.js development. |

### Configuration & Schema

Research on configuration formats, schema validation, and YAML frontmatter processing.

| Document | Description |
|----------|-------------|
| [json-schema-validation-libraries-research.md](../research/json-schema-validation-libraries-research.md) | Research on JSON Schema validation libraries for .NET recommending JsonSchema.Net + Yaml2JsonNode for Draft 2020-12 support and YAML frontmatter validation. |
| [json-schema-validation-library-research.md](../research/json-schema-validation-library-research.md) | Comparison of NJsonSchema vs JsonSchema.Net for validating YAML doc-type schemas, with JsonSchema.Net recommended for Draft 2020-12 compliance. |
| [yaml-schema-formats.md](../research/yaml-schema-formats.md) | Comprehensive research on YAML schema formats covering JSON Schema for YAML validation, YAML 1.1 vs 1.2 specifications, and schema definition patterns. |

### Git & Version Control

Research on Git operations and version control integration.

| Document | Description |
|----------|-------------|
| [git-current-branch-detection.md](../research/git-current-branch-detection.md) | Research on methods for detecting current Git branch including git branch --show-current (Git 2.22+), git rev-parse, and handling edge cases like detached HEAD. |

### Verification & Corrections

Documents that verify specifications or correct previous assumptions.

| Document | Description |
|----------|-------------|
| [sequential-thinking-mcp-verification.md](../research/sequential-thinking-mcp-verification.md) | Verification that the correct npm package for Sequential Thinking MCP is @modelcontextprotocol/server-sequential-thinking (not @anthropics/sequential-thinking-mcp). |

---

## Using Research Documents

When implementing features from the specification:

1. **Check for relevant research** - Use this index to find research documents that inform the feature you are implementing
2. **Verify currency** - Research documents may reference package versions; verify against current releases
3. **Cross-reference with spec** - Research informs decisions documented in the specification files in `/spec/`
4. **Update as needed** - If research becomes outdated, create new research or update existing documents

---

## Document Statistics

| Category | Document Count |
|----------|----------------|
| Claude Code Integration | 11 |
| Semantic Kernel & RAG | 10 |
| .NET Development | 11 |
| Infrastructure & Database | 7 |
| Testing & Quality | 6 |
| CI/CD & Marketplace | 4 |
| Configuration & Schema | 3 |
| Git & Version Control | 1 |
| Verification & Corrections | 1 |
| **Total** | **54** |
