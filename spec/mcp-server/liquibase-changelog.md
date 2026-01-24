# Liquibase Changelog Specification

> **Status**: [DRAFT]
> **Parent**: [database-schema.md](./database-schema.md)
> **Research**: [liquibase-changelog-format-research.md](../../research/liquibase-changelog-format-research.md)

> **Background**: Detailed guidance on Docker configuration for PostgreSQL/pgvector with Liquibase migrations, including init scripts and container architecture options. See [Liquibase with PostgreSQL/pgvector in Docker](../../research/liquibase-pgvector-docker-init.md).

---

## Overview

Database migrations for the `tenant_management` schema use Liquibase with an XML-based changelog format. The structure separates concerns:

- **Master changelog** (`changelog.xml`) - Entry point with include directives
- **Change XML files** - Individual changeset definitions
- **SQL files** - Raw SQL for changes and rollbacks

---

## Directory Structure

```
docker/postgres/changelog/
├── changelog.xml                    # Master entry point
├── changes/
│   ├── 001-create-tenant-schema/
│   │   ├── change.xml               # Changeset definition
│   │   ├── change.sql               # Forward migration SQL
│   │   └── rollback.sql             # Rollback SQL
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

> **Note**: The pgvector extension is enabled via the Docker init script (`init-db.sh`) before Liquibase runs. See [infrastructure.md](../infrastructure.md) for details.

---

## Master Changelog

### File: `changelog.xml`

```xml
<?xml version="1.0" encoding="UTF-8"?>
<databaseChangeLog
    xmlns="http://www.liquibase.org/xml/ns/dbchangelog"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xsi:schemaLocation="http://www.liquibase.org/xml/ns/dbchangelog
        http://www.liquibase.org/xml/ns/dbchangelog/dbchangelog-latest.xsd">

    <!--
        CHANGELOG FORMAT CONVENTION
        ==========================

        This changelog uses include directives to delegate to individual change XML files.
        Each change directory contains:

        - change.xml    : Changeset definition with <sqlFile> references
        - change.sql    : Raw SQL for the forward migration
        - rollback.sql  : Raw SQL for explicit rollback

        Naming convention: NNN-description/
        Where NNN is a zero-padded sequential number (001, 002, etc.)

        Example structure:
            changes/001-create-tenant-schema/
            ├── change.xml
            ├── change.sql
            └── rollback.sql
    -->

    <!-- Initial schema setup -->
    <include file="changes/001-create-tenant-schema/change.xml" relativeToChangelogFile="true"/>
    <include file="changes/002-create-repo-paths-table/change.xml" relativeToChangelogFile="true"/>
    <include file="changes/003-create-branches-table/change.xml" relativeToChangelogFile="true"/>
    <include file="changes/004-create-indexes/change.xml" relativeToChangelogFile="true"/>

</databaseChangeLog>
```

---

## Changeset Format

### Template: `changes/NNN-description/change.xml`

```xml
<?xml version="1.0" encoding="UTF-8"?>
<databaseChangeLog
    xmlns="http://www.liquibase.org/xml/ns/dbchangelog"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xsi:schemaLocation="http://www.liquibase.org/xml/ns/dbchangelog
        http://www.liquibase.org/xml/ns/dbchangelog/dbchangelog-latest.xsd">

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

### Changeset Attributes

| Attribute | Value | Description |
|-----------|-------|-------------|
| `id` | `NNN-description` | Unique identifier matching directory name |
| `author` | `csharp-compounding-docs` | Consistent author for all plugin changes |

### sqlFile Attributes

| Attribute | Value | Description |
|-----------|-------|-------------|
| `path` | `change.sql` or `rollback.sql` | Relative path to SQL file |
| `relativeToChangelogFile` | `true` | Path is relative to the change.xml file |
| `encoding` | `UTF-8` | Character encoding of the SQL file |
| `splitStatements` | `true` | Allow multiple statements separated by `;` |
| `stripComments` | `false` | Preserve SQL comments for documentation |

---

## Initial Changesets

### 001-create-tenant-schema

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

---

### 002-create-repo-paths-table

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

---

### 003-create-branches-table

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

---

### 004-create-indexes

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

---

## Adding New Migrations

