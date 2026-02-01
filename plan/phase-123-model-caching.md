# Phase 123: Ollama Model Caching in CI

> **Status**: NOT_STARTED
> **Effort Estimate**: 3-4 hours
> **Category**: Testing Framework
> **Prerequisites**: Phase 122 (Ollama Testcontainer Integration)

---

## Spec References

This phase implements the model caching strategy defined in:

- **spec/testing/ci-cd-pipeline.md** - [Model Download Optimization](../spec/testing/ci-cd-pipeline.md#model-download-optimization) and [Cache Strategy](../spec/testing/ci-cd-pipeline.md#cache-strategy)
- **research/github-actions-cache-limits.md** - Comprehensive analysis of GitHub Actions cache limits, eviction policies, and Ollama-specific caching strategies

---

## Objectives

1. Configure GitHub Actions cache for Ollama models with appropriate key strategy
2. Select and configure smaller CI-specific models that fit within cache limits
3. Implement cache versioning strategy for model updates
4. Configure cache invalidation triggers
5. Document model size considerations and cache budget allocation
6. Optimize model pre-download workflow step

---

## Acceptance Criteria

### Cache Configuration
- [ ] GitHub Actions cache configured for Ollama models in `.github/workflows/test.yml`
- [ ] Cache path set to `~/.ollama` for model storage
- [ ] Cache key includes OS, model names, and version identifier
- [ ] Cache restore-keys configured for partial cache hits

### CI-Specific Models
- [ ] CI uses `nomic-embed-text:v1.5` (~274MB) for embeddings instead of `mxbai-embed-large`
- [ ] CI uses `tinyllama` (~637MB) for generation instead of `mistral`
- [ ] Total CI model cache size under 1GB (well within 10GB limit)
- [ ] Model selection documented in workflow comments

### Cache Key Strategy
- [ ] Primary key format: `${{ runner.os }}-ollama-<models>-v<version>`
- [ ] Example: `Linux-ollama-tinyllama-nomic-v1`
- [ ] Version incremented when models are updated or changed
- [ ] Restore keys fallback to OS-prefix for partial restoration

### Cache Invalidation
- [ ] Version number in cache key allows manual invalidation
- [ ] Workflow documents how to invalidate cache (increment version)
- [ ] GitHub Actions cache eviction policy (7-day inactivity) documented
- [ ] CI runs at least weekly to prevent cache eviction

### Workflow Integration
- [ ] Model caching integrated before Ollama pull step
- [ ] Cache hit skips model download
- [ ] Cache miss triggers model download and cache save
- [ ] Workflow logs indicate cache hit/miss status

---

## Implementation Notes

### GitHub Actions Cache Limits

Per research findings, GitHub Actions has the following constraints:

| Limit Type | Value | Impact |
|------------|-------|--------|
| Total cache per repository | 10 GB | Shared across all branches/workflows |
| Cache retention | 7 days | Evicted if not accessed within 7 days |
| Single entry limit | No hard limit | Only total size applies |

**Budget Allocation**:
- Ollama models (CI): ~1 GB (tinyllama + nomic-embed-text)
- NuGet packages: ~500 MB typical
- Docker layers: ~2-3 GB if using BuildKit GHA cache
- **Remaining**: ~5.5 GB for other caches

### CI Model Selection Rationale

| Production Model | CI Alternative | Size Savings | Compatibility |
|------------------|----------------|--------------|---------------|
| `mxbai-embed-large` (~669MB) | `nomic-embed-text:v1.5` (~274MB) | 59% smaller | Compatible embedding dimensions |
| `mistral` (~4.1GB) | `tinyllama` (~637MB) | 84% smaller | Same API, lower quality acceptable for tests |

**Total Production Models**: ~4.8 GB (exceeds practical cache limit)
**Total CI Models**: ~911 MB (easily fits in cache)

### Workflow Implementation

Update `.github/workflows/test.yml` integration-tests job:

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
      id: cache-ollama
      uses: actions/cache@v4
      with:
        path: ~/.ollama
        key: ${{ runner.os }}-ollama-tinyllama-nomic-v1
        restore-keys: |
          ${{ runner.os }}-ollama-

    - name: Pull Docker Images
      run: |
        docker pull pgvector/pgvector:pg16 &
        docker pull ollama/ollama:latest &
        wait

    - name: Pre-download CI Models
      if: steps.cache-ollama.outputs.cache-hit != 'true'
      run: |
        # Start Ollama server temporarily for model download
        docker run -d --name ollama-download \
          -v ~/.ollama:/root/.ollama \
          ollama/ollama

        # Wait for Ollama to be ready
        sleep 5

        # Pull CI-optimized models (smaller than production)
        docker exec ollama-download ollama pull nomic-embed-text:v1.5
        docker exec ollama-download ollama pull tinyllama

        # Stop the temporary container
        docker stop ollama-download
        docker rm ollama-download

        echo "Models downloaded and cached"

    - name: Verify Cached Models
      if: steps.cache-ollama.outputs.cache-hit == 'true'
      run: |
        echo "Using cached Ollama models"
        ls -la ~/.ollama/models/ || echo "Models directory structure may vary"

    - name: Restore & Build
      run: dotnet build

    - name: Integration Tests
      run: dotnet test tests/CompoundDocs.IntegrationTests --no-build
      env:
        OLLAMA_MODELS_PATH: ~/.ollama
        CI_EMBEDDING_MODEL: nomic-embed-text:v1.5
        CI_GENERATION_MODEL: tinyllama
```

### Environment Variables for CI Model Selection

Tests should respect environment variables for model selection:

```csharp
// In test configuration or fixture
public static class CIModelConfiguration
{
    public static string EmbeddingModel =>
        Environment.GetEnvironmentVariable("CI_EMBEDDING_MODEL")
        ?? "mxbai-embed-large";

    public static string GenerationModel =>
        Environment.GetEnvironmentVariable("CI_GENERATION_MODEL")
        ?? "mistral";
}
```

### Cache Key Versioning Strategy

The cache key version (`v1`, `v2`, etc.) should be incremented when:

1. **Model changes**: Adding, removing, or changing CI model selection
2. **Model version updates**: Pulling newer versions of the same model
3. **Ollama version changes**: Major Ollama updates may change storage format
4. **Corruption recovery**: If cache becomes corrupted

**To invalidate cache**: Change `v1` to `v2` in the workflow file and commit.

### Cache Eviction Prevention

GitHub evicts caches not accessed within 7 days. Strategies to prevent eviction:

1. **Regular CI runs**: Ensure CI runs at least weekly (push or scheduled)
2. **Scheduled workflow** (optional):

```yaml
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  schedule:
    # Run weekly on Sunday at midnight to keep cache warm
    - cron: '0 0 * * 0'
```

### Alternative: Using ai-action/ollama-action

For simpler setup, consider the ollama-action which handles caching internally:

```yaml
- uses: ai-action/ollama-action@v2
  with:
    model: tinyllama
    cache: true

- uses: ai-action/ollama-action@v2
  with:
    model: nomic-embed-text:v1.5
    cache: true
```

**Trade-offs**:
- Simpler configuration
- Less control over cache key strategy
- Each model cached separately (may hit restore-keys inefficiently)

### E2E Tests Configuration

E2E tests may need production-equivalent models for accuracy testing. Options:

1. **Use same CI models**: Faster, but may miss production-specific edge cases
2. **Scheduled full E2E**: Run with production models weekly/manually
3. **Custom Docker image**: Pre-bake production models (see research document)

For this phase, E2E tests will use the same CI models as integration tests.

---

## Dependencies

### Depends On
- **Phase 122**: Ollama Testcontainer Integration (Testcontainers configured for Ollama)
- **Phase 110**: xUnit Test Framework Configuration (test infrastructure in place)

### Blocks
- **Phase 124+**: CI pipeline optimization phases
- Full E2E test coverage with production models (optional future phase)

---

## Verification Steps

After completing this phase, verify:

1. **Cache key generates correctly** (local dry-run):
   ```bash
   echo "Linux-ollama-tinyllama-nomic-v1"
   ```

2. **First workflow run downloads models**:
   - Push change to trigger CI
   - Verify "Pre-download CI Models" step runs
   - Verify models appear in cache (~911 MB total)

3. **Subsequent runs use cache**:
   - Trigger another CI run
   - Verify cache hit message: "Using cached Ollama models"
   - Verify "Pre-download CI Models" step is skipped

4. **Cache size within budget**:
   - Check GitHub Actions cache usage in repository settings
   - Verify Ollama cache < 1 GB

5. **Integration tests pass with CI models**:
   ```bash
   # Locally with CI model env vars
   CI_EMBEDDING_MODEL=nomic-embed-text:v1.5 CI_GENERATION_MODEL=tinyllama \
     dotnet test tests/CompoundDocs.IntegrationTests
   ```

6. **Cache invalidation works**:
   - Change `v1` to `v2` in cache key
   - Verify new cache created on next run

---

## Key Technical Decisions

### Model Selection for CI

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Embedding model | `nomic-embed-text:v1.5` | 274MB, compatible dimensions, MIT license |
| Generation model | `tinyllama` | 637MB, adequate for test assertions, fast inference |
| Cache version format | Semantic (`v1`, `v2`) | Simple, human-readable, easy to increment |

### Cache Strategy

| Aspect | Decision | Rationale |
|--------|----------|-----------|
| Cache path | `~/.ollama` | Standard Ollama model storage location |
| Key includes models | Yes | Ensures cache invalidation on model change |
| Restore-keys fallback | OS-prefix only | Allows partial cache restoration |
| Combined vs separate caches | Combined | Single cache restore is faster than multiple |

### Production Model Parity

| Test Type | Models | Parity Level |
|-----------|--------|--------------|
| Unit tests | Mocked | N/A (no real models) |
| Integration tests | CI models | Functional parity (same API) |
| E2E tests (CI) | CI models | Functional parity |
| E2E tests (scheduled) | Production models | Full parity (future phase) |

---

## Cost Analysis

Per research findings:

| Scenario | Cost |
|----------|------|
| CI models cached (< 1 GB) | Free (within 10 GB limit) |
| Cache eviction (weekly CI) | Free (cache maintained) |
| Expanded cache (if needed) | $0.07/GB/month |

**Estimated monthly cost**: $0 (well within free tier)

---

## Notes

- The `nomic-embed-text` model produces 768-dimension embeddings; ensure pgvector schema supports this
- `tinyllama` uses the same chat completion API as `mistral`, so test code remains unchanged
- If embedding dimension mismatch occurs, consider using `all-minilm` (384 dimensions, ~45 MB) as alternative
- Cache hit/miss metrics should be monitored in GitHub Actions logs for optimization
- Consider adding a periodic cache cleanup workflow if repository approaches 10 GB limit
- The Testcontainers Ollama module may have its own caching; coordinate to avoid duplication
