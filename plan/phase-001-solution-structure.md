# Phase 001: Solution & Project Structure

> **Status**: NOT_STARTED
> **Effort Estimate**: 2-4 hours
> **Category**: Infrastructure Setup
> **Prerequisites**: None (first phase)

---

## Spec References

This phase implements the foundational project structure defined in:

- **SPEC.md** - [Project Repository Structure](../SPEC.md#project-repository-structure) (lines 313-360)
- **SPEC.md** - [Technology Stack](../SPEC.md#technology-stack) (lines 299-310)
- **structure/root.md** - Technology stack and project layout summary

---

## Objectives

1. Create the .NET solution file (`csharp-compounding-docs.sln`) at the repository root
2. Establish the directory structure for source code (`src/`), tests (`tests/`), and plugin (`plugins/`)
3. Configure shared build properties via `Directory.Build.props`
4. Ensure `.gitignore` covers all .NET build artifacts
5. Create initial `README.md` with project overview and setup instructions

---

## Acceptance Criteria

- [ ] Solution file `csharp-compounding-docs.sln` exists at repository root
- [ ] Directory structure matches SPEC.md repository structure:
  - [ ] `src/` directory exists with placeholder `.gitkeep` files
  - [ ] `tests/` directory exists with placeholder `.gitkeep` files
  - [ ] `plugins/csharp-compounding-docs/` directory structure exists
  - [ ] `scripts/` directory exists
  - [ ] `docker/` directory exists
  - [ ] `marketplace/` directory exists
- [ ] `Directory.Build.props` at root configures:
  - [ ] Target framework: `net9.0`
  - [ ] Nullable reference types enabled
  - [ ] Implicit usings enabled
  - [ ] Treat warnings as errors in Release configuration
  - [ ] Common package versions centralized
- [ ] `.gitignore` includes all standard .NET entries (bin/, obj/, *.user, etc.)
- [ ] `README.md` contains:
  - [ ] Project name and description
  - [ ] Prerequisites section
  - [ ] Build instructions placeholder
  - [ ] Link to SPEC.md for detailed documentation

---

## Implementation Notes

### Solution File Creation

Use the .NET CLI to create an empty solution:

```bash
dotnet new sln -n csharp-compounding-docs
```

The solution will be populated with projects in subsequent phases:
- Phase 002: CompoundDocs.Common (shared library)
- Phase 003: CompoundDocs.McpServer (MCP server)
- Phase 004: CompoundDocs.Cleanup (console app)

### Directory Structure

Create the following directory tree per SPEC.md:

```
csharp-compound-engineering/
├── src/
│   ├── CompoundDocs.McpServer/      # Created in Phase 003
│   ├── CompoundDocs.Cleanup/        # Created in Phase 004
│   └── CompoundDocs.Common/         # Created in Phase 002
├── plugins/
│   └── csharp-compounding-docs/
│       ├── .claude-plugin/
│       ├── skills/
│       ├── agents/
│       │   └── research/
│       └── hooks/
├── scripts/
├── docker/
├── marketplace/
└── tests/
    ├── CompoundDocs.Tests/              # Created in Phase TBD
    ├── CompoundDocs.IntegrationTests/   # Created in Phase TBD
    └── CompoundDocs.E2ETests/           # Created in Phase TBD
```

Use `.gitkeep` files in empty directories to ensure they are tracked by Git.

### Directory.Build.props

Create at repository root with the following configuration:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors Condition="'$(Configuration)' == 'Release'">true</TreatWarningsAsErrors>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <!-- Central package version management -->
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
```

Also create `Directory.Packages.props` for central package version management (NuGet Central Package Management):

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <!-- Versions will be added as projects are created -->
  </ItemGroup>
</Project>
```

### .gitignore Additions

Verify the existing `.gitignore` contains standard .NET entries. Add any missing entries:

```gitignore
# Build results
[Bb]in/
[Oo]bj/
[Ll]og/
[Ll]ogs/

# User-specific files
*.rsuser
*.suo
*.user
*.userosscache
*.sln.docstates

# Visual Studio
.vs/
*.userprefs

# Rider
.idea/

# Test results
[Tt]est[Rr]esult*/
*.trx
coverage*/

# NuGet
*.nupkg
*.snupkg
.nuget/
packages/

# Build server
artifacts/
```

### README.md Structure

```markdown
# CSharp Compound Docs

A Claude Code plugin implementing the "compound-engineering" paradigm for C#/.NET projects.

## Overview

This plugin captures and retrieves institutional knowledge through:
- Disk-based markdown documentation storage
- RAG and semantic search via bundled MCP server
- PostgreSQL + pgvector for vector storage
- Ollama for embeddings and generation

## Prerequisites

- .NET 9.0 SDK
- Docker Desktop
- Claude Code CLI

## Getting Started

_Build and run instructions will be added as the project develops._

## Documentation

See [SPEC.md](./SPEC.md) for detailed specifications.

## License

_License information to be determined._
```

---

## Dependencies

### Depends On
- None (this is the first phase)

### Blocks
- Phase 002: CompoundDocs.Common Library (requires solution file)
- Phase 003: MCP Server Project (requires solution and directory structure)
- Phase 004: Cleanup Console App (requires solution and directory structure)
- All subsequent phases (require foundational structure)

---

## Verification Steps

After completing this phase, verify:

1. **Solution loads**: `dotnet build` completes (with warnings about empty solution)
2. **Directory structure**: All directories exist as specified
3. **Git tracking**: `git status` shows all new files
4. **No build artifacts**: `.gitignore` properly excludes bin/obj directories

---

## Notes

- The solution file will be empty initially; projects are added in subsequent phases
- PowerShell scripts should use `#!/usr/bin/env pwsh` shebang for cross-platform compatibility
- The `.github/workflows/` directory is not created in this phase; it will be added when CI/CD is configured
