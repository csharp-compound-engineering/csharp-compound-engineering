# Suggested Commands

## Build & Run
```bash
# Build all projects
dotnet build

# Run MCP server (HTTP transport, port 8080)
dotnet run --project src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj

# Run GitSync Job (standalone, iterates all configured repos)
dotnet run --project src/CompoundDocs.GitSync.Job/CompoundDocs.GitSync.Job.csproj
```

## Testing
```bash
# Run all tests
dotnet test

# Run a specific test by name
dotnet test --filter "FullyQualifiedName~TestName"

# Run a specific test project
dotnet test tests/CompoundDocs.Tests.Unit/CompoundDocs.Tests.Unit.csproj
dotnet test tests/CompoundDocs.Tests.Integration/CompoundDocs.Tests.Integration.csproj
dotnet test tests/CompoundDocs.Tests.E2E/CompoundDocs.Tests.E2E.csproj

# Run tests with coverage + enforce 100% threshold
bash scripts/coverage-merge.sh
```

## Release & Deployment Scripts
```bash
bash scripts/release-prepare.sh    # Version bumps
bash scripts/release-docker.sh     # Docker build/push
bash scripts/release-helm.sh       # Helm chart packaging/push
bash scripts/release-docs.sh       # Docs site build
```

## JavaScript/Node Tooling
```bash
# IMPORTANT: Use pnpm, NOT npm
pnpm install
pnpm run <script>
```

## Git & System Utilities (macOS/Darwin)
```bash
git status
git log --oneline -10
git diff
git diff --cached
```

## Formatting & Linting
- Code style enforced at build time via `.editorconfig` + `EnforceCodeStyleInBuild=true`
- No separate formatting/linting commands needed — build catches style issues
- Pre-commit: gitleaks for secret scanning
