# Infrastructure Specification

> **Status**: [DRAFT]
> **Parent**: [SPEC.md](../SPEC.md)

> **Background**: Comprehensive research on Docker Compose configuration, PostgreSQL initialization, health checks, and MCP server connectivity patterns. See [Docker Compose for PostgreSQL with Host-Based MCP Server](../research/docker-compose-postgresql-host-mcp-research.md).

---

## Overview

Infrastructure consists of:
- **Docker Compose stack** (PostgreSQL + Ollama) - shared across all MCP server instances
- **PowerShell launcher script** - discovers/starts containers, launches MCP server
- **Home directory configuration** - `~/.claude/.csharp-compounding-docs/`
- **Cleanup Console App** - periodic orphan data removal

---

## Prerequisites

### Required

| Requirement | Version | Notes |
|-------------|---------|-------|
| **Docker Desktop** | Latest | Required - no alternative deployment supported |
| **Docker Compose** | v2+ | Included with Docker Desktop |
| **.NET Runtime** | 10.0+ | For MCP server execution |
| **PowerShell** | 7+ | Cross-platform, for launcher script |

### Docker Requirement

**Docker is a hard requirement.** If Docker is not installed or not running, the plugin will fail to activate with a clear error message:

```
ERROR: Docker is required but not installed or not running.

Please install Docker Desktop from:
  https://www.docker.com/products/docker-desktop/

After installation, ensure Docker is running and try again.
```

**No alternative deployment** (native PostgreSQL/Ollama) is supported in MVP. This simplifies:
- Installation complexity
- Cross-platform compatibility
- Version management

---

## Directory Structure

```
~/.claude/.csharp-compounding-docs/
├── docker-compose.yml           # Docker Compose configuration
├── ollama-config.json           # Ollama settings (GPU, models)
├── data/                        # PostgreSQL data volume
│   └── pgdata/
└── ollama/                      # Ollama model storage (optional)
    └── models/
```

---

## Docker Compose Stack

### Location

Template bundled with plugin, copied to `~/.claude/.csharp-compounding-docs/docker-compose.yml` on first run.

### Services

#### PostgreSQL + pgvector + Liquibase

> **Background**: Research on PostgreSQL Docker image variants, data persistence strategies, volume mounting, and initialization scripts. See [PostgreSQL Docker Data Mounting Research](../research/postgresql-docker-data-mounting-research.md).

> **Background**: Detailed guide on integrating Liquibase with pgvector in Docker, including custom Dockerfile patterns, init scripts, and changelog organization for tenant management schemas. See [Liquibase with PostgreSQL/pgvector in Docker](../research/liquibase-pgvector-docker-init.md).

Uses a custom Dockerfile that extends `pgvector/pgvector:pg16` with Liquibase for schema management.

```yaml
services:
  postgres:
    build:
      context: ./docker/postgres
      dockerfile: Dockerfile
    container_name: csharp-compounding-docs-postgres
    environment:
      POSTGRES_USER: compounding
      POSTGRES_PASSWORD: compounding
      POSTGRES_DB: compounding_docs
    volumes:
      - ~/.claude/.csharp-compounding-docs/data/pgdata:/var/lib/postgresql/data
      - ./docker/postgres/changelog:/liquibase/changelog
    ports:
      - "127.0.0.1:5433:5432"  # Non-standard port to avoid conflicts
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U compounding -d compounding_docs"]
      interval: 10s
      timeout: 5s
      retries: 5
```

**Custom Dockerfile** (`docker/postgres/Dockerfile`):
```dockerfile
FROM pgvector/pgvector:pg16

# Install Liquibase
RUN apt-get update && apt-get install -y wget default-jre && \
    wget -qO- https://github.com/liquibase/liquibase/releases/download/v4.25.0/liquibase-4.25.0.tar.gz | tar xz -C /opt && \
    ln -s /opt/liquibase /usr/local/bin/liquibase

# Copy init script
COPY init-db.sh /docker-entrypoint-initdb.d/

# Copy Liquibase changelog
COPY changelog /liquibase/changelog
```

**Init Script** (`docker/postgres/init-db.sh`):
```bash
#!/bin/bash
set -e

# Enable pgvector extension (pre-installed in pgvector/pgvector image)
# Must be enabled per-database before Liquibase creates tables with vector columns
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE EXTENSION IF NOT EXISTS vector;
EOSQL

# Run Liquibase migrations
# See spec/mcp-server/liquibase-changelog.md for changelog format details
# For changelog XML format, rollback strategies, and PostgreSQL-specific considerations,
# see research/liquibase-changelog-format-research.md
liquibase --changeLogFile=/liquibase/changelog/changelog.xml \
          --url="jdbc:postgresql://localhost:5432/$POSTGRES_DB" \
          --username="$POSTGRES_USER" \
          --password="$POSTGRES_PASSWORD" \
          update
```

#### Ollama

