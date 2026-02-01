# Phase 007: Liquibase tenant_management Schema

> **Status**: [PLANNED]
> **Estimated Effort**: M
> **Prerequisites**: Phase 006
> **Category**: Infrastructure Setup

---

## Spec References

- [spec/mcp-server/liquibase-changelog.md](../spec/mcp-server/liquibase-changelog.md) - Changelog format and structure
- [spec/mcp-server/database-schema.md](../spec/mcp-server/database-schema.md#tenant-management-schema) - Schema definition

---

## Objectives

1. Create the Liquibase master changelog file (`changelog.xml`)
2. Implement changeset 001: Create `tenant_management` schema
3. Implement changeset 002: Create `repo_paths` table
4. Implement changeset 003: Create `branches` table
5. Implement changeset 004: Create indexes for common query patterns
6. Create all SQL files (change.sql and rollback.sql) for each changeset

---

## Acceptance Criteria

- [ ] Master changelog (`changelog.xml`) exists with include directives for all changesets
- [ ] Directory structure follows spec: `docker/postgres/changelog/changes/NNN-description/`
- [ ] Each changeset has `change.xml`, `change.sql`, and `rollback.sql`
- [ ] Changeset IDs use format `NNN-description` with author `csharp-compounding-docs`
- [ ] All SQL uses `IF EXISTS`/`IF NOT EXISTS` for idempotency
- [ ] Rollback scripts cascade delete where appropriate
- [ ] Table comments document purpose and column semantics
- [ ] All migrations run successfully via Liquibase
- [ ] All rollbacks execute cleanly without errors

---

## Implementation Notes

### Directory Structure

Create the following structure under `docker/postgres/`:

```
docker/postgres/changelog/
├── changelog.xml
├── changes/
│   ├── 001-create-tenant-schema/
│   │   ├── change.xml
│   │   ├── change.sql
│   │   └── rollback.sql
│   ├── 002-create-repo-paths-table/
│   │   ├── change.xml
│   │   ├── change.sql
│   │   └── rollback.sql
│   ├── 003-create-branches-table/
│   │   ├── change.xml
│   │   ├── change.sql
│   │   └── rollback.sql
│   └── 004-create-indexes/
│       ├── change.xml
│       ├── change.sql
│       └── rollback.sql
```

### Master Changelog (changelog.xml)

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

### Changeset 001: Create tenant_management Schema

**change.xml**:
```xml
<?xml version="1.0" encoding="UTF-8"?>
<databaseChangeLog
    xmlns="http://www.liquibase.org/xml/ns/dbchangelog"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xsi:schemaLocation="http://www.liquibase.org/xml/ns/dbchangelog
        http://www.liquibase.org/xml/ns/dbchangelog/dbchangelog-latest.xsd">

    <changeSet id="001-create-tenant-schema" author="csharp-compounding-docs">
        <comment>Create the tenant_management schema for multi-tenant isolation</comment>

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

**change.sql**:
```sql
-- Create tenant_management schema
-- This schema holds tables for tracking projects, paths, and branches
-- across multiple tenants (repositories/worktrees)

CREATE SCHEMA IF NOT EXISTS tenant_management;
```

**rollback.sql**:
```sql
-- Drop tenant_management schema and all contained objects
-- WARNING: This will cascade delete all tables in the schema

DROP SCHEMA IF EXISTS tenant_management CASCADE;
```

### Changeset 002: Create repo_paths Table

**change.xml**:
```xml
<?xml version="1.0" encoding="UTF-8"?>
<databaseChangeLog
    xmlns="http://www.liquibase.org/xml/ns/dbchangelog"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xsi:schemaLocation="http://www.liquibase.org/xml/ns/dbchangelog
        http://www.liquibase.org/xml/ns/dbchangelog/dbchangelog-latest.xsd">

    <changeSet id="002-create-repo-paths-table" author="csharp-compounding-docs">
        <comment>Create repo_paths table to track encountered repository paths</comment>

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

**change.sql**:
```sql
-- Create repo_paths table
-- Tracks absolute paths to repositories/worktrees
-- path_hash is SHA256(normalized_path)[0:16] for tenant isolation

CREATE TABLE IF NOT EXISTS tenant_management.repo_paths (
    path_hash VARCHAR(64) PRIMARY KEY,
    absolute_path TEXT NOT NULL,
    project_name TEXT NOT NULL,
    first_seen TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_seen TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE tenant_management.repo_paths IS 'Tracks encountered repository paths for tenant isolation';
COMMENT ON COLUMN tenant_management.repo_paths.path_hash IS 'SHA256 hash of normalized absolute path (first 16 chars)';
COMMENT ON COLUMN tenant_management.repo_paths.absolute_path IS 'Full absolute path to repository root';
COMMENT ON COLUMN tenant_management.repo_paths.project_name IS 'Extracted project name from path or git remote';
```

**rollback.sql**:
```sql
-- Drop repo_paths table

DROP TABLE IF EXISTS tenant_management.repo_paths;
```

### Changeset 003: Create branches Table

**change.xml**:
```xml
<?xml version="1.0" encoding="UTF-8"?>
<databaseChangeLog
    xmlns="http://www.liquibase.org/xml/ns/dbchangelog"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xsi:schemaLocation="http://www.liquibase.org/xml/ns/dbchangelog
        http://www.liquibase.org/xml/ns/dbchangelog/dbchangelog-latest.xsd">

    <changeSet id="003-create-branches-table" author="csharp-compounding-docs">
        <comment>Create branches table to track encountered git branches per project</comment>

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

**change.sql**:
```sql
-- Create branches table
-- Tracks git branches encountered for each project
-- Used for cleanup of orphaned branch data

CREATE TABLE IF NOT EXISTS tenant_management.branches (
    id SERIAL PRIMARY KEY,
    project_name TEXT NOT NULL,
    branch_name TEXT NOT NULL,
    first_seen TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_seen TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(project_name, branch_name)
);

COMMENT ON TABLE tenant_management.branches IS 'Tracks git branches encountered per project';
COMMENT ON COLUMN tenant_management.branches.project_name IS 'Project name (matches repo_paths.project_name)';
COMMENT ON COLUMN tenant_management.branches.branch_name IS 'Git branch name';
```

**rollback.sql**:
```sql
-- Drop branches table

DROP TABLE IF EXISTS tenant_management.branches;
```

### Changeset 004: Create Indexes

**change.xml**:
```xml
<?xml version="1.0" encoding="UTF-8"?>
<databaseChangeLog
    xmlns="http://www.liquibase.org/xml/ns/dbchangelog"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xsi:schemaLocation="http://www.liquibase.org/xml/ns/dbchangelog
        http://www.liquibase.org/xml/ns/dbchangelog/dbchangelog-latest.xsd">

    <changeSet id="004-create-indexes" author="csharp-compounding-docs">
        <comment>Create indexes for common query patterns</comment>

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

**change.sql**:
```sql
-- Create indexes for common query patterns

-- Index for looking up branches by project (cleanup operations)
CREATE INDEX IF NOT EXISTS idx_branches_project
ON tenant_management.branches (project_name);

-- Index for looking up repo paths by project name
CREATE INDEX IF NOT EXISTS idx_repo_paths_project
ON tenant_management.repo_paths (project_name);
```

**rollback.sql**:
```sql
-- Drop indexes

DROP INDEX IF EXISTS tenant_management.idx_branches_project;
DROP INDEX IF EXISTS tenant_management.idx_repo_paths_project;
```

### sqlFile Attribute Reference

| Attribute | Value | Description |
|-----------|-------|-------------|
| `path` | `change.sql` or `rollback.sql` | Relative path to SQL file |
| `relativeToChangelogFile` | `true` | Path is relative to the change.xml file |
| `encoding` | `UTF-8` | Character encoding of the SQL file |
| `splitStatements` | `true` | Allow multiple statements separated by `;` |
| `stripComments` | `false` | Preserve SQL comments for documentation |

### Testing Migrations

After creating all files, test migrations with:

```bash
# Apply pending changes
docker exec csharp-compounding-docs-postgres \
    liquibase --changeLogFile=/liquibase/changelog/changelog.xml \
              --url="jdbc:postgresql://localhost:5432/compounding_docs" \
              --username=compounding \
              --password=compounding \
              update

# Verify schema was created
docker exec csharp-compounding-docs-postgres \
    psql -U compounding -d compounding_docs \
    -c "SELECT schema_name FROM information_schema.schemata WHERE schema_name = 'tenant_management';"

# Verify tables exist
docker exec csharp-compounding-docs-postgres \
    psql -U compounding -d compounding_docs \
    -c "SELECT table_name FROM information_schema.tables WHERE table_schema = 'tenant_management';"

# Test rollback (one at a time)
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

- **Phase 006**: Docker Compose infrastructure must exist with PostgreSQL container and Liquibase setup

### Blocks

- **Phase 008+**: Any phase requiring database access depends on this schema being available
- **MCP Server startup**: Server validates database connection and schema presence

---

## Files to Create

| File Path | Description |
|-----------|-------------|
| `docker/postgres/changelog/changelog.xml` | Master changelog entry point |
| `docker/postgres/changelog/changes/001-create-tenant-schema/change.xml` | Changeset definition |
| `docker/postgres/changelog/changes/001-create-tenant-schema/change.sql` | Schema creation SQL |
| `docker/postgres/changelog/changes/001-create-tenant-schema/rollback.sql` | Schema rollback SQL |
| `docker/postgres/changelog/changes/002-create-repo-paths-table/change.xml` | Changeset definition |
| `docker/postgres/changelog/changes/002-create-repo-paths-table/change.sql` | Table creation SQL |
| `docker/postgres/changelog/changes/002-create-repo-paths-table/rollback.sql` | Table rollback SQL |
| `docker/postgres/changelog/changes/003-create-branches-table/change.xml` | Changeset definition |
| `docker/postgres/changelog/changes/003-create-branches-table/change.sql` | Table creation SQL |
| `docker/postgres/changelog/changes/003-create-branches-table/rollback.sql` | Table rollback SQL |
| `docker/postgres/changelog/changes/004-create-indexes/change.xml` | Changeset definition |
| `docker/postgres/changelog/changes/004-create-indexes/change.sql` | Index creation SQL |
| `docker/postgres/changelog/changes/004-create-indexes/rollback.sql` | Index rollback SQL |

**Total files**: 13
