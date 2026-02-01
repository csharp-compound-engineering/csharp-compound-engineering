# Phase 002: Docker Compose Infrastructure Base

> **Status**: [PLANNED]
> **Estimated Effort**: M
> **Prerequisites**: Phase 001
> **Category**: Infrastructure Setup

---

## Spec References

- [spec/infrastructure.md - Directory Structure](../spec/infrastructure.md#directory-structure)
- [spec/infrastructure.md - Docker Compose Stack](../spec/infrastructure.md#docker-compose-stack)
- [spec/infrastructure.md - Port Assignments](../spec/infrastructure.md#port-assignments)
- [spec/infrastructure.md - Data Persistence](../spec/infrastructure.md#data-persistence)
- [structure/infrastructure.md](../structure/infrastructure.md)

---

## Objectives

1. Create the `~/.claude/.csharp-compounding-docs/` directory structure template
2. Create the base `docker-compose.yml` template with PostgreSQL service
3. Define the PostgreSQL + pgvector service configuration
4. Configure volume bind mounts for data persistence
5. Set up port assignments to avoid conflicts with standard installations
6. Define the Docker Compose network configuration
7. Create placeholder structure for Ollama service (configured in Phase 003)

---

## Acceptance Criteria

- [ ] Template directory structure documented and scripted
- [ ] `docker-compose.yml` base template created with PostgreSQL service
- [ ] PostgreSQL service uses `pgvector/pgvector:pg16` as base image reference
- [ ] Volume bind mount configured for `~/.claude/.csharp-compounding-docs/data/pgdata`
- [ ] PostgreSQL exposed on port 5433 (mapped from internal 5432)
- [ ] Port bindings use `127.0.0.1` to restrict to localhost only
- [ ] Docker Compose project name defined as `csharp-compounding-docs`
- [ ] Health check configured for PostgreSQL service
- [ ] Environment variables defined for PostgreSQL credentials
- [ ] Network configuration allows future service additions

---

## Implementation Notes

### Directory Structure Template

The following directory structure must be created on first run:

```
~/.claude/.csharp-compounding-docs/
├── docker-compose.yml           # Docker Compose configuration (from template)
├── ollama-config.json           # Ollama settings (from template)
├── data/                        # PostgreSQL data volume
│   └── pgdata/                  # Actual PostgreSQL data directory
└── ollama/                      # Ollama model storage
    └── models/                  # Downloaded model files
```

**Implementation Location**: This structure creation logic will be implemented in the PowerShell launcher script (Phase 006), but the template files are created in this phase.

### Docker Compose Base Template

Create `templates/docker-compose.yml` in the plugin bundle:

```yaml
version: "3.8"

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
      - ${COMPOUNDING_DOCS_HOME}/data/pgdata:/var/lib/postgresql/data
      - ./docker/postgres/changelog:/liquibase/changelog
    ports:
      - "127.0.0.1:5433:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U compounding -d compounding_docs"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - compounding-network
    restart: unless-stopped

  # Ollama service placeholder - configured in Phase 003
  # ollama:
  #   image: ollama/ollama:latest
  #   ...

networks:
  compounding-network:
    driver: bridge
    name: csharp-compounding-docs-network
```

**Note**: The `${COMPOUNDING_DOCS_HOME}` variable will be replaced by the launcher script with the actual path (`~/.claude/.csharp-compounding-docs/`).

### PostgreSQL Service Configuration Details

| Setting | Value | Rationale |
|---------|-------|-----------|
| Base Image | `pgvector/pgvector:pg16` | PostgreSQL 16 with pgvector extension pre-installed |
| Container Name | `csharp-compounding-docs-postgres` | Unique, identifiable name |
| Database User | `compounding` | Simple, dedicated user |
| Database Password | `compounding` | Local-only access, simplicity for MVP |
| Database Name | `compounding_docs` | Descriptive name |
| External Port | `5433` | Avoids conflict with standard PostgreSQL (5432) |
| Internal Port | `5432` | Standard PostgreSQL port inside container |

### Volume Mount Strategy

**Bind Mount** (not named volume) is used for:
- User visibility into data location
- Easy backup/restore
- Persistence across container rebuilds
- Cross-platform compatibility

```yaml
volumes:
  - ~/.claude/.csharp-compounding-docs/data/pgdata:/var/lib/postgresql/data
```

**Important**: The pgdata directory must:
- Be empty on first run (PostgreSQL initializes it)
- Have appropriate permissions (handled by Docker)
- Survive container recreation

### Port Binding Security

Ports are bound to `127.0.0.1` only:

```yaml
ports:
  - "127.0.0.1:5433:5432"
```

This ensures:
- Services only accessible from localhost
- No external network exposure
- Compatible with firewall configurations

### Health Check Configuration

```yaml
healthcheck:
  test: ["CMD-SHELL", "pg_isready -U compounding -d compounding_docs"]
  interval: 10s
  timeout: 5s
  retries: 5
```

- **Interval**: 10 seconds between checks
- **Timeout**: 5 seconds per check attempt
- **Retries**: 5 attempts before marking unhealthy
- **Start Period**: Default (0s) - immediate health checking

### Network Configuration

A dedicated bridge network provides:
- Service-to-service DNS resolution
- Isolation from other Docker networks
- Future extensibility for additional services

```yaml
networks:
  compounding-network:
    driver: bridge
    name: csharp-compounding-docs-network
```

### Docker Compose Project Name

The stack uses a fixed project name for consistent identification:

```bash
docker compose -p csharp-compounding-docs up -d
```

This ensures:
- Consistent container naming
- Easy stack management (stop, remove, logs)
- Avoids conflicts with other compose projects

---

## Files to Create

| File | Location | Purpose |
|------|----------|---------|
| `docker-compose.yml` | `templates/docker-compose.yml` | Base compose template |
| `Dockerfile` | `docker/postgres/Dockerfile` | Custom PostgreSQL image (placeholder, detailed in Phase 004) |

---

## Dependencies

### Depends On
- **Phase 001**: Repository initialization - solution file and directory structure must exist

### Blocks
- **Phase 003**: Ollama Service Configuration - requires base docker-compose.yml
- **Phase 004**: PostgreSQL Custom Dockerfile with Liquibase - requires service definition
- **Phase 005**: PostgreSQL Initialization Script - requires container configuration
- **Phase 006**: PowerShell Launcher Script - requires docker-compose.yml template

---

## Testing Considerations

This phase creates templates only. Validation occurs in later phases:
- Template syntax validation (Phase 004)
- Container startup testing (Phase 006)
- Integration testing (dedicated testing phases)

---

## Security Notes

1. **Credentials**: Hardcoded credentials are acceptable for MVP since:
   - Services bound to localhost only
   - No network exposure
   - Single-user development tool

2. **Future Enhancement**: Post-MVP may consider:
   - Environment variable injection for credentials
   - Docker secrets integration
   - User-configurable credentials
