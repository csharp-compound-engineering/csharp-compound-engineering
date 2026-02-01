# CSharp Compound Docs

[![Build Status](https://img.shields.io/github/actions/workflow/status/your-org/csharp-compound-engineering/test.yml?branch=master)](https://github.com/your-org/csharp-compound-engineering/actions)
[![Coverage](https://img.shields.io/badge/coverage-100%25-brightgreen)](https://your-org.github.io/csharp-compound-engineering/coverage/latest/)
[![Version](https://img.shields.io/badge/version-0.1.0--preview-blue)](https://github.com/your-org/csharp-compound-engineering/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-9.0-purple)](https://dotnet.microsoft.com/)

A Claude Code plugin implementing the **compound-engineering paradigm** for C#/.NET projects. Capture and retrieve institutional knowledge through semantic search and RAG-powered documentation.

## Features

- **Intelligent Knowledge Capture** - Automatically detect and capture problems, insights, codebase knowledge, tool configurations, and coding styles from conversations
- **Semantic Search** - Find relevant documentation using natural language queries powered by vector embeddings
- **RAG-Powered Answers** - Get synthesized answers with source attribution using Retrieval-Augmented Generation
- **Multi-Tenant Isolation** - Support for multiple projects and git branches with complete data isolation
- **File System as Source** - All documentation stored as markdown files; database is a derived index
- **Custom Doc-Types** - Create project-specific documentation types with custom schemas
- **Git Worktree Support** - Work on multiple branches simultaneously with separate document contexts
- **Local-First** - All processing happens locally with Ollama; no external API calls required

## Quick Start

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
- [PowerShell 7+](https://github.com/PowerShell/PowerShell)
- [Claude Code](https://claude.ai/code) or [Claude Desktop](https://claude.ai/desktop)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/your-org/csharp-compound-engineering.git
   cd csharp-compound-engineering
   ```

2. **Start infrastructure**
   ```bash
   docker compose -p csharp-compounding-docs up -d
   ```

3. **Pull required Ollama models**
   ```bash
   docker compose -p csharp-compounding-docs exec ollama ollama pull mxbai-embed-large
   docker compose -p csharp-compounding-docs exec ollama ollama pull mistral
   ```

4. **Configure Claude Code**

   Add to your Claude Desktop config (`~/Library/Application Support/Claude/claude_desktop_config.json` on macOS):
   ```json
   {
     "mcpServers": {
       "csharp-compounding-docs": {
         "command": "dotnet",
         "args": [
           "run",
           "--project",
           "/path/to/csharp-compound-engineering/src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj"
         ]
       }
     }
   }
   ```

5. **Initialize in your project**
   ```
   /cdocs:activate
   ```

### Basic Usage

**Capture a problem you solved:**
```
I just fixed the database connection pool exhaustion bug. The issue was
that SqlConnection objects weren't being disposed in background jobs.
```
The `/cdocs:problem` skill will auto-trigger and capture the solution.

**Query your documentation:**
```
/cdocs:query How do we handle connection pool issues?
```

**Search for specific documents:**
```
/cdocs:search database configuration
```

## Documentation

| Document | Description |
|----------|-------------|
| [Architecture](docs/ARCHITECTURE.md) | System design and component overview |
| [API Reference](docs/API-REFERENCE.md) | Complete MCP tools documentation |
| [Configuration](docs/configuration.md) | Configuration options and examples |
| [Troubleshooting](docs/TROUBLESHOOTING.md) | Common issues and solutions |
| [Contributing](CONTRIBUTING.md) | Development guidelines |
| [Security](SECURITY.md) | Security policy and best practices |

## Skills

### Capture Skills
| Skill | Purpose |
|-------|---------|
| `/cdocs:problem` | Capture solved problems and bugs |
| `/cdocs:insight` | Capture product/project insights |
| `/cdocs:codebase` | Capture architectural knowledge |
| `/cdocs:tool` | Capture tool/library knowledge |
| `/cdocs:style` | Capture coding preferences |

### Query Skills
| Skill | Purpose |
|-------|---------|
| `/cdocs:query` | RAG query for synthesized answers |
| `/cdocs:search` | Semantic search for documents |
| `/cdocs:search-external` | Search external project docs |
| `/cdocs:query-external` | RAG query external docs |

### Utility Skills
| Skill | Purpose |
|-------|---------|
| `/cdocs:activate` | Activate project context |
| `/cdocs:create-type` | Create custom doc-types |
| `/cdocs:promote` | Change document visibility |
| `/cdocs:delete` | Delete documents |
| `/cdocs:todo` | File-based todo tracking |
| `/cdocs:worktree` | Git worktree management |
| `/cdocs:research` | Orchestrate research agents |

## MCP Tools

The plugin provides 9 MCP tools:

| Tool | Description |
|------|-------------|
| `activate_project` | Activate a project for the session |
| `rag_query` | Answer questions using RAG |
| `semantic_search` | Search by semantic similarity |
| `index_document` | Manually index a document |
| `list_doc_types` | List available doc-types |
| `search_external_docs` | Search external documentation |
| `rag_query_external` | RAG query external docs |
| `delete_documents` | Delete documents by context |
| `update_promotion_level` | Update document visibility |

See [API Reference](docs/API-REFERENCE.md) for complete documentation.

## Technology Stack

| Component | Technology |
|-----------|------------|
| MCP Server | .NET 9.0 Generic Host |
| AI Framework | Microsoft Semantic Kernel |
| Vector Database | PostgreSQL + pgvector |
| Embeddings | Ollama (mxbai-embed-large) |
| RAG Generation | Ollama (mistral) |
| Containerization | Docker Compose |
| Scripting | PowerShell 7+ |

## Project Structure

```
csharp-compound-engineering/
├── src/                          # Source code
│   ├── CompoundDocs.McpServer/   # MCP server application
│   ├── CompoundDocs.Cleanup/     # Cleanup console app
│   └── CompoundDocs.Common/      # Shared library
├── tests/                        # Test projects
├── plugins/                      # Claude Code plugin files
├── docker/                       # Docker configurations
├── scripts/                      # PowerShell scripts
├── docs/                         # Documentation
├── marketplace/                  # Marketplace assets
└── spec/                         # Specifications
```

## Development

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true

# Run MCP server
dotnet run --project src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for detailed development guidelines.

## Configuration

### Global Configuration
Located at `~/.claude/.csharp-compounding-docs/global-config.json`:
```json
{
  "postgres": {
    "host": "127.0.0.1",
    "port": 5433,
    "database": "compounding_docs"
  },
  "ollama": {
    "host": "127.0.0.1",
    "port": 11435,
    "generationModel": "mistral"
  }
}
```

### Project Configuration
Located at `<project>/.csharp-compounding-docs/config.json`:
```json
{
  "projectName": "my-project",
  "rag": {
    "relevanceThreshold": 0.7,
    "maxResults": 3
  }
}
```

See [Configuration Guide](docs/configuration.md) for all options.

## Troubleshooting

**MCP server not starting?**
```bash
# Check Docker containers
docker compose -p csharp-compounding-docs ps

# View logs
docker compose -p csharp-compounding-docs logs
```

**Ollama not responding?**
```bash
# Test connection
curl http://127.0.0.1:11435/api/tags

# Pull models
docker compose -p csharp-compounding-docs exec ollama ollama pull mxbai-embed-large
```

See [Troubleshooting Guide](docs/TROUBLESHOOTING.md) for more solutions.

## Roadmap

- [ ] GitHub Copilot integration
- [ ] VS Code extension
- [ ] Team collaboration features
- [ ] Cloud deployment option
- [ ] Additional embedding model support

## Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## Security

For security concerns, please see [SECURITY.md](SECURITY.md). Do not create public issues for security vulnerabilities.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Inspired by [compound-engineering-plugin](https://github.com/anthropics/compound-engineering-plugin)
- Built with [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel)
- Vector search powered by [pgvector](https://github.com/pgvector/pgvector)
- Local LLM by [Ollama](https://ollama.ai/)

---

**Made with Claude Code**