To add a new migration:

1. Create a new directory: `changes/NNN-description/`
2. Copy the change.xml template and update:
   - `id` attribute to match directory name
   - `<comment>` with description
   - Add `dbms="postgresql"` if using PostgreSQL-specific SQL
3. Write `change.sql` with forward migration
4. Write `rollback.sql` with explicit rollback
5. Add `<include>` directive to `changelog.xml`

### Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Directory | `NNN-kebab-case-description` | `005-add-metadata-column` |
| Changeset ID | Same as directory | `005-add-metadata-column` |
| Author | `csharp-compounding-docs` | (consistent) |

---

## Running Migrations

### Via Docker Init Script

Migrations run automatically on container startup via `init-db.sh`:

```bash
liquibase --changeLogFile=/liquibase/changelog/changelog.xml \
          --url="jdbc:postgresql://localhost:5432/compounding_docs" \
          --username=compounding \
          --password=compounding \
          update
```

### Manual Execution

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

# Show pending changes
docker exec csharp-compounding-docs-postgres \
    liquibase --changeLogFile=/liquibase/changelog/changelog.xml \
              --url="jdbc:postgresql://localhost:5432/compounding_docs" \
              --username=compounding \
              --password=compounding \
              status
```

---

## Best Practices

### Changeset Guidelines

1. **One logical change per changeset** - Don't mix unrelated DDL; avoids partial failures with auto-commit
2. **Never modify deployed changesets** - Always add new changesets for modifications
3. **Always write explicit rollback SQL** - No automatic rollback for `sqlFile` changes
4. **Test rollbacks** - Run `rollback-sql` to preview before committing
5. **Use descriptive IDs** - `001-create-tenant-schema` over just `1`

### SQL Guidelines

1. **Preserve comments** - Set `stripComments="false"` to document intent
2. **Use IF EXISTS/IF NOT EXISTS** - Make scripts idempotent where possible
3. **Use UTF-8 encoding** - Explicit `encoding="UTF-8"` for consistency

### File Organization

1. **Sequential numbering only** - No gaps, no reordering after release
2. **Root changelog as configuration only** - Use includes, not inline changesets
3. **Use forward slashes** - `/` works on all platforms
4. **Relative paths** - Set `relativeToChangelogFile="true"` for portability

### PostgreSQL-Specific

1. **Extensions via init script** - pgvector enabled in `init-db.sh` before Liquibase runs
2. **Use `dbms="postgresql"`** - For database-specific SQL in changesets (if needed)
3. **Idempotent extension setup** - Init script uses `CREATE EXTENSION IF NOT EXISTS`

---

## Resolved Questions

1. ~~Should we add a pgvector extension changeset, or is that handled by the base image?~~
   **Resolved**: Handled via Docker init script (`init-db.sh`), not Liquibase. The script runs `CREATE EXTENSION IF NOT EXISTS vector;` before Liquibase migrations. This keeps extension management simple and separate from schema migrations.

2. ~~What's the strategy for schema changes after initial release?~~
   **Resolved**: Add new changesets with sequential numbering (005, 006, etc.). Never modify deployed changesets - always add new ones.

## PostgreSQL-Specific Notes

- The pgvector extension is enabled via Docker init script before Liquibase runs
- Use `dbms="postgresql"` attribute on changesets with PostgreSQL-specific SQL (if needed)
- The `vector` data type (used by Semantic Kernel) requires the extension to be enabled first

---

## References

- [Research: Liquibase Changelog Format](../../research/liquibase-changelog-format-research.md) - Comprehensive research on Liquibase patterns
- [Liquibase XML Changelog Format](https://docs.liquibase.com/concepts/changelogs/xml-format.html)
- [Liquibase include](https://docs.liquibase.com/change-types/include.html)
- [Liquibase sqlFile](https://docs.liquibase.com/change-types/sql-file.html)
- [Liquibase Rollback Workflow](https://docs.liquibase.com/workflows/liquibase-community/using-rollback.html)
- [Liquibase Best Practices](https://docs.liquibase.com/concepts/bestpractices.html)
- [Liquibase PostgreSQL Tutorial](https://docs.liquibase.com/start/tutorials/postgresql/postgresql.html)
