# CompoundDocs Plugin Marketplace

> **Note:** The marketplace definition has moved to `.claude-plugin/marketplace.json`. This directory is kept for the icon asset only.

## Installation

Add the marketplace:
```
/plugin marketplace add michaelmccord/csharp-compound-engineering
```

Install the plugin:
```
/plugin install cd@cd-marketplace
```

Or install directly during development:
```bash
claude --plugin-dir /path/to/csharp-compound-engineering
```

## What's Included

### Plugin: `cd`

GraphRAG-powered knowledge base for project documentation. Provides intelligent document retrieval via a single `rag_query` MCP tool backed by Neptune, OpenSearch, and Bedrock.

**Skills:**
| Skill | Description |
|-------|-------------|
| `/cd:cdocs-query [question]` | Query the knowledge base for project documentation and patterns |
| `/cd:cdocs-help` | Show help for available CompoundDocs commands |

**Agents:**
| Agent | Description |
|-------|-------------|
| `best-practices-researcher` | Research best practices for technologies and patterns |
| `framework-docs-researcher` | Find API documentation, code examples, and framework guidance |
| `repo-research-analyst` | Analyze repository structure and map codebase architecture |
| `git-history-analyzer` | Analyze git history for patterns, changes, and subject matter experts |

## Technical Stack

- **.NET 9.0** — MCP server runtime
- **Amazon Neptune Serverless** — Knowledge graph (openCypher)
- **Amazon OpenSearch Serverless** — Vector search (k-NN, 1024-dim Titan Embed V2)
- **AWS Bedrock** — Claude Haiku/Sonnet for entity extraction and RAG synthesis
- **MCP HTTP Transport** — Multi-developer concurrent access

## License

MIT
