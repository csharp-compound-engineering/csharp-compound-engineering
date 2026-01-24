# CI/CD Pipeline Specification

> **Status**: [DRAFT]
> **Last Updated**: 2025-01-23
> **Parent**: [spec/testing.md](../testing.md)

---

## Overview

This document specifies the GitHub Actions CI/CD pipeline for automated testing of the `csharp-compounding-docs` plugin.

> **Background**: Comprehensive patterns for GitHub Actions workflows with .NET including matrix builds, Docker integration, caching strategies, and release automation. See [GitHub Actions CI/CD Research](../../research/github-actions-dotnet-cicd-research.md).

---

## Pipeline Architecture

### Stages

| Stage | Tests | Timeout | Dependencies |
|-------|-------|---------|--------------|
| Unit Tests | `CompoundDocs.Tests` | 5 min | None |
| Integration Tests | `CompoundDocs.IntegrationTests` | 30 min | Unit Tests |
| E2E Tests | `CompoundDocs.E2ETests` | 30 min | Integration Tests |

> **Background**: Test organization patterns including xUnit Traits for category-based filtering (`--filter "Category=Integration"`), shared fixtures with `IClassFixture` and `ICollectionFixture`, and async lifecycle management. See [Unit Testing Research](../../research/unit-testing-xunit-moq-shouldly.md).

### Trigger Events

```yaml
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
```

---

## GitHub Actions Workflow

**`.github/workflows/test.yml`**:

```yaml
name: Test

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore

      - name: Unit Tests with Coverage
        run: |
          dotnet test tests/CompoundDocs.Tests --no-build \
            /p:CollectCoverage=true \
            /p:CoverletOutputFormat=cobertura \
            /p:CoverletOutput=./coverage/unit/

      - name: Upload Coverage
        uses: actions/upload-artifact@v4
        with:
          name: unit-coverage
          path: ./coverage/unit/

  integration-tests:
    runs-on: ubuntu-latest
    needs: unit-tests
    timeout-minutes: 30
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Pull Docker Images
        run: |
          docker pull pgvector/pgvector:pg16 &
          docker pull ollama/ollama:latest &
          wait

      - name: Pre-download Ollama Models
        run: |
          docker run -d --name ollama-setup -v ollama-models:/root/.ollama ollama/ollama
          docker exec ollama-setup ollama pull mxbai-embed-large
          docker exec ollama-setup ollama pull mistral
          docker stop ollama-setup

      - name: Restore & Build
        run: dotnet build

      - name: Integration Tests
        run: dotnet test tests/CompoundDocs.IntegrationTests --no-build
        env:
          OLLAMA_MODELS_VOLUME: ollama-models

  e2e-tests:
    runs-on: ubuntu-latest
    needs: integration-tests
    timeout-minutes: 30
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Pull Docker Images
        run: |
          docker pull pgvector/pgvector:pg16 &
          docker pull ollama/ollama:latest &
          wait

      - name: Restore & Build
        run: dotnet build

      - name: E2E Tests
        run: dotnet test tests/CompoundDocs.E2ETests --no-build
        timeout-minutes: 15
```

> **Background**: .NET Aspire `DistributedApplicationTestingBuilder` for integration testing with PostgreSQL and Ollama containers, including MCP client patterns and database isolation strategies. See [Aspire Testing Research](../../research/aspire-testing-mcp-client.md).

---

## Model Download Optimization

Ollama model downloads can be slow (several GB). The pipeline pre-downloads models to a named volume that persists across workflow steps.

### First Run Behavior

On the first workflow run or when cache is cleared:
1. Docker volume `ollama-models` is created
2. Models are pulled (~2-5 minutes depending on network)
3. Volume persists for subsequent jobs in the same workflow

### Cache Strategy

GitHub Actions has a 10GB cache limit per repository with 7-day eviction. For CI, use smaller models that fit within cache limits.

**Recommended CI Models**:
| Model | Size | Use Case |
|-------|------|----------|
| `nomic-embed-text:v1.5` | ~274MB | Embeddings (CI alternative to mxbai-embed-large) |
| `tinyllama` | ~637MB | Generation (CI alternative to mistral) |

```yaml
- name: Cache Ollama Models
  uses: actions/cache@v4
  with:
    path: ~/.ollama
    key: ${{ runner.os }}-ollama-tinyllama-nomic-v1

- name: Pre-download CI Models
  run: |
    ollama pull nomic-embed-text:v1.5
    ollama pull tinyllama
```

**Note**: Production models (`mxbai-embed-large`, `mistral`) are used locally and in E2E tests. CI uses smaller equivalents for speed. See [research/github-actions-cache-limits.md](../../research/github-actions-cache-limits.md) for detailed analysis.

---

## Coverage Reporting

> **Background**: Coverlet MSBuild integration for 100% coverage threshold enforcement with `<Threshold>100</Threshold>`, exclusion patterns via `ExcludeByAttribute` and `ExcludeByFile`, and Cobertura output format. See [Coverlet Coverage Research](../../research/coverlet-code-coverage-research.md).

### ReportGenerator

