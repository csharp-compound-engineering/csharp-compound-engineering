# Phase 003: PostgreSQL with pgvector Setup

> **Category**: Infrastructure Setup
> **Prerequisites**: Phase 002 (Docker Compose Base Configuration)
> **Estimated Effort**: Medium
> **Risk Level**: Low

---

## Overview

This phase establishes the PostgreSQL database infrastructure with pgvector extension support for vector similarity search, integrated with Liquibase for schema migrations. The custom Docker image extends `pgvector/pgvector:pg16` with embedded Liquibase to provide automatic schema management on container startup.

---

## Objectives

1. Create custom PostgreSQL Dockerfile extending pgvector image with Liquibase
2. Implement init-db.sh script for pgvector extension enablement
3. Configure Liquibase integration for automatic migration execution
4. Establish database initialization sequence
5. Document HNSW index configuration parameters

---

## Deliverables

### 1. Custom PostgreSQL Dockerfile

**File**: `docker/postgres/Dockerfile`

```dockerfile
FROM pgvector/pgvector:pg16

# Install Java (required for Liquibase) and wget
RUN apt-get update && apt-get install -y --no-install-recommends \
    openjdk-17-jre-headless \
    wget \
    && rm -rf /var/lib/apt/lists/* \
    && apt-get clean

# Install Liquibase
ARG LIQUIBASE_VERSION=4.25.0
RUN mkdir -p /opt/liquibase \
    && wget -qO- "https://github.com/liquibase/liquibase/releases/download/v${LIQUIBASE_VERSION}/liquibase-${LIQUIBASE_VERSION}.tar.gz" \
    | tar -xz -C /opt/liquibase \
    && chmod +x /opt/liquibase/liquibase \
    && ln -s /opt/liquibase/liquibase /usr/local/bin/liquibase

# Download PostgreSQL JDBC driver (required for Liquibase)
ARG POSTGRES_JDBC_VERSION=42.7.4
RUN wget -q "https://jdbc.postgresql.org/download/postgresql-${POSTGRES_JDBC_VERSION}.jar" \
    -O /opt/liquibase/lib/postgresql.jar

# Create directories for changelogs
RUN mkdir -p /liquibase/changelog

# Copy init script (runs after PostgreSQL starts, before accepting connections)
COPY init-db.sh /docker-entrypoint-initdb.d/01-init-db.sh
RUN chmod +x /docker-entrypoint-initdb.d/01-init-db.sh

# Copy Liquibase changelog files
COPY changelog /liquibase/changelog

# Data volume
VOLUME ["/var/lib/postgresql/data"]

EXPOSE 5432
```

**Rationale**:
- Extends official `pgvector/pgvector:pg16` image which has pgvector pre-installed
- Java 17 JRE is the minimum required for Liquibase 4.x
- Liquibase 4.25.0 is a stable version with good PostgreSQL support
- Scripts in `/docker-entrypoint-initdb.d/` run on first container initialization only

---

### 2. Database Initialization Script

**File**: `docker/postgres/init-db.sh`

```bash
#!/bin/bash
set -e

echo "=== PostgreSQL Initialization Script ==="
echo "Database: $POSTGRES_DB"
echo "User: $POSTGRES_USER"

# Step 1: Enable pgvector extension
# This MUST happen before Liquibase runs, as vector columns depend on it
echo "Enabling pgvector extension..."
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE EXTENSION IF NOT EXISTS vector;
EOSQL
echo "pgvector extension enabled."

# Step 2: Run Liquibase migrations
# The changelog creates the tenant_management schema and tables
echo "Running Liquibase migrations..."
liquibase --changeLogFile=/liquibase/changelog/changelog.xml \
          --url="jdbc:postgresql://localhost:5432/$POSTGRES_DB" \
          --username="$POSTGRES_USER" \
          --password="$POSTGRES_PASSWORD" \
          --log-level=INFO \
          update

# Check exit status
if [ $? -eq 0 ]; then
    echo "=== Liquibase migrations completed successfully ==="
else
    echo "=== ERROR: Liquibase migrations failed ===" >&2
    exit 1
fi

# Step 3: Show migration status
echo ""
echo "=== Applied Migrations ==="
liquibase --changeLogFile=/liquibase/changelog/changelog.xml \
          --url="jdbc:postgresql://localhost:5432/$POSTGRES_DB" \
          --username="$POSTGRES_USER" \
          --password="$POSTGRES_PASSWORD" \
          --log-level=WARN \
          history

echo ""
echo "=== Database Initialization Complete ==="
```

