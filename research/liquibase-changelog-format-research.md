# Liquibase Changelog and Changeset Format Research

This document covers the Liquibase changelog and changeset format, focusing on XML-based changelogs with SQL file delegation, include directives, rollback strategies, and PostgreSQL-specific considerations.

## Table of Contents

1. [XML Changelog Format](#1-xml-changelog-format)
2. [Include Directives](#2-include-directives)
3. [sqlFile Elements](#3-sqlfile-elements)
4. [Changeset Structure](#4-changeset-structure)
5. [Sequential Versioning](#5-sequential-versioning)
6. [Rollback Strategies](#6-rollback-strategies)
7. [PostgreSQL-Specific Considerations](#7-postgresql-specific-considerations)
8. [Best Practices Summary](#8-best-practices-summary)
9. [References](#9-references)

---

## 1. XML Changelog Format

### Overview

A changelog is the central configuration file that defines all database changes. It can be written in XML, YAML, JSON, or Formatted SQL. XML changelogs provide strong tooling support, IDE validation via XSD schemas, and automatic rollback generation for many change types.

### XML Schema Declaration

The standard XML changelog header with full namespace declarations:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<databaseChangeLog
    xmlns="http://www.liquibase.org/xml/ns/dbchangelog"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xmlns:ext="http://www.liquibase.org/xml/ns/dbchangelog-ext"
    xmlns:pro="http://www.liquibase.org/xml/ns/pro"
    xsi:schemaLocation="http://www.liquibase.org/xml/ns/dbchangelog
        http://www.liquibase.org/xml/ns/dbchangelog/dbchangelog-latest.xsd
        http://www.liquibase.org/xml/ns/dbchangelog-ext
        http://www.liquibase.org/xml/ns/dbchangelog/dbchangelog-ext.xsd
        http://www.liquibase.org/xml/ns/pro
        http://www.liquibase.org/xml/ns/pro/liquibase-pro-latest.xsd">

    <!-- Changesets or includes go here -->

</databaseChangeLog>
```

### Header Component Breakdown

| Namespace | Purpose |
|-----------|---------|
| `xmlns` (default) | Core Liquibase elements (`changeSet`, `include`, etc.) |
| `xmlns:xsi` | XML Schema Instance for validation |
| `xmlns:ext` | Liquibase extension elements |
| `xmlns:pro` | Liquibase Pro features |

### XSD Version Options

- **`dbchangelog-latest.xsd`**: Automatically uses the XSD matching your Liquibase version
- **Specific version**: Use `dbchangelog-4.25.xsd` for explicit version pinning
- The XML parser looks for XSD documents on the classpath first (including in the Liquibase JAR), so no internet access is required

### Minimal Changelog Example

```xml
<?xml version="1.0" encoding="UTF-8"?>
<databaseChangeLog
    xmlns="http://www.liquibase.org/xml/ns/dbchangelog"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xsi:schemaLocation="http://www.liquibase.org/xml/ns/dbchangelog
        http://www.liquibase.org/xml/ns/dbchangelog/dbchangelog-latest.xsd">

    <changeSet id="1" author="developer">
        <createTable tableName="example">
            <column name="id" type="int" autoIncrement="true">
                <constraints primaryKey="true"/>
            </column>
            <column name="name" type="varchar(255)"/>
        </createTable>
    </changeSet>

</databaseChangeLog>
```

---

## 2. Include Directives

### Purpose

The `<include>` and `<includeAll>` tags allow you to break up your root changelog into smaller, more manageable pieces. This is essential for organizing large projects.

### Why Use Include Over XML's Built-in Include

Liquibase needs to uniquely identify each changeset with an `id`, `author`, and `filepath`. The include tag ensures Liquibase maintains this tracking correctly, whereas XML's native include would present all changes as coming from a single large document.

### Include Tag

Use `<include>` to explicitly reference individual changelog files:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<databaseChangeLog
    xmlns="http://www.liquibase.org/xml/ns/dbchangelog"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xsi:schemaLocation="http://www.liquibase.org/xml/ns/dbchangelog
        http://www.liquibase.org/xml/ns/dbchangelog/dbchangelog-latest.xsd">

    <!-- Include individual changelog files -->
    <include file="migrations/001-create-users-table.xml"/>
    <include file="migrations/002-create-orders-table.xml"/>
    <include file="migrations/003-add-email-to-users.xml"/>

    <!-- Include SQL changelogs -->
    <include file="migrations/004-seed-data.sql"/>

</databaseChangeLog>
```

### Include Attributes

| Attribute | Description | Default |
|-----------|-------------|---------|
| `file` | Path to the changelog file (required) | - |
| `relativeToChangelogFile` | If `true`, path is relative to the current changelog file | `false` |
| `context` | Only include if the specified context is active | - |
| `labels` | Only include if the specified labels match | - |
| `ignore` | If `true`, skip this include | `false` |

### Include with Relative Paths

```xml
<include file="tables/users.xml" relativeToChangelogFile="true"/>
<include file="tables/orders.xml" relativeToChangelogFile="true"/>
```

### IncludeAll Tag

Use `<includeAll>` to include all changelog files in a directory:

```xml
<includeAll path="migrations/"
            relativeToChangelogFile="true"/>
```

### IncludeAll Behavior

- Includes all XML, YAML, and JSON files as changelog files
- Includes all SQL files as individual changes
- Recursively includes subdirectories by default
- **Files are processed in alphabetical order** - enforce a naming strategy to control execution order

### IncludeAll Attributes

| Attribute | Description | Default |
|-----------|-------------|---------|
| `path` | Directory path to include (required) | - |
| `relativeToChangelogFile` | Path is relative to current changelog | `false` |
| `resourceFilter` | Class name of filter to apply | - |
| `context` | Only include if context is active | - |
| `labels` | Only include if labels match | - |
| `minDepth` | Minimum directory depth | - |
| `maxDepth` | Maximum directory depth | - |
| `endsWithFilter` | Only include files ending with this suffix | - |

### File Path Best Practices

1. **Always use forward slashes** (`/`) regardless of operating system
2. **Use relative paths** instead of absolute paths for portability
3. **Set `relativeToChangelogFile="true"`** for maintainability

### Warning: Avoid Circular References

Liquibase does not check for looping changelogs. Creating a circular reference will cause an infinite loop:

```xml
<!-- changelog-a.xml includes changelog-b.xml -->
<!-- changelog-b.xml includes changelog-a.xml -->
<!-- This creates an infinite loop! -->
```

---

## 3. sqlFile Elements

### Overview

The `<sqlFile>` change type allows you to execute SQL from external files. This is useful for:

- Complex changes not supported by Liquibase's abstract change types
- Stored procedures and functions
- Database-specific SQL
- Keeping SQL separate from changelog metadata

### Basic sqlFile Usage

```xml
<changeSet id="001" author="developer">
    <sqlFile path="sql/001-create-users-table.sql"
             relativeToChangelogFile="true"/>
</changeSet>
```

### sqlFile Attributes

| Attribute | Description | Default |
|-----------|-------------|---------|
| `path` | Path to the SQL file (required) | - |
| `relativeToChangelogFile` | Path is relative to changelog | `false` |
| `encoding` | Character encoding of the file | Platform default |
| `splitStatements` | Split file into separate statements | `true` |
| `stripComments` | Remove comments before execution | `true` |
| `endDelimiter` | Custom statement delimiter | `;` or `GO` |
| `dbms` | Database types to execute on | All |

### Complete sqlFile Example

```xml
<changeSet id="001-create-users" author="developer">
    <sqlFile path="sql/001-create-users-table.sql"
             relativeToChangelogFile="true"
             encoding="UTF-8"
             splitStatements="true"
             stripComments="false"
             endDelimiter=";"/>
</changeSet>
```

### sqlFile with Rollback

```xml
<changeSet id="001-create-users" author="developer">
    <sqlFile path="sql/001-create-users-table.sql"
             relativeToChangelogFile="true"/>
    <rollback>
        <sqlFile path="sql/001-create-users-table-rollback.sql"
                 relativeToChangelogFile="true"/>
    </rollback>
</changeSet>
```

### Statement Splitting

By default, Liquibase splits statements on `;` or `GO` at the end of lines. For complex SQL (like stored procedures), you may need:

```xml
<sqlFile path="stored-procedure.sql"
         splitStatements="false"/>
```

Or use a custom delimiter:

```xml
<sqlFile path="stored-procedure.sql"
         endDelimiter="\nGO"
         splitStatements="true"/>
```

### Database-Specific Execution

Use the `dbms` attribute to target specific databases:

```xml
<changeSet id="001" author="developer">
    <sqlFile path="sql/postgresql/001-create-extension.sql"
             relativeToChangelogFile="true"
             dbms="postgresql"/>
</changeSet>
```

---

## 4. Changeset Structure

### Required Attributes

Every changeset must have:

| Attribute | Description |
|-----------|-------------|
| `id` | Unique identifier within the changelog file |
| `author` | Creator of the changeset |

The combination of `id`, `author`, and `filepath` creates a globally unique identifier.

### Why Both ID and Author?

It's too easy for multiple developers to create changesets with the same ID, especially when using multiple branches. Source control merges won't detect duplicate IDs. The author attribute reduces collision probability.

### Basic Changeset Structure

```xml
<changeSet id="001-create-users-table" author="developer">
    <!-- Changes go here -->
</changeSet>
```

### Optional Attributes

| Attribute | Description | Default |
|-----------|-------------|---------|
| `dbms` | Target database types | All |
| `context` | Execution context filter | - |
| `labels` | Label-based filtering | - |
| `runOnChange` | Re-run when checksum changes | `false` |
| `runAlways` | Run on every deployment | `false` |
| `failOnError` | Fail migration on error | `true` |
| `runInTransaction` | Wrap in transaction | `true` |
| `logicalFilePath` | Override physical filepath | - |
| `objectQuotingStrategy` | Quote object names | - |

### runOnChange

Re-executes when the changeset content changes:

```xml
<changeSet id="create-view-active-users" author="developer" runOnChange="true">
    <createView viewName="active_users" replaceIfExists="true">
        SELECT * FROM users WHERE active = true
    </createView>
</changeSet>
```

**Use cases**: Views, stored procedures, functions using CREATE OR REPLACE logic.

### runAlways

Executes on every deployment:

```xml
<changeSet id="refresh-materialized-view" author="developer" runAlways="true">
    <sql>REFRESH MATERIALIZED VIEW monthly_stats;</sql>
</changeSet>
```

**Warning**: Modifying a `runAlways` changeset will cause a checksum error.

### failOnError

Continue migration even if this changeset fails:

```xml
<changeSet id="optional-index" author="developer" failOnError="false">
    <createIndex indexName="idx_users_email" tableName="users">
        <column name="email"/>
    </createIndex>
</changeSet>
```

**Best practice**: Use `dbms`, `context`, or `labels` instead of `failOnError` when possible.

### Context-Based Execution

```xml
<changeSet id="001-seed-test-data" author="developer" context="test">
    <insert tableName="users">
        <column name="name" value="Test User"/>
    </insert>
</changeSet>
```

Run with: `liquibase --contexts=test update`

### Changeset Tracking

After execution, Liquibase inserts a row into the `DATABASECHANGELOG` table with:

- `id`
- `author`
- `filename` (filepath)
- `dateexecuted`
- `md5sum` (checksum)
- `description`
- `tag`
- `liquibase` (version)
- `deployment_id`

---

## 5. Sequential Versioning

### Naming Conventions

Since `<includeAll>` processes files alphabetically, use consistent naming conventions:

#### Sequential Numbering Approach

```
migrations/
  001-create-users-table.xml
  002-create-orders-table.xml
  003-add-email-to-users.xml
  004-create-products-table.xml
```

#### Date-Based Approach

```
migrations/
  2024-01-15-001-create-users-table.xml
  2024-01-15-002-create-orders-table.xml
  2024-01-20-001-add-email-to-users.xml
```

#### Version-Based Approach

```
migrations/
  v1.0.0/
    001-create-users-table.xml
    002-create-orders-table.xml
  v1.1.0/
    001-add-email-to-users.xml
```

### Changeset ID Conventions

| Convention | Example | Use Case |
|------------|---------|----------|
| Sequential | `001`, `002`, `003` | Simple projects |
| Descriptive | `add_users_table_001` | Better documentation |
| Ticket-based | `JIRA-123-create-users` | Issue tracking |
| UUID | `a1b2c3d4-...` | Guaranteed uniqueness |

### Recommended ID Format

```xml
<!-- Descriptive with sequence -->
<changeSet id="001-create-users-table" author="developer">

<!-- Ticket-based -->
<changeSet id="PROJ-42-create-users-table" author="developer">

<!-- Date and author based -->
<changeSet id="20240115-jsmith-create-users" author="jsmith">
```

### File Organization Strategies

#### By Object Type

```
changelogs/
  tables/
    users.xml
    orders.xml
  views/
    active_users.xml
  procedures/
    calculate_totals.xml
```

#### By Release

```
changelogs/
  release-1.0/
    001-initial-schema.xml
  release-1.1/
    001-add-email.xml
    002-add-indexes.xml
```

#### Hybrid Approach

```
db/
  changelog.xml              # Root changelog
  migrations/
    001-initial-schema/
      001-create-users.xml
      001-create-users.sql
      001-create-users-rollback.sql
    002-add-orders/
      002-create-orders.xml
      002-create-orders.sql
      002-create-orders-rollback.sql
```

---

## 6. Rollback Strategies

### Automatic Rollbacks

Many Liquibase change types generate automatic rollback commands:

| Change Type | Automatic Rollback |
|-------------|-------------------|
| `createTable` | `dropTable` |
| `addColumn` | `dropColumn` |
| `createIndex` | `dropIndex` |
| `createView` | `dropView` |
| `addForeignKeyConstraint` | `dropForeignKeyConstraint` |

### Changes Without Automatic Rollback

These require explicit rollback statements:

- `dropTable`
- `dropColumn`
- `insert`
- `update`
- `delete`
- `sql` / `sqlFile`
- Custom change types

### Explicit Rollback with SQL

```xml
<changeSet id="001-create-users" author="developer">
    <createTable tableName="users">
        <column name="id" type="int" autoIncrement="true">
            <constraints primaryKey="true"/>
        </column>
        <column name="name" type="varchar(255)"/>
    </createTable>
    <rollback>
        DROP TABLE users;
    </rollback>
</changeSet>
```

### Rollback with sqlFile

```xml
<changeSet id="001-create-users" author="developer">
    <sqlFile path="sql/001-create-users.sql"
             relativeToChangelogFile="true"/>
    <rollback>
        <sqlFile path="sql/001-create-users-rollback.sql"
                 relativeToChangelogFile="true"/>
    </rollback>
</changeSet>
```

### Recommended File Structure for sqlFile + Rollback

```
migrations/
  001-create-users-table/
    changelog.xml
    up.sql
    down.sql
```

**changelog.xml:**
```xml
<?xml version="1.0" encoding="UTF-8"?>
<databaseChangeLog
    xmlns="http://www.liquibase.org/xml/ns/dbchangelog"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xsi:schemaLocation="http://www.liquibase.org/xml/ns/dbchangelog
        http://www.liquibase.org/xml/ns/dbchangelog/dbchangelog-latest.xsd">

    <changeSet id="001-create-users-table" author="developer">
        <sqlFile path="up.sql" relativeToChangelogFile="true"/>
        <rollback>
            <sqlFile path="down.sql" relativeToChangelogFile="true"/>
        </rollback>
    </changeSet>

</databaseChangeLog>
```

**up.sql:**
```sql
CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    email VARCHAR(255) UNIQUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_users_email ON users(email);
```

**down.sql:**
```sql
DROP INDEX IF EXISTS idx_users_email;
DROP TABLE IF EXISTS users;
```

### Multiple Rollback Statements

```xml
<changeSet id="001" author="developer">
    <createTable tableName="table1">...</createTable>
    <createTable tableName="table2">...</createTable>
    <rollback>
        DROP TABLE table2;
        DROP TABLE table1;
    </rollback>
</changeSet>
```

### Empty Rollback (Intentionally No Rollback)

```xml
<changeSet id="001-seed-data" author="developer">
    <insert tableName="config">
        <column name="key" value="app.version"/>
        <column name="value" value="1.0"/>
    </insert>
    <rollback/>  <!-- Explicitly empty -->
</changeSet>
```

### Rollback to Previous Changeset

Reference an earlier changeset's state:

```xml
<changeSet id="002-modify-users" author="developer">
    <addColumn tableName="users">
        <column name="middle_name" type="varchar(100)"/>
    </addColumn>
    <rollback changeSetId="001-create-users" changeSetAuthor="developer"/>
</changeSet>
```

### Running Rollbacks

```bash
# Rollback last N changesets
liquibase rollback-count 1

# Rollback to a tag
liquibase rollback --tag=v1.0

# Rollback to a date
liquibase rollback-to-date 2024-01-01

# Preview rollback SQL
liquibase rollback-sql --tag=v1.0
```

---

## 7. PostgreSQL-Specific Considerations

### Schema Handling

Liquibase places tracking tables and applies changes to the database's default schema. Override with:

```properties
# liquibase.properties
liquibase.liquibaseSchemaName=liquibase
liquibase.defaultSchemaName=myapp
```

Or in the changelog:

```xml
<changeSet id="001" author="developer">
    <sql>SET search_path TO myapp, public;</sql>
</changeSet>
```

### PostgreSQL Extensions

Extensions like `uuid-ossp`, `pgcrypto`, or `PostGIS` require special handling:

```xml
<changeSet id="000-enable-extensions" author="developer" dbms="postgresql">
    <sql>
        CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
        CREATE EXTENSION IF NOT EXISTS "pgcrypto";
    </sql>
    <rollback>
        DROP EXTENSION IF EXISTS "pgcrypto";
        DROP EXTENSION IF EXISTS "uuid-ossp";
    </rollback>
</changeSet>
```

### UUID Support

```xml
<changeSet id="001-create-users-with-uuid" author="developer" dbms="postgresql">
    <preConditions onFail="MARK_RAN">
        <sqlCheck expectedResult="1">
            SELECT COUNT(*) FROM pg_extension WHERE extname = 'uuid-ossp'
        </sqlCheck>
    </preConditions>
    <createTable tableName="users">
        <column name="id" type="uuid" defaultValueComputed="uuid_generate_v4()">
            <constraints primaryKey="true"/>
        </column>
        <column name="name" type="varchar(255)"/>
    </createTable>
</changeSet>
```

### Search Path Issues

Known issue: Liquibase may set `search_path` to only the default schema, causing problems with extensions in the public schema. Solutions:

1. Fully qualify extension functions: `public.uuid_generate_v4()`
2. Set search_path explicitly in the session
3. Install extensions in the application schema

### PostgreSQL-Specific Data Types

```xml
<changeSet id="001" author="developer" dbms="postgresql">
    <createTable tableName="events">
        <column name="id" type="serial">
            <constraints primaryKey="true"/>
        </column>
        <column name="data" type="jsonb"/>
        <column name="tags" type="text[]"/>
        <column name="location" type="point"/>
        <column name="metadata" type="hstore"/>
        <column name="ip_address" type="inet"/>
        <column name="time_range" type="tstzrange"/>
    </createTable>
</changeSet>
```

### Sequences

```xml
<changeSet id="001-create-sequence" author="developer" dbms="postgresql">
    <createSequence sequenceName="order_number_seq"
                    startValue="1000"
                    incrementBy="1"/>
</changeSet>
```

### Partial Indexes (PostgreSQL-specific)

Use raw SQL for PostgreSQL-specific features:

```xml
<changeSet id="001-partial-index" author="developer" dbms="postgresql">
    <sql>
        CREATE INDEX idx_active_users ON users(email) WHERE active = true;
    </sql>
    <rollback>
        DROP INDEX IF EXISTS idx_active_users;
    </rollback>
</changeSet>
```

### VACUUM Operations

VACUUM cannot run in a transaction:

```xml
<changeSet id="vacuum-users" author="developer"
           dbms="postgresql"
           runInTransaction="false">
    <sql>VACUUM ANALYZE users;</sql>
    <rollback/>
</changeSet>
```

### Liquibase PostgreSQL Extension

For additional PostgreSQL-specific change types, add the extension:

**Maven:**
```xml
<dependency>
    <groupId>org.liquibase.ext</groupId>
    <artifactId>liquibase-postgresql</artifactId>
    <version>${liquibase.version}</version>
</dependency>
```

**Extension XSD:**
```xml
xmlns:ext="http://www.liquibase.org/xml/ns/dbchangelog-ext"
xsi:schemaLocation="...
    http://www.liquibase.org/xml/ns/dbchangelog-ext
    http://www.liquibase.org/xml/ns/dbchangelog/dbchangelog-ext.xsd"
```

---

## 8. Best Practices Summary

### Changeset Best Practices

1. **One change per changeset**: Avoids partial failures with auto-commit databases
2. **Never modify deployed changesets**: Add new changesets for modifications
3. **Use descriptive IDs**: `001-create-users-table` over `1`
4. **Always include rollback**: Even if it's just `<rollback/>`
5. **Use `dbms` for database-specific changes**: Better than `failOnError="false"`
6. **Make changes idempotent when possible**: Use `IF NOT EXISTS`, preconditions

### File Organization

1. **Root changelog as configuration only**: Use includes, not inline changesets
2. **Consistent naming convention**: Alphabetical ordering matters with `includeAll`
3. **Use forward slashes**: `/` works on all platforms
4. **Relative paths with `relativeToChangelogFile="true"`**: Improves portability

### Rollback Best Practices

1. **Write explicit rollbacks for all SQL changes**: No automatic rollback for raw SQL
2. **Test rollbacks**: Run `rollback-sql` to preview
3. **Keep rollback files alongside migration files**: Easy to maintain
4. **Rollback should be the inverse of the change**: CREATE -> DROP, INSERT -> DELETE

### PostgreSQL-Specific

1. **Enable extensions first**: In a dedicated initial changeset
2. **Use `dbms="postgresql"`**: For database-specific features
3. **Handle search_path**: Either fully qualify or set explicitly
4. **Use `runInTransaction="false"`**: For operations like VACUUM

### Version Control

1. **Never delete changesets**: Mark as run or use preconditions
2. **Tag releases**: `liquibase tag v1.0`
3. **Review changesets in PRs**: Check for rollbacks, idempotency
4. **Test migrations**: Include migration tests in CI/CD

---

## 9. References

### Official Documentation

- [XML Changelog Format](https://docs.liquibase.com/concepts/changelogs/xml-format.html)
- [What is a Changelog?](https://docs.liquibase.com/concepts/changelogs/home.html)
- [What is a Changeset?](https://docs.liquibase.com/concepts/changelogs/changeset.html)
- [include](https://docs.liquibase.com/change-types/include.html)
- [includeAll](https://docs.liquibase.com/change-types/includeall.html)
- [sqlFile](https://docs.liquibase.com/change-types/sql-file.html)
- [Rollback Workflow](https://docs.liquibase.com/workflows/liquibase-community/using-rollback.html)
- [Best Practices](https://docs.liquibase.com/concepts/bestpractices.html)
- [Connect Liquibase with PostgreSQL](https://docs.liquibase.com/start/tutorials/postgresql/postgresql.html)
- [How to specify schema names for Postgres](https://support.liquibase.com/hc/en-us/articles/29383086091547-How-to-specify-schema-names-for-Postgres)

### Changelog Attributes

- [runOnChange](https://docs.liquibase.com/concepts/changelogs/attributes/runonchange.html)
- [runAlways](https://docs.liquibase.com/concepts/changelogs/attributes/run-always.html)
- [failOnError](https://docs.liquibase.com/concepts/changelogs/attributes/fail-on-error.html)
- [Contexts](https://docs.liquibase.com/concepts/changelogs/attributes/contexts.html)

### Extensions

- [Liquibase PostgreSQL Extension (GitHub)](https://github.com/liquibase/liquibase-postgresql)

### Community Resources

- [Using XML Changelogs in Liquibase](https://www.liquibase.com/blog/using-xml-changelogs-liquibase)
- [Postgres Schema Migration](https://www.liquibase.com/blog/postgres-schema-migration)
- [Idempotent Liquibase Change Sets](https://imhoratiu.wordpress.com/2023/05/30/idempotent-liquibase-change-sets/)
- [Liquibase Changeset Tips](https://matteoagius.medium.com/follow-these-liquibase-changeset-tips-or-suffer-the-possibility-of-a-partially-migrated-aurora-db6811cf5d7e)

---

## Appendix: Complete Example Structure

### Directory Layout

```
db/
  changelog.xml                    # Root changelog (entry point)
  migrations/
    000-extensions/
      changelog.xml
      enable-extensions.sql
    001-create-users/
      changelog.xml
      up.sql
      down.sql
    002-create-orders/
      changelog.xml
      up.sql
      down.sql
```

### Root Changelog (db/changelog.xml)

```xml
<?xml version="1.0" encoding="UTF-8"?>
<databaseChangeLog
    xmlns="http://www.liquibase.org/xml/ns/dbchangelog"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xsi:schemaLocation="http://www.liquibase.org/xml/ns/dbchangelog
        http://www.liquibase.org/xml/ns/dbchangelog/dbchangelog-latest.xsd">

    <include file="migrations/000-extensions/changelog.xml" relativeToChangelogFile="true"/>
    <include file="migrations/001-create-users/changelog.xml" relativeToChangelogFile="true"/>
    <include file="migrations/002-create-orders/changelog.xml" relativeToChangelogFile="true"/>

</databaseChangeLog>
```

### Migration Changelog (db/migrations/001-create-users/changelog.xml)

```xml
<?xml version="1.0" encoding="UTF-8"?>
<databaseChangeLog
    xmlns="http://www.liquibase.org/xml/ns/dbchangelog"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xsi:schemaLocation="http://www.liquibase.org/xml/ns/dbchangelog
        http://www.liquibase.org/xml/ns/dbchangelog/dbchangelog-latest.xsd">

    <changeSet id="001-create-users-table" author="developer">
        <sqlFile path="up.sql" relativeToChangelogFile="true"/>
        <rollback>
            <sqlFile path="down.sql" relativeToChangelogFile="true"/>
        </rollback>
    </changeSet>

</databaseChangeLog>
```

### Up Migration (db/migrations/001-create-users/up.sql)

```sql
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    email VARCHAR(255) NOT NULL UNIQUE,
    name VARCHAR(255) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_users_active ON users(active) WHERE active = true;

COMMENT ON TABLE users IS 'Application users';
COMMENT ON COLUMN users.password_hash IS 'BCrypt hashed password';
```

### Down Migration (db/migrations/001-create-users/down.sql)

```sql
DROP INDEX IF EXISTS idx_users_active;
DROP INDEX IF EXISTS idx_users_email;
DROP TABLE IF EXISTS users;
```