Coverage visualization uses [ReportGenerator](https://github.com/danielpalme/ReportGenerator) to generate HTML reports from Cobertura XML.

```yaml
- name: Generate Coverage Report
  uses: danielpalme/ReportGenerator-GitHub-Action@5
  with:
    reports: '**/coverage.cobertura.xml'
    targetdir: 'coveragereport'
    reporttypes: 'Html;Badges;MarkdownSummaryGithub'
    title: 'Code Coverage Report'
    tag: '${{ github.run_number }}'

- name: Add Coverage to Job Summary
  run: cat coveragereport/SummaryGithub.md >> $GITHUB_STEP_SUMMARY

- name: Upload Coverage Artifact
  uses: actions/upload-artifact@v4
  with:
    name: coverage-report
    path: coveragereport
```

### GitHub Pages Publishing (Per Release)

Coverage HTML is published to GitHub Pages on each release:

```yaml
# In release.yml, after tests pass
- name: Deploy Coverage to GitHub Pages
  run: |
    VERSION=${{ github.event.release.tag_name }}
    mkdir -p gh-pages/coverage/$VERSION
    cp -r coveragereport/* gh-pages/coverage/$VERSION/

    # Update latest symlink
    cd gh-pages/coverage
    rm -rf latest
    cp -r $VERSION latest
```

**URL Structure**:
- Latest: `https://{org}.github.io/{repo}/coverage/latest/`
- Per-version: `https://{org}.github.io/{repo}/coverage/v1.0.0/`

See [research/reportgenerator-coverage-visualization.md](../../research/reportgenerator-coverage-visualization.md) for complete workflow.

---

## Failure Handling

### Threshold Failures

When coverage drops below 100%, Coverlet exits with non-zero code, failing the job.

**Example failure message**:
```
Threshold not met. Expected 100% line coverage, got 95.2%.
```

### Test Failures

Failed tests produce standard xUnit output with test names and failure reasons.

### Timeout Handling

- E2E tests have 15-minute timeout per job
- Individual tests can specify `[Fact(Timeout = 120000)]` for 2-minute limits

---

## Local Pipeline Simulation

Developers can simulate the CI pipeline locally:

```bash
# Unit tests
dotnet test tests/CompoundDocs.Tests /p:CollectCoverage=true

# Integration tests (requires Docker)
dotnet test tests/CompoundDocs.IntegrationTests

# E2E tests (requires Docker)
dotnet test tests/CompoundDocs.E2ETests
```

---

## Release Workflow (semantic-release)

The plugin uses [semantic-release](https://github.com/semantic-release/semantic-release) for fully automated versioning and releases based on commit message conventions.

### Version Determination

| Commit Type | Version Bump | Example |
|-------------|--------------|---------|
| `fix:` | PATCH (0.0.X) | `fix(auth): resolve token expiration` |
| `feat:` | MINOR (0.X.0) | `feat(query): add fuzzy search support` |
| `feat!:` or `BREAKING CHANGE` | MAJOR (X.0.0) | `feat!: change tool response format` |
| `docs:`, `chore:`, `test:` | No release | `docs: update README` |

### Files Updated on Release

semantic-release automatically updates version numbers in:
- `Directory.Build.props` — .NET assembly version
- `.claude-plugin/plugin.json` — Claude Code plugin manifest version
- `CHANGELOG.md` — Auto-generated from commits

### Release Workflow

**`.github/workflows/release.yml`**:

```yaml
name: Release

on:
  push:
    branches: [main]

permissions:
  contents: read

jobs:
  release:
    name: Release
    runs-on: ubuntu-latest
    permissions:
      contents: write
      issues: write
      pull-requests: write

    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          fetch-depth: 0
          persist-credentials: false

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: 'lts/*'

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install dependencies
        run: npm clean-install

      - name: Build .NET project
        run: dotnet build --configuration Release

      - name: Run tests
        run: dotnet test --configuration Release --no-build

      - name: Release
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: npx semantic-release
```

### semantic-release Configuration

**`.releaserc.json`**:

```json
{
  "branches": ["main"],
  "tagFormat": "v${version}",
  "plugins": [
    "@semantic-release/commit-analyzer",
    "@semantic-release/release-notes-generator",
    [
      "@semantic-release/changelog",
      { "changelogFile": "CHANGELOG.md" }
    ],
    [
      "semantic-release-dotnet",
      { "paths": ["Directory.Build.props"] }
    ],
    [
      "@google/semantic-release-replace-plugin",
      {
        "replacements": [
          {
            "files": [".claude-plugin/plugin.json"],
            "from": "\"version\": \".*\"",
            "to": "\"version\": \"${nextRelease.version}\""
          }
        ]
      }
    ],
    [
      "@semantic-release/git",
      {
        "assets": [
          "CHANGELOG.md",
          "Directory.Build.props",
          ".claude-plugin/plugin.json"
        ],
        "message": "chore(release): ${nextRelease.version} [skip ci]"
      }
    ],
    "@semantic-release/github"
  ]
}
```

### Node.js Dependencies

**`package.json`** (root):

```json
{
  "name": "csharp-compounding-docs",
  "version": "0.0.0-development",
  "private": true,
  "devDependencies": {
    "semantic-release": "^24.0.0",
    "@semantic-release/changelog": "^6.0.3",
    "@semantic-release/git": "^10.0.1",
    "@semantic-release/github": "^10.0.0",
    "@google/semantic-release-replace-plugin": "^1.2.7",
    "semantic-release-dotnet": "^3.0.0"
  }
}
```

### Commit Message Convention

The project follows [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

**Examples**:
```bash
# Bug fix (PATCH release)
git commit -m "fix(mcp): handle empty query gracefully"

# New feature (MINOR release)
git commit -m "feat(skills): add /cdocs:export skill"

# Breaking change (MAJOR release)
git commit -m "feat(api)!: change tool response schema

BREAKING CHANGE: All tool responses now include metadata field"
```

### Rationale

- **Eliminates manual versioning** — Version determined automatically from commits
- **Consistent changelogs** — Generated from commit history
- **Enforces discipline** — Developers must write meaningful commit messages
- **Single source of truth** — Version flows from Git tags to all version files

---

## Change Log

| Date | Changes |
|------|---------|
| 2025-01-23 | Added semantic-release workflow for automated versioning |
| 2025-01-23 | Initial draft |
