# Diagnostics and Troubleshooting Guide

This guide provides comprehensive troubleshooting steps for common issues in the CompoundDocs system, including error scenarios, logging configuration, and dependency health checks.

## Table of Contents

1. [Enabling Debug Logging](#enabling-debug-logging)
2. [Understanding Correlation IDs](#understanding-correlation-ids)
3. [Common Error Scenarios](#common-error-scenarios)
4. [Docker and PostgreSQL Issues](#docker-and-postgresql-issues)
5. [Ollama Connectivity Issues](#ollama-connectivity-issues)
6. [Health Checks and System Status](#health-checks-and-system-status)
7. [Log Interpretation Guide](#log-interpretation-guide)

---

## Enabling Debug Logging

### Configuration

Debug logging can be enabled in two ways:

#### 1. Via Environment Variable

```bash
# Enable debug logging for the entire application
export SERILOG_LOGGING_LEVEL=Debug

# Run the MCP server with debug logging
dotnet run --project src/CompoundDocs.McpServer
```

#### 2. Via appsettings.json

Update the `appsettings.json` configuration:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

#### 3. Via Docker Environment

In `docker-compose.yml`:

```yaml
services:
  mcp-server:
    environment:
      - SERILOG_LOGGING_LEVEL=Debug
      - COMPOUND_DOCS_LOG_LEVEL=Debug
```

### Log Levels

- **Verbose**: Most detailed logging (rarely used)
- **Debug**: Development-level details about execution flow
- **Information**: Important operational events (default)
- **Warning**: Non-critical issues that should be investigated
- **Error**: Critical failures that prevent operation
- **Fatal**: System-stopping errors

### Viewing Debug Logs

```bash
# Tail logs in Docker
docker-compose logs -f mcp-server

# Filter logs by component
docker-compose logs -f mcp-server | grep "DocumentProcessor"

# Filter by correlation ID
docker-compose logs -f mcp-server | grep "8a4c9e2b"
```

---

## Understanding Correlation IDs

### What is a Correlation ID?

A Correlation ID is a unique identifier (8-character alphanumeric string by default) that tracks a single request or operation through the entire system. This allows you to trace how data flows through multiple components and services.

### Example Correlation ID Flow

```
Request arrives: CorrelationId=8a4c9e2b
[15:30:42 INF] 8a4c9e2b Document processing started
[15:30:42 DBG] 8a4c9e2b Parsing frontmatter from file.md
[15:30:42 DBG] 8a4c9e2b Generating embeddings
[15:30:43 DBG] 8a4c9e2b Embedding generated: 384 dimensions
[15:30:43 DBG] 8a4c9e2b Storing document in vector store
[15:30:43 INF] 8a4c9e2b Document processing completed successfully
```

### Extracting Correlation IDs from Logs

```bash
# Get all unique correlation IDs from logs
docker-compose logs mcp-server | awk '{print $3}' | sort | uniq -c | sort -rn

# Track a specific correlation ID through the entire flow
docker-compose logs mcp-server | grep "8a4c9e2b"

# Find correlation IDs associated with errors
docker-compose logs mcp-server | grep "ERR" | awk '{print $3}' | sort | uniq
```

### Using Correlation IDs for Debugging

When reporting issues:

1. Identify the correlation ID(s) associated with the problem
2. Extract all logs for that correlation ID
3. Review the sequence of operations to identify where the failure occurred
4. Include the correlation ID and relevant log excerpt in bug reports

---

## Common Error Scenarios

### Scenario 1: Document Processing Failures

#### Symptom
```
[15:30:42 ERR] 8a4c9e2b Failed to process document: /docs/file.md
ArgumentException: Document has no frontmatter but requires validation against schema.
```

#### Root Causes
1. Missing YAML frontmatter in the markdown file
2. Invalid YAML syntax in frontmatter
3. Missing required fields for the document type

#### Solution Steps
1. Enable debug logging to see validation details
2. Check the document's frontmatter section:
   ```markdown
   ---
   title: My Document
   doc_type: problem
   ---
   # Content here
   ```
3. Ensure all required fields are present for the document's `doc_type`
4. Validate YAML syntax using an online YAML validator
5. Check the DocumentProcessor logs to see what fields failed validation

#### Verification
```bash
# Check that the document now processes successfully
docker-compose logs -f mcp-server | grep "Document processing"
```

### Scenario 2: Embedding Generation Timeout

#### Symptom
```
[15:30:45 ERR] 8a4c9e2b Failed to generate embedding for content of length 5243
TaskCanceledException: The operation was canceled.
```

#### Root Causes
1. Ollama service is not running or not responding
2. Network connectivity issue between MCP server and Ollama
3. Ollama is overloaded with requests
4. Model is not loaded in Ollama

#### Solution Steps
1. Verify Ollama is running:
   ```bash
   docker-compose ps | grep ollama
   curl http://ollama:11434/api/tags
   ```
2. Check Ollama logs:
   ```bash
   docker-compose logs -f ollama
   ```
3. Verify the embedding model is loaded:
   ```bash
   curl http://ollama:11434/api/tags
   # Should return: mxbai-embed-large in the models list
   ```
4. If model is not loaded, pull it:
   ```bash
   docker-compose exec ollama ollama pull mxbai-embed-large
   ```
5. Check the timeout configuration in `appsettings.json`
6. Increase timeout if Ollama is slow:
   ```json
   {
     "CompoundDocs": {
       "Ollama": {
         "Timeout": "30s"  // Increase from default 10s
       }
     }
   }
   ```

#### Verification
```bash
# Test Ollama connectivity
curl -X POST http://localhost:11434/api/generate \
  -d '{"model": "mxbai-embed-large", "prompt": "test"}'
```

### Scenario 3: Vector Store (PostgreSQL) Connection Issues

#### Symptom
```
[15:30:40 ERR] 8a4c9e2b PostgreSQL health check failed
NpgsqlException: Unable to connect to database at localhost:5432
```

#### Root Causes
1. PostgreSQL container is not running
2. Network connectivity between containers is broken
3. Incorrect connection string
4. PostgreSQL port is already in use
5. PostgreSQL hasn't finished initializing

#### Solution Steps
1. Verify PostgreSQL is running:
   ```bash
   docker-compose ps | grep postgres
   ```
2. Check PostgreSQL logs:
   ```bash
   docker-compose logs -f postgres
   ```
3. Verify the connection string matches your environment:
   ```bash
   docker-compose exec postgres psql -U compound_user -d compound_docs -c "SELECT 1"
   ```
4. If port 5432 is in use, either:
   - Stop the conflicting service
   - Change the port in docker-compose.yml
5. Wait for PostgreSQL to initialize (may take 30-60 seconds):
   ```bash
   docker-compose logs postgres | grep "database system is ready"
   ```
6. Verify pgvector extension is installed:
   ```bash
   docker-compose exec postgres psql -U compound_user -d compound_docs \
     -c "CREATE EXTENSION IF NOT EXISTS vector"
   ```

#### Verification
```bash
# Test PostgreSQL connectivity
docker-compose exec postgres psql -U compound_user -d compound_docs \
  -c "SELECT version(); SELECT EXISTS(SELECT 1 FROM pg_extension WHERE extname = 'vector');"
```

### Scenario 4: File Watcher Not Detecting Changes

#### Symptom
```
[15:30:40 INF] File watcher started but no changes detected
Document files are modified but not automatically processed
```

#### Root Causes
1. File watcher is not started
2. Include/exclude patterns don't match the modified files
3. File system permissions prevent watching
4. Watcher buffer overflowed (too many changes at once)

#### Solution Steps
1. Verify the file watcher is running:
   ```bash
   docker-compose logs mcp-server | grep "Started watching"
   ```
2. Check the watcher configuration in `appsettings.json`:
   ```json
   {
     "CompoundDocs": {
       "FileWatcher": {
         "IncludePatterns": ["**/*.md"],
         "ExcludePatterns": ["**/node_modules/**", "**/.git/**"],
         "DebounceMs": 500
       }
     }
   }
   ```
3. Verify file patterns:
   - Check that modified files match the `IncludePatterns`
   - Ensure they don't match `ExcludePatterns`
4. Enable debug logging to see file events:
   ```bash
   export SERILOG_LOGGING_LEVEL=Debug
   docker-compose up
   ```
5. Check for file system watcher errors:
   ```bash
   docker-compose logs mcp-server | grep "watcher error"
   ```
6. Increase debounce time if many files are changing:
   ```json
   {
     "CompoundDocs": {
       "FileWatcher": {
         "DebounceMs": 2000  // Increase from default 500ms
       }
     }
   }
   ```

#### Verification
```bash
# Create a test file and watch for changes
touch /path/to/docs/test.md
docker-compose logs -f mcp-server | grep "test.md"
```

---

## Docker and PostgreSQL Issues

### Issue: "docker-compose: command not found"

**Solution:**
```bash
# Install Docker Compose
brew install docker-compose  # macOS

# Or verify Docker Compose is installed
docker compose version  # Docker Compose V2 (built into Docker)

# Use 'docker compose' instead of 'docker-compose' for V2
docker compose up
```

### Issue: PostgreSQL port already in use

**Error:**
```
Error starting userland proxy: listen tcp4 0.0.0.0:5432: bind: address already in use
```

**Solution:**
```bash
# Find process using port 5432
lsof -i :5432

# Stop the process (or change port in docker-compose.yml)
kill -9 <PID>

# Alternative: Change port in docker-compose.yml
# ports:
#   - "5433:5432"  # Use 5433 instead of 5432
```

### Issue: PostgreSQL initialization hangs

**Error:**
```
postgres_1 | LOG: database system is not ready
postgres_1 | FATAL: the database system is in recovery mode
```

**Solution:**
```bash
# Wait for initialization to complete (can take 1-2 minutes)
docker-compose logs -f postgres | tail -20

# If stuck, restart PostgreSQL
docker-compose restart postgres

# Give it time to initialize
sleep 60
docker-compose logs postgres | grep "ready to accept"
```

### Issue: pgvector extension not installed

**Error:**
```
ERROR: relation "pgvector" does not exist
```

**Solution:**
```bash
# Connect to PostgreSQL and create the extension
docker-compose exec postgres psql -U compound_user -d compound_docs

# In psql shell:
CREATE EXTENSION IF NOT EXISTS vector;

# Verify installation
SELECT * FROM pg_extension WHERE extname = 'vector';
```

---

## Ollama Connectivity Issues

### Issue: "Connection refused" when calling Ollama

**Error:**
```
HttpRequestException: Connection refused (http://ollama:11434/api/generate)
```

**Solution:**
```bash
# Verify Ollama container is running
docker-compose ps | grep ollama

# If not running, start it
docker-compose up -d ollama

# Wait for Ollama to be ready
docker-compose logs -f ollama | grep "listening on"

# Test connectivity
curl http://localhost:11434/api/tags
```

### Issue: Model not found

**Error:**
```
OllamaException: "mxbai-embed-large" not found
```

**Solution:**
```bash
# List available models
curl http://localhost:11434/api/tags

# Pull the required model
docker-compose exec ollama ollama pull mxbai-embed-large

# Verify it was loaded
curl http://localhost:11434/api/tags
```

### Issue: Ollama out of memory

**Error:**
```
CUDA out of memory
OOM: unable to allocate memory
```

**Solution:**
```bash
# In docker-compose.yml, increase allocated memory:
services:
  ollama:
    environment:
      - OLLAMA_KEEP_ALIVE=10m  # Reduce model keep-alive time
      - OLLAMA_MAX_LOADED_MODELS=1  # Load one model at a time
    deploy:
      resources:
        limits:
          memory: 8G  # Increase memory limit

# Restart Ollama
docker-compose restart ollama
```

### Issue: Slow embedding generation

**Symptom:**
```
Embedding generation takes >5 seconds per document
```

**Solution:**
1. Check if GPU is being used:
   ```bash
   docker-compose exec ollama nvidia-smi
   ```
2. If no GPU, consider:
   - Running Ollama on a machine with GPU support
   - Using a smaller embedding model
   - Adjusting batch size settings
3. Monitor Ollama performance:
   ```bash
   docker-compose logs -f ollama | grep "duration"
   ```

---

## Health Checks and System Status

### Automatic Health Checks

The system performs periodic health checks every 60 seconds. Results are logged at INFO level:

```
[15:30:40 INF] Health check completed [OK] in 234ms
[15:30:40 DBG]   PostgreSQL: Healthy - Connected to localhost:5432 (45ms)
[15:30:40 DBG]   Ollama: Healthy - Connected to http://ollama:11434 (89ms)
[15:30:40 DBG]   VectorStore: Healthy - pgvector extension is available (100ms)
```

### Manual Health Check

Access the health check endpoint if exposed:

```bash
# If health endpoint is exposed on port 5000
curl http://localhost:5000/health

# Response format:
{
  "status": "OK",
  "checks": [
    {
      "name": "PostgreSQL",
      "status": "Healthy",
      "duration_ms": 45
    },
    {
      "name": "Ollama",
      "status": "Healthy",
      "duration_ms": 89
    },
    {
      "name": "VectorStore",
      "status": "Healthy",
      "duration_ms": 100
    }
  ],
  "total_duration_ms": 234,
  "checked_at": "2024-01-25T15:30:40Z"
}
```

### Interpreting Health Check Results

- **Healthy**: Service is responsive and working correctly
- **Degraded**: Service is working but performance is reduced
- **Unhealthy**: Service is not responding or critical functionality is broken

When any service is Unhealthy, the overall system status is Unhealthy.

---

## Log Interpretation Guide

### Structured Logging Properties

CompoundDocs uses structured logging with these common properties:

| Property | Purpose | Example |
|----------|---------|---------|
| `{Timestamp}` | When the event occurred | `15:30:42.123` |
| `{Level}` | Severity level | `INF`, `DBG`, `WRN`, `ERR` |
| `{CorrelationId}` | Request tracking ID | `8a4c9e2b` |
| `{Message}` | Human-readable message | `Processing document: /docs/file.md` |
| `{TenantKey}` | Which tenant is affected | `tenant-123` |
| `{DurationMs}` | How long operation took | `234` |

### Example Log Analysis

**Log Entry:**
```
[15:30:42 INF] 8a4c9e2b Processing {Count} documents; TenantKey=tenant-123
```

**What it means:**
- Time: 15:30:42
- Level: Information (normal operation)
- Correlation ID: 8a4c9e2b (track with this ID)
- Operation: Processing multiple documents
- Tenant: tenant-123

**Log Entry:**
```
[15:30:45 ERR] 8a4c9e2b Failed to process document: /docs/file.md
NpgsqlException: Unable to connect to database
```

**What it means:**
- Time: 15:30:45
- Level: Error (operation failed)
- Correlation ID: 8a4c9e2b
- Problem: Database connection issue
- Next step: Check PostgreSQL health

### Common Log Patterns

**Successful document processing:**
```
INF Processing document: /docs/file.md
DBG Parsing frontmatter from file.md
DBG Generating embeddings
DBG Embedding generated: 384 dimensions
DBG Storing document in vector store
INF Document processed successfully
```

**Failed document processing:**
```
INF Processing document: /docs/file.md
ERR Failed to process document
[Exception details follow]
```

**Service startup:**
```
INF HealthCheckService starting, check interval: 60s
INF Started watching for file changes in /project/docs
INF MCP server started successfully
```

**Dependency issue:**
```
WRN PostgreSQL health check failed; Connection refused
WRN Ollama health check failed; Timeout after 10s
```

---

## Advanced Diagnostics

### Collecting Diagnostic Information

Create a comprehensive diagnostic report:

```bash
#!/bin/bash
# Save this as diagnostics.sh

echo "=== System Information ===" > diagnostics.log
docker-compose ps >> diagnostics.log

echo -e "\n=== PostgreSQL Status ===" >> diagnostics.log
docker-compose exec postgres pg_isready -h localhost >> diagnostics.log

echo -e "\n=== Ollama Status ===" >> diagnostics.log
curl -s http://localhost:11434/api/tags >> diagnostics.log

echo -e "\n=== Recent Logs ===" >> diagnostics.log
docker-compose logs --tail=100 >> diagnostics.log

# Send diagnostics.log to support
```

### Performance Profiling

Monitor system performance:

```bash
# Watch container resource usage
docker stats

# Monitor MCP server logs for latency information
docker-compose logs -f mcp-server | grep "latency\|Duration"

# Check database query performance
docker-compose exec postgres psql -U compound_user -d compound_docs \
  -c "SELECT query, calls, mean_time FROM pg_stat_statements ORDER BY mean_time DESC LIMIT 10;"
```

---

## Getting Help

When reporting issues, include:

1. Correlation ID from the failing operation
2. Log excerpt showing the error (with context)
3. Output of `docker-compose ps`
4. Output of `docker-compose logs --tail=200`
5. Environment information (OS, Docker version, RAM available)
6. Steps to reproduce the issue
