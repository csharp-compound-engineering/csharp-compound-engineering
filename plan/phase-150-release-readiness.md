# Phase 150: Release Readiness Checklist

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Final Integration
> **Prerequisites**: All previous phases (1-149)

---

## Spec References

This phase implements the final release readiness validation defined in:

- **spec/testing.md** - [100% Coverage Enforcement](../spec/testing.md#100-coverage-enforcement) (lines 213-248)
- **spec/marketplace.md** - [Release Process](../spec/marketplace.md#release-process) (lines 411-471)
- **spec/marketplace.md** - [Plugin Manifest](../spec/marketplace.md#plugin-manifest) (lines 46-115)
- **spec/testing.md** - [Test Categories](../spec/testing.md#test-categories) (lines 112-171)

---

## Objectives

1. Verify all unit, integration, and E2E tests pass
2. Confirm 100% code coverage across line, branch, and method metrics
3. Validate all 17 plugin skills are functional
4. Validate all MCP tools are operational
5. Ensure marketplace manifest is complete and valid
6. Verify documentation completeness
7. Confirm CI/CD pipeline executes successfully

---

## Acceptance Criteria

### Test Verification

- [ ] All unit tests pass: `dotnet test tests/CompoundDocs.Tests`
- [ ] All integration tests pass: `dotnet test tests/CompoundDocs.IntegrationTests`
- [ ] All E2E tests pass: `dotnet test tests/CompoundDocs.E2ETests`
- [ ] No test warnings or skipped tests without documented justification
- [ ] Test execution time within acceptable bounds:
  - [ ] Unit tests: < 2 minutes total
  - [ ] Integration tests: < 10 minutes total
  - [ ] E2E tests: < 30 minutes total

### Coverage Verification

- [ ] Line coverage: 100%
- [ ] Branch coverage: 100%
- [ ] Method coverage: 100%
- [ ] Coverage report generated: `coverage/cobertura.xml`
- [ ] HTML coverage report generated and viewable
- [ ] All `[ExcludeFromCodeCoverage]` attributes include `Justification` parameter
- [ ] Coverage exclusions reviewed and approved:
  - [ ] Generated code exclusions documented
  - [ ] Infrastructure startup exclusions documented
  - [ ] DTO/record exclusions documented

### Skills Verification

All 17 skills must be functional with proper YAML frontmatter:

**Capture Skills**:
- [ ] `cdocs:problem` - Creates problem documentation
- [ ] `cdocs:insight` - Creates insight documentation
- [ ] `cdocs:codebase` - Creates codebase documentation
- [ ] `cdocs:tool` - Creates tool documentation
- [ ] `cdocs:style` - Creates style documentation

**Query Skills**:
- [ ] `cdocs:query` - RAG-based contextual query returns relevant results
- [ ] `cdocs:search` - Semantic vector search returns ranked results
- [ ] `cdocs:search-external` - External knowledge base search works
- [ ] `cdocs:query-external` - RAG query against external docs works

**Management Skills**:
- [ ] `cdocs:activate` - Project activation works correctly
- [ ] `cdocs:delete` - Document deletion works correctly
- [ ] `cdocs:promote` - Document promotion updates boost factor
- [ ] `cdocs:create-type` - Custom document type creation works

**Utility Skills**:
- [ ] `cdocs:research` - Multi-source research workflow completes
- [ ] `cdocs:capture-select` - Interactive type selection works
- [ ] `cdocs:todo` - TODO documentation capture works
- [ ] `cdocs:worktree` - Git worktree integration works

### MCP Tools Verification

All MCP tools must be operational via stdio transport:

- [ ] `rag_query` - RAG query tool responds correctly
- [ ] `semantic_search` - Semantic search tool responds correctly
- [ ] `index_document` - Document indexing tool works
- [ ] `list_doc_types` - Document type listing works
- [ ] `search_external` - External search tool works
- [ ] `rag_external` - External RAG tool works
- [ ] `delete_documents` - Document deletion tool works
- [ ] `update_promotion` - Promotion update tool works
- [ ] `activate_project` - Project activation tool works
- [ ] MCP server starts without errors
- [ ] MCP server handles graceful shutdown
- [ ] MCP server responds to capability queries
- [ ] Error responses follow MCP protocol format

### Marketplace Manifest Verification

- [ ] `manifest.json` exists at `marketplace/plugins/csharp-compounding-docs/manifest.json`
- [ ] Manifest JSON is valid (parseable)
- [ ] All required fields populated:
  - [ ] `name`: "csharp-compounding-docs"
  - [ ] `display_name`: "CSharp Compound Docs"
  - [ ] `version`: Follows semver (e.g., "1.0.0")
  - [ ] `description`: Clear and accurate
  - [ ] `author.name`: Populated
  - [ ] `author.url`: Valid URL
  - [ ] `repository`: Valid GitHub URL
  - [ ] `license`: "MIT"
  - [ ] `keywords`: Array with relevant terms
  - [ ] `claude_code_version`: ">=1.0.0"
- [ ] `components.skills` array contains all 17 skills
- [ ] `components.mcp_servers` array references the plugin's MCP server
- [ ] `dependencies.runtime` includes: dotnet-10.0, docker, powershell-7
- [ ] `install` section complete with type, url, path
- [ ] `.mcp.json` at plugin root is valid and references correct launch script

### Documentation Verification

- [ ] README.md complete with:
  - [ ] Project overview
  - [ ] Prerequisites section
  - [ ] Installation instructions
  - [ ] Quick start guide
  - [ ] Skills reference
  - [ ] MCP tools reference
  - [ ] Configuration options
  - [ ] Troubleshooting section
- [ ] CHANGELOG.md exists with release notes
- [ ] LICENSE file present (MIT)
- [ ] Contributing guidelines (if applicable)
- [ ] API documentation generated (if applicable)

### CI/CD Pipeline Verification

- [ ] GitHub Actions workflow file exists: `.github/workflows/ci.yml`
- [ ] CI pipeline executes successfully on push
- [ ] Build step completes without errors
- [ ] All test stages pass:
  - [ ] Unit tests stage
  - [ ] Integration tests stage
  - [ ] E2E tests stage
- [ ] Coverage thresholds enforced (build fails if < 100%)
- [ ] Docker services start correctly in CI:
  - [ ] PostgreSQL with pgvector
  - [ ] Ollama with embedding model
- [ ] Release workflow exists: `.github/workflows/release.yml`
- [ ] Release workflow triggers on version tags (v*)
- [ ] Release workflow packages plugin correctly
- [ ] Release workflow updates marketplace

---

## Implementation Notes

### Pre-Release Verification Script

Create a comprehensive verification script:

**`scripts/verify-release-readiness.ps1`**:
```powershell
#!/usr/bin/env pwsh
# Release Readiness Verification Script

param(
    [switch]$SkipTests,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$script:errors = @()
$script:warnings = @()

function Write-Check {
    param([string]$Name, [bool]$Passed, [string]$Message = "")
    if ($Passed) {
        Write-Host "[PASS] $Name" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] $Name" -ForegroundColor Red
        if ($Message) { Write-Host "       $Message" -ForegroundColor Red }
        $script:errors += $Name
    }
}

function Write-Section {
    param([string]$Name)
    Write-Host "`n=== $Name ===" -ForegroundColor Cyan
}

# 1. Test Verification
Write-Section "Test Verification"

if (-not $SkipTests) {
    # Unit tests
    $unitResult = dotnet test tests/CompoundDocs.Tests --no-build --verbosity quiet 2>&1
    Write-Check "Unit tests pass" ($LASTEXITCODE -eq 0) $unitResult

    # Integration tests
    $intResult = dotnet test tests/CompoundDocs.IntegrationTests --no-build --verbosity quiet 2>&1
    Write-Check "Integration tests pass" ($LASTEXITCODE -eq 0) $intResult

    # E2E tests
    $e2eResult = dotnet test tests/CompoundDocs.E2ETests --no-build --verbosity quiet 2>&1
    Write-Check "E2E tests pass" ($LASTEXITCODE -eq 0) $e2eResult
}

# 2. Coverage Verification
Write-Section "Coverage Verification"

$coverageFile = "coverage/cobertura.xml"
$coverageExists = Test-Path $coverageFile
Write-Check "Coverage report exists" $coverageExists

if ($coverageExists) {
    [xml]$coverage = Get-Content $coverageFile
    $lineRate = [double]$coverage.coverage.'line-rate' * 100
    $branchRate = [double]$coverage.coverage.'branch-rate' * 100

    Write-Check "Line coverage = 100%" ($lineRate -eq 100) "Actual: $lineRate%"
    Write-Check "Branch coverage = 100%" ($branchRate -eq 100) "Actual: $branchRate%"
}

# 3. Manifest Verification
Write-Section "Manifest Verification"

$manifestPath = "marketplace/plugins/csharp-compounding-docs/manifest.json"
$manifestExists = Test-Path $manifestPath
Write-Check "manifest.json exists" $manifestExists

if ($manifestExists) {
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json

    Write-Check "name field populated" ($manifest.name -eq "csharp-compounding-docs")
    Write-Check "version follows semver" ($manifest.version -match '^\d+\.\d+\.\d+')
    Write-Check "17 skills declared" ($manifest.components.skills.Count -eq 17)
    Write-Check "MCP server configured" ($manifest.components.mcp_servers.Count -gt 0)
    Write-Check "Runtime dependencies listed" ($manifest.dependencies.runtime.Count -eq 3)
}

# 4. MCP Configuration Verification
Write-Section "MCP Configuration Verification"

$mcpConfigPath = ".mcp.json"
$mcpConfigExists = Test-Path $mcpConfigPath
Write-Check ".mcp.json exists" $mcpConfigExists

if ($mcpConfigExists) {
    $mcpConfig = Get-Content $mcpConfigPath -Raw | ConvertFrom-Json
    Write-Check "MCP server registered" ($null -ne $mcpConfig.mcpServers.'csharp-compounding-docs')
}

# 5. Documentation Verification
Write-Section "Documentation Verification"

Write-Check "README.md exists" (Test-Path "README.md")
Write-Check "CHANGELOG.md exists" (Test-Path "CHANGELOG.md")
Write-Check "LICENSE exists" (Test-Path "LICENSE")

# 6. CI/CD Verification
Write-Section "CI/CD Verification"

Write-Check "CI workflow exists" (Test-Path ".github/workflows/ci.yml")
Write-Check "Release workflow exists" (Test-Path ".github/workflows/release.yml")

# Summary
Write-Host "`n=== Summary ===" -ForegroundColor Cyan
if ($script:errors.Count -eq 0) {
    Write-Host "All checks passed! Ready for release." -ForegroundColor Green
    exit 0
} else {
    Write-Host "Failed checks: $($script:errors.Count)" -ForegroundColor Red
    foreach ($err in $script:errors) {
        Write-Host "  - $err" -ForegroundColor Red
    }
    exit 1
}
```

### Skill Verification Matrix

Create a manual verification checklist for skills:

| Skill | Command | Expected Behavior | Status |
|-------|---------|-------------------|--------|
| `cdocs:activate` | `/cdocs:activate` | Prompts for or uses current project | |
| `cdocs:problem` | `/cdocs:problem` | Creates problem doc with frontmatter | |
| `cdocs:insight` | `/cdocs:insight` | Creates insight doc with frontmatter | |
| `cdocs:codebase` | `/cdocs:codebase` | Creates codebase doc with frontmatter | |
| `cdocs:tool` | `/cdocs:tool` | Creates tool doc with frontmatter | |
| `cdocs:style` | `/cdocs:style` | Creates style doc with frontmatter | |
| `cdocs:query` | `/cdocs:query <question>` | Returns RAG-synthesized answer | |
| `cdocs:search` | `/cdocs:search <terms>` | Returns ranked document list | |
| `cdocs:search-external` | `/cdocs:search-external <terms>` | Searches external knowledge | |
| `cdocs:query-external` | `/cdocs:query-external <question>` | RAG against external docs | |
| `cdocs:delete` | `/cdocs:delete <path>` | Removes document and chunks | |
| `cdocs:promote` | `/cdocs:promote <path>` | Updates boost factor | |
| `cdocs:create-type` | `/cdocs:create-type` | Creates custom doc type | |
| `cdocs:research` | `/cdocs:research <topic>` | Multi-source research | |
| `cdocs:capture-select` | `/cdocs:capture-select` | Interactive type picker | |
| `cdocs:todo` | `/cdocs:todo` | Creates TODO document | |
| `cdocs:worktree` | `/cdocs:worktree` | Git worktree operations | |

### MCP Tool Verification Commands

Test each MCP tool via direct invocation:

```bash
# Start MCP server
pwsh scripts/launch-mcp-server.ps1 &

# Test tools (using mcp-client or curl for testing)
# Note: Actual testing done via E2E tests, but manual verification can use:

# 1. List capabilities
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | nc localhost 3000

# 2. Test rag_query
echo '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"rag_query","arguments":{"query":"test"}}}' | nc localhost 3000

# 3. Test semantic_search
echo '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"semantic_search","arguments":{"query":"test","limit":5}}}' | nc localhost 3000
```

### Release Checklist Document

Create a final release checklist as a GitHub issue template:

**`.github/ISSUE_TEMPLATE/release-checklist.md`**:
```markdown
---
name: Release Checklist
about: Checklist for releasing a new version
title: 'Release v[VERSION]'
labels: release
---

## Pre-Release Checks

### Code Quality
- [ ] All tests passing (unit, integration, E2E)
- [ ] 100% code coverage verified
- [ ] No compiler warnings
- [ ] No linter errors

### Functionality
- [ ] All 17 skills manually tested
- [ ] All MCP tools verified working
- [ ] MCP server starts and stops cleanly
- [ ] Error handling works correctly

### Documentation
- [ ] README updated with any changes
- [ ] CHANGELOG updated with release notes
- [ ] Version numbers updated in all locations

### Manifest
- [ ] manifest.json version updated
- [ ] .csproj version updated
- [ ] api/plugins.json updated

### CI/CD
- [ ] CI pipeline passes on release branch
- [ ] Release workflow tested

## Release Steps

1. [ ] Create release branch: `git checkout -b release/vX.Y.Z`
2. [ ] Run verification: `pwsh scripts/verify-release-readiness.ps1`
3. [ ] Update CHANGELOG.md
4. [ ] Commit version bumps
5. [ ] Create tag: `git tag vX.Y.Z`
6. [ ] Push tag: `git push origin vX.Y.Z`
7. [ ] Verify GitHub Actions release workflow
8. [ ] Verify marketplace updated
9. [ ] Create GitHub release with notes

## Post-Release

- [ ] Verify plugin installable from marketplace
- [ ] Test installation on clean environment
- [ ] Announce release (if applicable)
```

### Version Synchronization

Ensure version is consistent across all locations:

| Location | File | Field |
|----------|------|-------|
| Manifest | `marketplace/plugins/csharp-compounding-docs/manifest.json` | `version` |
| MCP Server | `src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj` | `<Version>` |
| Changelog | `CHANGELOG.md` | Header |
| Plugin Registry | `marketplace/api/plugins.json` | `version` |

### Final Build Verification

```bash
# Clean build
dotnet clean
dotnet build -c Release

# Run all tests with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Generate coverage report
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coveragereport" \
  -reporttypes:"Html;Badges;MarkdownSummaryGithub"

# Verify manifest
python -m json.tool marketplace/plugins/csharp-compounding-docs/manifest.json

# Run release readiness script
pwsh scripts/verify-release-readiness.ps1
```

---

## Dependencies

### Depends On
- All previous phases (1-149) must be completed
- Specifically critical:
  - **Phase 109-124**: All testing infrastructure
  - **Phase 126-127**: Marketplace structure and manifest
  - **Phase 81-101**: All skills implemented
  - **Phase 71-80**: All MCP tools implemented
  - **Phase 121-124**: CI/CD pipeline setup

### Blocks
- Initial release (v1.0.0)
- Marketplace publication
- User installation availability

---

## Verification Steps

After completing this phase, verify:

1. **Run release readiness script**:
   ```bash
   pwsh scripts/verify-release-readiness.ps1
   ```

2. **Verify all tests pass**:
   ```bash
   dotnet test --verbosity normal
   ```

3. **Check coverage report**:
   ```bash
   open coveragereport/index.html
   # Verify 100% coverage
   ```

4. **Validate manifest**:
   ```bash
   jq '.' marketplace/plugins/csharp-compounding-docs/manifest.json
   jq '.components.skills | length' marketplace/plugins/csharp-compounding-docs/manifest.json
   # Expected: 17
   ```

5. **Test MCP server startup**:
   ```bash
   pwsh scripts/launch-mcp-server.ps1 --version
   pwsh scripts/launch-mcp-server.ps1 &
   # Verify server starts without errors
   kill %1
   ```

6. **Verify CI/CD on GitHub**:
   - Push to a test branch
   - Verify all GitHub Actions jobs pass
   - Check coverage report in job summary

7. **Manual skill spot-check**:
   - Open Claude Code with plugin loaded
   - Run `/cdocs:activate`
   - Run `/cdocs:query "test query"`
   - Verify responses are correct

---

## Notes

- This phase is a comprehensive verification checkpoint before release
- No new code is written; this is purely validation
- Any failures discovered should be fixed in the appropriate earlier phase
- Consider creating a release branch and running verification there
- Document any known issues or limitations in release notes
- The release readiness script should be added to CI as a pre-release gate
- All team members should review the checklist before approving release
- Consider a soft launch or beta period before full marketplace publication
