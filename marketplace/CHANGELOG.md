# Changelog

All notable changes to CSharp Compound Docs will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Planned
- Multi-tenant support for enterprise deployments
- Custom embedding provider plugins
- Real-time document synchronization
- Advanced analytics dashboard
- Knowledge graph visualization UI

---

## [0.1.0-preview] - 2025-01-25

### Added

#### Core Features
- **Semantic Search**: Natural language document search powered by Semantic Kernel 1.70.0 embeddings
- **Document Indexing**: Automatic chunking and embedding generation for documentation files
- **Compound Document Generation**: Synthesize information from multiple sources into coherent responses
- **RAG Integration**: Retrieval-Augmented Generation for grounded, accurate responses

#### MCP Integration
- **MCP Server**: Full Model Context Protocol implementation via ModelContextProtocol NuGet
- **Tool Support**: `index`, `search`, `compound`, `list`, `delete`, and `status` tools
- **Resource Access**: Expose indexed documents and knowledge graph as MCP resources
- **Prompt Templates**: Pre-built prompts for common documentation tasks

#### Infrastructure
- **PostgreSQL Support**: Database backend with Entity Framework Core 9.0
- **pgvector Integration**: High-performance vector similarity search
- **Configurable Embeddings**: Support for OpenAI and Azure OpenAI embedding models
- **Flexible Chunking**: Configurable chunk size and overlap for optimal retrieval

#### Configuration
- Environment variable configuration support
- JSON configuration file support
- Runtime configuration validation
- Sensible defaults for quick setup

### Technical Stack
- .NET 9.0
- PostgreSQL 16 with pgvector extension
- Semantic Kernel 1.70.0
- ModelContextProtocol NuGet package
- Entity Framework Core 9.0

### Known Limitations
- Preview release - API may change in future versions
- Single-tenant only in this release
- Requires manual PostgreSQL and pgvector setup
- Limited to text-based documentation formats (Markdown, plain text, HTML)

### Security
- Connection string encryption at rest
- Input sanitization for all user-provided queries
- Parameterized database queries to prevent SQL injection

---

## Version History

| Version | Date | Status |
|---------|------|--------|
| 0.1.0-preview | 2025-01-25 | Current |

---

## Upgrade Guide

### From Pre-release Versions

If you were using any pre-release or development builds:

1. **Backup your database** before upgrading
2. Run database migrations: `dotnet ef database update`
3. Re-index your documentation: `@compound-docs index --rebuild`
4. Update your Claude configuration with any new required settings

---

## Contributing

See [CONTRIBUTING.md](https://github.com/your-org/csharp-compound-engineering/CONTRIBUTING.md) for guidelines on:
- Reporting bugs
- Suggesting features
- Submitting pull requests

---

[Unreleased]: https://github.com/your-org/csharp-compound-engineering/compare/v0.1.0-preview...HEAD
[0.1.0-preview]: https://github.com/your-org/csharp-compound-engineering/releases/tag/v0.1.0-preview
