# Phase 121: GitHub Actions Test Workflow

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Testing Framework
> **Prerequisites**: Phase 109 (Test Project Structure), Phase 112 (E2E Test Fixtures)

---

## Spec References

This phase implements the GitHub Actions test workflow defined in:

- **spec/testing/ci-cd-pipeline.md** - [Pipeline Architecture](../spec/testing/ci-cd-pipeline.md#pipeline-architecture) and [GitHub Actions Workflow](../spec/testing/ci-cd-pipeline.md#github-actions-workflow)
- **spec/testing/ci-cd-pipeline.md** - [Coverage Reporting](../spec/testing/ci-cd-pipeline.md#coverage-reporting)
- **spec/testing/ci-cd-pipeline.md** - [Failure Handling](../spec/testing/ci-cd-pipeline.md#failure-handling)
- **research/github-actions-dotnet-cicd-research.md** - GitHub Actions patterns for .NET CI/CD

---

## Objectives

1. Create `.github/workflows/test.yml` workflow file
2. Configure three-stage execution pipeline (Unit -> Integration -> E2E)
3. Set up .NET 10.0 SDK configuration
4. Configure stage dependencies with `needs` clauses
5. Set job-level and step-level timeouts
6. Configure Docker image pulls for PostgreSQL and Ollama
7. Pre-download Ollama models to named volume
8. Configure Coverlet coverage collection for unit tests
9. Set up ReportGenerator for coverage visualization
10. Add coverage report to GitHub job summary
11. Upload coverage artifacts for downstream consumption
12. Enforce 100% coverage threshold

---

## Acceptance Criteria

- [ ] `.github/workflows/` directory exists
- [ ] `test.yml` workflow file created with proper structure
- [ ] Workflow triggers configured:
  - [ ] `push` to `main` branch
  - [ ] `pull_request` targeting `main` branch
- [ ] Three-stage job pipeline:
  - [ ] `unit-tests` job (no dependencies)
  - [ ] `integration-tests` job (depends on `unit-tests`)
  - [ ] `e2e-tests` job (depends on `integration-tests`)
- [ ] .NET 10.0.x SDK setup in all jobs
- [ ] Timeout configuration:
  - [ ] `unit-tests`: 5 minutes (default job timeout sufficient)
  - [ ] `integration-tests`: 30 minutes job timeout
  - [ ] `e2e-tests`: 30 minutes job timeout, 15 minutes step timeout
- [ ] Docker integration:
  - [ ] Parallel pull of `pgvector/pgvector:pg16` and `ollama/ollama:latest`
  - [ ] Ollama model pre-download to named volume
- [ ] Coverage configuration:
  - [ ] Coverlet MSBuild integration with Cobertura output
  - [ ] ReportGenerator HTML report generation
  - [ ] Coverage summary added to `$GITHUB_STEP_SUMMARY`
  - [ ] Coverage artifact upload
- [ ] Environment variable configuration:
  - [ ] `OLLAMA_MODELS_VOLUME` for integration tests
- [ ] Test failure reporting:
  - [ ] xUnit TRX logger output
  - [ ] Non-zero exit on threshold failure

---

## Implementation Notes

### Directory Structure

```
.github/
└── workflows/
    └── test.yml
```

### Complete test.yml Workflow

Create `.github/workflows/test.yml`:

```yaml
name: Test

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

permissions:
  contents: read

env:
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE: true
  DOTNET_NOLOGO: true

jobs:
  unit-tests:
    name: Unit Tests
    runs-on: ubuntu-latest
    timeout-minutes: 10
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Unit Tests with Coverage
        run: |
          dotnet test tests/CompoundDocs.Tests --no-build --configuration Release \
            --logger "trx;LogFileName=unit-test-results.trx" \
            /p:CollectCoverage=true \
            /p:CoverletOutputFormat=cobertura \
            /p:CoverletOutput=./coverage/unit/ \
            /p:Threshold=100 \
            /p:ThresholdType=line,branch,method \
            /p:ThresholdStat=total

      - name: Generate Coverage Report
        if: always()
        uses: danielpalme/ReportGenerator-GitHub-Action@5
        with:
          reports: '**/coverage/unit/coverage.cobertura.xml'
          targetdir: 'coveragereport'
          reporttypes: 'Html;Badges;MarkdownSummaryGithub'
          title: 'Unit Test Coverage Report'
          tag: '${{ github.run_number }}'

      - name: Add Coverage to Job Summary
        if: always()
        run: |
          if [ -f coveragereport/SummaryGithub.md ]; then
            cat coveragereport/SummaryGithub.md >> $GITHUB_STEP_SUMMARY
          fi

      - name: Upload Coverage Report
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: unit-coverage-report
          path: coveragereport/
          retention-days: 30

      - name: Upload Coverage Data
        uses: actions/upload-artifact@v4
        with:
          name: unit-coverage
          path: ./coverage/unit/
          retention-days: 7

      - name: Upload Test Results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: unit-test-results
          path: '**/unit-test-results.trx'
          retention-days: 7

  integration-tests:
    name: Integration Tests
    runs-on: ubuntu-latest
    needs: unit-tests
    timeout-minutes: 30
    steps:
      - name: Checkout
        uses: actions/checkout@v4

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
          sleep 5  # Allow Ollama to initialize
          docker exec ollama-setup ollama pull mxbai-embed-large
          docker exec ollama-setup ollama pull mistral
          docker stop ollama-setup
          docker rm ollama-setup

      - name: Restore & Build
        run: dotnet build --configuration Release

      - name: Integration Tests
        run: |
          dotnet test tests/CompoundDocs.IntegrationTests --no-build --configuration Release \
            --logger "trx;LogFileName=integration-test-results.trx"
        env:
          OLLAMA_MODELS_VOLUME: ollama-models

      - name: Upload Test Results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: integration-test-results
          path: '**/integration-test-results.trx'
          retention-days: 7

  e2e-tests:
    name: E2E Tests
    runs-on: ubuntu-latest
    needs: integration-tests
    timeout-minutes: 30
    steps:
      - name: Checkout
        uses: actions/checkout@v4

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
          sleep 5
          docker exec ollama-setup ollama pull mxbai-embed-large
          docker exec ollama-setup ollama pull mistral
          docker stop ollama-setup
          docker rm ollama-setup

      - name: Restore & Build
        run: dotnet build --configuration Release

      - name: E2E Tests
        run: |
          dotnet test tests/CompoundDocs.E2ETests --no-build --configuration Release \
            --logger "trx;LogFileName=e2e-test-results.trx"
        timeout-minutes: 15
        env:
          OLLAMA_MODELS_VOLUME: ollama-models

      - name: Upload Test Results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: e2e-test-results
          path: '**/e2e-test-results.trx'
          retention-days: 7
```

### Stage Dependencies and Timeouts

The pipeline executes sequentially with explicit dependencies:

```
unit-tests (10 min)
    │
    ▼
integration-tests (30 min) ──► needs: unit-tests
    │
    ▼
e2e-tests (30 min) ──► needs: integration-tests
```

**Timeout Configuration**:

| Stage | Job Timeout | Step Timeout | Rationale |
|-------|-------------|--------------|-----------|
| Unit Tests | 10 min | N/A | Fast isolated tests, coverage generation |
| Integration Tests | 30 min | N/A | Docker startup, model loading |
| E2E Tests | 30 min | 15 min | Full workflow scenarios with Ollama |

### Coverage Threshold Enforcement

Coverage thresholds are enforced via Coverlet MSBuild properties:

```bash
/p:Threshold=100
/p:ThresholdType=line,branch,method
/p:ThresholdStat=total
```

**Failure Behavior**:
- When coverage drops below 100%, Coverlet returns non-zero exit code
- This fails the `unit-tests` job
- Subsequent jobs (`integration-tests`, `e2e-tests`) are skipped
- PR cannot be merged until coverage is restored

**Example Failure Message**:
```
Coverlet: Threshold not met. Expected 100% line coverage, got 95.2%.
```

### Docker Image Strategy

Both PostgreSQL and Ollama images are pulled in parallel to minimize setup time:

```yaml
- name: Pull Docker Images
  run: |
    docker pull pgvector/pgvector:pg16 &
    docker pull ollama/ollama:latest &
    wait
```

The `pgvector/pgvector:pg16` image includes:
- PostgreSQL 16
- pgvector extension pre-installed
- Required for HNSW indexing in vector search

### Ollama Model Pre-download

Models are pre-downloaded to a named Docker volume that persists across job steps:

```yaml
- name: Pre-download Ollama Models
  run: |
    docker run -d --name ollama-setup -v ollama-models:/root/.ollama ollama/ollama
    sleep 5  # Allow Ollama to initialize
    docker exec ollama-setup ollama pull mxbai-embed-large
    docker exec ollama-setup ollama pull mistral
    docker stop ollama-setup
    docker rm ollama-setup
```

**Models Downloaded**:

| Model | Size | Purpose |
|-------|------|---------|
| `mxbai-embed-large` | ~670MB | Embedding generation (1024 dimensions) |
| `mistral` | ~4GB | RAG generation responses |

**Note**: GitHub Actions runners have 10GB cache limit per repository. Consider using smaller models (`nomic-embed-text:v1.5`, `tinyllama`) for faster CI in future optimization phase.

### Test Failure Reporting

All test jobs output TRX format results for detailed failure reporting:

```yaml
--logger "trx;LogFileName=unit-test-results.trx"
```

Results are uploaded as artifacts for post-run analysis:

```yaml
- name: Upload Test Results
  if: always()
  uses: actions/upload-artifact@v4
  with:
    name: unit-test-results
    path: '**/unit-test-results.trx'
```

The `if: always()` condition ensures results are uploaded even when tests fail.

### Coverage Report Generation

ReportGenerator creates multiple output formats:

```yaml
- name: Generate Coverage Report
  uses: danielpalme/ReportGenerator-GitHub-Action@5
  with:
    reports: '**/coverage/unit/coverage.cobertura.xml'
    targetdir: 'coveragereport'
    reporttypes: 'Html;Badges;MarkdownSummaryGithub'
```

| Report Type | Purpose |
|-------------|---------|
| `Html` | Detailed interactive coverage report |
| `Badges` | Coverage badge images |
| `MarkdownSummaryGithub` | GitHub job summary integration |

### GitHub Job Summary Integration

Coverage summary is appended to the workflow run summary:

```yaml
- name: Add Coverage to Job Summary
  if: always()
  run: |
    if [ -f coveragereport/SummaryGithub.md ]; then
      cat coveragereport/SummaryGithub.md >> $GITHUB_STEP_SUMMARY
    fi
```

This displays coverage metrics directly in the GitHub Actions UI without downloading artifacts.

---

## Dependencies

### Depends On
- **Phase 109**: Test Project Structure (test projects must exist)
- **Phase 112**: E2E Test Fixtures (E2E infrastructure for workflow execution)

### Blocks
- **Phase 122**: Release Workflow (requires test workflow as precursor)
- **Phase 123**: Coverage Publishing to GitHub Pages

---

## Verification Steps

After completing this phase, verify:

1. **Workflow file exists and is valid YAML**:
   ```bash
   cat .github/workflows/test.yml
   yamllint .github/workflows/test.yml
   ```

2. **Workflow triggers correctly** (push a test commit):
   ```bash
   git add .github/workflows/test.yml
   git commit -m "test: add test workflow"
   git push
   ```

3. **GitHub Actions tab shows workflow**:
   - Navigate to repository > Actions
   - Verify "Test" workflow appears
   - Verify three jobs visible in workflow visualization

4. **Stage dependencies enforced**:
   - Verify `integration-tests` waits for `unit-tests`
   - Verify `e2e-tests` waits for `integration-tests`

5. **Coverage report appears in job summary**:
   - Click on workflow run
   - Check Summary section for coverage table

6. **Artifacts uploaded**:
   - Click on workflow run
   - Verify `unit-coverage-report`, `unit-coverage`, test results artifacts exist

7. **Threshold enforcement works** (temporarily break coverage):
   - Add untested code
   - Push and verify workflow fails with threshold message

---

## Configuration Options

### Alternative: Smaller CI Models

For faster CI execution (at cost of different embedding behavior), use smaller models:

```yaml
- name: Pre-download CI Models
  run: |
    docker run -d --name ollama-setup -v ollama-models:/root/.ollama ollama/ollama
    sleep 5
    docker exec ollama-setup ollama pull nomic-embed-text:v1.5  # 274MB
    docker exec ollama-setup ollama pull tinyllama              # 637MB
    docker stop ollama-setup
    docker rm ollama-setup
```

Add environment variables to test steps:
```yaml
env:
  CDOCS_EMBEDDING_MODEL: nomic-embed-text:v1.5
  CDOCS_GENERATION_MODEL: tinyllama
```

**Trade-offs**:
- `nomic-embed-text:v1.5` outputs 768 dimensions vs. 1024 for `mxbai-embed-large`
- `tinyllama` produces lower quality RAG responses than `mistral`
- Tests must account for different embedding dimensions

### Alternative: Parallel Test Execution

For repositories with independent test suites, stages can run in parallel:

```yaml
jobs:
  unit-tests:
    # ... (no needs clause)

  integration-tests:
    # Remove: needs: unit-tests
    # ... runs in parallel with unit-tests

  e2e-tests:
    needs: [unit-tests, integration-tests]  # Fan-in after both complete
```

This is NOT recommended for this project because:
- Integration tests may catch issues unit tests miss
- E2E tests require stable integration test fixtures
- Sequential execution provides faster feedback on fundamental failures

---

## Notes

- GitHub Actions runners include Docker pre-installed; no setup action needed
- The `actions/setup-dotnet@v4` action supports .NET 10.0.x via version matching
- Named Docker volumes (`ollama-models`) persist within a single workflow run but NOT across runs
- Consider adding `actions/cache@v4` for NuGet packages in future optimization phase
- The `if: always()` pattern ensures cleanup/upload steps run regardless of test success
- TRX format is compatible with Azure DevOps test result viewers and VS Test Explorer
- Coverage HTML reports can be published to GitHub Pages (see Phase 123)
