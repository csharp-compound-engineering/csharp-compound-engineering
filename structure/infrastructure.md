# Infrastructure Structure Summary

This file contains summaries for the infrastructure specification and its children.

---

## spec/infrastructure.md

### What This File Covers

This specification defines the infrastructure layer for the csharp-compounding-docs system, including:

- **Docker Compose Stack**: PostgreSQL (with pgvector and Liquibase) and Ollama services running as shared containers
- **Prerequisites**: Docker Desktop (hard requirement), .NET 10.0+, PowerShell 7+
- **Directory Structure**: User-level configuration at `~/.claude/.csharp-compounding-docs/` containing docker-compose.yml, ollama-config.json, and data volumes
- **Service Configuration**: Custom PostgreSQL Dockerfile extending pgvector with Liquibase for schema migrations; Ollama with optional GPU support (NVIDIA, AMD, Apple Silicon)
- **PowerShell Launcher Scripts**: `start-infrastructure.ps1` (manages Docker stack) and `launch-mcp-server.ps1` (launches MCP server with connection parameters)
- **Port Assignments**: PostgreSQL on 5433, Ollama on 11435 (non-standard to avoid conflicts)
- **Data Persistence**: Bind mounts for PostgreSQL data and Ollama models
- **Cleanup Console App**: Standalone .NET app for removing orphaned data (dead repos, deleted branches)

### Structural Relationships

- **Parent**: [SPEC.md](../SPEC.md) - The root specification document
- **Children**:
  - [infrastructure/cleanup-app.md](./infrastructure/cleanup-app.md) - Detailed cleanup app specification
- **Siblings** (other top-level specs):
  - [mcp-server.md](./mcp-server.md) - MCP server specification (references Apple Silicon note, uses infrastructure connection params)
  - [configuration.md](./configuration.md) - Configuration system
  - [skills.md](./skills.md) - Skills system
  - [doc-types.md](./doc-types.md) - Document types
  - [testing.md](./testing.md) - Testing framework
  - [observability.md](./observability.md) - Observability/monitoring
  - [marketplace.md](./marketplace.md) - Marketplace features
  - [agents.md](./agents.md) - Agent system
- **Related Research** (referenced in background notes):
  - docker-compose-postgresql-host-mcp-research.md
  - postgresql-docker-data-mounting-research.md
  - liquibase-pgvector-docker-init.md
  - ollama-docker-gpu-research.md
  - claude-code-plugin-installation-mechanism.md

---

## spec/infrastructure/cleanup-app.md

### What This File Covers

This specification defines a standalone .NET console application (`CompoundDocs.Cleanup`) that periodically removes orphaned data from the database. The app:

- **Detects orphaned paths**: Repo paths in `tenant_management.repo_paths` where the directory no longer exists on disk
- **Detects orphaned branches**: Branches in `tenant_management.branches` that no longer exist on the git remote (verified via `git ls-remote --heads origin`)
- **Implements as BackgroundService**: Uses .NET Generic Host with `BackgroundService` pattern for periodic execution
- **Runs in Docker**: Operates as a container in the shared infrastructure stack with volume mounts for path detection
- **Configurable**: Supports interval configuration, dry-run mode, and optional manual one-time execution via `--once` flag
- **Safety features**: Includes logging of all deletions, dry-run preview mode, and optional grace period

### Structural Relationships

- **Parent**: `spec/infrastructure.md` - The main infrastructure specification that covers Docker Compose stack, PostgreSQL/Ollama services, PowerShell launcher scripts, and directory structure
- **Grandparent**: `spec/SPEC.md` - The root specification document
- **Siblings**: None (currently the only child document under `spec/infrastructure/`)
- **Sibling specs at parent level**: `mcp-server.md`, `agents.md`, `skills.md`, `configuration.md`, `testing.md`, `observability.md`, `doc-types.md`, `marketplace.md`, `research-index.md`
