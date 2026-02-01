# Troubleshooting Guide

This guide covers common issues and their solutions when working with the CSharp Compound Docs MCP plugin.

## Table of Contents

- [Quick Diagnostics](#quick-diagnostics)
- [PostgreSQL Issues](#postgresql-issues)
- [pgvector Issues](#pgvector-issues)
- [Ollama Issues](#ollama-issues)
- [Embedding Issues](#embedding-issues)
- [MCP Server Issues](#mcp-server-issues)
- [Claude Code Integration Issues](#claude-code-integration-issues)
- [File Watcher Issues](#file-watcher-issues)
- [Performance Issues](#performance-issues)
- [Log Files and Diagnostics](#log-files-and-diagnostics)

---

## Quick Diagnostics

Run these commands to quickly diagnose common issues:

### Check Infrastructure Status

```bash
# Check if Docker containers are running
docker compose -p csharp-compounding-docs ps

# Check container health
docker compose -p csharp-compounding-docs ps --format "table {{.Name}}\t{{.Status}}\t{{.Health}}"

# View recent logs
docker compose -p csharp-compounding-docs logs --tail 50
```

### Test Service Connectivity

```bash
# Test PostgreSQL connection
psql -h 127.0.0.1 -p 5433 -U compounding -d compounding_docs -c "SELECT 1;"

# Test Ollama API
curl http://127.0.0.1:11435/api/tags

# Test pgvector extension
psql -h 127.0.0.1 -p 5433 -U compounding -d compounding_docs -c "SELECT * FROM pg_extension WHERE extname = 'vector';"
```

### Health Check Endpoints

| Service | Health Check |
|---------|--------------|
| PostgreSQL | `pg_isready -h 127.0.0.1 -p 5433 -U compounding` |
| Ollama | `curl http://127.0.0.1:11435/api/tags` |
| MCP Server | Check stderr output for startup messages |

---

## PostgreSQL Issues

### Connection Refused

**Symptoms**:
- "Connection refused" errors
- "could not connect to server" messages

**Causes and Solutions**:

1. **Container not running**
   ```bash
   # Check container status
   docker compose -p csharp-compounding-docs ps

   # Start containers
   docker compose -p csharp-compounding-docs up -d
   ```

2. **Wrong port**
   ```bash
   # Default port is 5433 (not 5432)
   psql -h 127.0.0.1 -p 5433 -U compounding -d compounding_docs
   ```

3. **Port conflict**
   ```bash
   # Check if port is in use
   lsof -i :5433

   # If conflicting, modify docker-compose.yml
   # Change "127.0.0.1:5433:5432" to another port
   ```

### Authentication Failed

**Symptoms**:
- "password authentication failed"
- "FATAL: role does not exist"

**Solutions**:

1. **Verify credentials**
   ```bash
   # Default credentials
   # Username: compounding
   # Password: compounding
   # Database: compounding_docs
   ```

2. **Reset database volume**
   ```bash
   # WARNING: This deletes all data
   docker compose -p csharp-compounding-docs down -v
   docker compose -p csharp-compounding-docs up -d
   ```

### Database Does Not Exist

**Symptoms**:
- "database does not exist"

**Solution**:
```bash
# Connect to default database and create
psql -h 127.0.0.1 -p 5433 -U compounding -d postgres

# In psql:
CREATE DATABASE compounding_docs;
\q

# Or restart containers to run init scripts
docker compose -p csharp-compounding-docs down
docker compose -p csharp-compounding-docs up -d
```

---

## pgvector Issues

### Extension Not Enabled

**Symptoms**:
- "type vector does not exist"
- "function cosine_distance does not exist"

**Solution**:
```sql
-- Connect to database
psql -h 127.0.0.1 -p 5433 -U compounding -d compounding_docs

-- Enable extension
CREATE EXTENSION IF NOT EXISTS vector;

-- Verify
SELECT * FROM pg_extension WHERE extname = 'vector';
```

### Extension Not Available

**Symptoms**:
- "could not open extension control file"
- "extension 'vector' is not available"

**Cause**: Using standard PostgreSQL image instead of pgvector image

**Solution**:
```bash
# Ensure using correct image in docker-compose.yml
# Image should be: pgvector/pgvector:pg16

# Rebuild containers
docker compose -p csharp-compounding-docs down
docker compose -p csharp-compounding-docs build --no-cache
docker compose -p csharp-compounding-docs up -d
```

### Vector Dimension Mismatch

**Symptoms**:
- "expected N dimensions, not M"
- "different vector dimensions"

**Cause**: Embedding model changed or misconfigured

**Solution**:
```sql
-- Check current table definition
\d documents

-- The embedding column should be: vector(1024)
-- mxbai-embed-large produces 1024-dimensional vectors

-- If dimensions don't match, you must rebuild the index:
DROP TABLE IF EXISTS documents CASCADE;
DROP TABLE IF EXISTS document_chunks CASCADE;
-- Then restart MCP server to recreate tables
```

---

## Ollama Issues

### Ollama Not Responding

**Symptoms**:
- "Connection refused" to Ollama API
- Timeout on embedding generation

**Solutions**:

1. **Check container status**
   ```bash
   docker compose -p csharp-compounding-docs ps ollama
   ```

2. **Check if service is listening**
   ```bash
   curl -v http://127.0.0.1:11435/api/tags
   ```

3. **View Ollama logs**
   ```bash
   docker compose -p csharp-compounding-docs logs ollama --tail 100
   ```

4. **Restart Ollama**
   ```bash
   docker compose -p csharp-compounding-docs restart ollama
   ```

### Model Not Found

**Symptoms**:
- "model not found" error
- "pull model" suggestions

**Solution**:
```bash
# Pull required models
docker compose -p csharp-compounding-docs exec ollama ollama pull mxbai-embed-large
docker compose -p csharp-compounding-docs exec ollama ollama pull mistral

# Verify models available
docker compose -p csharp-compounding-docs exec ollama ollama list
```

### Out of Memory (OOM)

**Symptoms**:
- Ollama container restarts
- "OOM killed" in Docker logs

**Solutions**:

1. **Increase Docker memory limit**
   - Docker Desktop > Settings > Resources > Memory

2. **Use smaller model**
   ```bash
   # Edit global config
   # ~/.claude/.csharp-compounding-docs/global-config.json
   # Change generationModel to a smaller variant
   ```

3. **Enable GPU acceleration**
   ```bash
   # Edit ollama-config.json
   # Set gpu.enabled: true
   # Set gpu.type: "nvidia" or "amd"
   ```

### Apple Silicon / macOS Issues

**Symptoms**:
- GPU acceleration not working in Docker
- Slow embedding generation

**Cause**: Metal GPU acceleration not available in Docker

**Solution**: Use native Ollama installation on macOS
```bash
# Install Ollama natively
brew install ollama

# Run Ollama service
ollama serve

# Ollama will be available at default port 11434
# Update configuration to point to localhost:11434
```

---

## Embedding Issues

### Dimension Mismatch

**Symptoms**:
- Vector search returns no results
- Errors about embedding dimensions

**Cause**: Wrong embedding model or dimension configuration

**Solution**:
The embedding model is fixed to `mxbai-embed-large` (1024 dimensions). Ensure:

1. Correct model is pulled
   ```bash
   docker compose -p csharp-compounding-docs exec ollama ollama pull mxbai-embed-large
   ```

2. Database table has correct dimension
   ```sql
   -- Check column definition
   SELECT column_name, data_type, udt_name
   FROM information_schema.columns
   WHERE table_name = 'documents' AND column_name = 'embedding';
   ```

### Slow Embedding Generation

**Symptoms**:
- Document indexing takes >10 seconds per document
- Timeouts during indexing

**Solutions**:

1. **Enable GPU acceleration** (if available)

2. **Check Ollama resource usage**
   ```bash
   docker stats csharp-compounding-docs-ollama
   ```

3. **Reduce concurrent requests**
   - The MCP server queues embedding requests
   - Check for excessive concurrent indexing operations

---

## MCP Server Issues

### Server Not Starting

**Symptoms**:
- MCP server process exits immediately
- No response from Claude Code

**Diagnostic Steps**:

1. **Run server manually to see errors**
   ```bash
   dotnet run --project src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj
   ```

2. **Check for missing dependencies**
   ```bash
   dotnet restore
   ```

3. **Verify .NET version**
   ```bash
   dotnet --version
   # Should be 9.0 or higher
   ```

### Tool Not Found

**Symptoms**:
- "Tool not found" errors in Claude Code
- MCP tools not appearing in tool list

**Solutions**:

1. **Verify MCP configuration**
   ```json
   // Check .claude/mcp.json or claude_desktop_config.json
   {
     "mcpServers": {
       "csharp-compounding-docs": {
         "command": "dotnet",
         "args": ["run", "--project", "/path/to/CompoundDocs.McpServer.csproj"]
       }
     }
   }
   ```

2. **Restart Claude Code**
   - Close and reopen Claude Code/Desktop

3. **Check server startup logs**
   - Look for tool registration messages in stderr

### Project Not Activated

**Symptoms**:
- "PROJECT_NOT_ACTIVATED" error from tools
- Tools require activation first

**Solution**:
```
# In Claude Code, run:
/cdocs:activate

# Or manually call the MCP tool
activate_project config_path="/path/to/.csharp-compounding-docs/config.json" branch_name="main"
```

---

## Claude Code Integration Issues

### MCP Server Not Detected

**Symptoms**:
- Tools don't appear in Claude Code
- "No MCP servers configured"

**Solutions**:

1. **Check configuration file location**
   - macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`
   - Windows: `%APPDATA%\Claude\claude_desktop_config.json`
   - Linux: `~/.config/Claude/claude_desktop_config.json`

2. **Validate JSON syntax**
   ```bash
   cat ~/Library/Application\ Support/Claude/claude_desktop_config.json | jq .
   ```

3. **Verify path is absolute**
   ```json
   {
     "mcpServers": {
       "csharp-compounding-docs": {
         "command": "/usr/local/share/dotnet/dotnet",
         "args": ["run", "--project", "/full/path/to/project.csproj"]
       }
     }
   }
   ```

### Skills Not Auto-Invoking

**Symptoms**:
- Capture skills don't trigger on relevant conversations
- `/cdocs:` commands not recognized

**Solutions**:

1. **Verify skills are installed**
   - Check `.claude/skills/` directory

2. **Check skill frontmatter**
   - Skills need correct `auto-invoke:` configuration

3. **Manual invocation**
   - Type `/cdocs:problem` directly to invoke

---

## File Watcher Issues

### Changes Not Detected

**Symptoms**:
- New documents not indexed
- Modified documents not updated in search

**Solutions**:

1. **Check file location**
   - Files must be in `./csharp-compounding-docs/` directory

2. **Verify file extension**
   - Only `.md` files are watched by default

3. **Check debounce timing**
   - Changes are debounced (default 500ms)
   - Wait for debounce period to complete

4. **Manual re-index**
   ```
   # Use index_document tool
   index_document path="./csharp-compounding-docs/problems/my-doc.md"
   ```

### Too Many File Watchers

**Symptoms**:
- "ENOSPC: System limit for number of file watchers reached"

**Solution (Linux)**:
```bash
# Increase inotify limit
echo fs.inotify.max_user_watches=524288 | sudo tee -a /etc/sysctl.conf
sudo sysctl -p
```

---

## Performance Issues

### Slow Search Results

**Symptoms**:
- Semantic search takes >1 second
- RAG queries timeout

**Solutions**:

1. **Check HNSW index**
   ```sql
   SELECT indexname, indexdef
   FROM pg_indexes
   WHERE tablename = 'documents';
   ```

2. **Rebuild index if needed**
   ```sql
   REINDEX INDEX documents_embedding_idx;
   ```

3. **Reduce result count**
   - Lower `max_results` in project config

### High Memory Usage

**Symptoms**:
- MCP server using >500MB
- OOM errors

**Solutions**:

1. **Check for memory leaks**
   ```bash
   dotnet-counters monitor --process-id <pid>
   ```

2. **Restart MCP server**
   - Claude Code will restart it automatically

3. **Reduce batch sizes**
   - Index fewer documents concurrently

---

## Log Files and Diagnostics

### Log Locations

| Component | Log Location |
|-----------|--------------|
| MCP Server | stderr (captured by Claude Code) |
| PostgreSQL | `docker compose logs postgres` |
| Ollama | `docker compose logs ollama` |
| File Watcher | stderr with MCP Server |

### Enabling Debug Logging

```json
// In project config or environment
{
  "logLevel": "Debug"
}

// Or via environment variable
COMPOUNDING_LOG_LEVEL=Debug
```

### Diagnostic Commands

```bash
# Full system diagnostic
docker compose -p csharp-compounding-docs ps
docker compose -p csharp-compounding-docs logs --tail 100

# Database statistics
psql -h 127.0.0.1 -p 5433 -U compounding -d compounding_docs -c "
SELECT relname, n_tup_ins, n_tup_upd, n_tup_del
FROM pg_stat_user_tables;
"

# Vector index statistics
psql -h 127.0.0.1 -p 5433 -U compounding -d compounding_docs -c "
SELECT * FROM pg_stat_user_indexes WHERE indexrelname LIKE '%embedding%';
"
```

### Getting Help

If issues persist:

1. Check the [GitHub Issues](https://github.com/your-org/csharp-compound-engineering/issues)
2. Search existing issues for similar problems
3. Open a new issue with:
   - Error messages (full text)
   - Steps to reproduce
   - Environment details (OS, .NET version, Docker version)
   - Relevant log output