> **Background**: Comprehensive guide on running Ollama in Docker with GPU passthrough for NVIDIA, AMD, and Apple Silicon platforms, including performance tuning and security considerations. See [Ollama Docker GPU Research Report](../research/ollama-docker-gpu-research.md).

```yaml
  ollama:
    image: ollama/ollama:latest
    container_name: csharp-compounding-docs-ollama
    volumes:
      - ~/.claude/.csharp-compounding-docs/ollama/models:/root/.ollama
    ports:
      - "127.0.0.1:11435:11434"  # Non-standard port to avoid conflicts
    # GPU configuration injected by launcher script based on ollama-config.json
```

### Named Stack

The compose stack uses a project name for identification:

```bash
docker compose -p csharp-compounding-docs up -d
```

---

## Ollama Configuration

> **Background**: Details on GPU configuration options, environment variables for performance tuning, and multi-GPU setups are covered in the research. See [Ollama Docker GPU Research Report](../research/ollama-docker-gpu-research.md).

### File: `~/.claude/.csharp-compounding-docs/ollama-config.json`

```json
{
  "generation_model": "mistral",
  "gpu": {
    "enabled": false,
    "type": null
  }
}
```

**Note**: The embedding model is fixed to `mxbai-embed-large` (1024 dimensions) and is not configurable.

### GPU Configuration Options

| Platform | `type` value | Docker Config |
|----------|--------------|---------------|
| No GPU | `null` | No additional config |
| NVIDIA CUDA | `"nvidia"` | `deploy.resources.reservations.devices` |
| AMD ROCm | `"amd"` | `devices: ["/dev/kfd", "/dev/dri"]` |
| Apple Metal | `"apple"` | N/A (use native Ollama on macOS) |

### NVIDIA GPU Configuration (injected)

```yaml
  ollama:
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: all
              capabilities: [gpu]
```

### AMD GPU Configuration (injected)

```yaml
  ollama:
    devices:
      - /dev/kfd
      - /dev/dri
    group_add:
      - video
```

### Apple Silicon Note

