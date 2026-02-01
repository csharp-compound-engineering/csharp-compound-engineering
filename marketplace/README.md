# CSharp Compound Docs

**Intelligent documentation management for Claude with semantic search and compound document orchestration.**

---

## Overview

CSharp Compound Docs is a powerful Claude plugin that transforms how you interact with documentation. Built on the compound engineering paradigm, it enables semantic understanding of your documentation ecosystem, intelligent retrieval, and contextual knowledge synthesis.

### Value Proposition

- **Semantic Understanding**: Go beyond keyword matching with AI-powered semantic search that understands the meaning and context of your queries
- **Compound Documents**: Automatically synthesize information from multiple sources into coherent, contextual responses
- **Knowledge Graph**: Build interconnected documentation that Claude can traverse intelligently
- **Version Aware**: Track documentation changes and provide historically accurate information

---

## Key Features

### Semantic Search
![Semantic Search](screenshots/semantic-search.png)
*Placeholder for semantic search screenshot*

Query your documentation using natural language. The plugin uses Semantic Kernel embeddings to find relevant content based on meaning, not just keywords.

### Document Orchestration
![Document Orchestration](screenshots/orchestration.png)
*Placeholder for document orchestration screenshot*

Automatically combine information from multiple documents to answer complex queries that span your entire knowledge base.

### Knowledge Management
![Knowledge Graph](screenshots/knowledge-graph.png)
*Placeholder for knowledge graph screenshot*

Visualize and manage relationships between documents, concepts, and entities in your documentation ecosystem.

### RAG-Powered Responses
![RAG Responses](screenshots/rag-responses.png)
*Placeholder for RAG responses screenshot*

Retrieval-Augmented Generation ensures Claude's responses are grounded in your actual documentation, reducing hallucinations and improving accuracy.

---

## Installation

### Prerequisites

- .NET 9.0 SDK or later
- PostgreSQL 16 with pgvector extension
- Claude Desktop or compatible MCP client

### Quick Install

1. **Install via Claude Marketplace** (Recommended)

   Search for "CSharp Compound Docs" in the Claude marketplace and click Install.

2. **Manual Installation**

   ```bash
   # Clone the repository
   git clone https://github.com/your-org/csharp-compound-engineering.git
   cd csharp-compound-engineering

   # Build the project
   dotnet build

   # Run the MCP server
   dotnet run --project src/CompoundDocs.McpServer
   ```

### Database Setup

```bash
# Ensure PostgreSQL is running with pgvector
psql -c "CREATE EXTENSION IF NOT EXISTS vector;"

# Run migrations
dotnet ef database update --project src/CompoundDocs.Infrastructure
```

---

## Quick Start

### 1. Configure Your Connection

Add the following to your Claude configuration:

```json
{
  "mcpServers": {
    "compound-docs": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/CompoundDocs.McpServer"],
      "env": {
        "COMPOUND_DOCS_CONNECTION_STRING": "Host=localhost;Database=compound_docs;Username=postgres;Password=yourpassword"
      }
    }
  }
}
```

### 2. Index Your Documentation

```
@compound-docs index /path/to/your/documentation
```

### 3. Start Querying

```
@compound-docs search "How do I configure authentication?"
```

### 4. Create Compound Documents

```
@compound-docs compound "Create a getting started guide combining setup and authentication docs"
```

---

## Configuration Options

| Option | Description | Default |
|--------|-------------|---------|
| `COMPOUND_DOCS_CONNECTION_STRING` | PostgreSQL connection string | Required |
| `COMPOUND_DOCS_EMBEDDING_MODEL` | Semantic Kernel embedding model | `text-embedding-ada-002` |
| `COMPOUND_DOCS_CHUNK_SIZE` | Document chunk size for indexing | `1000` |
| `COMPOUND_DOCS_CHUNK_OVERLAP` | Overlap between chunks | `200` |
| `COMPOUND_DOCS_MAX_RESULTS` | Maximum search results | `10` |
| `COMPOUND_DOCS_SIMILARITY_THRESHOLD` | Minimum similarity score | `0.7` |
| `COMPOUND_DOCS_LOG_LEVEL` | Logging verbosity | `Information` |

### Advanced Configuration

For detailed configuration options including:
- Custom embedding providers
- Multi-tenant setups
- Performance tuning
- Security hardening

See the [Full Documentation](https://github.com/your-org/csharp-compound-engineering/docs).

---

## Documentation

- [Full Documentation](https://github.com/your-org/csharp-compound-engineering/docs)
- [API Reference](https://github.com/your-org/csharp-compound-engineering/docs/api)
- [Architecture Guide](https://github.com/your-org/csharp-compound-engineering/docs/architecture)
- [Contributing Guide](https://github.com/your-org/csharp-compound-engineering/CONTRIBUTING.md)
- [Changelog](./CHANGELOG.md)

---

## Technical Stack

- **.NET 9.0** - Modern, high-performance runtime
- **PostgreSQL 16** with **pgvector** - Vector similarity search
- **Semantic Kernel 1.70.0** - AI orchestration and embeddings
- **MCP Protocol** via **ModelContextProtocol** NuGet - Claude integration

---

## Support

- [GitHub Issues](https://github.com/your-org/csharp-compound-engineering/issues)
- [Discussions](https://github.com/your-org/csharp-compound-engineering/discussions)

---

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/your-org/csharp-compound-engineering/LICENSE) file for details.
