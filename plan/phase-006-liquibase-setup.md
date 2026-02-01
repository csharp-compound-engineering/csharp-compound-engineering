# Phase 006: Liquibase Migration System Setup

> **Status**: [PLANNED]
> **Estimated Effort**: M
> **Category**: Infrastructure Setup
> **Prerequisites**: Phase 003 (Docker Infrastructure)

---

## Spec References

- [spec/mcp-server/liquibase-changelog.md](../spec/mcp-server/liquibase-changelog.md) - Complete Liquibase specification
- [spec/infrastructure.md](../spec/infrastructure.md#postgresql--pgvector--liquibase) - Docker integration details
- [spec/mcp-server/database-schema.md](../spec/mcp-server/database-schema.md) - Schema management split (Liquibase vs Semantic Kernel)
- [research/liquibase-changelog-format-research.md](../research/liquibase-changelog-format-research.md) - XML format best practices
- [research/liquibase-pgvector-docker-init.md](../research/liquibase-pgvector-docker-init.md) - Docker initialization patterns

---

## Objectives

1. Create Liquibase directory structure under `docker/postgres/`
2. Configure master `changelog.xml` with include directives
3. Implement initial four changesets for `tenant_management` schema
4. Create Docker init script for pgvector extension and Liquibase execution
5. Update custom Dockerfile to include Liquibase installation
6. Document migration workflow for development and production

---

## Acceptance Criteria

- [ ] Directory structure follows spec: `docker/postgres/changelog/changes/NNN-description/`
- [ ] Master `changelog.xml` uses include directives (not inline changesets)
- [ ] Each changeset has `change.xml`, `change.sql`, and `rollback.sql` files
- [ ] Changeset IDs use `NNN-description` format with author `csharp-compounding-docs`
- [ ] Init script enables pgvector extension BEFORE Liquibase runs
- [ ] Migrations create `tenant_management` schema with `repo_paths` and `branches` tables
- [ ] All rollback scripts are tested and functional
- [ ] Dockerfile installs Liquibase 4.25+ with PostgreSQL JDBC driver
- [ ] Documentation covers manual migration commands

---

## Implementation Notes

### Directory Structure

Create the following directory structure under `docker/postgres/`:

```
docker/postgres/
├── Dockerfile                       # Custom pgvector+Liquibase image
├── init-db.sh                       # Docker entrypoint init script
└── changelog/
    ├── changelog.xml                # Master entry point
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

### Master Changelog Configuration

The master `changelog.xml` must:
- Use XML format with `dbchangelog-latest.xsd` schema
- Include individual changesets via `<include>` directives
- Set `relativeToChangelogFile="true"` for all includes
- Use forward slashes for all paths (cross-platform compatibility)
- Include header comment documenting the changelog format convention

Example structure:
```xml
<?xml version="1.0" encoding="UTF-8"?>
<databaseChangeLog
    xmlns="http://www.liquibase.org/xml/ns/dbchangelog"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xsi:schemaLocation="http://www.liquibase.org/xml/ns/dbchangelog
        http://www.liquibase.org/xml/ns/dbchangelog/dbchangelog-latest.xsd">

    <!-- Include all changesets -->
    <include file="changes/001-create-tenant-schema/change.xml" relativeToChangelogFile="true"/>
    <include file="changes/002-create-repo-paths-table/change.xml" relativeToChangelogFile="true"/>
    <!-- ... -->

</databaseChangeLog>
```

### Changeset Template

Each changeset uses a consistent XML template:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<databaseChangeLog ...>
    <changeSet id="NNN-description" author="csharp-compounding-docs">
        <comment>Brief description of what this change does</comment>

        <sqlFile path="change.sql"
                 relativeToChangelogFile="true"
                 encoding="UTF-8"
                 splitStatements="true"
                 stripComments="false"/>

        <rollback>
            <sqlFile path="rollback.sql"
                     relativeToChangelogFile="true"
                     encoding="UTF-8"
                     splitStatements="true"
                     stripComments="false"/>
        </rollback>
    </changeSet>
</databaseChangeLog>
```

Key attributes:
- `stripComments="false"` - Preserve SQL comments for documentation
- `splitStatements="true"` - Allow multiple statements separated by `;`
- `encoding="UTF-8"` - Explicit encoding for consistency

### Init Script Requirements

The `init-db.sh` script must:

1. **Run as part of `/docker-entrypoint-initdb.d/`** - PostgreSQL runs these on first database creation
2. **Enable pgvector extension FIRST** - Required before Liquibase creates tables with vector columns
3. **Run Liquibase migrations** - Apply all pending changesets
4. **Use `set -e`** - Fail fast on any error

Script pattern:
```bash
#!/bin/bash
set -e

# Enable pgvector extension (must happen before Liquibase)
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE EXTENSION IF NOT EXISTS vector;
EOSQL

# Run Liquibase migrations
liquibase --changeLogFile=/liquibase/changelog/changelog.xml \
          --url="jdbc:postgresql://localhost:5432/$POSTGRES_DB" \
          --username="$POSTGRES_USER" \
          --password="$POSTGRES_PASSWORD" \
          update
```

### Dockerfile Updates

Extend `pgvector/pgvector:pg16` with Liquibase:

```dockerfile
FROM pgvector/pgvector:pg16

# Install Java (required for Liquibase) and wget
RUN apt-get update && apt-get install -y wget default-jre && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

# Install Liquibase
ARG LIQUIBASE_VERSION=4.25.0
RUN wget -qO- https://github.com/liquibase/liquibase/releases/download/v${LIQUIBASE_VERSION}/liquibase-${LIQUIBASE_VERSION}.tar.gz | tar xz -C /opt && \
    ln -s /opt/liquibase/liquibase /usr/local/bin/liquibase

# Download PostgreSQL JDBC driver
ARG POSTGRES_JDBC_VERSION=42.7.4
RUN wget -O /opt/liquibase/lib/postgresql.jar \
    https://jdbc.postgresql.org/download/postgresql-${POSTGRES_JDBC_VERSION}.jar

# Create directories
RUN mkdir -p /liquibase/changelog

# Copy init script and changelog
COPY init-db.sh /docker-entrypoint-initdb.d/
COPY changelog /liquibase/changelog

RUN chmod +x /docker-entrypoint-initdb.d/init-db.sh
```

### Initial Changesets Content

**001-create-tenant-schema**: Creates the `tenant_management` schema
- SQL: `CREATE SCHEMA IF NOT EXISTS tenant_management;`
- Rollback: `DROP SCHEMA IF EXISTS tenant_management CASCADE;`

**002-create-repo-paths-table**: Creates `repo_paths` table
- Columns: `path_hash` (PK), `absolute_path`, `project_name`, `first_seen`, `last_seen`
- `path_hash` is `VARCHAR(64)` for SHA256 truncated hash
- Rollback: `DROP TABLE IF EXISTS tenant_management.repo_paths;`

**003-create-branches-table**: Creates `branches` table
- Columns: `id` (SERIAL PK), `project_name`, `branch_name`, `first_seen`, `last_seen`
- Unique constraint on `(project_name, branch_name)`
- Rollback: `DROP TABLE IF EXISTS tenant_management.branches;`

**004-create-indexes**: Creates performance indexes
- `idx_branches_project` on `branches(project_name)`
- `idx_repo_paths_project` on `repo_paths(project_name)`
- Rollback: Drop both indexes

### Schema Management Split

**Important**: This phase ONLY creates `tenant_management` schema tables via Liquibase.

Vector tables (`CompoundDocument`, `DocumentChunk`, etc.) are managed by Semantic Kernel's `EnsureCollectionExistsAsync()` in the `compounding` schema. This separation is intentional:
- Liquibase: Relational tables requiring explicit DDL control and audit trail
- Semantic Kernel: Vector collections with auto-managed HNSW indexes

### Migration Workflow Documentation

Create `docker/postgres/README.md` documenting:

1. **Automatic migrations** - Run on container first start via init script
2. **Manual apply** - `docker exec ... liquibase update`
3. **Manual rollback** - `docker exec ... liquibase rollback-count N`
4. **Status check** - `docker exec ... liquibase status`
5. **Adding new migrations** - Step-by-step guide

Example manual commands:
```bash
# Apply pending changes
docker exec csharp-compounding-docs-postgres \
    liquibase --changeLogFile=/liquibase/changelog/changelog.xml \
              --url="jdbc:postgresql://localhost:5432/compounding_docs" \
              --username=compounding \
              --password=compounding \
              update

# Rollback last changeset
docker exec csharp-compounding-docs-postgres \
    liquibase --changeLogFile=/liquibase/changelog/changelog.xml \
              --url="jdbc:postgresql://localhost:5432/compounding_docs" \
              --username=compounding \
              --password=compounding \
              rollback-count 1
```

---

## Dependencies

### Depends On

- **Phase 003**: Docker Infrastructure setup must exist (docker-compose.yml, directory structure)

### Blocks

- **Phase XXX**: Database schema phases that need tables to exist
- **Phase XXX**: MCP server database layer implementation
- **Phase XXX**: File watcher service (needs `repo_paths` and `branches` tables)

---

## Verification Steps

1. **Build custom Docker image**:
   ```bash
   docker build -t csharp-compounding-docs-postgres docker/postgres/
   ```

2. **Start fresh container** (remove existing volume first):
   ```bash
   docker volume rm csharp-compounding-docs_pgdata || true
   docker compose -p csharp-compounding-docs up -d postgres
   ```

3. **Verify pgvector extension**:
   ```bash
   docker exec csharp-compounding-docs-postgres \
       psql -U compounding -d compounding_docs -c "SELECT * FROM pg_extension WHERE extname = 'vector';"
   ```

4. **Verify schema and tables**:
   ```bash
   docker exec csharp-compounding-docs-postgres \
       psql -U compounding -d compounding_docs -c "\dn tenant_management"
   docker exec csharp-compounding-docs-postgres \
       psql -U compounding -d compounding_docs -c "\dt tenant_management.*"
   ```

5. **Verify Liquibase tracking**:
   ```bash
   docker exec csharp-compounding-docs-postgres \
       psql -U compounding -d compounding_docs -c "SELECT id, author, filename FROM databasechangelog;"
   ```

6. **Test rollback**:
   ```bash
   docker exec csharp-compounding-docs-postgres \
       liquibase --changeLogFile=/liquibase/changelog/changelog.xml \
                 --url="jdbc:postgresql://localhost:5432/compounding_docs" \
                 --username=compounding --password=compounding \
                 rollback-count 1
   # Verify index was dropped
   # Then re-apply
   docker exec csharp-compounding-docs-postgres \
       liquibase ... update
   ```

---

## Files to Create

| File | Purpose |
|------|---------|
| `docker/postgres/Dockerfile` | Custom image with Liquibase |
| `docker/postgres/init-db.sh` | Init script for pgvector + migrations |
| `docker/postgres/changelog/changelog.xml` | Master changelog |
| `docker/postgres/changelog/changes/001-create-tenant-schema/change.xml` | Changeset definition |
| `docker/postgres/changelog/changes/001-create-tenant-schema/change.sql` | Forward migration |
| `docker/postgres/changelog/changes/001-create-tenant-schema/rollback.sql` | Rollback |
| `docker/postgres/changelog/changes/002-create-repo-paths-table/change.xml` | Changeset definition |
| `docker/postgres/changelog/changes/002-create-repo-paths-table/change.sql` | Forward migration |
| `docker/postgres/changelog/changes/002-create-repo-paths-table/rollback.sql` | Rollback |
| `docker/postgres/changelog/changes/003-create-branches-table/change.xml` | Changeset definition |
| `docker/postgres/changelog/changes/003-create-branches-table/change.sql` | Forward migration |
| `docker/postgres/changelog/changes/003-create-branches-table/rollback.sql` | Rollback |
| `docker/postgres/changelog/changes/004-create-indexes/change.xml` | Changeset definition |
| `docker/postgres/changelog/changes/004-create-indexes/change.sql` | Forward migration |
| `docker/postgres/changelog/changes/004-create-indexes/rollback.sql` | Rollback |
| `docker/postgres/README.md` | Migration workflow documentation |

---

## Best Practices Checklist

- [ ] One logical change per changeset (no mixing unrelated DDL)
- [ ] Never modify deployed changesets (add new ones instead)
- [ ] Explicit rollback SQL for all changes
- [ ] Use `IF EXISTS`/`IF NOT EXISTS` for idempotency
- [ ] Preserve SQL comments (`stripComments="false"`)
- [ ] Sequential numbering with no gaps
- [ ] Consistent author name across all changesets
- [ ] Forward slashes in all paths
- [ ] Relative paths with `relativeToChangelogFile="true"`