**Key Execution Order**:
1. pgvector extension enabled first (required for vector column types)
2. Liquibase migrations run to create tenant_management schema
3. Migration history displayed for verification

**Important Notes**:
- Script runs on **first container start only** (PostgreSQL init behavior)
- Subsequent starts skip init scripts if data volume exists
- Use `docker volume rm` to force re-initialization

---

### 3. Liquibase Changelog Directory Structure

**Directory**: `docker/postgres/changelog/`

```
changelog/
├── changelog.xml                    # Master entry point
└── changes/
    ├── 001-create-tenant-schema/
    │   ├── change.xml
    │   ├── change.sql
    │   └── rollback.sql
    ├── 002-create-repo-paths-table/
    │   ├── change.xml
    │   ├── change.sql
    │   └── rollback.sql
    ├── 003-create-branches-table/
    │   ├── change.xml
    │   ├── change.sql
    │   └── rollback.sql
    └── 004-create-indexes/
        ├── change.xml
        ├── change.sql
        └── rollback.sql
```

**Master Changelog** (`changelog.xml`):
```xml
<?xml version="1.0" encoding="UTF-8"?>
<databaseChangeLog
    xmlns="http://www.liquibase.org/xml/ns/dbchangelog"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xsi:schemaLocation="http://www.liquibase.org/xml/ns/dbchangelog
        http://www.liquibase.org/xml/ns/dbchangelog/dbchangelog-latest.xsd">

    <!-- Initial schema setup -->
    <include file="changes/001-create-tenant-schema/change.xml" relativeToChangelogFile="true"/>
    <include file="changes/002-create-repo-paths-table/change.xml" relativeToChangelogFile="true"/>
    <include file="changes/003-create-branches-table/change.xml" relativeToChangelogFile="true"/>
    <include file="changes/004-create-indexes/change.xml" relativeToChangelogFile="true"/>

</databaseChangeLog>
```

See [spec/mcp-server/liquibase-changelog.md](/spec/mcp-server/liquibase-changelog.md) for complete changeset definitions.

---

### 4. Docker Compose Service Definition

**File**: `docker-compose.yml` (PostgreSQL service section)

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
      - ./docker/postgres/changelog:/liquibase/changelog:ro
    ports:
      - "127.0.0.1:5433:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U compounding -d compounding_docs"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 60s
    command:
      - "postgres"
      - "-c"
      - "shared_buffers=256MB"
      - "-c"
      - "work_mem=64MB"
      - "-c"
      - "maintenance_work_mem=512MB"
```

**Configuration Notes**:
- Port `5433` to avoid conflicts with local PostgreSQL installations
- `start_period: 60s` allows time for Liquibase migrations on first start
- Changelog mounted read-only for development iteration
- Memory settings tuned for vector operations

---

### 5. HNSW Index Configuration Parameters

HNSW (Hierarchical Navigable Small World) indexes provide fast approximate nearest neighbor search for vector embeddings.

**Configured Parameters** (Medium configuration for 1,000-10,000 documents):

| Parameter | Value | Description |
|-----------|-------|-------------|
| `m` | 32 | Maximum connections per node in the graph |
| `ef_construction` | 128 | Search depth during index building |
| `ef_search` | 64 | Search depth during query execution (set at query time) |

**SQL for HNSW Index Creation** (executed by Semantic Kernel, not Liquibase):
```sql
-- Created automatically by Semantic Kernel EnsureCollectionExistsAsync()
-- The index uses these parameters:
CREATE INDEX documents_embedding_idx ON compounding.documents
USING hnsw (embedding vector_cosine_ops)
WITH (m = 32, ef_construction = 128);

