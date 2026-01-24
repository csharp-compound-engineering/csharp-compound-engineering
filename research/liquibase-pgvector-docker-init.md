# Liquibase with PostgreSQL/pgvector in Docker

**Research Date:** January 22, 2026
**Purpose:** Using Liquibase for database schema management with PostgreSQL and pgvector in Docker containers, including custom Dockerfile setup, init scripts, and changelog examples for tenant management schemas.

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Architecture Options](#2-architecture-options)
3. [Custom Dockerfile: pgvector with Liquibase](#3-custom-dockerfile-pgvector-with-liquibase)
4. [Docker Compose Configuration](#4-docker-compose-configuration)
5. [Liquibase Changelog Organization](#5-liquibase-changelog-organization)
6. [Changelog Examples](#6-changelog-examples)
7. [Init Script for Container Startup](#7-init-script-for-container-startup)
8. [Handling Schema Evolution](#8-handling-schema-evolution)
9. [pgvector-Specific Considerations](#9-pgvector-specific-considerations)
10. [Best Practices](#10-best-practices)
11. [Complete Project Structure](#11-complete-project-structure)
12. [Sources](#12-sources)

---

## 1. Executive Summary

### When to Use Liquibase vs Semantic Kernel's EnsureCollectionExistsAsync()

| Scenario | Liquibase | Semantic Kernel |
|----------|-----------|-----------------|
| Simple vector collections | Overkill | Recommended |
| Complex multi-tenant schemas | Recommended | Insufficient |
| Fine-grained index control | Recommended | Limited |
| Audit requirements | Recommended | Not available |
| Team coordination | Recommended | Challenging |
| Non-vector tables (metadata, config) | Recommended | Not applicable |

### Recommendation

For this project with `tenant_management` schema, `repo_paths`, `branches`, and `documents` tables, **Liquibase is recommended** because:
- Multiple schemas and non-vector tables require explicit DDL control
- HNSW index parameters need fine-tuning beyond Semantic Kernel's defaults
- Team collaboration benefits from versioned, auditable migrations
- Rollback capability for production deployments

---

## 2. Architecture Options

### Option A: Separate Liquibase Container (Recommended)

```
┌─────────────────────────────────────────────────────────────┐
│                    Docker Compose                            │
│                                                              │
│  ┌──────────────────┐     ┌──────────────────────────────┐  │
│  │   pgvector       │     │   Liquibase                  │  │
│  │   Container      │◄────│   Container                  │  │
│  │   (pg17)         │     │   (runs migrations)          │  │
│  └──────────────────┘     └──────────────────────────────┘  │
│           ▲                                                  │
│           │                                                  │
└───────────┼──────────────────────────────────────────────────┘
            │
    ┌───────┴────────┐
    │   MCP Server   │
    │   (Host)       │
    └────────────────┘
```

**Pros:** Clean separation, official images, easy updates
**Cons:** Two containers to manage

### Option B: Custom pgvector Image with Embedded Liquibase

```
┌─────────────────────────────────────────────────────────────┐
│                    Docker Compose                            │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │   Custom pgvector-liquibase Container                 │   │
│  │   (PostgreSQL + pgvector + Liquibase)                 │   │
│  │   Init script runs Liquibase on startup               │   │
│  └──────────────────────────────────────────────────────┘   │
│           ▲                                                  │
└───────────┼──────────────────────────────────────────────────┘
            │
    ┌───────┴────────┐
    │   MCP Server   │
    │   (Host)       │
    └────────────────┘
```

**Pros:** Single container, migrations always run
**Cons:** Larger image, custom maintenance

---

## 3. Custom Dockerfile: pgvector with Liquibase

### Option A: Extend pgvector Image with Liquibase

```dockerfile
# Dockerfile.pgvector-liquibase
FROM pgvector/pgvector:pg16

# Install Java (required for Liquibase)
RUN apt-get update && apt-get install -y --no-install-recommends \
    openjdk-17-jre-headless \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Install Liquibase
ARG LIQUIBASE_VERSION=4.30.0
RUN mkdir -p /opt/liquibase \
    && curl -L "https://github.com/liquibase/liquibase/releases/download/v${LIQUIBASE_VERSION}/liquibase-${LIQUIBASE_VERSION}.tar.gz" \
    | tar -xz -C /opt/liquibase \
    && chmod +x /opt/liquibase/liquibase \
    && ln -s /opt/liquibase/liquibase /usr/local/bin/liquibase

# Download PostgreSQL JDBC driver (required for Liquibase 5.0+)
ARG POSTGRES_JDBC_VERSION=42.7.4
RUN curl -L "https://jdbc.postgresql.org/download/postgresql-${POSTGRES_JDBC_VERSION}.jar" \
    -o /opt/liquibase/lib/postgresql.jar

# Create directories for changelogs and scripts
RUN mkdir -p /liquibase/changelog /docker-entrypoint-initdb.d

# Copy custom init script
COPY docker/init-with-liquibase.sh /docker-entrypoint-initdb.d/99-run-liquibase.sh
RUN chmod +x /docker-entrypoint-initdb.d/99-run-liquibase.sh

# Copy changelog files
COPY liquibase/changelog /liquibase/changelog
COPY liquibase/liquibase.properties /liquibase/liquibase.properties

# Default PostgreSQL data directory
VOLUME ["/var/lib/postgresql/data"]

EXPOSE 5432
```

### Option B: Multi-Stage Build (Smaller Image)

```dockerfile
# Dockerfile.pgvector-liquibase-slim
# Stage 1: Download Liquibase
FROM alpine:3.19 AS liquibase-downloader

ARG LIQUIBASE_VERSION=4.30.0
ARG POSTGRES_JDBC_VERSION=42.7.4

RUN apk add --no-cache curl tar

RUN mkdir -p /opt/liquibase \
    && curl -L "https://github.com/liquibase/liquibase/releases/download/v${LIQUIBASE_VERSION}/liquibase-${LIQUIBASE_VERSION}.tar.gz" \
    | tar -xz -C /opt/liquibase

RUN curl -L "https://jdbc.postgresql.org/download/postgresql-${POSTGRES_JDBC_VERSION}.jar" \
    -o /opt/liquibase/lib/postgresql.jar

# Stage 2: Final image
FROM pgvector/pgvector:pg16

# Install minimal Java runtime
RUN apt-get update && apt-get install -y --no-install-recommends \
    openjdk-17-jre-headless \
    && rm -rf /var/lib/apt/lists/* \
    && apt-get clean

# Copy Liquibase from builder
COPY --from=liquibase-downloader /opt/liquibase /opt/liquibase
RUN ln -s /opt/liquibase/liquibase /usr/local/bin/liquibase

# Setup directories
RUN mkdir -p /liquibase/changelog

# Copy application files
COPY docker/init-with-liquibase.sh /docker-entrypoint-initdb.d/99-run-liquibase.sh
COPY liquibase/changelog /liquibase/changelog
COPY liquibase/liquibase.properties /liquibase/liquibase.properties

RUN chmod +x /docker-entrypoint-initdb.d/99-run-liquibase.sh

EXPOSE 5432
```

---

## 4. Docker Compose Configuration

### Option A: Separate Containers (Recommended for CI/CD)

```yaml
# docker-compose.yml
services:
  postgres:
    image: pgvector/pgvector:pg16
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
      # Minimal init: just enable pgvector extension
      - ./docker/init-pgvector.sql:/docker-entrypoint-initdb.d/01-init-pgvector.sql:ro
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

  liquibase:
    image: liquibase/liquibase:4.30-alpine
    container_name: rag-liquibase
    depends_on:
      postgres:
        condition: service_healthy
    environment:
      LIQUIBASE_COMMAND_URL: jdbc:postgresql://postgres:5432/${POSTGRES_DB:-rag_db}
      LIQUIBASE_COMMAND_USERNAME: ${POSTGRES_USER:-rag_user}
      LIQUIBASE_COMMAND_PASSWORD: ${POSTGRES_PASSWORD}
      LIQUIBASE_COMMAND_CHANGELOG_FILE: /liquibase/changelog/db.changelog-master.yaml
    volumes:
      - ./liquibase/changelog:/liquibase/changelog:ro
    command: update
    # Only run once, then exit
    restart: "no"

  # Optional: Ollama for embeddings
  ollama:
    image: ollama/ollama:latest
    container_name: rag-ollama
    restart: unless-stopped
    ports:
      - "127.0.0.1:11434:11434"
    volumes:
      - ollama_data:/root/.ollama
    profiles:
      - with-ollama

volumes:
  pgdata:
    driver: local
  ollama_data:
    driver: local
```

### Option B: Custom Image (Self-Contained)

```yaml
# docker-compose.yml
services:
  postgres:
    build:
      context: .
      dockerfile: Dockerfile.pgvector-liquibase
    image: rag-postgres-liquibase:local
    container_name: rag-postgres
    restart: unless-stopped
    environment:
      POSTGRES_USER: ${POSTGRES_USER:-rag_user}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:?POSTGRES_PASSWORD is required}
      POSTGRES_DB: ${POSTGRES_DB:-rag_db}
      # Liquibase will use these via init script
      LIQUIBASE_CHANGELOG_FILE: /liquibase/changelog/db.changelog-master.yaml
    ports:
      - "127.0.0.1:5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
      # Override changelogs for development
      - ./liquibase/changelog:/liquibase/changelog:ro
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER:-rag_user} -d ${POSTGRES_DB:-rag_db}"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 60s  # Longer to account for migrations
    command:
      - "postgres"
      - "-c"
      - "shared_buffers=256MB"
      - "-c"
      - "work_mem=64MB"

volumes:
  pgdata:
    driver: local
```

### Environment File (.env)

```bash
# .env
POSTGRES_USER=rag_user
POSTGRES_PASSWORD=your_secure_password_here
POSTGRES_DB=rag_db

# Liquibase settings (for separate container approach)
LIQUIBASE_LOG_LEVEL=INFO
```

---

## 5. Liquibase Changelog Organization

### Recommended Directory Structure

```
project-root/
├── docker-compose.yml
├── Dockerfile.pgvector-liquibase
├── .env
├── docker/
│   ├── init-pgvector.sql              # Minimal: CREATE EXTENSION vector
│   └── init-with-liquibase.sh         # Init script for embedded approach
└── liquibase/
    ├── liquibase.properties           # Connection config
    └── changelog/
        ├── db.changelog-master.yaml   # Root changelog
        └── changes/
            ├── 001-enable-pgvector.yaml
            ├── 002-create-tenant-schema.yaml
            ├── 003-create-repo-paths-table.yaml
            ├── 004-create-branches-table.yaml
            ├── 005-create-documents-table.yaml
            └── 006-create-hnsw-indexes.yaml
```

### Master Changelog (db.changelog-master.yaml)

```yaml
# liquibase/changelog/db.changelog-master.yaml
databaseChangeLog:
  - preConditions:
      - onFail: HALT
      - dbms:
          type: postgresql

  # Include all changesets in order
  - include:
      file: changes/001-enable-pgvector.yaml
      relativeToChangelogFile: true

  - include:
      file: changes/002-create-tenant-schema.yaml
      relativeToChangelogFile: true

  - include:
      file: changes/003-create-repo-paths-table.yaml
      relativeToChangelogFile: true

  - include:
      file: changes/004-create-branches-table.yaml
      relativeToChangelogFile: true

  - include:
      file: changes/005-create-documents-table.yaml
      relativeToChangelogFile: true

  - include:
      file: changes/006-create-hnsw-indexes.yaml
      relativeToChangelogFile: true
```

### Alternative: Using includeAll

```yaml
# db.changelog-master.yaml (using includeAll)
databaseChangeLog:
  - preConditions:
      - onFail: HALT
      - dbms:
          type: postgresql

  # Include all files from changes/ directory in alphabetical order
  - includeAll:
      path: changes/
      relativeToChangelogFile: true
      errorIfMissingOrEmpty: true
```

---

## 6. Changelog Examples

### 001-enable-pgvector.yaml

```yaml
# liquibase/changelog/changes/001-enable-pgvector.yaml
databaseChangeLog:
  - changeSet:
      id: 001-enable-pgvector
      author: rag-mcp-server
      comment: Enable pgvector extension for vector similarity search
      preConditions:
        - onFail: MARK_RAN
        - not:
            - sqlCheck:
                expectedResult: 1
                sql: SELECT COUNT(*) FROM pg_extension WHERE extname = 'vector'
      changes:
        - sql:
            sql: CREATE EXTENSION IF NOT EXISTS vector;
      rollback:
        - sql:
            sql: DROP EXTENSION IF EXISTS vector CASCADE;
```

### 002-create-tenant-schema.yaml

```yaml
# liquibase/changelog/changes/002-create-tenant-schema.yaml
databaseChangeLog:
  - changeSet:
      id: 002-create-tenant-management-schema
      author: rag-mcp-server
      comment: Create tenant_management schema for multi-tenant support
      preConditions:
        - onFail: MARK_RAN
        - not:
            - sqlCheck:
                expectedResult: 1
                sql: SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = 'tenant_management'
      changes:
        - sql:
            sql: CREATE SCHEMA IF NOT EXISTS tenant_management;
        - sql:
            sql: |
              COMMENT ON SCHEMA tenant_management IS
              'Schema for tenant-specific data including repository paths, branches, and document embeddings';
      rollback:
        - sql:
            sql: DROP SCHEMA IF EXISTS tenant_management CASCADE;
```

### 003-create-repo-paths-table.yaml

```yaml
# liquibase/changelog/changes/003-create-repo-paths-table.yaml
databaseChangeLog:
  - changeSet:
      id: 003-create-repo-paths-table
      author: rag-mcp-server
      comment: Create repo_paths table for tracking repository directories
      changes:
        - createTable:
            schemaName: tenant_management
            tableName: repo_paths
            columns:
              - column:
                  name: id
                  type: BIGSERIAL
                  constraints:
                    primaryKey: true
                    primaryKeyName: pk_repo_paths
              - column:
                  name: path
                  type: TEXT
                  constraints:
                    nullable: false
              - column:
                  name: name
                  type: VARCHAR(255)
                  constraints:
                    nullable: false
              - column:
                  name: description
                  type: TEXT
              - column:
                  name: is_active
                  type: BOOLEAN
                  defaultValueBoolean: true
                  constraints:
                    nullable: false
              - column:
                  name: metadata
                  type: JSONB
                  defaultValue: '{}'
              - column:
                  name: created_at
                  type: TIMESTAMPTZ
                  defaultValueComputed: CURRENT_TIMESTAMP
                  constraints:
                    nullable: false
              - column:
                  name: updated_at
                  type: TIMESTAMPTZ
                  defaultValueComputed: CURRENT_TIMESTAMP
                  constraints:
                    nullable: false

        # Add unique constraint on path
        - addUniqueConstraint:
            schemaName: tenant_management
            tableName: repo_paths
            columnNames: path
            constraintName: uq_repo_paths_path

        # Add index on name for lookups
        - createIndex:
            schemaName: tenant_management
            tableName: repo_paths
            indexName: idx_repo_paths_name
            columns:
              - column:
                  name: name

        # Add GIN index on metadata for JSONB queries
        - sql:
            sql: |
              CREATE INDEX idx_repo_paths_metadata
              ON tenant_management.repo_paths USING gin (metadata jsonb_path_ops);

      rollback:
        - dropTable:
            schemaName: tenant_management
            tableName: repo_paths
```

### 004-create-branches-table.yaml

```yaml
# liquibase/changelog/changes/004-create-branches-table.yaml
databaseChangeLog:
  - changeSet:
      id: 004-create-branches-table
      author: rag-mcp-server
      comment: Create branches table for tracking git branches per repository
      changes:
        - createTable:
            schemaName: tenant_management
            tableName: branches
            columns:
              - column:
                  name: id
                  type: BIGSERIAL
                  constraints:
                    primaryKey: true
                    primaryKeyName: pk_branches
              - column:
                  name: repo_path_id
                  type: BIGINT
                  constraints:
                    nullable: false
                    foreignKeyName: fk_branches_repo_path
                    references: tenant_management.repo_paths(id)
                    deleteCascade: true
              - column:
                  name: branch_name
                  type: VARCHAR(255)
                  constraints:
                    nullable: false
              - column:
                  name: is_default
                  type: BOOLEAN
                  defaultValueBoolean: false
                  constraints:
                    nullable: false
              - column:
                  name: last_commit_hash
                  type: VARCHAR(40)
              - column:
                  name: last_indexed_at
                  type: TIMESTAMPTZ
              - column:
                  name: metadata
                  type: JSONB
                  defaultValue: '{}'
              - column:
                  name: created_at
                  type: TIMESTAMPTZ
                  defaultValueComputed: CURRENT_TIMESTAMP
                  constraints:
                    nullable: false
              - column:
                  name: updated_at
                  type: TIMESTAMPTZ
                  defaultValueComputed: CURRENT_TIMESTAMP
                  constraints:
                    nullable: false

        # Unique constraint: one branch name per repo
        - addUniqueConstraint:
            schemaName: tenant_management
            tableName: branches
            columnNames: repo_path_id, branch_name
            constraintName: uq_branches_repo_branch

        # Index for finding branches by repo
        - createIndex:
            schemaName: tenant_management
            tableName: branches
            indexName: idx_branches_repo_path_id
            columns:
              - column:
                  name: repo_path_id

      rollback:
        - dropTable:
            schemaName: tenant_management
            tableName: branches
```

### 005-create-documents-table.yaml

```yaml
# liquibase/changelog/changes/005-create-documents-table.yaml
databaseChangeLog:
  - changeSet:
      id: 005-create-documents-table
      author: rag-mcp-server
      comment: Create documents table with vector embeddings for semantic search
      changes:
        - createTable:
            schemaName: tenant_management
            tableName: documents
            columns:
              - column:
                  name: id
                  type: BIGSERIAL
                  constraints:
                    primaryKey: true
                    primaryKeyName: pk_documents
              - column:
                  name: branch_id
                  type: BIGINT
                  constraints:
                    nullable: false
                    foreignKeyName: fk_documents_branch
                    references: tenant_management.branches(id)
                    deleteCascade: true
              - column:
                  name: file_path
                  type: TEXT
                  constraints:
                    nullable: false
              - column:
                  name: chunk_index
                  type: INTEGER
                  defaultValueNumeric: 0
                  constraints:
                    nullable: false
              - column:
                  name: chunk_count
                  type: INTEGER
                  defaultValueNumeric: 1
                  constraints:
                    nullable: false
              - column:
                  name: content
                  type: TEXT
                  constraints:
                    nullable: false
              - column:
                  name: content_hash
                  type: BYTEA
              - column:
                  name: language
                  type: VARCHAR(50)
              - column:
                  name: metadata
                  type: JSONB
                  defaultValue: '{}'
              - column:
                  name: created_at
                  type: TIMESTAMPTZ
                  defaultValueComputed: CURRENT_TIMESTAMP
                  constraints:
                    nullable: false
              - column:
                  name: updated_at
                  type: TIMESTAMPTZ
                  defaultValueComputed: CURRENT_TIMESTAMP
                  constraints:
                    nullable: false

        # Add vector column using raw SQL (Liquibase doesn't natively support vector type)
        - sql:
            sql: |
              ALTER TABLE tenant_management.documents
              ADD COLUMN content_embedding vector(1024);
            comment: Add 1024-dimension vector column for mxbai-embed-large embeddings

        # Unique constraint: one chunk per file per branch
        - addUniqueConstraint:
            schemaName: tenant_management
            tableName: documents
            columnNames: branch_id, file_path, chunk_index
            constraintName: uq_documents_branch_file_chunk

        # Index for finding documents by branch
        - createIndex:
            schemaName: tenant_management
            tableName: documents
            indexName: idx_documents_branch_id
            columns:
              - column:
                  name: branch_id

        # Index on content_hash for deduplication
        - createIndex:
            schemaName: tenant_management
            tableName: documents
            indexName: idx_documents_content_hash
            columns:
              - column:
                  name: content_hash

        # Index on language for filtering
        - createIndex:
            schemaName: tenant_management
            tableName: documents
            indexName: idx_documents_language
            columns:
              - column:
                  name: language

        # GIN index on metadata
        - sql:
            sql: |
              CREATE INDEX idx_documents_metadata
              ON tenant_management.documents USING gin (metadata jsonb_path_ops);

      rollback:
        - dropTable:
            schemaName: tenant_management
            tableName: documents
```

### 006-create-hnsw-indexes.yaml

```yaml
# liquibase/changelog/changes/006-create-hnsw-indexes.yaml
databaseChangeLog:
  - changeSet:
      id: 006-create-hnsw-index
      author: rag-mcp-server
      comment: Create HNSW index for fast approximate nearest neighbor search on document embeddings
      preConditions:
        - onFail: MARK_RAN
        - sqlCheck:
            expectedResult: 1
            sql: SELECT COUNT(*) FROM pg_extension WHERE extname = 'vector'
      changes:
        # Create HNSW index with optimized parameters
        # m=16: default connections per node (good balance of recall/speed)
        # ef_construction=64: candidates during build (higher = better recall, slower build)
        - sql:
            sql: |
              CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_documents_content_embedding_hnsw
              ON tenant_management.documents
              USING hnsw (content_embedding vector_cosine_ops)
              WITH (m = 16, ef_construction = 64);
            comment: |
              HNSW index for cosine similarity search.
              Parameters tuned for 1M+ vectors with good recall/speed tradeoff.
              Use SET hnsw.ef_search = 100 at query time for better recall.

      rollback:
        - sql:
            sql: DROP INDEX IF EXISTS tenant_management.idx_documents_content_embedding_hnsw;

  - changeSet:
      id: 006b-create-partial-hnsw-index
      author: rag-mcp-server
      comment: Optional partial HNSW index for specific languages (example for code-heavy repos)
      preConditions:
        - onFail: MARK_RAN
        - sqlCheck:
            expectedResult: 1
            sql: SELECT COUNT(*) FROM pg_extension WHERE extname = 'vector'
      changes:
        - sql:
            sql: |
              -- Partial index for code files only (faster for code-specific queries)
              CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_documents_code_embedding_hnsw
              ON tenant_management.documents
              USING hnsw (content_embedding vector_cosine_ops)
              WITH (m = 16, ef_construction = 64)
              WHERE language IN ('csharp', 'python', 'javascript', 'typescript', 'go', 'rust', 'java');
            comment: Partial HNSW index for code files only - improves query speed for code-specific searches

      rollback:
        - sql:
            sql: DROP INDEX IF EXISTS tenant_management.idx_documents_code_embedding_hnsw;
```

### 007-create-triggers.yaml (Optional)

```yaml
# liquibase/changelog/changes/007-create-triggers.yaml
databaseChangeLog:
  - changeSet:
      id: 007-create-updated-at-trigger-function
      author: rag-mcp-server
      comment: Create trigger function for automatic updated_at timestamp
      changes:
        - sql:
            splitStatements: false
            sql: |
              CREATE OR REPLACE FUNCTION tenant_management.update_updated_at_column()
              RETURNS TRIGGER AS $$
              BEGIN
                  NEW.updated_at = CURRENT_TIMESTAMP;
                  RETURN NEW;
              END;
              $$ LANGUAGE plpgsql;

      rollback:
        - sql:
            sql: DROP FUNCTION IF EXISTS tenant_management.update_updated_at_column() CASCADE;

  - changeSet:
      id: 007b-create-updated-at-triggers
      author: rag-mcp-server
      comment: Attach updated_at triggers to all tables
      changes:
        - sql:
            sql: |
              CREATE TRIGGER trg_repo_paths_updated_at
                  BEFORE UPDATE ON tenant_management.repo_paths
                  FOR EACH ROW
                  EXECUTE FUNCTION tenant_management.update_updated_at_column();

              CREATE TRIGGER trg_branches_updated_at
                  BEFORE UPDATE ON tenant_management.branches
                  FOR EACH ROW
                  EXECUTE FUNCTION tenant_management.update_updated_at_column();

              CREATE TRIGGER trg_documents_updated_at
                  BEFORE UPDATE ON tenant_management.documents
                  FOR EACH ROW
                  EXECUTE FUNCTION tenant_management.update_updated_at_column();

      rollback:
        - sql:
            sql: |
              DROP TRIGGER IF EXISTS trg_repo_paths_updated_at ON tenant_management.repo_paths;
              DROP TRIGGER IF EXISTS trg_branches_updated_at ON tenant_management.branches;
              DROP TRIGGER IF EXISTS trg_documents_updated_at ON tenant_management.documents;
```

---

## 7. Init Script for Container Startup

### For Separate Container Approach

The Liquibase container in docker-compose handles this automatically with the `command: update` directive.

### For Embedded Approach (Custom Image)

```bash
#!/bin/bash
# docker/init-with-liquibase.sh
# This script runs as part of PostgreSQL's docker-entrypoint-initdb.d

set -e

echo "=== Running Liquibase Migrations ==="

# PostgreSQL is already running at this point (init scripts run after pg starts)
# Connection details are available via environment variables

# Set Liquibase connection properties
export LIQUIBASE_COMMAND_URL="jdbc:postgresql://localhost:5432/${POSTGRES_DB}"
export LIQUIBASE_COMMAND_USERNAME="${POSTGRES_USER}"
export LIQUIBASE_COMMAND_PASSWORD="${POSTGRES_PASSWORD}"
export LIQUIBASE_COMMAND_CHANGELOG_FILE="${LIQUIBASE_CHANGELOG_FILE:-/liquibase/changelog/db.changelog-master.yaml}"

# Run Liquibase update
echo "Running Liquibase update..."
/opt/liquibase/liquibase \
    --log-level=INFO \
    update

# Check if successful
if [ $? -eq 0 ]; then
    echo "=== Liquibase migrations completed successfully ==="
else
    echo "=== ERROR: Liquibase migrations failed ===" >&2
    exit 1
fi

# Optional: Run Liquibase status to show what was applied
echo ""
echo "=== Migration Status ==="
/opt/liquibase/liquibase \
    --log-level=WARN \
    status --verbose
```

### Minimal pgvector Init (for separate container approach)

```sql
-- docker/init-pgvector.sql
-- This runs BEFORE Liquibase container starts
-- Only enables the extension; Liquibase handles everything else

CREATE EXTENSION IF NOT EXISTS vector;

-- Grant necessary permissions to the app user
-- (Liquibase runs as this user for migrations)
-- No additional grants needed if POSTGRES_USER is the superuser
```

---

## 8. Handling Schema Evolution

### Adding a New Column

```yaml
# liquibase/changelog/changes/008-add-document-summary.yaml
databaseChangeLog:
  - changeSet:
      id: 008-add-document-summary-column
      author: rag-mcp-server
      comment: Add summary column for document abstracts
      changes:
        - addColumn:
            schemaName: tenant_management
            tableName: documents
            columns:
              - column:
                  name: summary
                  type: TEXT
                  afterColumn: content
              - column:
                  name: summary_embedding
                  type: vector(1024)
                  afterColumn: summary

      rollback:
        - dropColumn:
            schemaName: tenant_management
            tableName: documents
            columnName: summary_embedding
        - dropColumn:
            schemaName: tenant_management
            tableName: documents
            columnName: summary
```

### Modifying an Index

```yaml
# liquibase/changelog/changes/009-tune-hnsw-index.yaml
databaseChangeLog:
  - changeSet:
      id: 009-tune-hnsw-index
      author: rag-mcp-server
      comment: Recreate HNSW index with higher m value for better recall
      changes:
        # Drop old index
        - sql:
            sql: DROP INDEX IF EXISTS tenant_management.idx_documents_content_embedding_hnsw;

        # Create new index with better parameters
        - sql:
            sql: |
              CREATE INDEX CONCURRENTLY idx_documents_content_embedding_hnsw
              ON tenant_management.documents
              USING hnsw (content_embedding vector_cosine_ops)
              WITH (m = 24, ef_construction = 100);
            comment: Increased m from 16 to 24 for better recall at slight memory cost

      rollback:
        - sql:
            sql: |
              DROP INDEX IF EXISTS tenant_management.idx_documents_content_embedding_hnsw;
              CREATE INDEX idx_documents_content_embedding_hnsw
              ON tenant_management.documents
              USING hnsw (content_embedding vector_cosine_ops)
              WITH (m = 16, ef_construction = 64);
```

### Data Migration Example

```yaml
# liquibase/changelog/changes/010-backfill-language.yaml
databaseChangeLog:
  - changeSet:
      id: 010-backfill-language-from-extension
      author: rag-mcp-server
      comment: Backfill language column based on file extension
      changes:
        - sql:
            sql: |
              UPDATE tenant_management.documents
              SET language = CASE
                  WHEN file_path LIKE '%.cs' THEN 'csharp'
                  WHEN file_path LIKE '%.py' THEN 'python'
                  WHEN file_path LIKE '%.js' THEN 'javascript'
                  WHEN file_path LIKE '%.ts' THEN 'typescript'
                  WHEN file_path LIKE '%.go' THEN 'go'
                  WHEN file_path LIKE '%.rs' THEN 'rust'
                  WHEN file_path LIKE '%.java' THEN 'java'
                  WHEN file_path LIKE '%.md' THEN 'markdown'
                  WHEN file_path LIKE '%.yaml' OR file_path LIKE '%.yml' THEN 'yaml'
                  WHEN file_path LIKE '%.json' THEN 'json'
                  ELSE 'unknown'
              END
              WHERE language IS NULL;

      rollback:
        - sql:
            sql: |
              UPDATE tenant_management.documents
              SET language = NULL
              WHERE language IN ('csharp', 'python', 'javascript', 'typescript',
                                 'go', 'rust', 'java', 'markdown', 'yaml', 'json', 'unknown');
```

---

## 9. pgvector-Specific Considerations

### Vector Column Types and Dimensions

| Embedding Model | Dimensions | Vector Type |
|-----------------|------------|-------------|
| mxbai-embed-large | 1024 | `vector(1024)` |
| nomic-embed-text | 768 | `vector(768)` |
| text-embedding-3-small | 1536 | `vector(1536)` |
| text-embedding-3-large | 3072 | `vector(3072)` |

### HNSW Index Parameters

| Parameter | Default | Recommendation | Notes |
|-----------|---------|----------------|-------|
| `m` | 16 | 16-32 | More connections = better recall, more memory |
| `ef_construction` | 64 | 64-128 | Higher = better index quality, slower build |

### Query-Time Tuning

```sql
-- Set at session level for better recall
SET hnsw.ef_search = 100;  -- Default is 40

-- Or per-transaction
SET LOCAL hnsw.ef_search = 200;
```

### Distance Functions and Operators

| Use Case | Operator | Operator Class |
|----------|----------|----------------|
| Text embeddings | `<=>` (cosine) | `vector_cosine_ops` |
| Normalized vectors | `<#>` (inner product) | `vector_ip_ops` |
| General purpose | `<->` (L2/Euclidean) | `vector_l2_ops` |

### Creating Vector Columns in Liquibase

Since Liquibase doesn't natively support the `vector` type, always use raw SQL:

```yaml
# CORRECT: Use sql change type
- sql:
    sql: ALTER TABLE schema.table ADD COLUMN embedding vector(1024);

# INCORRECT: createColumn doesn't support vector type
- addColumn:
    columns:
      - column:
          name: embedding
          type: vector(1024)  # This may not work correctly
```

---

## 10. Best Practices

### Changelog Organization

1. **One change per changeset** - Easier to debug and rollback
2. **Numeric prefixes** - `001-`, `002-` ensures execution order
3. **Descriptive IDs** - `005-create-documents-table` not `changeset-5`
4. **Include comments** - Document why, not just what

### Rollback Strategy

1. **Always define rollbacks** - Even for simple changes
2. **Test rollbacks** - Run `liquibase rollback-count 1` in development
3. **Consider data loss** - Rollbacks may lose data; document this

### Performance

1. **Use CONCURRENTLY** - For index creation on production data
2. **Batch data migrations** - Don't update millions of rows in one transaction
3. **Create indexes after data load** - Faster than incremental index updates

### Security

1. **No passwords in changelogs** - Use environment variables
2. **Least privilege** - Run migrations with limited permissions
3. **Audit trail** - Liquibase's DATABASECHANGELOG provides this

### CI/CD Integration

```yaml
# Example GitHub Actions workflow
- name: Run Liquibase Migrations
  run: |
    docker compose up -d postgres
    docker compose run --rm liquibase update
    docker compose run --rm liquibase status --verbose
```

---

## 11. Complete Project Structure

```
project-root/
├── docker-compose.yml
├── Dockerfile.pgvector-liquibase      # Optional: custom image
├── .env                               # POSTGRES_PASSWORD, etc.
├── .env.example                       # Template for .env
├── .gitignore                         # Include .env
│
├── docker/
│   ├── init-pgvector.sql              # Minimal init for separate container
│   └── init-with-liquibase.sh         # Init script for embedded approach
│
├── liquibase/
│   ├── liquibase.properties           # Default connection config
│   └── changelog/
│       ├── db.changelog-master.yaml   # Root changelog
│       └── changes/
│           ├── 001-enable-pgvector.yaml
│           ├── 002-create-tenant-schema.yaml
│           ├── 003-create-repo-paths-table.yaml
│           ├── 004-create-branches-table.yaml
│           ├── 005-create-documents-table.yaml
│           ├── 006-create-hnsw-indexes.yaml
│           └── 007-create-triggers.yaml
│
├── src/
│   └── RagMcpServer/
│       └── ...
│
└── scripts/
    ├── start-dev.sh                   # Start PostgreSQL + run migrations
    └── reset-db.sh                    # Drop and recreate database
```

### liquibase.properties (default config)

```properties
# liquibase/liquibase.properties
# These can be overridden by environment variables

driver: org.postgresql.Driver
url: jdbc:postgresql://localhost:5432/rag_db
username: rag_user
# password is set via LIQUIBASE_COMMAND_PASSWORD environment variable

changeLogFile: changelog/db.changelog-master.yaml
liquibase.hub.mode: off
```

### Start Script (scripts/start-dev.sh)

```bash
#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
cd "$PROJECT_ROOT"

echo "=== Starting RAG Development Environment ==="

# Start PostgreSQL
echo "Starting PostgreSQL with pgvector..."
docker compose up -d postgres

# Wait for PostgreSQL
echo "Waiting for PostgreSQL to be healthy..."
until docker compose exec -T postgres pg_isready -U rag_user -d rag_db > /dev/null 2>&1; do
    sleep 1
done
echo "PostgreSQL is ready!"

# Run Liquibase migrations
echo "Running Liquibase migrations..."
docker compose run --rm liquibase update

# Show migration status
echo ""
echo "=== Migration Status ==="
docker compose run --rm liquibase status --verbose

echo ""
echo "=== Environment Ready ==="
echo "PostgreSQL: localhost:5432"
echo "Database: rag_db"
echo "Schema: tenant_management"
```

---

## 12. Sources

### Liquibase Documentation
- [Using Liquibase and Docker](https://docs.liquibase.com/workflows/liquibase-community/using-liquibase-and-docker.html)
- [Using Liquibase and PostgreSQL with Docker](https://docs.liquibase.com/pro/integration-guide-4-33/using-liquibase-and-postgresql-with-docker)
- [YAML Changelog Example](https://docs.liquibase.com/oss/user-guide-4-33/yaml-changelog-example)
- [createIndex Change Type](https://docs.liquibase.com/change-types/create-index.html)
- [sql Change Type](https://docs.liquibase.com/change-types/sql.html)
- [endDelimiter SQL Attribute](https://docs.liquibase.com/change-types/enddelimiter-sql.html)
- [Connect Changelogs using include/includeAll](https://docs.liquibase.com/community/implementation-guide-5-0/connect-your-changelogs-using-include-or-includeall)
- [How to Structure a Complex Changelog](https://support.liquibase.com/hc/en-us/articles/29383071573659-How-to-Structure-a-Complex-Changelog)
- [Design Your Liquibase Project](https://docs.liquibase.com/start/design-liquibase-project.html)

### Docker Images
- [Liquibase Official Docker Image](https://hub.docker.com/_/liquibase)
- [liquibase/liquibase Docker Hub](https://hub.docker.com/r/liquibase/liquibase)
- [kilna/liquibase-postgres-docker GitHub](https://github.com/kilna/liquibase-postgres-docker)
- [pgvector/pgvector Docker Hub](https://hub.docker.com/r/pgvector/pgvector)

### pgvector and HNSW
- [HNSW Indexes with Postgres and pgvector - Crunchy Data](https://www.crunchydata.com/blog/hnsw-indexes-with-postgres-and-pgvector)
- [pgvector GitHub Repository](https://github.com/pgvector/pgvector)
- [Understanding pgvector's HNSW Index Storage - Lantern Blog](https://lantern.dev/blog/pgvector-storage)
- [HNSW Indexes - Supabase Docs](https://supabase.com/docs/guides/ai/vector-indexes/hnsw-indexes)

### Integration Examples
- [Integrating Vector Data with Langchain4j, PostgreSQL, and Liquibase](https://blog.samzhu.dev/2023/12/19/Integrating-Vector-Data-with-Langchain4j-PostgreSQL-and-Liquibase-in-Spring-Boot/)
- [Database Migrations with Liquibase and Docker - Medium](https://marcos-lobo.medium.com/database-migrations-with-liquibase-and-docker-b13c9db45a7a)
- [Using Liquibase in Kubernetes](https://www.liquibase.com/blog/using-liquibase-in-kubernetes)

### Best Practices
- [includeAll Documentation](https://docs.liquibase.com/change-types/includeall.html)
- [Docker Postgres: Troubleshooting init.sql Not Running](https://www.codegenes.net/blog/docker-postgres-does-not-run-init-file-in-docker-entrypoint-initdb-d/)

---

*This research report was compiled for the csharp-compound-engineering project to support using Liquibase for database schema management with PostgreSQL/pgvector in Docker containers.*
