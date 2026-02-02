# Changelog

All notable changes to the CompoundDocs plugin will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Planned
- Additional skills for document capture workflows
- Cross-repository knowledge graph linking

---

## [1.0.0] - 2026-02-01

### Added

#### Core
- **GraphRAG Pipeline**: embed → vector search → graph enrichment → LLM synthesis
- **Single MCP Tool**: `rag_query` — intelligent document retrieval with source attribution
- **Emergent Doc Types**: LLM-discovered document classifications via Haiku/Bedrock

#### Plugin Components
- **Skills**: `cdocs-query` (knowledge base query), `cdocs-help` (usage help)
- **Agents**: best-practices-researcher, framework-docs-researcher, repo-research-analyst, git-history-analyzer
- **Plugin manifest**: `.claude-plugin/plugin.json` and `.claude-plugin/marketplace.json`

#### Infrastructure
- **Amazon Neptune Serverless**: Knowledge graph with openCypher queries
- **Amazon OpenSearch Serverless**: k-NN vector search (1024-dim, Titan Embed V2)
- **AWS Bedrock**: Claude Sonnet/Haiku for RAG synthesis, Titan Embed V2 for embeddings
- **MCP HTTP Transport**: Multi-developer concurrent access
- **K8s CronJob Worker**: Git polling → incremental graph updates

### Changed (from 0.1.0-preview)
- Replaced PostgreSQL/pgvector with Neptune + OpenSearch
- Replaced Ollama with AWS Bedrock
- Reduced from 9 tools to 1 (`rag_query`)
- Moved from custom YAML skills/agents to Claude Code plugin format
- Removed tenant/session/project activation concepts

---

[Unreleased]: https://github.com/michaelmccord/csharp-compound-engineering/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/michaelmccord/csharp-compound-engineering/releases/tag/v1.0.0
