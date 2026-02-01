# Phase 122: Docker Integration in CI

> **Status**: NOT_STARTED
> **Effort Estimate**: 3-4 hours
> **Category**: Testing Framework
> **Prerequisites**: Phase 121 (Integration Test Fixtures)

---

## Spec References

This phase implements the Docker integration for CI defined in:

- **spec/testing/ci-cd-pipeline.md** - [GitHub Actions Workflow](../spec/testing/ci-cd-pipeline.md#github-actions-workflow), [Model Download Optimization](../spec/testing/ci-cd-pipeline.md#model-download-optimization)
- **research/github-actions-dotnet-cicd-research.md** - GitHub Actions patterns with Docker containers

---

## Objectives

1. Configure PostgreSQL (pgvector) container setup in CI workflows
2. Configure Ollama container setup in CI workflows
3. Implement model pre-downloading strategy for Ollama
4. Create container health check mechanisms
5. Implement container cleanup procedures
6. Configure GitHub Actions caching for Ollama models
7. Create reusable workflow components for Docker services

---

## Acceptance Criteria

### PostgreSQL Container Setup
- [ ] `pgvector/pgvector:pg16` image pulled in integration and E2E test jobs
- [ ] Container starts successfully with health check verification
- [ ] Database connection string available to test runner
- [ ] Container isolated per workflow run to prevent cross-contamination

### Ollama Container Setup
- [ ] `ollama/ollama:latest` image pulled in integration and E2E test jobs
- [ ] Named volume `ollama-models` created for model persistence within workflow
- [ ] Container starts successfully with health check verification
- [ ] Ollama API endpoint available to test runner
- [ ] Models volume mounted correctly for pre-downloaded models

### Model Pre-downloading
- [ ] CI-optimized models pre-downloaded before test execution:
  - [ ] `nomic-embed-text:v1.5` (~274MB) for embeddings
  - [ ] `tinyllama` (~637MB) for generation
- [ ] Model download occurs in dedicated setup step
- [ ] Volume persists models across workflow jobs
- [ ] Download step completes within reasonable time (~5 minutes)

### Container Health Checks
- [ ] PostgreSQL health check verifies `pg_isready` status
- [ ] Ollama health check verifies API endpoint responsiveness
- [ ] Health checks have appropriate timeout and retry configuration
- [ ] Failed health checks fail the workflow with clear error messages

### Container Cleanup
- [ ] Containers stopped after job completion
- [ ] Volumes cleaned up (ephemeral per-run strategy)
- [ ] No resource leaks between workflow runs
- [ ] Cleanup occurs even on job failure (using `always()` condition)

### GitHub Actions Cache
- [ ] Ollama models cached using `actions/cache@v4`
- [ ] Cache key includes model names and versions
- [ ] Cache restored before model pull attempts
- [ ] Cache size within GitHub's 10GB repository limit

---

## Implementation Notes

### Docker Image Pull Strategy

Create parallel image pulls to reduce setup time:

```yaml
- name: Pull Docker Images
  run: |
    docker pull pgvector/pgvector:pg16 &
    docker pull ollama/ollama:latest &
    wait
```

### PostgreSQL Container Configuration

```yaml
services:
  postgres:
    image: pgvector/pgvector:pg16
    env:
      POSTGRES_USER: test
      POSTGRES_PASSWORD: test
      POSTGRES_DB: compounddocs_test
    ports:
      - 5432:5432
    options: >-
      --health-cmd pg_isready
      --health-interval 10s
      --health-timeout 5s
      --health-retries 5
```

**Alternative (Docker run)**:
```yaml
- name: Start PostgreSQL
  run: |
    docker run -d --name postgres \
      -e POSTGRES_USER=test \
      -e POSTGRES_PASSWORD=test \
      -e POSTGRES_DB=compounddocs_test \
      -p 5432:5432 \
      --health-cmd "pg_isready -U test" \
      --health-interval 10s \
      --health-timeout 5s \
      --health-retries 5 \
      pgvector/pgvector:pg16

- name: Wait for PostgreSQL
  run: |
    timeout 60 bash -c 'until docker exec postgres pg_isready -U test; do sleep 2; done'
```

### Ollama Container with Model Pre-download

```yaml
- name: Cache Ollama Models
  uses: actions/cache@v4
  with:
    path: ~/.ollama
    key: ${{ runner.os }}-ollama-nomic-tinyllama-v1
    restore-keys: |
      ${{ runner.os }}-ollama-

- name: Start Ollama for Model Download
  run: |
    docker run -d --name ollama-setup \
      -v ~/.ollama:/root/.ollama \
      ollama/ollama

- name: Wait for Ollama Ready
  run: |
    timeout 30 bash -c 'until docker exec ollama-setup curl -s http://localhost:11434/api/tags > /dev/null 2>&1; do sleep 2; done'

- name: Pre-download CI Models
  run: |
    docker exec ollama-setup ollama pull nomic-embed-text:v1.5
    docker exec ollama-setup ollama pull tinyllama

- name: Stop Setup Container
  run: docker stop ollama-setup && docker rm ollama-setup
```

### Ollama Service Container for Tests

```yaml
- name: Start Ollama Service
  run: |
    docker run -d --name ollama \
      -v ~/.ollama:/root/.ollama \
      -p 11434:11434 \
      ollama/ollama

- name: Wait for Ollama Service Ready
  run: |
    timeout 30 bash -c 'until curl -s http://localhost:11434/api/tags > /dev/null 2>&1; do sleep 2; done'
```

### Health Check Script

Create `.github/scripts/wait-for-services.sh`:

```bash
#!/bin/bash
set -e

echo "Waiting for PostgreSQL..."
timeout 60 bash -c 'until pg_isready -h localhost -p 5432 -U test; do sleep 2; done'
echo "PostgreSQL is ready!"

echo "Waiting for Ollama..."
timeout 60 bash -c 'until curl -s http://localhost:11434/api/tags > /dev/null 2>&1; do sleep 2; done'
echo "Ollama is ready!"

echo "All services are ready!"
```

### Container Cleanup

```yaml
- name: Cleanup Containers
  if: always()
  run: |
    docker stop postgres ollama 2>/dev/null || true
    docker rm postgres ollama 2>/dev/null || true
```

### Environment Variables for Tests

```yaml
env:
  ConnectionStrings__CompoundDocs: "Host=localhost;Port=5432;Database=compounddocs_test;Username=test;Password=test"
  Ollama__Endpoint: "http://localhost:11434"
  Ollama__EmbeddingModel: "nomic-embed-text:v1.5"
  Ollama__GenerationModel: "tinyllama"
```

### Complete Integration Test Job

```yaml
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

    - name: Cache Ollama Models
      uses: actions/cache@v4
      with:
        path: ~/.ollama
        key: ${{ runner.os }}-ollama-nomic-tinyllama-v1

    - name: Pull Docker Images
      run: |
        docker pull pgvector/pgvector:pg16 &
        docker pull ollama/ollama:latest &
        wait

    - name: Start PostgreSQL
      run: |
        docker run -d --name postgres \
          -e POSTGRES_USER=test \
          -e POSTGRES_PASSWORD=test \
          -e POSTGRES_DB=compounddocs_test \
          -p 5432:5432 \
          pgvector/pgvector:pg16

    - name: Pre-download Ollama Models
      run: |
        docker run -d --name ollama-setup -v ~/.ollama:/root/.ollama ollama/ollama
        sleep 5
        docker exec ollama-setup ollama pull nomic-embed-text:v1.5
        docker exec ollama-setup ollama pull tinyllama
        docker stop ollama-setup && docker rm ollama-setup

    - name: Start Ollama Service
      run: |
        docker run -d --name ollama \
          -v ~/.ollama:/root/.ollama \
          -p 11434:11434 \
          ollama/ollama

    - name: Wait for Services
      run: |
        timeout 60 bash -c 'until docker exec postgres pg_isready -U test; do sleep 2; done'
        timeout 30 bash -c 'until curl -s http://localhost:11434/api/tags > /dev/null 2>&1; do sleep 2; done'

    - name: Restore & Build
      run: dotnet build

    - name: Integration Tests
      run: dotnet test tests/CompoundDocs.IntegrationTests --no-build
      env:
        ConnectionStrings__CompoundDocs: "Host=localhost;Port=5432;Database=compounddocs_test;Username=test;Password=test"
        Ollama__Endpoint: "http://localhost:11434"
        Ollama__EmbeddingModel: "nomic-embed-text:v1.5"
        Ollama__GenerationModel: "tinyllama"

    - name: Cleanup Containers
      if: always()
      run: |
        docker stop postgres ollama 2>/dev/null || true
        docker rm postgres ollama 2>/dev/null || true
```

---

## Dependencies

### Depends On
- **Phase 121**: Integration Test Fixtures (Aspire fixtures that connect to containers)
- **Phase 109**: Test Project Structure (test projects must exist)
- **Phase 002**: Docker Infrastructure (local Docker compose knowledge)

### Blocks
- **Phase 123+**: CI pipeline completion phases
- Integration and E2E tests running in CI

---

## Verification Steps

After completing this phase, verify:

1. **Workflow syntax is valid**:
   ```bash
   # Use actionlint or GitHub's built-in validator
   actionlint .github/workflows/test.yml
   ```

2. **Docker images are available**:
   ```bash
   docker pull pgvector/pgvector:pg16
   docker pull ollama/ollama:latest
   ```

3. **Health check script works locally**:
   ```bash
   chmod +x .github/scripts/wait-for-services.sh
   ./.github/scripts/wait-for-services.sh
   ```

4. **Model cache key is correct**:
   - Verify cache key includes all model names
   - Verify cache path matches Ollama's model storage location

5. **Cleanup runs on failure**:
   - Trigger a test failure and verify containers are still cleaned up

6. **Environment variables propagate**:
   - Verify test runner can read connection strings and Ollama config

---

## Key Technical Decisions

### CI Model Selection

| Model | Size | Production Equivalent | Purpose |
|-------|------|----------------------|---------|
| `nomic-embed-text:v1.5` | ~274MB | `mxbai-embed-large` | Embeddings |
| `tinyllama` | ~637MB | `mistral` | Generation |

**Rationale**: Smaller models reduce CI time while maintaining functional equivalence. Total cache size ~911MB stays well within GitHub's 10GB limit.

### Container Orchestration Strategy

| Approach | Decision | Rationale |
|----------|----------|-----------|
| GitHub Services | Considered | Limited flexibility for health checks |
| Docker run | **Selected** | Full control over container lifecycle |
| Docker Compose | Rejected | Overkill for CI, adds complexity |

### Volume Strategy

| Approach | Decision | Rationale |
|----------|----------|-----------|
| Named volumes | Workflow-scoped | Persist within workflow run |
| Bind mounts | Cache path | Enable GitHub Actions cache integration |
| Ephemeral | Per-job | Fresh state, no contamination |

### Health Check Configuration

| Service | Check Method | Timeout | Retries |
|---------|--------------|---------|---------|
| PostgreSQL | `pg_isready` | 5s | 12 (60s total) |
| Ollama | HTTP `/api/tags` | 5s | 6 (30s total) |

---

## Notes

- The `always()` condition on cleanup ensures containers are removed even when tests fail
- Model pre-download adds ~5 minutes to first run but saves time on cache hits
- Consider using GitHub's `services` syntax for simpler cases, but Docker run provides more control
- The cache key includes model names so cache is invalidated when models change
- If cache misses are frequent, consider using a self-hosted runner with persistent storage
- Testcontainers integration (Phase 121) may override some of this configuration when tests manage their own containers