-- Set at query time for better recall:
SET hnsw.ef_search = 64;
```

**Scale Guidance**:

| Scale | Document Count | Configuration |
|-------|----------------|---------------|
| Small | < 1,000 | pgvector defaults (m=16) |
| **Medium** | 1K-10K | **Current choice** (m=32, ef_construction=128) |
| Large | 10K-100K | m=48, ef_construction=200 |
| Very Large | 100K+ | Consider partitioning by tenant |

**Schema Responsibility Division**:
- **Liquibase**: Creates `tenant_management` schema (repo_paths, branches tables)
- **Semantic Kernel**: Creates `compounding` schema with vector collections (documents, chunks)

---

## Implementation Tasks

### Task 1: Create Docker Directory Structure
```bash
mkdir -p docker/postgres/changelog/changes
```

### Task 2: Create Dockerfile
- [ ] Create `docker/postgres/Dockerfile` as specified above
- [ ] Verify base image tag matches spec (`pgvector/pgvector:pg16`)
- [ ] Ensure Liquibase version is 4.25.0

### Task 3: Create Init Script
- [ ] Create `docker/postgres/init-db.sh`
- [ ] Ensure script is executable (`chmod +x`)
- [ ] Verify pgvector extension is enabled before Liquibase runs

### Task 4: Create Liquibase Changelog Structure
- [ ] Create `docker/postgres/changelog/changelog.xml` (master)
- [ ] Create `changes/001-create-tenant-schema/` directory and files
- [ ] Create `changes/002-create-repo-paths-table/` directory and files
- [ ] Create `changes/003-create-branches-table/` directory and files
- [ ] Create `changes/004-create-indexes/` directory and files
- [ ] Reference spec/mcp-server/liquibase-changelog.md for SQL content

### Task 5: Update Docker Compose
- [ ] Add postgres service to docker-compose.yml
- [ ] Configure build context and Dockerfile path
- [ ] Set environment variables
- [ ] Configure volume mounts for data persistence
- [ ] Set health check configuration

### Task 6: Test Database Initialization
- [ ] Build custom image: `docker build -t csharp-compounding-docs-postgres ./docker/postgres`
- [ ] Start container and verify pgvector extension
- [ ] Verify Liquibase migrations apply successfully
- [ ] Verify tenant_management schema exists with correct tables

---

## Verification Steps

### 1. Build Verification
```bash
docker build -t csharp-compounding-docs-postgres ./docker/postgres
```
Expected: Image builds without errors

### 2. Container Start Verification
```bash
docker compose -p csharp-compounding-docs up -d postgres
docker compose -p csharp-compounding-docs logs postgres
```
Expected: Logs show pgvector extension enabled and Liquibase migrations applied

### 3. Extension Verification
```bash
docker exec csharp-compounding-docs-postgres psql -U compounding -d compounding_docs -c "SELECT * FROM pg_extension WHERE extname = 'vector';"
```
Expected: One row showing vector extension

### 4. Schema Verification
```bash
docker exec csharp-compounding-docs-postgres psql -U compounding -d compounding_docs -c "\dn"
```
Expected: `tenant_management` schema exists

### 5. Table Verification
```bash
docker exec csharp-compounding-docs-postgres psql -U compounding -d compounding_docs -c "\dt tenant_management.*"
```
Expected: `repo_paths` and `branches` tables exist

### 6. Liquibase History Verification
```bash
docker exec csharp-compounding-docs-postgres psql -U compounding -d compounding_docs -c "SELECT id, author, dateexecuted FROM databasechangelog ORDER BY orderexecuted;"
```
Expected: Four changesets listed (001 through 004)

### 7. Health Check Verification
```bash
docker inspect csharp-compounding-docs-postgres --format='{{.State.Health.Status}}'
```
Expected: `healthy` after start_period

---

## Dependencies

### Upstream Dependencies
- Phase 002: Docker Compose base configuration must exist

### Downstream Dependencies
- Phase 004+: MCP Server will connect to this database
- Semantic Kernel will create vector collections in `compounding` schema

---

## Spec References

| Document | Section | Relevance |
|----------|---------|-----------|
| [spec/infrastructure.md](/spec/infrastructure.md) | PostgreSQL + pgvector + Liquibase | Docker service configuration |
| [spec/mcp-server/database-schema.md](/spec/mcp-server/database-schema.md) | Tenant Management Schema | SQL definitions for tables |
| [spec/mcp-server/liquibase-changelog.md](/spec/mcp-server/liquibase-changelog.md) | Full changelog | Complete XML and SQL content |
| [research/postgresql-pgvector-research.md](/research/postgresql-pgvector-research.md) | All sections | pgvector setup and HNSW tuning |
| [research/liquibase-pgvector-docker-init.md](/research/liquibase-pgvector-docker-init.md) | Sections 3, 7 | Dockerfile and init script patterns |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Liquibase migration fails | Init script has explicit error handling and exit on failure |
| pgvector not enabled | Extension creation runs before Liquibase; migrations would fail visibly |
| Data loss on rebuild | Data persisted to host volume; clearly document `docker volume rm` behavior |
| Port conflict with local PostgreSQL | Non-standard port 5433 avoids default 5432 |
| Long startup time | `start_period: 60s` health check accounts for first-run migrations |

---

## Success Criteria

1. Custom Docker image builds successfully
2. Container starts and reaches healthy status
3. pgvector extension is enabled in database
4. All four Liquibase changesets apply without errors
5. `tenant_management.repo_paths` and `tenant_management.branches` tables exist
6. Health check passes and container remains stable
7. Data persists across container restarts (when using same volume)