> **See also**: [mcp-server.md - Apple Silicon Note](./mcp-server.md#apple-silicon-note) for MCP server runtime behavior when Ollama isn't running.

For macOS with Apple Silicon, the launcher script should detect this and assume Ollama is running natively instead of in Docker (Metal acceleration not available in Docker). It should look for it at the default port.

---

## PowerShell Launcher Script

### File: `scripts/start-infrastructure.ps1`

### Shebang

```powershell
#!/usr/bin/env pwsh
```

### Purpose

1. Ensure `~/.claude/.csharp-compounding-docs/` directory exists
2. Copy template files if missing (docker-compose.yml, ollama-config.json)
3. Read `ollama-config.json` and modify docker-compose.yml for GPU if needed
4. Check if Docker Compose stack is running
5. If not running, start it
6. Wait for services to be healthy
7. Output connection parameters for MCP server

### Output Format

Script outputs JSON to stdout for the MCP server to consume:

```json
{
  "postgres": {
    "host": "127.0.0.1",
    "port": 5433,
    "database": "compounding_docs",
    "username": "compounding",
    "password": "compounding"
  },
  "ollama": {
    "host": "127.0.0.1",
    "port": 11435
  }
}
```

### Script Flow

```powershell
#!/usr/bin/env pwsh

$CompoundingDocsDir = Join-Path $HOME ".claude/.csharp-compounding-docs"
$DockerComposeFile = Join-Path $CompoundingDocsDir "docker-compose.yml"
$OllamaConfigFile = Join-Path $CompoundingDocsDir "ollama-config.json"

# 1. Ensure directory exists
if (-not (Test-Path $CompoundingDocsDir)) {
    New-Item -ItemType Directory -Path $CompoundingDocsDir -Force
}

# 2. Copy templates if missing
# ... (copy from plugin bundle)

# 3. Read ollama config and modify compose if GPU enabled
# ... (parse JSON, inject GPU config)

# 4. Check if stack is running
$stackStatus = docker compose -p csharp-compounding-docs ps --format json 2>$null

# 5. Start if not running
if (-not $stackStatus -or ($stackStatus | ConvertFrom-Json | Where-Object { $_.State -ne "running" })) {
    docker compose -p csharp-compounding-docs -f $DockerComposeFile up -d
}

# 6. Wait for health
# ... (poll until healthy)

# 7. Output connection info
@{
    postgres = @{
        host = "127.0.0.1"
        port = 5433
        # ...
    }
    ollama = @{
        host = "127.0.0.1"
        port = 11435
    }
} | ConvertTo-Json
```

---

## MCP Server Launch

> **Background**: Research on plugin manifest format, MCP server registration, and the `${CLAUDE_PLUGIN_ROOT}` environment variable. See [Claude Code Plugin Installation Mechanism](../research/claude-code-plugin-installation-mechanism.md).

### Claude Code MCP Configuration

In `.claude/mcp.json` (user or project):

```json
{
  "mcpServers": {
    "csharp-compounding-docs": {
      "command": "pwsh",
      "args": [
        "-File",
        "${CLAUDE_PLUGIN_ROOT}/scripts/launch-mcp-server.ps1"
      ]
    }
  }
}
```

**Note**: `${CLAUDE_PLUGIN_ROOT}` resolves to the plugin's installation directory at runtime. See [Plugin Installation Research](../research/claude-code-plugin-installation-mechanism.md#environment-variable-claude_plugin_root) for details on path resolution and scopes.

### Launch Script: `scripts/launch-mcp-server.ps1`

```powershell
#!/usr/bin/env pwsh
# Note: $PSScriptRoot resolves to ${CLAUDE_PLUGIN_ROOT}/scripts/ at runtime

# 1. Start infrastructure and get connection info
$infraOutput = & (Join-Path $PSScriptRoot "start-infrastructure.ps1")
$infraConfig = $infraOutput | ConvertFrom-Json

# 2. Launch MCP server with connection parameters
$mcpServerPath = Join-Path $PSScriptRoot "../src/CompoundDocs.McpServer/bin/Release/net10.0/CompoundDocs.McpServer"

& $mcpServerPath `
    --postgres-host $infraConfig.postgres.host `
    --postgres-port $infraConfig.postgres.port `
    --postgres-database $infraConfig.postgres.database `
    --postgres-user $infraConfig.postgres.username `
    --postgres-password $infraConfig.postgres.password `
    --ollama-host $infraConfig.ollama.host `
    --ollama-port $infraConfig.ollama.port
```

---

## Port Assignments

To avoid conflicts with other services:

| Service | Internal Port | Exposed Port |
|---------|---------------|--------------|
| PostgreSQL | 5432 | 5433 |
| Ollama | 11434 | 11435 |

---

## Data Persistence

> **Background**: Detailed comparison of named volumes vs bind mounts, backup strategies, and PostgreSQL 18+ PGDATA changes. See [PostgreSQL Docker Data Mounting Research](../research/postgresql-docker-data-mounting-research.md).

### PostgreSQL Data

- Volume mount: `~/.claude/.csharp-compounding-docs/data/pgdata`
- Survives container restarts and upgrades
- Single schema with tenant isolation via compound keys (project_name, branch_name, path_hash)

### Ollama Models

- Volume mount: `~/.claude/.csharp-compounding-docs/ollama/models`
- Models downloaded once, reused across containers
- Pull required models on first use

---

## First-Run Experience

1. User installs plugin
2. User's `.claude/mcp.json` configured (manually or via skill)
3. First MCP server launch:
   - Creates `~/.claude/.csharp-compounding-docs/`
   - Copies template docker-compose.yml
   - Creates default ollama-config.json
   - Pulls Docker images (duration varies by connection speed)
   - Starts containers
   - Pulls Ollama models (duration varies by connection speed)
4. Subsequent launches: fast (containers already running)

**Note**: First-run timing is intentionally unspecified. Download duration depends on network speed, and progress is visible via Docker/Ollama stdout. No specific timing guarantees are made.

---

## Shutdown Behavior

Docker Compose stack is **not** automatically stopped when MCP server exits. This is intentional:
- Multiple Claude Code sessions may share the stack
- Startup is slow; keeping containers running improves UX
- Users can manually stop with `docker compose -p csharp-compounding-docs down`

### Optional: Cleanup Script

`scripts/stop-infrastructure.ps1`:

```powershell
#!/usr/bin/env pwsh
docker compose -p csharp-compounding-docs down
```

---

## Cleanup Console App

A standalone .NET console app (`CompoundDocs.Cleanup`) that periodically removes orphaned data from the database. Runs as a Docker container in the shared infrastructure stack.

**Detects and removes**:
- Orphaned repo paths (directories that no longer exist)
- Orphaned branches (branches deleted from remote)

> **Full specification**: [infrastructure/cleanup-app.md](./infrastructure/cleanup-app.md)

---

## Resolved Decisions

| Decision | Resolution |
|----------|------------|
| Ollama model auto-pull | Yes, auto-pull on first use |
| Embedding model | `mxbai-embed-large` (1024 dimensions) |
| Generation model | `mistral` |

## Open Questions

1. ~~How to handle Docker not being installed?~~ **Resolved**: Docker is required - fail with clear error message and install link. See [Prerequisites](#prerequisites).
2. Should there be a "status" script to check infrastructure health?
3. ~~What happens if user has conflicting port usage?~~ **Resolved**: Port conflicts are the user's responsibility. Users can modify `~/.claude/.csharp-compounding-docs/docker-compose.yml` to change ports and update the MCP server configuration in Claude Code's `.mcp.json` accordingly.
4. ~~Should cleanup app run as a system service or manually?~~ **Resolved**: Runs as Docker container in docker-compose stack. Can also be run manually with `--once` flag.

