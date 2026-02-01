# Phase 114: ReportGenerator Coverage Visualization

> **Status**: NOT_STARTED
> **Effort Estimate**: 2-3 hours
> **Category**: Testing Framework
> **Prerequisites**: Phase 112 (Coverlet Configuration)

---

## Spec References

This phase implements coverage visualization defined in:

- **spec/testing.md** - [Coverage Visualization](../spec/testing.md#coverage-visualization) (lines 446-475)
- **research/reportgenerator-coverage-visualization.md** - Full research document

---

## Objectives

1. Install ReportGenerator as a local .NET tool for the repository
2. Configure HTML report generation from Cobertura XML output
3. Establish standard report output locations
4. Document report customization options
5. Provide local development usage patterns

---

## Acceptance Criteria

- [ ] ReportGenerator is configured as a local .NET tool in `.config/dotnet-tools.json`
- [ ] Report generation command is documented and functional
- [ ] Coverage reports are generated in `coveragereport/` directory
- [ ] Report types include: `Html`, `Badges`, `MarkdownSummaryGithub`
- [ ] `.gitignore` excludes coverage report output directories
- [ ] Local development workflow is documented in contributing guide or README

---

## Implementation Notes

### Tool Installation

Install ReportGenerator as a local tool (preferred for reproducible builds):

```bash
# Create tool manifest if not exists
dotnet new tool-manifest

# Install ReportGenerator locally
dotnet tool install dotnet-reportgenerator-globaltool
```

This creates/updates `.config/dotnet-tools.json`:

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-reportgenerator-globaltool": {
      "version": "5.5.1",
      "commands": [
        "reportgenerator"
      ]
    }
  }
}
```

### Tool Restoration

Other developers restore tools via:

```bash
dotnet tool restore
```

### Report Generation Command

Standard command for local development:

```bash
dotnet tool run reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coveragereport" \
  -reporttypes:"Html;Badges;MarkdownSummaryGithub"
```

### Report Output Location

Reports are generated to `coveragereport/` at repository root:

```
coveragereport/
├── index.html              # Main entry point
├── badge_linecoverage.svg  # Line coverage badge
├── badge_branchcoverage.svg # Branch coverage badge
├── badge_methodcoverage.svg # Method coverage badge
├── badge_combined.svg      # Combined badge
├── SummaryGithub.md        # Markdown summary for GitHub
└── ... (per-assembly reports)
```

### .gitignore Updates

Add coverage report directories to `.gitignore`:

```gitignore
# Coverage reports
coveragereport/
coveragehistory/
TestResults/
```

### Report Types Used

| Type | Purpose |
|------|---------|
| `Html` | Full navigable HTML report for local browsing |
| `Badges` | SVG badges for README and documentation |
| `MarkdownSummaryGithub` | Markdown summary for GitHub PR comments and job summaries |

### Local Development Workflow

Complete workflow for running tests with coverage visualization:

```bash
# 1. Run tests with coverage collection
dotnet test --collect:"XPlat Code Coverage"

# 2. Generate HTML report
dotnet tool run reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coveragereport" \
  -reporttypes:"Html;Badges;MarkdownSummaryGithub"

# 3. Open report in browser (macOS)
open coveragereport/index.html

# 3. Open report in browser (Windows)
start coveragereport/index.html

# 3. Open report in browser (Linux)
xdg-open coveragereport/index.html
```

### Convenience Script (Optional)

Create `scripts/Generate-CoverageReport.ps1`:

```powershell
#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs tests with coverage and generates HTML report.

.DESCRIPTION
    Executes all tests with Coverlet code coverage collection,
    then generates an HTML report using ReportGenerator.

.PARAMETER Open
    If specified, opens the report in the default browser after generation.

.EXAMPLE
    ./scripts/Generate-CoverageReport.ps1 -Open
#>

param(
    [switch]$Open
)

$ErrorActionPreference = "Stop"

Write-Host "Running tests with coverage collection..." -ForegroundColor Cyan
dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults

Write-Host "`nGenerating coverage report..." -ForegroundColor Cyan
dotnet tool run reportgenerator `
    -reports:"**/coverage.cobertura.xml" `
    -targetdir:"coveragereport" `
    -reporttypes:"Html;Badges;MarkdownSummaryGithub"

Write-Host "`nCoverage report generated at: coveragereport/index.html" -ForegroundColor Green

if ($Open) {
    Write-Host "Opening report in browser..." -ForegroundColor Cyan
    if ($IsMacOS) {
        open coveragereport/index.html
    } elseif ($IsWindows) {
        Start-Process coveragereport/index.html
    } else {
        xdg-open coveragereport/index.html
    }
}
```

### Report Customization Options

Common customization parameters:

```bash
# With title
-title:"CompoundDocs Coverage Report"

# With history tracking (for trend charts)
-historydir:"coveragehistory"
-reporttypes:"Html;HtmlChart;Badges"

# With assembly filtering
-assemblyfilters:"+CompoundDocs.*;-*.Tests"

# With class filtering (exclude generated code)
-classfilters:"-*Generated*;-*Migrations*"

# Dark theme
-reporttypes:"Html_Dark;Badges"
```

### Minimum Coverage Threshold Verification

ReportGenerator can also enforce coverage thresholds:

```bash
dotnet tool run reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coveragereport" \
  -reporttypes:"Html;TextSummary" \
  -minimumCoverageThresholds:lineCoverage=100
```

**Note**: Threshold enforcement is already handled by Coverlet in `Directory.Build.props`. This is an alternative verification mechanism.

---

## Dependencies

### Depends On
- Phase 112: Coverlet Configuration (provides Cobertura XML output)

### Blocks
- CI/CD Coverage Publishing Phases (GitHub Actions, GitHub Pages)

---

## Verification Steps

After completing this phase, verify:

1. **Tool installed**: `dotnet tool list` shows `dotnet-reportgenerator-globaltool`
2. **Tool manifest exists**: `.config/dotnet-tools.json` contains ReportGenerator entry
3. **Tool restores**: `dotnet tool restore` succeeds in a fresh clone
4. **Report generates**: Running the report generation command produces output in `coveragereport/`
5. **HTML report accessible**: `coveragereport/index.html` opens in browser and shows coverage data
6. **Badges generated**: SVG badge files exist in `coveragereport/`
7. **Markdown summary generated**: `coveragereport/SummaryGithub.md` exists
8. **gitignore effective**: `git status` does not show `coveragereport/` directory

---

## Notes

- ReportGenerator version should be pinned in the tool manifest for reproducibility
- The `MarkdownSummaryGithub` report type is specifically formatted for GitHub and will be used in CI/CD for PR comments and job summaries
- History tracking (`-historydir`) is optional for local development but will be used in CI/CD for trend visualization
- Badge files can be referenced in README after they are published to GitHub Pages
- For CI/CD integration, see the research document for complete GitHub Actions workflow examples
