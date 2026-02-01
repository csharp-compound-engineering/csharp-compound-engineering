# Installation Guide

This guide covers installing CSharp Compound Docs for use with Claude Code.

## Prerequisites

Before installing, ensure you have the following:

### Required
- **.NET 9.0 SDK** or later - [Download](https://dotnet.microsoft.com/download)
- **PostgreSQL 16+** with pgvector extension - [Download](https://www.postgresql.org/download/)
- **Docker** (recommended) - [Download](https://www.docker.com/products/docker-desktop/)

### Optional
- **PowerShell 7+** - For launch scripts on Windows/macOS/Linux
- **Ollama** - For local embeddings (alternative to OpenAI)

---

## Installation Methods

### Method 1: Claude Marketplace (Recommended)

1. Open Claude Code
2. Open the command palette (`Cmd+Shift+P` / `Ctrl+Shift+P`)
3. Type "Install Plugin"
4. Search for "CSharp Compound Docs"
5. Click Install

The plugin will be installed to `~/.claude/plugins/csharp-compounding-docs/`.

### Method 2: Git Clone (Manual)

```bash
# Clone the repository
git clone https://github.com/your-org/csharp-compound-engineering.git
cd csharp-compound-engineering

# Build the solution
dotnet build

# The plugin is now ready at ./plugins/csharp-compounding-docs/
```

### Method 3: Project-Scoped Installation

For project-specific installation (not global):

```bash
# In your project root
mkdir -p .claude/plugins
cd .claude/plugins
git clone https://github.com/your-org/csharp-compound-engineering.git csharp-compounding-docs
```

---

## Database Setup

### Option A: Docker (Recommended)

The fastest way to get PostgreSQL with pgvector running:

```bash
# Start the database container
docker compose up -d postgres

# Verify pgvector is available
docker exec -it compound-docs-postgres psql -U postgres -c "SELECT * FROM pg_extension WHERE extname = 'vector';"
```

The Docker Compose file includes:
- PostgreSQL 16 with pgvector pre-installed
- Persistent data volume
- Health checks

### Option B: Existing PostgreSQL

If you have an existing PostgreSQL installation:

```bash
# Connect to your database
psql -U postgres

# Create the database
CREATE DATABASE compound_docs;

# Connect to the new database
\c compound_docs

# Install pgvector extension
CREATE EXTENSION IF NOT EXISTS vector;

# Verify installation
SELECT * FROM pg_extension WHERE extname = 'vector';
```

### Run Migrations

After database setup, apply the schema migrations:

```bash
cd src/CompoundDocs.Infrastructure
dotnet ef database update
```

---

## Configuration

### MCP Server Registration

Add the MCP server to your Claude configuration. Create or edit `~/.claude/mcp.json`:

```json
{
  "mcpServers": {
    "csharp-compounding-docs": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "~/.claude/plugins/csharp-compounding-docs/src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj"
      ],
      "env": {
        "ConnectionStrings__CompoundDocs": "Host=localhost;Database=compound_docs;Username=postgres;Password=postgres"
      }
    }
  }
}
```

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ConnectionStrings__CompoundDocs` | PostgreSQL connection string | Required |
| `Embedding__Provider` | Embedding provider (Ollama, OpenAI, Azure) | `Ollama` |
| `Embedding__Model` | Model name for embeddings | `nomic-embed-text` |
| `Ollama__BaseUrl` | Ollama API base URL | `http://localhost:11434` |
| `OpenAI__ApiKey` | OpenAI API key (if using OpenAI) | - |
| `Logging__LogLevel__Default` | Log level | `Information` |

### appsettings.json

For development, you can use `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "CompoundDocs": "Host=localhost;Database=compound_docs;Username=postgres;Password=postgres"
  },
  "Embedding": {
    "Provider": "Ollama",
    "Model": "nomic-embed-text",
    "Dimensions": 768
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

---

## Embedding Provider Setup

### Ollama (Recommended for Local Development)

```bash
# Install Ollama
# macOS
brew install ollama

# Linux
curl -fsSL https://ollama.com/install.sh | sh

# Start Ollama
ollama serve

# Pull the embedding model
ollama pull nomic-embed-text
```

### OpenAI

Set your API key:

```bash
export OpenAI__ApiKey="sk-..."
```

Or in appsettings:

```json
{
  "Embedding": {
    "Provider": "OpenAI",
    "Model": "text-embedding-3-small"
  },
  "OpenAI": {
    "ApiKey": "sk-..."
  }
}
```

---

## Verification

After installation, verify everything is working:

### 1. Check MCP Server

```bash
# Start the server manually to test
dotnet run --project src/CompoundDocs.McpServer

# You should see:
# info: CompoundDocs.McpServer[0]
#       MCP Server started
```

### 2. Check Database Connection

```bash
# The server logs will show:
# info: CompoundDocs.Infrastructure[0]
#       Database connection verified
```

### 3. Check Embedding Provider

```bash
# For Ollama, verify the model is available:
curl http://localhost:11434/api/tags
```

### 4. Test in Claude Code

Open Claude Code and try:

```
/cdocs-activate
```

If successful, you'll see a confirmation that the plugin is active.

---

## Troubleshooting

### Common Issues

#### "Connection refused" to PostgreSQL
- Ensure PostgreSQL is running: `docker ps` or `pg_isready`
- Check the connection string includes the correct host/port
- Verify firewall allows connections on port 5432

#### "pgvector extension not found"
- Run `CREATE EXTENSION vector;` in your database
- For Docker, ensure you're using the `pgvector/pgvector` image

#### "Ollama connection failed"
- Ensure Ollama is running: `ollama serve`
- Check the base URL matches your configuration
- Pull the embedding model: `ollama pull nomic-embed-text`

#### "MCP server not responding"
- Check the server is running: `dotnet run --project src/CompoundDocs.McpServer`
- Verify the path in `mcp.json` is correct
- Check logs for startup errors

### Getting Help

- [GitHub Issues](https://github.com/your-org/csharp-compound-engineering/issues)
- [Troubleshooting Guide](./TROUBLESHOOTING.md)
- [Architecture Documentation](./ARCHITECTURE.md)

---

## Next Steps

After installation:

1. **Read the Quick Start** - Learn basic usage patterns
2. **Configure Document Types** - Set up your documentation structure
3. **Index Your First Documents** - Start building your knowledge base
4. **Explore Skills** - Use `/cdocs-query` and other skills

See the [First-Time Setup Guide](./first-time-setup.md) for detailed onboarding steps.
