# Suggested Commands

## Build Commands
```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj

# Run the MCP server
dotnet run --project src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj

# Run tests (when available)
dotnet test
```

## Docker Commands
```bash
# Start services (PostgreSQL with pgvector)
docker-compose up -d

# Stop services
docker-compose down
```

## Git Commands (Darwin/macOS)
```bash
git status
git add <file>
git commit -m "message"
git push
git log --oneline
```

## File System (Darwin)
```bash
ls -la          # List files with details
find . -name "*.cs"  # Find C# files
grep -r "pattern" .  # Search in files
```
