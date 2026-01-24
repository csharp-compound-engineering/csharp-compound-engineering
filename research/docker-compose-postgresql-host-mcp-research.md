# Docker Compose for PostgreSQL with Host-Based MCP Server

**Research Date:** January 22, 2026
**Purpose:** Running PostgreSQL with pgvector in Docker Compose while the MCP server runs directly on the host machine for Claude Code plugin development.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Docker Compose for PostgreSQL Only](#2-docker-compose-for-postgresql-only)
3. [PostgreSQL Initialization](#3-postgresql-initialization)
4. [Host-to-Container Connectivity](#4-host-to-container-connectivity)
5. [Development Workflow](#5-development-workflow)
6. [Claude Code Plugin Context](#6-claude-code-plugin-context)
7. [Startup Orchestration](#7-startup-orchestration)
8. [Optional: Ollama Container](#8-optional-ollama-container)
9. [Complete Examples](#9-complete-examples)
10. [Data Management](#10-data-management)
11. [Best Practices](#11-best-practices)
12. [Sources](#12-sources)

---

## 1. Architecture Overview

### Design Pattern

This architecture separates concerns:

```
+------------------+     stdio      +------------------+
|   Claude Code    | <----------->  |    MCP Server    |
|   (Host client)  |                |  (Host process)  |
+------------------+                +--------+---------+
                                             |
                                    TCP localhost:5432
                                             |
                                    +--------v---------+
                                    |   PostgreSQL     |
                                    |   (Docker)       |
                                    +------------------+
```

### Why This Architecture?

1. **Simplified stdio Transport**: MCP server runs directly on host, avoiding container stdio complexity
2. **Easy Debugging**: Run and debug the .NET MCP server in your IDE
3. **Docker for Infrastructure Only**: PostgreSQL (and optionally Ollama) run in containers
4. **Hot Reload**: Modify MCP server code without container rebuilds
5. **Natural Development Flow**: `dotnet run` or `dotnet watch` for the MCP server

### Components

| Component | Runs In | Port | Purpose |
|-----------|---------|------|---------|
| MCP Server (.NET) | Host | stdio | Tools, resources, prompts for Claude |
| PostgreSQL + pgvector | Docker | 5432 | Vector database storage |
| Ollama (optional) | Docker | 11434 | Local LLM for embeddings/inference |

---

## 2. Docker Compose for PostgreSQL Only

### Minimal Configuration

```yaml
# docker-compose.yml
services:
  postgres:
    image: pgvector/pgvector:pg17
    container_name: rag-postgres
    restart: unless-stopped
    environment:
      POSTGRES_USER: rag_user
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-development_password}
      POSTGRES_DB: rag_db
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
      - ./init:/docker-entrypoint-initdb.d:ro
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U rag_user -d rag_db"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s

volumes:
  pgdata:
```

### Environment Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `POSTGRES_PASSWORD` | Yes | - | Superuser password (use `.env` file) |
| `POSTGRES_USER` | No | `postgres` | Superuser username |
| `POSTGRES_DB` | No | `$POSTGRES_USER` | Default database name |
| `PGDATA` | No | `/var/lib/postgresql/data` | Data directory |

**Note**: For PostgreSQL 18+, `PGDATA` changes to `/var/lib/postgresql/18/docker`.

### Port Mapping

```yaml
ports:
  - "5432:5432"           # Bind to all interfaces
  - "127.0.0.1:5432:5432" # Bind to localhost only (more secure)
```

The second format restricts access to local connections only, which is recommended for development.

### Volume Mounting

```yaml
volumes:
  # Named volume for data (recommended for persistence)
  - pgdata:/var/lib/postgresql/data

  # Bind mount for initialization scripts (read-only)
  - ./init:/docker-entrypoint-initdb.d:ro
```

### Health Checks

PostgreSQL health check using `pg_isready`:

```yaml
healthcheck:
  test: ["CMD-SHELL", "pg_isready -U rag_user -d rag_db"]
  interval: 10s
  timeout: 5s
  retries: 5
  start_period: 30s
```

**Important**: The `-U` flag prevents log warnings. Without it, `pg_isready` tries to connect using the system user.

For stricter checking (verifies query execution, not just port):

```yaml
healthcheck:
  test: ["CMD-SHELL", "pg_isready -U rag_user -d rag_db && psql -U rag_user -d rag_db -c 'SELECT 1'"]
  interval: 10s
  timeout: 5s
  retries: 5
  start_period: 30s
```

---

## 3. PostgreSQL Initialization

### How docker-entrypoint-initdb.d Works

Scripts in `/docker-entrypoint-initdb.d/` execute:
- **Only once**: During first container startup
- **When data directory is empty**: If `/var/lib/postgresql/data` already exists, scripts are skipped
- **In alphabetical order**: Use numeric prefixes (`01_`, `02_`) for ordering

Supported file types:
- `.sql` - SQL scripts
- `.sql.gz` - Gzipped SQL scripts
- `.sh` - Shell scripts

### Initialization Script for RAG with pgvector

Create `init/01_schema.sql`:

```sql
-- Enable pgvector extension
CREATE EXTENSION IF NOT EXISTS vector;

-- Optional: Full-text search support
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Main documents table
CREATE TABLE IF NOT EXISTS documents (
    id BIGSERIAL PRIMARY KEY,
    external_id TEXT UNIQUE,

    -- Content
    title TEXT,
    content TEXT NOT NULL,
    content_hash BYTEA,

    -- Chunking
    chunk_index INTEGER DEFAULT 0,
    chunk_count INTEGER DEFAULT 1,
    parent_id BIGINT REFERENCES documents(id) ON DELETE CASCADE,

    -- Vector embedding (adjust dimensions to your model)
    embedding vector(768),

    -- Source tracking
    source_type TEXT CHECK (source_type IN ('pdf', 'web', 'api', 'manual')),
    source_url TEXT,

    -- Flexible metadata
    metadata JSONB DEFAULT '{}',
    tags TEXT[] DEFAULT '{}',

    -- Timestamps
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    indexed_at TIMESTAMPTZ
);

-- Collections for organization
CREATE TABLE IF NOT EXISTS collections (
    id BIGSERIAL PRIMARY KEY,
    name TEXT UNIQUE NOT NULL,
    description TEXT,
    embedding_model TEXT NOT NULL DEFAULT 'nomic-embed-text',
    embedding_dimensions INTEGER NOT NULL DEFAULT 768,
    settings JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- Add collection reference
ALTER TABLE documents ADD COLUMN IF NOT EXISTS collection_id BIGINT REFERENCES collections(id);

-- Create default collection
INSERT INTO collections (name, description)
VALUES ('default', 'Default document collection')
ON CONFLICT (name) DO NOTHING;

-- Create HNSW index for vector similarity search
CREATE INDEX IF NOT EXISTS idx_documents_embedding_hnsw
ON documents USING hnsw (embedding vector_cosine_ops)
WITH (m = 16, ef_construction = 64);

-- Supporting indexes
CREATE INDEX IF NOT EXISTS idx_documents_metadata ON documents USING gin (metadata jsonb_path_ops);
CREATE INDEX IF NOT EXISTS idx_documents_tags ON documents USING gin (tags);
CREATE INDEX IF NOT EXISTS idx_documents_collection ON documents (collection_id);
CREATE INDEX IF NOT EXISTS idx_documents_created_at ON documents (created_at DESC);
CREATE INDEX IF NOT EXISTS idx_documents_source_type ON documents (source_type);
CREATE INDEX IF NOT EXISTS idx_documents_content_hash ON documents (content_hash);

-- Updated timestamp trigger
CREATE OR REPLACE FUNCTION update_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS documents_updated_at ON documents;
CREATE TRIGGER documents_updated_at
    BEFORE UPDATE ON documents
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at();

-- Helper function for similarity search
CREATE OR REPLACE FUNCTION search_documents(
    query_embedding vector(768),
    match_count INTEGER DEFAULT 10,
    filter_metadata JSONB DEFAULT NULL,
    filter_collection_id BIGINT DEFAULT NULL,
    similarity_threshold FLOAT DEFAULT 0.0
)
RETURNS TABLE (
    id BIGINT,
    content TEXT,
    metadata JSONB,
    similarity FLOAT
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        d.id,
        d.content,
        d.metadata,
        1 - (d.embedding <=> query_embedding) AS similarity
    FROM documents d
    WHERE
        d.embedding IS NOT NULL
        AND (filter_collection_id IS NULL OR d.collection_id = filter_collection_id)
        AND (filter_metadata IS NULL OR d.metadata @> filter_metadata)
        AND 1 - (d.embedding <=> query_embedding) >= similarity_threshold
    ORDER BY d.embedding <=> query_embedding
    LIMIT match_count;
END;
$$ LANGUAGE plpgsql;
```

### Idempotent Initialization

All statements use `IF NOT EXISTS` or `ON CONFLICT` to handle restarts gracefully. This ensures:
- Safe to run multiple times
- Won't fail if objects already exist
- Supports incremental schema additions

### Important: Extension Creation Note

There's a known quirk where `CREATE EXTENSION pg_vector;` (with underscore) doesn't work in init scripts. Always use:

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

If you encounter issues, create an `init.sh` script instead:

```bash
#!/bin/bash
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE EXTENSION IF NOT EXISTS vector;
EOSQL
```

---

## 4. Host-to-Container Connectivity

### Connection from Host to PostgreSQL Container

When the MCP server runs on the host machine, connect to PostgreSQL using:

| Setting | Value |
|---------|-------|
| Host | `localhost` |
| Port | `5432` (or your mapped port) |
| Database | `rag_db` |
| Username | `rag_user` |
| Password | (from environment/config) |

### .NET Connection String

```csharp
// Standard format
var connectionString = "Host=localhost;Port=5432;Database=rag_db;Username=rag_user;Password=your_password";

// With connection pooling
var connectionString = "Host=localhost;Port=5432;Database=rag_db;Username=rag_user;Password=your_password;Pooling=true;MinPoolSize=5;MaxPoolSize=100";
```

### appsettings.json Configuration

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=rag_db;Username=rag_user;Password=${POSTGRES_PASSWORD}"
  }
}
```

### Environment Variable Override

```bash
# Set in shell before running MCP server
export ConnectionStrings__PostgreSQL="Host=localhost;Port=5432;Database=rag_db;Username=rag_user;Password=dev_password"
```

### Network Considerations

**Host connecting to container**: Use `localhost:5432`

**Container connecting to host** (reverse scenario): Use `host.docker.internal` (macOS/Windows) or `172.17.0.1` (Linux)

**Firewall**: Ensure port 5432 is accessible. The Docker port mapping handles this, but verify with:

```bash
# Test connection from host
psql -h localhost -p 5432 -U rag_user -d rag_db
```

---

## 5. Development Workflow

### Starting PostgreSQL

```bash
# Start PostgreSQL in the background
docker compose up -d

# View logs
docker compose logs -f postgres

# Check status
docker compose ps
```

### Stopping PostgreSQL

```bash
# Stop containers (data persists in volume)
docker compose down

# Stop and remove volumes (resets database)
docker compose down -v
```

### Resetting the Database

```bash
# Full reset: remove volumes and restart
docker compose down -v
docker compose up -d
```

### Viewing PostgreSQL Logs

```bash
# Follow logs
docker compose logs -f postgres

# Recent logs
docker compose logs --tail=100 postgres
```

### Connecting with psql from Host

```bash
# Using psql client (must be installed)
psql -h localhost -p 5432 -U rag_user -d rag_db

# Or use docker exec
docker compose exec postgres psql -U rag_user -d rag_db
```

### Connecting with GUI Tools

Configure your preferred PostgreSQL client (pgAdmin, DBeaver, DataGrip):

| Setting | Value |
|---------|-------|
| Host | `localhost` |
| Port | `5432` |
| Database | `rag_db` |
| Username | `rag_user` |
| Password | (your password) |

---

## 6. Claude Code Plugin Context

### How MCP Servers are Launched

Claude Code launches MCP servers as child processes using stdio transport:
1. Claude Code reads MCP configuration
2. Spawns the server process with specified command/args
3. Communicates via stdin/stdout (JSON-RPC)
4. Logs go to stderr (critical for stdio transport)

### Configuration Locations

**Claude Desktop** (`claude_desktop_config.json`):
- macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`
- Windows: `%APPDATA%\Claude\claude_desktop_config.json`

**Claude Code** (`.mcp.json` in project root):
```json
{
  "mcpServers": {
    "rag-server": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "src/RagMcpServer/RagMcpServer.csproj"],
      "env": {
        "ConnectionStrings__PostgreSQL": "Host=localhost;Port=5432;Database=rag_db;Username=rag_user;Password=dev_password",
        "Ollama__Endpoint": "http://localhost:11434"
      }
    }
  }
}
```

### Production Configuration (Published Executable)

```json
{
  "mcpServers": {
    "rag-server": {
      "type": "stdio",
      "command": "/path/to/RagMcpServer",
      "env": {
        "ConnectionStrings__PostgreSQL": "Host=localhost;Port=5432;Database=rag_db;Username=rag_user;Password=secure_password"
      }
    }
  }
}
```

### Environment Variables in MCP Config

Pass connection strings and configuration via the `env` section:

```json
{
  "env": {
    "DOTNET_ENVIRONMENT": "Development",
    "ConnectionStrings__PostgreSQL": "Host=localhost;...",
    "Rag__EmbeddingModel": "nomic-embed-text",
    "Rag__TopK": "5"
  }
}
```

### Working Directory Considerations

MCP servers are launched with Claude Code's working directory. Use absolute paths or configure paths relative to the project:

```json
{
  "args": ["run", "--project", "${CLAUDE_PLUGIN_ROOT}/src/RagMcpServer/RagMcpServer.csproj"]
}
```

### Critical: PostgreSQL Must Be Running

The MCP server depends on PostgreSQL. Ensure containers are started before launching Claude Code, or implement graceful degradation in your MCP server.

---

## 7. Startup Orchestration

### Challenge

Claude Code launches the MCP server immediately. PostgreSQL must be ready to accept connections, or the server will fail.

### Solution 1: Manual Startup Sequence

Start PostgreSQL before using Claude Code:

```bash
# Terminal 1: Start PostgreSQL
cd /path/to/project
docker compose up -d

# Wait for healthy status
docker compose ps  # Should show "healthy"

# Then start Claude Code
```

### Solution 2: Startup Script

Create `start-dev.sh`:

```bash
#!/bin/bash
set -e

echo "Starting PostgreSQL..."
docker compose up -d

echo "Waiting for PostgreSQL to be healthy..."
until docker compose exec postgres pg_isready -U rag_user -d rag_db > /dev/null 2>&1; do
    echo "Waiting for PostgreSQL..."
    sleep 2
done

echo "PostgreSQL is ready!"
echo "You can now use Claude Code with the MCP server."
```

### Solution 3: Wrapper Script for MCP Server

Create a wrapper that MCP configuration points to:

`scripts/run-mcp-server.sh`:
```bash
#!/bin/bash
set -e

# Start PostgreSQL if not running
if ! docker compose ps | grep -q "postgres.*healthy"; then
    docker compose up -d postgres

    # Wait for health
    until docker compose exec -T postgres pg_isready -U rag_user -d rag_db > /dev/null 2>&1; do
        sleep 1
    done
fi

# Run the MCP server
exec dotnet run --project src/RagMcpServer/RagMcpServer.csproj
```

MCP configuration:
```json
{
  "mcpServers": {
    "rag-server": {
      "type": "stdio",
      "command": "/bin/bash",
      "args": ["scripts/run-mcp-server.sh"]
    }
  }
}
```

### Solution 4: Graceful Degradation in MCP Server

Handle missing database gracefully in code:

```csharp
public class VectorStoreService : IVectorStoreService
{
    private readonly NpgsqlDataSource? _dataSource;
    private readonly ILogger<VectorStoreService> _logger;
    private bool _isAvailable = false;

    public VectorStoreService(
        IConfiguration config,
        ILogger<VectorStoreService> logger)
    {
        _logger = logger;

        try
        {
            var connectionString = config.GetConnectionString("PostgreSQL");
            if (!string.IsNullOrEmpty(connectionString))
            {
                var builder = new NpgsqlDataSourceBuilder(connectionString);
                builder.UseVector();
                _dataSource = builder.Build();
                _isAvailable = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PostgreSQL not available. Vector operations disabled.");
        }
    }

    public async Task<List<SearchResult>> SearchAsync(float[] embedding, int limit, CancellationToken ct)
    {
        if (!_isAvailable)
        {
            return new List<SearchResult>
            {
                new() { Content = "Vector database not available. Please start PostgreSQL." }
            };
        }

        // Normal search implementation...
    }
}
```

### Solution 5: Health Check in MCP Tool

```csharp
[McpServerToolType]
public static class HealthTools
{
    [McpServerTool]
    [Description("Check if the RAG system is fully operational")]
    public static async Task<string> CheckHealth(
        IVectorStoreService vectorStore,
        CancellationToken ct)
    {
        var status = new
        {
            PostgreSQL = await vectorStore.IsAvailableAsync(ct),
            Timestamp = DateTime.UtcNow
        };

        return JsonSerializer.Serialize(status);
    }
}
```

---

## 8. Optional: Ollama Container

### Adding Ollama to Docker Compose

```yaml
# docker-compose.yml
services:
  postgres:
    # ... (previous postgres config)

  ollama:
    image: ollama/ollama:latest
    container_name: rag-ollama
    restart: unless-stopped
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama
    # GPU support (uncomment for NVIDIA GPU)
    # deploy:
    #   resources:
    #     reservations:
    #       devices:
    #         - driver: nvidia
    #           count: all
    #           capabilities: [gpu]

volumes:
  pgdata:
  ollama_data:
```

### GPU Support Configuration

**NVIDIA GPU (using deploy resources)**:
```yaml
ollama:
  image: ollama/ollama:latest
  deploy:
    resources:
      reservations:
        devices:
          - driver: nvidia
            count: all
            capabilities: [gpu]
```

**NVIDIA GPU (using runtime)**:
```yaml
ollama:
  image: ollama/ollama:latest
  runtime: nvidia
  environment:
    - NVIDIA_VISIBLE_DEVICES=all
```

**Prerequisites for GPU**:
1. NVIDIA GPU installed
2. NVIDIA drivers installed
3. NVIDIA Container Toolkit installed
4. Run: `sudo nvidia-ctk runtime configure --runtime=docker`

### Model Persistence

The volume `ollama_data:/root/.ollama` ensures:
- Downloaded models persist across restarts
- No re-downloading after container recreation
- Fast startup after initial model pull

### Pulling Models

```bash
# Pull embedding model
docker compose exec ollama ollama pull nomic-embed-text

# Pull inference model
docker compose exec ollama ollama pull llama3.2

# List models
docker compose exec ollama ollama list
```

### Connecting from Host MCP Server

```csharp
// In appsettings.json
{
  "Ollama": {
    "Endpoint": "http://localhost:11434"
  }
}

// In code
var ollamaUri = new Uri(config["Ollama:Endpoint"] ?? "http://localhost:11434");
var ollamaClient = new OllamaApiClient(ollamaUri);
```

### Health Check for Ollama

```yaml
ollama:
  healthcheck:
    test: ["CMD-SHELL", "curl -f http://localhost:11434/api/tags || exit 1"]
    interval: 30s
    timeout: 10s
    retries: 3
    start_period: 60s
```

---

## 9. Complete Examples

### Full docker-compose.yml

```yaml
# docker-compose.yml
services:
  postgres:
    image: pgvector/pgvector:pg17
    container_name: rag-postgres
    restart: unless-stopped
    environment:
      POSTGRES_USER: ${POSTGRES_USER:-rag_user}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:?POSTGRES_PASSWORD is required}
      POSTGRES_DB: ${POSTGRES_DB:-rag_db}
    ports:
      - "127.0.0.1:5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
      - ./init:/docker-entrypoint-initdb.d:ro
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER:-rag_user} -d ${POSTGRES_DB:-rag_db}"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s
    command:
      - "postgres"
      - "-c"
      - "shared_buffers=256MB"
      - "-c"
      - "work_mem=64MB"
      - "-c"
      - "maintenance_work_mem=512MB"
      - "-c"
      - "effective_cache_size=1GB"
      - "-c"
      - "max_connections=100"

  ollama:
    image: ollama/ollama:latest
    container_name: rag-ollama
    restart: unless-stopped
    ports:
      - "127.0.0.1:11434:11434"
    volumes:
      - ollama_data:/root/.ollama
    healthcheck:
      test: ["CMD-SHELL", "curl -sf http://localhost:11434/api/tags > /dev/null || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 60s
    profiles:
      - with-ollama
    # Uncomment for NVIDIA GPU support
    # deploy:
    #   resources:
    #     reservations:
    #       devices:
    #         - driver: nvidia
    #           count: all
    #           capabilities: [gpu]

volumes:
  pgdata:
    driver: local
  ollama_data:
    driver: local
```

### Environment File (.env)

```bash
# .env (add to .gitignore!)
POSTGRES_USER=rag_user
POSTGRES_PASSWORD=your_secure_password_here
POSTGRES_DB=rag_db
```

### .gitignore Addition

```gitignore
# Docker secrets
.env
*.env.local
```

### MCP Configuration (.mcp.json)

```json
{
  "mcpServers": {
    "rag-server": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "src/RagMcpServer/RagMcpServer.csproj", "--no-build"],
      "env": {
        "DOTNET_ENVIRONMENT": "Development",
        "ConnectionStrings__PostgreSQL": "Host=localhost;Port=5432;Database=rag_db;Username=rag_user;Password=your_secure_password_here",
        "Ollama__Endpoint": "http://localhost:11434",
        "Rag__EmbeddingModel": "nomic-embed-text",
        "Rag__EmbeddingDimensions": "768"
      }
    }
  }
}
```

### appsettings.json for MCP Server

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Npgsql": "Warning"
    },
    "Console": {
      "LogToStandardErrorThreshold": "Trace"
    }
  },
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=rag_db;Username=rag_user;Password=dev_password"
  },
  "Ollama": {
    "Endpoint": "http://localhost:11434"
  },
  "Rag": {
    "EmbeddingModel": "nomic-embed-text",
    "EmbeddingDimensions": 768,
    "TopK": 5,
    "SimilarityThreshold": 0.7
  }
}
```

### Startup Script (start-dev.sh)

```bash
#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "=== RAG Development Environment ==="

# Start PostgreSQL
echo "Starting PostgreSQL..."
docker compose up -d postgres

# Wait for PostgreSQL
echo "Waiting for PostgreSQL..."
until docker compose exec -T postgres pg_isready -U rag_user -d rag_db > /dev/null 2>&1; do
    sleep 1
done
echo "PostgreSQL is ready!"

# Optionally start Ollama
if [[ "$1" == "--with-ollama" ]]; then
    echo "Starting Ollama..."
    docker compose --profile with-ollama up -d ollama

    echo "Waiting for Ollama..."
    until curl -sf http://localhost:11434/api/tags > /dev/null 2>&1; do
        sleep 2
    done
    echo "Ollama is ready!"

    # Pull embedding model if not present
    if ! docker compose exec -T ollama ollama list | grep -q "nomic-embed-text"; then
        echo "Pulling nomic-embed-text model..."
        docker compose exec -T ollama ollama pull nomic-embed-text
    fi
fi

echo ""
echo "=== Environment Ready ==="
echo "PostgreSQL: localhost:5432"
if [[ "$1" == "--with-ollama" ]]; then
    echo "Ollama: localhost:11434"
fi
echo ""
echo "You can now use Claude Code with the MCP server."
```

### .NET Service Registration

```csharp
// Program.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pgvector.Npgsql;

var builder = Host.CreateApplicationBuilder(args);

// CRITICAL: All logs to stderr for stdio transport
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// PostgreSQL with pgvector
builder.Services.AddSingleton<NpgsqlDataSource>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("PostgreSQL")
        ?? throw new InvalidOperationException("PostgreSQL connection string is required");

    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    dataSourceBuilder.UseVector();
    return dataSourceBuilder.Build();
});

// Custom services
builder.Services.AddSingleton<IVectorStoreService, PostgresVectorStoreService>();
builder.Services.AddSingleton<IEmbeddingService, OllamaEmbeddingService>();
builder.Services.AddHttpClient();

// MCP Server
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly();

await builder.Build().RunAsync();
```

---

## 10. Data Management

### Backup Strategies

**Using pg_dump from container**:
```bash
# Backup to file
docker compose exec -T postgres pg_dump -U rag_user rag_db > backup.sql

# Backup with compression
docker compose exec -T postgres pg_dump -U rag_user -Fc rag_db > backup.dump

# Backup specific tables
docker compose exec -T postgres pg_dump -U rag_user -t documents rag_db > documents.sql
```

**Automated backup script**:
```bash
#!/bin/bash
BACKUP_DIR="/backups"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
docker compose exec -T postgres pg_dump -U rag_user -Fc rag_db > "$BACKUP_DIR/rag_db_$TIMESTAMP.dump"

# Keep only last 7 backups
ls -tp "$BACKUP_DIR"/*.dump | tail -n +8 | xargs -r rm
```

### Restore Strategies

```bash
# Restore from SQL
docker compose exec -T postgres psql -U rag_user -d rag_db < backup.sql

# Restore from custom format
docker compose exec -T postgres pg_restore -U rag_user -d rag_db backup.dump

# Reset and restore
docker compose down -v
docker compose up -d
sleep 10  # Wait for init
docker compose exec -T postgres pg_restore -U rag_user -d rag_db backup.dump
```

### Volume Management

```bash
# List volumes
docker volume ls

# Inspect volume
docker volume inspect csharp-compound-engineering_pgdata

# Remove unused volumes
docker volume prune

# Remove specific volume (WARNING: deletes data!)
docker volume rm csharp-compound-engineering_pgdata
```

### Database Migrations

For schema changes after initial setup, use migration files:

`migrations/002_add_column.sql`:
```sql
-- Add new column if it doesn't exist
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'documents' AND column_name = 'new_column'
    ) THEN
        ALTER TABLE documents ADD COLUMN new_column TEXT;
    END IF;
END $$;
```

Apply migrations:
```bash
docker compose exec -T postgres psql -U rag_user -d rag_db < migrations/002_add_column.sql
```

### Clearing Vector Data

```sql
-- Delete all documents (keeps schema)
TRUNCATE documents RESTART IDENTITY CASCADE;

-- Delete from specific collection
DELETE FROM documents WHERE collection_id = 1;

-- Reindex after major deletions
REINDEX INDEX CONCURRENTLY idx_documents_embedding_hnsw;
VACUUM ANALYZE documents;
```

---

## 11. Best Practices

### Named Volumes vs Bind Mounts

| Aspect | Named Volumes | Bind Mounts |
|--------|---------------|-------------|
| Data location | Docker-managed | Host filesystem |
| Portability | High | Low (path-dependent) |
| Performance | Optimal | Varies |
| Backup access | Via container | Direct on host |
| Recommended for | Production data | Config files, init scripts |

**Recommendation**: Use named volumes for PostgreSQL data, bind mounts for initialization scripts.

### Environment Variable Security

1. **Never commit secrets**: Add `.env` to `.gitignore`
2. **Use `.env` files**: Store passwords outside compose file
3. **Consider Docker secrets**: For production deployments

```yaml
# docker-compose.yml with secrets
services:
  postgres:
    environment:
      POSTGRES_PASSWORD_FILE: /run/secrets/db_password
    secrets:
      - db_password

secrets:
  db_password:
    file: ./secrets/db_password.txt
```

### Resource Limits

```yaml
services:
  postgres:
    deploy:
      resources:
        limits:
          memory: 4G
          cpus: '2'
        reservations:
          memory: 1G
          cpus: '1'
```

### PostgreSQL Tuning for Containers

```yaml
command:
  - "postgres"
  - "-c"
  - "shared_buffers=256MB"        # 25% of container memory
  - "-c"
  - "work_mem=64MB"                # Per-operation memory
  - "-c"
  - "maintenance_work_mem=512MB"   # For index builds
  - "-c"
  - "effective_cache_size=1GB"     # Query planner hint
  - "-c"
  - "max_connections=100"          # Connection limit
```

### Development vs Production

**Development**:
- Use default passwords (documented in `.env.example`)
- Bind to localhost only (`127.0.0.1:5432:5432`)
- Enable debug logging
- Use profiles for optional services

**Production**:
- Use strong, rotated passwords
- Don't expose ports externally
- Use Docker secrets
- Set resource limits
- Enable backup automation
- Use read replicas for scale

### Healthcheck Recommendations

```yaml
healthcheck:
  test: ["CMD-SHELL", "pg_isready -U rag_user -d rag_db"]
  interval: 10s      # Check every 10 seconds
  timeout: 5s        # Fail if check takes longer
  retries: 5         # Mark unhealthy after 5 failures
  start_period: 30s  # Grace period for startup
```

### Logging Configuration

```yaml
services:
  postgres:
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
```

---

## 12. Sources

### Docker Compose and Networking
- [Docker Compose Networking](https://docs.docker.com/compose/how-tos/networking/)
- [Control Startup Order - Docker Compose](https://docs.docker.com/compose/how-tos/startup-order/)
- [Docker Compose Health Checks Guide](https://last9.io/blog/docker-compose-health-checks/)
- [From Docker CLI to Docker Compose](https://www.thedigitalcatonline.com/blog/2022/02/19/from-docker-cli-to-docker-compose/)

### PostgreSQL and pgvector
- [PostgreSQL Docker Hub](https://hub.docker.com/_/postgres)
- [pgvector/pgvector Docker Hub](https://hub.docker.com/r/pgvector/pgvector)
- [Setting Up PostgreSQL with pgvector Using Docker](https://dev.to/yukaty/setting-up-postgresql-with-pgvector-using-docker-hcl)
- [Automating Postgres and pgvector Setup with Docker](https://alpeshkumar.com/docker/automating-postgres-and-pgvector-setup-with-docker/)
- [Setting Up Postgres pgvector Docker RAG](https://userjot.com/blog/setting-up-postgres-pgvector-docker-rag)

### Docker Volumes and Data Persistence
- [Docker Volumes Documentation](https://docs.docker.com/engine/storage/volumes/)
- [Docker Volumes vs Bind Mounts](https://blog.logrocket.com/docker-volumes-vs-bind-mounts/)
- [Running PostgreSQL in Docker with Persistent Volume](https://dev.to/lovestaco/running-postgresql-in-docker-with-persistent-volume-4joe)
- [Docker Volumes in Production Guide](https://blog.shukebeta.com/2024/10/23/docker-volumes-in-production-a-practical-guide-to-named-volumes-vs-bind-mounts/)

### MCP Configuration
- [Claude Code MCP Documentation](https://code.claude.com/docs/en/mcp)
- [MCP JSON Configuration - FastMCP](https://gofastmcp.com/integrations/mcp-json-configuration)
- [Configuring MCP Tools in Claude Code](https://scottspence.com/posts/configuring-mcp-tools-in-claude-code)
- [Add MCP Servers to Claude Code - MCPcat](https://mcpcat.io/guides/adding-an-mcp-server-to-claude-code/)

### Ollama with Docker
- [Ollama Docker Documentation](https://docs.ollama.com/docker)
- [Ollama Docker Hub](https://hub.docker.com/r/ollama/ollama)
- [Running Ollama with Docker Compose and GPUs](https://dev.to/ajeetraina/running-ollama-with-docker-compose-and-gpus-lkn)
- [How to Run Ollama with Docker Compose and GPU Support](https://sleeplessbeastie.eu/2025/12/04/how-to-run-ollama-with-docker-compose-and-gpu-support/)

### Secrets and Security
- [Docker Compose Secrets](https://docs.docker.com/compose/how-tos/use-secrets/)
- [Managing Secrets in Docker Compose](https://phase.dev/blog/docker-compose-secrets/)
- [Docker Compose Secrets Guide](https://www.bitdoze.com/docker-compose-secrets/)

### .NET PostgreSQL
- [Npgsql Connection String Parameters](https://www.npgsql.org/doc/connection-string-parameters.html)
- [Npgsql Basic Usage](https://www.npgsql.org/doc/basic-usage.html)
- [PostgreSQL Connection Strings](https://www.connectionstrings.com/postgresql/)

---

*This research report was compiled for the csharp-compound-engineering project to support building a Claude Code MCP plugin with PostgreSQL/pgvector running in Docker Compose while the MCP server runs directly on the host.*
