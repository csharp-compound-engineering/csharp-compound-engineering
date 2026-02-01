# CSharp Compounding Docs Configuration Guide

This document provides comprehensive configuration documentation for the CSharp Compounding Docs plugin, including all available settings, environment variables, and examples for common scenarios.

## Table of Contents

- [Configuration Overview](#configuration-overview)
- [Global Configuration](#global-configuration)
- [Project Configuration](#project-configuration)
- [Environment Variables](#environment-variables)
- [MCP Server Configuration](#mcp-server-configuration)
- [Using Configuration Tools](#using-configuration-tools)
- [Common Scenarios](#common-scenarios)
- [Troubleshooting](#troubleshooting)

## Configuration Overview

CSharp Compounding Docs uses a two-tier configuration system:

1. **Global Configuration**: System-wide settings stored in `~/.claude/.csharp-compounding-docs/global-config.json`. These settings apply to all projects and include database connections and service endpoints.

2. **Project Configuration**: Project-specific settings stored in `<project-root>/.csharp-compounding-docs/config.json`. These settings control RAG behavior, file watching, and custom document types for a specific project.

### Configuration Precedence

Settings are resolved in the following order (highest to lowest priority):

1. Environment variables
2. Global configuration file
3. Default values

For project settings:

1. Project configuration file
2. Default values

## Global Configuration

Global configuration is stored at `~/.claude/.csharp-compounding-docs/global-config.json`.

### PostgreSQL Settings

Configure the PostgreSQL database connection used for storing document embeddings and metadata.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `postgres.host` | string | `127.0.0.1` | PostgreSQL server hostname |
| `postgres.port` | integer | `5433` | PostgreSQL server port |
| `postgres.database` | string | `compounding_docs` | Database name |
| `postgres.username` | string | `compounding` | Database username |
| `postgres.password` | string | `compounding` | Database password |

### Ollama Settings

Configure the Ollama service connection used for generating embeddings and LLM responses.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `ollama.host` | string | `127.0.0.1` | Ollama server hostname |
| `ollama.port` | integer | `11435` | Ollama server port |
| `ollama.generationModel` | string | `mistral` | LLM model for RAG synthesis |

**Note**: The embedding model (`mxbai-embed-large`) and embedding dimensions (1024) are fixed and cannot be changed.

### Example Global Configuration

```json
{
  "configDirectory": "/Users/username/.claude/.csharp-compounding-docs",
  "postgres": {
    "host": "127.0.0.1",
    "port": 5433,
    "database": "compounding_docs",
    "username": "compounding",
    "password": "compounding"
  },
  "ollama": {
    "host": "127.0.0.1",
    "port": 11435,
    "generationModel": "mistral"
  }
}
```

## Project Configuration

Project configuration is stored at `<project-root>/.csharp-compounding-docs/config.json`.

### RAG Settings

Configure the Retrieval-Augmented Generation behavior.

| Setting | Type | Default | Range | Description |
|---------|------|---------|-------|-------------|
| `rag.relevanceThreshold` | float | `0.7` | 0.0-1.0 | Minimum similarity score for document retrieval |
| `rag.maxResults` | integer | `3` | 1-100 | Maximum number of source documents to return |
| `rag.maxLinkedDocs` | integer | `5` | 0-50 | Maximum linked documents to include |

### Link Resolution Settings

Configure how document links are followed and resolved.

| Setting | Type | Default | Range | Description |
|---------|------|---------|-------|-------------|
| `linkResolution.maxDepth` | integer | `2` | 0-10 | Maximum depth for following document links |
| `linkResolution.maxLinkedDocs` | integer | `5` | 0-50 | Maximum linked documents to include |

### File Watcher Settings

Configure the file system watcher for automatic document indexing.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `fileWatcher.debounceMs` | integer | `500` | Debounce interval in milliseconds (100-10000) |
| `fileWatcher.includePatterns` | string[] | `["**/*.md"]` | Glob patterns for files to watch |
| `fileWatcher.excludePatterns` | string[] | `["**/node_modules/**", "**/.git/**"]` | Glob patterns to exclude |

### External Documentation Sources

Configure external documentation sources for cross-project search.

```json
{
  "externalDocs": [
    {
      "id": "shared-specs",
      "name": "Shared Specifications",
      "path": "/path/to/shared/docs",
      "enabled": true
    }
  ]
}
```

### Custom Document Types

Define custom document types with specific schemas and trigger phrases.

```json
{
  "customDocTypes": [
    {
      "id": "runbook",
      "name": "Runbook",
      "description": "Operational runbooks for incident response",
      "schema": null,
      "triggerPhrases": ["runbook", "incident response", "operations"]
    }
  ]
}
```

### Example Project Configuration

```json
{
  "projectName": "my-project",
  "rag": {
    "relevanceThreshold": 0.7,
    "maxResults": 3,
    "maxLinkedDocs": 5
  },
  "linkResolution": {
    "maxDepth": 2,
    "maxLinkedDocs": 5
  },
  "fileWatcher": {
    "debounceMs": 500,
    "includePatterns": ["**/*.md"],
    "excludePatterns": ["**/node_modules/**", "**/.git/**"]
  },
  "externalDocs": [],
  "customDocTypes": []
}
```

## Environment Variables

Environment variables override global configuration settings. This is useful for CI/CD pipelines, Docker deployments, or temporary overrides.

### Available Environment Variables

| Variable | Overrides | Description |
|----------|-----------|-------------|
| `COMPOUNDING_POSTGRES_HOST` | `postgres.host` | PostgreSQL server hostname |
| `COMPOUNDING_POSTGRES_PORT` | `postgres.port` | PostgreSQL server port |
| `COMPOUNDING_POSTGRES_DATABASE` | `postgres.database` | Database name |
| `COMPOUNDING_POSTGRES_USERNAME` | `postgres.username` | Database username |
| `COMPOUNDING_POSTGRES_PASSWORD` | `postgres.password` | Database password |
| `COMPOUNDING_OLLAMA_HOST` | `ollama.host` | Ollama server hostname |
| `COMPOUNDING_OLLAMA_PORT` | `ollama.port` | Ollama server port |
| `COMPOUNDING_OLLAMA_MODEL` | `ollama.generationModel` | LLM model for generation |

### Setting Environment Variables

**macOS/Linux (bash/zsh):**
```bash
export COMPOUNDING_POSTGRES_HOST=localhost
export COMPOUNDING_POSTGRES_PORT=5432
```

**Windows (PowerShell):**
```powershell
$env:COMPOUNDING_POSTGRES_HOST = "localhost"
$env:COMPOUNDING_POSTGRES_PORT = "5432"
```

**Windows (Command Prompt):**
```cmd
set COMPOUNDING_POSTGRES_HOST=localhost
set COMPOUNDING_POSTGRES_PORT=5432
```

## MCP Server Configuration

To use CSharp Compounding Docs with Claude Desktop, configure the MCP server in your Claude Desktop configuration file.

### Configuration File Locations

- **macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
- **Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
- **Linux**: `~/.config/Claude/claude_desktop_config.json`

### Basic Configuration

```json
{
  "mcpServers": {
    "csharp-compounding-docs": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/path/to/csharp-compounding-docs/src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj"
      ]
    }
  }
}
```

### Configuration with Environment Overrides

```json
{
  "mcpServers": {
    "csharp-compounding-docs": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/path/to/csharp-compounding-docs/src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj"
      ],
      "env": {
        "COMPOUNDING_POSTGRES_HOST": "db.example.com",
        "COMPOUNDING_POSTGRES_PORT": "5432",
        "COMPOUNDING_OLLAMA_HOST": "gpu-server.local",
        "COMPOUNDING_OLLAMA_MODEL": "llama3.1"
      }
    }
  }
}
```

### Using Pre-built Binary

```json
{
  "mcpServers": {
    "csharp-compounding-docs": {
      "command": "/path/to/csharp-compounding-docs/bin/CompoundDocs.McpServer"
    }
  }
}
```

## Using Configuration Tools

The plugin provides MCP tools for viewing and managing configuration.

### Getting Configuration

Use `get_config` to view current settings:

```
# Get all configuration
get_config scope="all"

# Get only global configuration
get_config scope="global"

# Get only project configuration (requires active project)
get_config scope="project"
```

The response includes:
- Current configuration values
- Environment variable override status
- For passwords, only indicates if set (not the actual value)

### Setting Configuration

Use `set_config` to update settings:

```
# Update global PostgreSQL host
set_config scope="global" setting="postgres.host" value="db.example.com"

# Update global Ollama port
set_config scope="global" setting="ollama.port" value="11434"

# Update project RAG threshold
set_config scope="project" setting="rag.relevanceThreshold" value="0.8"

# Update project max results
set_config scope="project" setting="rag.maxResults" value="5"
```

### Resetting Configuration

Use `reset_config` to restore default values:

```
# Reset global configuration to defaults
reset_config scope="global" confirm="yes"

# Reset project configuration to defaults
reset_config scope="project" confirm="yes"
```

**Warning**: Reset operations cannot be undone. Consider backing up your configuration first.

## Common Scenarios

### Scenario 1: Using Remote Database

For teams sharing a central PostgreSQL database:

```bash
export COMPOUNDING_POSTGRES_HOST=db.company.com
export COMPOUNDING_POSTGRES_PORT=5432
export COMPOUNDING_POSTGRES_USERNAME=team_user
export COMPOUNDING_POSTGRES_PASSWORD=secure_password
```

### Scenario 2: High-Performance RAG

For projects with many documents requiring faster retrieval:

```json
{
  "rag": {
    "relevanceThreshold": 0.8,
    "maxResults": 5,
    "maxLinkedDocs": 3
  }
}
```

### Scenario 3: Broad Search Coverage

For exploratory queries needing more results:

```json
{
  "rag": {
    "relevanceThreshold": 0.5,
    "maxResults": 10,
    "maxLinkedDocs": 10
  }
}
```

### Scenario 4: Using GPU Server for Ollama

For teams with a dedicated GPU server:

```bash
export COMPOUNDING_OLLAMA_HOST=gpu-server.local
export COMPOUNDING_OLLAMA_PORT=11434
export COMPOUNDING_OLLAMA_MODEL=llama3.1:70b
```

### Scenario 5: Docker Compose Development

The default Docker Compose setup uses non-standard ports to avoid conflicts:

| Service | Standard Port | Docker Compose Port |
|---------|--------------|---------------------|
| PostgreSQL | 5432 | 5433 |
| Ollama | 11434 | 11435 |

This allows the plugin to coexist with other local services.

### Scenario 6: Watching Additional File Types

To index more than just Markdown files:

```json
{
  "fileWatcher": {
    "debounceMs": 500,
    "includePatterns": ["**/*.md", "**/*.txt", "**/docs/**/*.rst"],
    "excludePatterns": ["**/node_modules/**", "**/.git/**", "**/build/**"]
  }
}
```

## Troubleshooting

### Configuration Not Loading

1. Check file permissions on configuration files
2. Verify JSON syntax is valid
3. Ensure the configuration directory exists

```bash
# Create global config directory
mkdir -p ~/.claude/.csharp-compounding-docs
```

### Environment Variables Not Working

1. Verify variables are exported (not just set)
2. Restart the MCP server after changing variables
3. Check variable names are spelled correctly (case-sensitive)

```bash
# Verify environment variable is set
echo $COMPOUNDING_POSTGRES_HOST
```

### Connection Errors

1. Verify services are running:
   ```bash
   docker-compose ps
   ```

2. Test PostgreSQL connection:
   ```bash
   psql -h 127.0.0.1 -p 5433 -U compounding -d compounding_docs
   ```

3. Test Ollama connection:
   ```bash
   curl http://127.0.0.1:11435/api/tags
   ```

### Project Configuration Not Found

1. Ensure you've activated a project first
2. Check the `.csharp-compounding-docs` directory exists in your project
3. Verify `config.json` has valid JSON syntax

### Reset Not Working

The `reset_config` command requires explicit confirmation with `confirm="yes"`. This is a safety measure to prevent accidental data loss.

## Configuration File Locations Summary

| Configuration | Location | Description |
|---------------|----------|-------------|
| Global Config | `~/.claude/.csharp-compounding-docs/global-config.json` | System-wide settings |
| Project Config | `<project>/.csharp-compounding-docs/config.json` | Project-specific settings |
| Docker Compose | `~/.claude/.csharp-compounding-docs/docker-compose.yml` | User's Docker configuration |
| PostgreSQL Data | `~/.claude/.csharp-compounding-docs/data/pgdata/` | Database files |
| Ollama Models | `~/.claude/.csharp-compounding-docs/ollama/models/` | Downloaded LLM models |
| Claude Desktop | See [MCP Server Configuration](#mcp-server-configuration) | MCP server registration |
