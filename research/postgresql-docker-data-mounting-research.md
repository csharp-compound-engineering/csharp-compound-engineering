# PostgreSQL Docker Data Mounting Research

This research document covers the official PostgreSQL Docker image configuration, data persistence through volume mounting, and best practices for running PostgreSQL in containers with local data persistence.

---

## Table of Contents

1. [Official PostgreSQL Docker Image](#1-official-postgresql-docker-image)
2. [Data Directory and Persistence](#2-data-directory-and-persistence)
3. [Volume Mounting Options](#3-volume-mounting-options)
4. [Bind Mount Configuration](#4-bind-mount-configuration)
5. [Named Volumes](#5-named-volumes-alternative)
6. [Environment Variables](#6-environment-variables)
7. [Initialization Scripts](#7-initialization-scripts)
8. [Docker Compose Configuration](#8-docker-compose-configuration)
9. [Data Directory Structure](#9-data-directory-structure)
10. [Backup and Restore](#10-backup-and-restore)
11. [Common Issues and Solutions](#11-common-issues-and-solutions)
12. [Complete Examples](#12-complete-examples)
13. [Best Practices](#13-best-practices)

---

## 1. Official PostgreSQL Docker Image

### Image Names and Tags

The official PostgreSQL Docker image is available at [Docker Hub](https://hub.docker.com/_/postgres).

**Current Available Tags (as of 2025):**

| Version | Tags |
|---------|------|
| PostgreSQL 18 | `18.1`, `18`, `latest`, `18-bookworm`, `18-alpine`, `18-trixie` |
| PostgreSQL 17 | `17.7`, `17`, `17-bookworm`, `17-alpine`, `17-trixie` |
| PostgreSQL 16 | `16.11`, `16`, `16-bookworm`, `16-alpine`, `16-trixie` |
| PostgreSQL 15 | `15.15`, `15`, `15-bookworm`, `15-alpine`, `15-trixie` |

### Image Variants

| Variant | Base OS | Size | Best For |
|---------|---------|------|----------|
| **Default/Bookworm** | Debian Bookworm | ~438MB | General use, stability, full tooling |
| **Alpine** | Alpine Linux | ~278MB | Minimal image size (36% smaller) |
| **Trixie** | Debian Trixie | ~Similar to Bookworm | Newer Debian features |

**Alpine Considerations:**
- Uses musl libc instead of glibc
- Software may have issues depending on libc requirements
- PostgreSQL is built from source for Alpine
- Fewer additional tools included (no git, bash by default)
- May have more limited architecture support

**Debian Considerations:**
- More stable and widely tested
- Better compatibility with most software
- Larger image size but more tooling included

### PostgreSQL vs pgvector Image

| Feature | `postgres` | `pgvector/pgvector` |
|---------|------------|---------------------|
| Base | Official PostgreSQL | Extends official postgres |
| pgvector | Not included | Pre-installed |
| Setup | Manual extension install | Ready to use |
| Tags | `postgres:17`, etc. | `pgvector/pgvector:pg17` |

**Recommendation for RAG applications:** Use `pgvector/pgvector:pg17` (or your preferred version) as it includes the vector extension pre-compiled and ready to use.

### Supported Architectures

- amd64, arm64v8, arm32v5, arm32v6, arm32v7
- i386, mips64le, ppc64le, riscv64, s390x

---

## 2. Data Directory and Persistence

### Default PGDATA Location

**PostgreSQL 17 and earlier:**
```
/var/lib/postgresql/data
```

**PostgreSQL 18 and later (CRITICAL CHANGE):**
```
/var/lib/postgresql/18/docker
```
For future versions, replace `18` with the major version number (e.g., `/var/lib/postgresql/19/docker`).

### Why This Changed in PostgreSQL 18

The new directory structure allows users upgrading between PostgreSQL major releases to use the faster `--link` option when running `pg_upgrade`. By mounting the parent directory `/var/lib/postgresql`, future images can create sibling folders for new versions, enabling nearly instantaneous migrations via hard-linking.

### Why Persistence is Critical

Without volume mounting:
1. Data is stored in an anonymous volume inside the container
2. When the container is removed, all data is **permanently lost**
3. Each `docker-compose down` followed by `up` creates a fresh database
4. No backup or recovery is possible

With proper volume mounting:
1. Data survives container restarts and removals
2. Data can be backed up from the host
3. Upgrades can preserve existing data
4. Multiple containers can share the same data (with caution)

---

## 3. Volume Mounting Options

### Named Volumes vs Bind Mounts Comparison

| Feature | Named Volumes | Bind Mounts |
|---------|---------------|-------------|
| **Management** | Docker-managed | User-managed |
| **Location** | Docker's volume directory | Any host path |
| **Permissions** | Docker handles automatically | Manual configuration |
| **Portability** | Works across Linux/Mac/Windows | Path-dependent |
| **Backup** | Via docker commands | Direct file access |
| **Best For** | Production databases | Development, full control |

### Syntax for Both Options

**Named Volume (Docker CLI):**
```bash
docker run -v pgdata:/var/lib/postgresql/data postgres:17
```

**Bind Mount (Docker CLI):**
```bash
docker run -v /host/path/to/data:/var/lib/postgresql/data postgres:17
```

**Named Volume (Docker Compose):**
```yaml
services:
  db:
    image: postgres:17
    volumes:
      - pgdata:/var/lib/postgresql/data

volumes:
  pgdata:
```

**Bind Mount (Docker Compose):**
```yaml
services:
  db:
    image: postgres:17
    volumes:
      - ./postgres-data:/var/lib/postgresql/data
```

---

## 4. Bind Mount Configuration

### Creating Host Directory

Before starting the container with a bind mount:

```bash
# Create the directory
mkdir -p /path/to/postgres-data

# Set correct ownership (postgres user UID is typically 999)
sudo chown -R 999:999 /path/to/postgres-data
```

### Permissions and Ownership

The PostgreSQL container runs as the `postgres` user, which typically has:
- UID: 999
- GID: 999

**Common Permission Issues:**
1. Host directory owned by different user
2. GID 999 conflicts with existing host group
3. SELinux blocking access (Linux)

**Solutions:**

```bash
# Option 1: Change host directory ownership
sudo chown -R 999:999 /path/to/postgres-data

# Option 2: Run container with specific user
docker run --user 999:999 -v /path/to/postgres-data:/var/lib/postgresql/data postgres:17

# Option 3: Docker Compose with user specification
services:
  db:
    image: postgres:17
    user: "999:999"
    volumes:
      - ./postgres-data:/var/lib/postgresql/data
```

### Platform-Specific Considerations

**macOS:**
- Docker Desktop handles permissions through its VM layer
- Bind mounts generally work without special configuration
- Performance may be slower than named volumes

**Linux:**
- Direct filesystem access, so permissions are critical
- May need `:Z` suffix for SELinux: `-v /path:/container/path:Z`
- UID/GID matching is essential

**Windows (WSL2):**
- Use WSL2 paths for best compatibility
- Windows paths may cause ownership errors
- Prefer named volumes on Windows for simplicity

---

## 5. Named Volumes (Alternative)

### Creating and Managing Named Volumes

```bash
# Create a named volume
docker volume create pgdata

# List volumes
docker volume ls

# Inspect volume details
docker volume inspect pgdata

# Remove a volume (WARNING: destroys data)
docker volume rm pgdata
```

### Where Named Volumes Are Stored

**Linux:**
```
/var/lib/docker/volumes/pgdata/_data
```

**macOS (Docker Desktop):**
```
Inside the Docker Desktop VM, not directly accessible from host
```

**Windows (Docker Desktop):**
```
Inside the WSL2 VM, accessible via \\wsl$\docker-desktop-data\...
```

### Backing Up Named Volumes

```bash
# Create a backup container that mounts the volume
docker run --rm \
  -v pgdata:/source:ro \
  -v $(pwd):/backup \
  alpine tar czf /backup/pgdata-backup.tar.gz -C /source .

# Restore from backup
docker run --rm \
  -v pgdata:/target \
  -v $(pwd):/backup \
  alpine sh -c "cd /target && tar xzf /backup/pgdata-backup.tar.gz"
```

### Pros and Cons

**Pros:**
- Managed by Docker (easier lifecycle)
- Better performance on macOS/Windows
- Automatic permission handling
- Works consistently across platforms

**Cons:**
- Less direct access to files
- Backup requires additional steps
- Hidden location on macOS/Windows
- Harder to migrate between Docker installations

---

## 6. Environment Variables

### Required Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `POSTGRES_PASSWORD` | **Yes** | Superuser password (must not be empty) |

### Optional Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `POSTGRES_USER` | `postgres` | Superuser username |
| `POSTGRES_DB` | `$POSTGRES_USER` | Default database name |
| `PGDATA` | `/var/lib/postgresql/data` (17-) or `/var/lib/postgresql/18/docker` (18+) | Data directory location |
| `POSTGRES_INITDB_ARGS` | (none) | Arguments for `initdb` |
| `POSTGRES_HOST_AUTH_METHOD` | (none) | Set to `trust` to allow passwordless local connections |

### Using Docker Secrets (_FILE Suffix)

For production environments, use secrets instead of plain environment variables:

```yaml
services:
  db:
    image: postgres:17
    environment:
      POSTGRES_PASSWORD_FILE: /run/secrets/db_password
    secrets:
      - db_password

secrets:
  db_password:
    file: ./secrets/db_password.txt
```

Supported `_FILE` suffix variables:
- `POSTGRES_PASSWORD_FILE`
- `POSTGRES_USER_FILE`
- `POSTGRES_DB_FILE`
- `POSTGRES_INITDB_ARGS_FILE`

### Custom PGDATA for Older Versions

To opt-in to the PostgreSQL 18+ directory structure on older versions:

```bash
docker run \
  --env PGDATA=/var/lib/postgresql/17/docker \
  --volume pgdata:/var/lib/postgresql \
  postgres:17
```

---

## 7. Initialization Scripts

### The `/docker-entrypoint-initdb.d/` Directory

This special directory allows you to run scripts when the database is first initialized.

**Supported File Types:**
- `.sql` - Executed directly by PostgreSQL
- `.sql.gz` - Decompressed and executed
- `.sh` - Executed as shell scripts

**Execution Order:** Alphabetical by filename

**Execution Timing:** Only runs on first startup when data directory is empty

### Example: Enabling pgvector Extension

Create `init-pgvector.sql`:
```sql
-- Enable pgvector extension
CREATE EXTENSION IF NOT EXISTS vector;

-- Create a sample table with vector column
CREATE TABLE IF NOT EXISTS documents (
    id SERIAL PRIMARY KEY,
    content TEXT,
    embedding vector(1536)
);

-- Create an index for similarity search
CREATE INDEX ON documents USING hnsw (embedding vector_l2_ops);
```

### Mounting Init Scripts

**Docker CLI:**
```bash
docker run \
  -v ./init-scripts:/docker-entrypoint-initdb.d \
  -v pgdata:/var/lib/postgresql/data \
  postgres:17
```

**Docker Compose:**
```yaml
services:
  db:
    image: pgvector/pgvector:pg17
    volumes:
      - ./init-scripts:/docker-entrypoint-initdb.d
      - pgdata:/var/lib/postgresql/data
```

### Shell Script Example

Create `01-setup.sh`:
```bash
#!/bin/bash
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE USER app_user WITH PASSWORD 'app_password';
    CREATE DATABASE app_db OWNER app_user;
    GRANT ALL PRIVILEGES ON DATABASE app_db TO app_user;
EOSQL
```

**Important:** Use `--username "$POSTGRES_USER"` to connect without password via Unix socket.

---

## 8. Docker Compose Configuration

### Complete Production-Ready Configuration

```yaml
version: '3.8'

services:
  db:
    image: pgvector/pgvector:pg17
    container_name: postgres-db
    restart: unless-stopped

    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: ${DB_PASSWORD:-changeme}
      POSTGRES_DB: ragdb
      # For PostgreSQL 18+, uncomment:
      # PGDATA: /var/lib/postgresql/18/docker

    volumes:
      # Data persistence (PostgreSQL 17 and earlier)
      - pgdata:/var/lib/postgresql/data
      # For PostgreSQL 18+, use:
      # - pgdata:/var/lib/postgresql

      # Initialization scripts
      - ./init-scripts:/docker-entrypoint-initdb.d:ro

      # Custom configuration (optional)
      # - ./postgresql.conf:/etc/postgresql/postgresql.conf:ro

    ports:
      - "5432:5432"

    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres -d ragdb"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s

    # Resource limits (optional)
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 2G
        reservations:
          cpus: '0.5'
          memory: 512M

    # Logging configuration
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"

volumes:
  pgdata:
    name: postgres-ragdb-data
```

### Health Check Options

**Basic (connection ready):**
```yaml
healthcheck:
  test: ["CMD-SHELL", "pg_isready -U postgres"]
  interval: 10s
  timeout: 5s
  retries: 5
```

**Robust (query execution):**
```yaml
healthcheck:
  test: ["CMD-SHELL", "pg_isready -U postgres && psql -U postgres -c 'SELECT 1'"]
  interval: 10s
  timeout: 5s
  retries: 5
  start_period: 30s
```

### Restart Policies

| Policy | Description |
|--------|-------------|
| `no` | Never restart (default) |
| `always` | Always restart until removed |
| `on-failure[:max]` | Restart on error exit codes |
| `unless-stopped` | Restart unless explicitly stopped |

**Recommendation:** Use `unless-stopped` for production databases.

### Service Dependencies

```yaml
services:
  api:
    build: .
    depends_on:
      db:
        condition: service_healthy
    restart: always

  db:
    image: pgvector/pgvector:pg17
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 5s
      timeout: 5s
      retries: 5
```

---

## 9. Data Directory Structure

### PGDATA Contents Overview

```
/var/lib/postgresql/data/
├── base/                    # Database files (one subdir per database OID)
├── global/                  # Cluster-wide tables
├── pg_commit_ts/           # Commit timestamp data
├── pg_dynshmem/            # Dynamic shared memory
├── pg_logical/             # Logical replication data
├── pg_multixact/           # Multi-transaction status
├── pg_notify/              # LISTEN/NOTIFY status
├── pg_replslot/            # Replication slot data
├── pg_serial/              # Serializable transaction info
├── pg_snapshots/           # Exported snapshots
├── pg_stat/                # Statistics subsystem files
├── pg_stat_tmp/            # Temporary statistics files
├── pg_subtrans/            # Subtransaction status
├── pg_tblspc/              # Symbolic links to tablespaces
├── pg_twophase/            # Two-phase commit files
├── pg_wal/                 # Write-Ahead Log (WAL) files
├── pg_xact/                # Transaction commit status
├── postgresql.conf         # Main configuration file
├── pg_hba.conf            # Client authentication config
├── pg_ident.conf          # User name mapping
├── PG_VERSION             # PostgreSQL major version
├── postmaster.opts        # Last startup options
└── postmaster.pid         # Lock file with PID
```

### Key Configuration Files

**postgresql.conf:**
- Main server configuration
- Memory settings, connections, logging
- Performance tuning parameters

**pg_hba.conf:**
- Client authentication rules
- Connection type, IP ranges, auth methods
- Controls who can connect and how

**pg_ident.conf:**
- Maps OS usernames to database usernames
- Used with ident/peer authentication

### WAL Files (pg_wal/)

- Write-Ahead Logs for crash recovery
- Each file is typically 16MB
- Automatically recycled after checkpoint
- Critical for point-in-time recovery

### Database Files (base/)

- Each database has its own subdirectory
- Named by database OID
- Tables/indexes split into 1GB segments
- Files named by filenode number

---

## 10. Backup and Restore

### Backup Methods

**Basic pg_dump:**
```bash
docker exec -t postgres-db pg_dump -U postgres mydb > backup.sql
```

**Compressed backup:**
```bash
docker exec -t postgres-db pg_dump -U postgres mydb | gzip -9 > backup.sql.gz
```

**All databases:**
```bash
docker exec -t postgres-db pg_dumpall -U postgres > all_databases.sql
```

**Custom format (recommended for large databases):**
```bash
docker exec -t postgres-db pg_dump -U postgres -Fc mydb > backup.dump
```

**Tar format:**
```bash
docker exec -t postgres-db pg_dump -U postgres -Ft mydb > backup.tar
```

### Restore Methods

**Basic restore:**
```bash
docker exec -i postgres-db psql -U postgres mydb < backup.sql
```

**Compressed restore:**
```bash
gunzip -c backup.sql.gz | docker exec -i postgres-db psql -U postgres mydb
```

**Custom/tar format restore:**
```bash
docker exec -i postgres-db pg_restore -U postgres -d mydb --clean --verbose < backup.dump
```

**With password (when needed):**
```bash
docker exec -i postgres-db bash -c "PGPASSWORD=mypass psql -U postgres mydb" < backup.sql
```

### Automated Backup with Cron

**Host cron job:**
```bash
# Add to crontab: crontab -e
0 2 * * * docker exec postgres-db pg_dump -U postgres mydb | gzip > /backups/mydb-$(date +\%F).sql.gz
```

**Docker-based backup container:**
```yaml
services:
  backup:
    image: postgres:17
    volumes:
      - ./backups:/backups
    environment:
      PGHOST: db
      PGUSER: postgres
      PGPASSWORD: ${DB_PASSWORD}
    entrypoint: /bin/sh -c
    command: |
      while true; do
        pg_dump mydb | gzip > /backups/mydb-$$(date +%F-%H%M).sql.gz
        sleep 86400
      done
    depends_on:
      - db
```

### Backup Best Practices

1. **Version matching:** Use the same PostgreSQL version for backup and restore
2. **Test restores:** Regularly test backup restoration on a separate instance
3. **Stop on error:** Use `--set ON_ERROR_STOP=on` during restore
4. **Restore to empty database:** Create a fresh database for restoration
5. **Container consistency:** Stop writes during backup for consistency

---

## 11. Common Issues and Solutions

### "Data directory has wrong ownership"

**Error:**
```
FATAL: data directory "/var/lib/postgresql/data" has wrong ownership
HINT: The server must be started by the user that owns the data directory.
```

**Solutions:**
```bash
# Solution 1: Fix ownership on host
sudo chown -R 999:999 /path/to/data

# Solution 2: Run with correct user in docker-compose
services:
  db:
    image: postgres:17
    user: "999:999"

# Solution 3: Use Docker-managed volume (avoids permission issues)
volumes:
  - pgdata:/var/lib/postgresql/data
```

### Permission Denied Errors

**Error:**
```
chmod: changing permissions of '/var/lib/postgresql/data': Permission denied
```

**Causes:**
- Host GID 999 conflicts with existing group
- NFS volume without proper permissions
- SELinux blocking access

**Solutions:**
```bash
# Check for GID conflicts
getent group 999

# Use different GID
docker run --user 1000:1000 ...

# SELinux fix (Linux)
docker run -v /path:/container/path:Z ...
```

### PostgreSQL 18+ Data Directory Issues

**Error:**
```
PostgreSQL won't start if a directory /var/lib/postgresql/data exists
```

**Solutions:**
```bash
# Option 1: Pin to PostgreSQL 17
image: postgres:17

# Option 2: Set PGDATA explicitly
environment:
  PGDATA: /var/lib/postgresql/18/docker
volumes:
  - pgdata:/var/lib/postgresql

# Option 3: Migrate data
# Move files to 18/docker subdirectory
```

### Initialization Scripts Not Running

**Possible causes:**
1. Data directory not empty (scripts only run on fresh init)
2. Scripts not executable (for .sh files)
3. Scripts have syntax errors

**Solutions:**
```bash
# Remove existing data to force re-initialization
docker volume rm pgdata

# Make shell scripts executable
chmod +x init-scripts/*.sh

# Check script syntax before mounting
bash -n init-scripts/setup.sh
```

### Container Won't Start After Host Restart

**Common causes:**
1. Volume mount path changed
2. Permissions changed
3. postmaster.pid lock file stale

**Solutions:**
```bash
# Remove stale PID file
sudo rm /path/to/data/postmaster.pid

# Check volume mount
docker inspect container_name | grep Mounts

# Verify permissions
ls -la /path/to/data
```

---

## 12. Complete Examples

### Docker Run with Bind Mount

```bash
# Create directory with correct permissions
mkdir -p ./postgres-data
sudo chown -R 999:999 ./postgres-data

# Run container
docker run -d \
  --name postgres-db \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=secretpassword \
  -e POSTGRES_DB=myapp \
  -v $(pwd)/postgres-data:/var/lib/postgresql/data \
  -v $(pwd)/init-scripts:/docker-entrypoint-initdb.d:ro \
  -p 5432:5432 \
  --restart unless-stopped \
  postgres:17
```

### Docker Run with Named Volume

```bash
# Create named volume
docker volume create pgdata

# Run container
docker run -d \
  --name postgres-db \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=secretpassword \
  -e POSTGRES_DB=myapp \
  -v pgdata:/var/lib/postgresql/data \
  -p 5432:5432 \
  --restart unless-stopped \
  postgres:17
```

### Docker Compose with Bind Mount

```yaml
version: '3.8'

services:
  db:
    image: postgres:17
    container_name: postgres-db
    restart: unless-stopped
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: secretpassword
      POSTGRES_DB: myapp
    volumes:
      - ./postgres-data:/var/lib/postgresql/data
      - ./init-scripts:/docker-entrypoint-initdb.d:ro
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5
```

### Docker Compose with Named Volume

```yaml
version: '3.8'

services:
  db:
    image: postgres:17
    container_name: postgres-db
    restart: unless-stopped
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: secretpassword
      POSTGRES_DB: myapp
    volumes:
      - pgdata:/var/lib/postgresql/data
      - ./init-scripts:/docker-entrypoint-initdb.d:ro
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  pgdata:
```

### Complete pgvector RAG Setup

**docker-compose.yml:**
```yaml
version: '3.8'

services:
  db:
    image: pgvector/pgvector:pg17
    container_name: rag-postgres
    restart: unless-stopped
    environment:
      POSTGRES_USER: raguser
      POSTGRES_PASSWORD: ${DB_PASSWORD:-ragpassword}
      POSTGRES_DB: ragdb
    volumes:
      - pgdata:/var/lib/postgresql/data
      - ./init-scripts:/docker-entrypoint-initdb.d:ro
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U raguser -d ragdb"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s

volumes:
  pgdata:
    name: rag-postgres-data
```

**init-scripts/01-setup-pgvector.sql:**
```sql
-- Enable pgvector extension
CREATE EXTENSION IF NOT EXISTS vector;

-- Create documents table for RAG
CREATE TABLE IF NOT EXISTS documents (
    id SERIAL PRIMARY KEY,
    content TEXT NOT NULL,
    metadata JSONB DEFAULT '{}',
    embedding vector(1536),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Create HNSW index for fast similarity search
CREATE INDEX IF NOT EXISTS documents_embedding_idx
ON documents USING hnsw (embedding vector_cosine_ops);

-- Create GIN index for metadata queries
CREATE INDEX IF NOT EXISTS documents_metadata_idx
ON documents USING gin (metadata);

-- Function to update timestamp
CREATE OR REPLACE FUNCTION update_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Trigger for auto-updating timestamp
CREATE TRIGGER documents_updated_at
    BEFORE UPDATE ON documents
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at();
```

---

## 13. Best Practices

### Production vs Development Configurations

**Development:**
- Use bind mounts for easy data inspection
- Enable verbose logging
- Use simple passwords (or trust auth locally)
- Keep data in project directory

**Production:**
- Use named volumes for reliability
- Use Docker secrets for passwords
- Enable SSL connections
- Configure resource limits
- Set up automated backups
- Use health checks

### Security Recommendations

1. **Never use default passwords** in production
2. **Use Docker secrets** instead of environment variables
3. **Limit port exposure** - don't bind to 0.0.0.0 if not needed
4. **Enable SSL** for remote connections
5. **Configure pg_hba.conf** to restrict connections
6. **Use non-root user** when possible
7. **Keep images updated** for security patches

### Performance Considerations

**Bind Mounts:**
- Fastest on Linux (direct filesystem access)
- Slower on macOS/Windows (through VM layer)
- Consider named volumes on Mac/Windows for better performance

**Configuration Tuning:**
```yaml
# Mount custom postgresql.conf
volumes:
  - ./postgresql.conf:/etc/postgresql/postgresql.conf:ro
command: postgres -c config_file=/etc/postgresql/postgresql.conf
```

Key parameters to tune:
- `shared_buffers` (25% of available RAM)
- `effective_cache_size` (50-75% of RAM)
- `maintenance_work_mem`
- `wal_buffers`
- `max_connections`

### Recommended Directory Locations

**Linux:**
```
/var/lib/docker-data/postgres/
/opt/postgres-data/
```

**macOS/Development:**
```
./data/postgres/
~/docker-data/postgres/
```

### Cleanup and Maintenance

```bash
# Remove unused volumes
docker volume prune

# Remove specific volume (WARNING: data loss)
docker volume rm volume_name

# Vacuum and analyze database
docker exec postgres-db psql -U postgres -c "VACUUM ANALYZE;"

# Check database size
docker exec postgres-db psql -U postgres -c "SELECT pg_database_size('mydb');"
```

### Upgrading PostgreSQL Versions

**For minor version upgrades (17.1 to 17.2):**
1. Stop container
2. Update image tag
3. Start container (data compatible)

**For major version upgrades (17 to 18):**
1. Create backup with pg_dump
2. Stop old container
3. Create new container with new version
4. Restore from backup

**PostgreSQL 18+ with new PGDATA structure:**
```bash
# Option 1: Dump and restore (safest)
docker exec old-container pg_dumpall -U postgres > backup.sql
docker-compose down
# Update compose file to use postgres:18
docker-compose up -d
docker exec -i new-container psql -U postgres < backup.sql

# Option 2: Migrate directory structure
mv /var/lib/postgresql/data/* /var/lib/postgresql/18/docker/
```

---

## Sources

- [PostgreSQL Official Docker Hub Image](https://hub.docker.com/_/postgres)
- [pgvector Docker Image](https://hub.docker.com/r/pgvector/pgvector)
- [Docker Hub PostgreSQL Tags](https://hub.docker.com/_/postgres/tags)
- [Docker Library PostgreSQL Documentation](https://github.com/docker-library/docs/blob/master/postgres/README.md)
- [PostgreSQL 18 PGDATA Change PR](https://github.com/docker-library/postgres/pull/1259)
- [PostgreSQL 18 Docker Issues](https://github.com/docker-library/postgres/issues/1370)
- [Docker Volumes Documentation](https://docs.docker.com/engine/storage/volumes/)
- [Docker Compose Startup Order](https://docs.docker.com/compose/how-tos/startup-order/)
- [PostgreSQL Database File Layout](https://www.postgresql.org/docs/current/storage-file-layout.html)
- [PostgreSQL pg_hba.conf Documentation](https://www.postgresql.org/docs/current/auth-pg-hba-conf.html)
- [DEV Community - PostgreSQL Docker Persistence](https://dev.to/iamrj846/how-to-persist-data-in-a-dockerized-postgres-database-using-volumes-15f0)
- [SimpleBackups - Docker Postgres Backup Guide](https://simplebackups.com/blog/docker-postgres-backup-restore-guide-with-examples)
- [Docker Community Forums - Permission Issues](https://forums.docker.com/t/data-directory-var-lib-postgresql-data-has-wrong-ownership-on-linux/97057)
- [LogRocket - Docker Volumes vs Bind Mounts](https://blog.logrocket.com/docker-volumes-vs-bind-mounts/)
- [Waiting for Postgres 18 - Docker Containers Smaller](https://ardentperf.com/2025/04/07/waiting-for-postgres-18-docker-containers-34-smaller/)

---

*Research completed: January 2025*
