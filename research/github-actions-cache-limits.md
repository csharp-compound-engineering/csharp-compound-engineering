# GitHub Actions Cache Limits and Ollama Model Caching Research

**Research Date:** January 2026
**Purpose:** Evaluate feasibility of caching ~5GB Ollama AI models in GitHub Actions CI for a .NET MCP server with PostgreSQL + Ollama integration tests.

---

## Executive Summary

**Verdict: Caching 5GB Ollama models in GitHub Actions is technically feasible but comes with significant constraints and costs. Alternative approaches are recommended for production CI pipelines.**

Key findings:
- GitHub Actions provides 10GB cache per repository by default (expandable with paid plans)
- Individual cache entries can exceed 5GB (no documented single-entry limit)
- Cache eviction occurs after 7 days of non-access or when repository limit is exceeded
- Better alternatives exist: custom Docker images with baked models, S3-backed caching, or self-hosted runners

---

## 1. GitHub Actions Cache Limits

### 1.1 Official Cache Limits

| Limit Type | Default Value | Notes |
|------------|---------------|-------|
| **Total cache per repository** | 10 GB | Shared across all branches and workflows |
| **Cache retention** | 7 days | Evicted if not accessed within 7 days |
| **Single entry limit** | Not explicitly documented | No hard limit found; 10GB total applies |
| **Number of cache entries** | Unlimited | Only total size is constrained |

**Sources:**
- [GitHub Actions Limits Documentation](https://docs.github.com/en/actions/reference/limits)
- [GitHub Actions Cache Size Changelog (Nov 2025)](https://github.blog/changelog/2025-11-20-github-actions-cache-size-can-now-exceed-10-gb-per-repository/)

### 1.2 Cache Eviction Policy

GitHub uses an LRU-like eviction policy:

1. **Time-based eviction:** Caches not accessed within 7 days are automatically removed
2. **Size-based eviction:** When total cache exceeds repository limit, oldest (by last access) caches are evicted first
3. **New cache priority:** GitHub will save new caches even if it triggers eviction of older ones

**Important:** If your CI runs infrequently (less than weekly), model caches will be evicted regardless of size.

### 1.3 Expanded Cache (Paid)

As of November 2025, GitHub allows cache storage beyond 10GB:

| Plan | Included Cache | Expandable | Cost for Additional |
|------|----------------|------------|---------------------|
| Free | 10 GB | No | N/A |
| Pro | 10 GB | Yes | $0.07/GB/month |
| Team | 10 GB | Yes | $0.07/GB/month |
| Enterprise | 10 GB | Yes | $0.07/GB/month |

**Cost estimate for 5GB Ollama cache:** ~$0.35/month additional (if exceeding 10GB total)

**Source:** [GitHub Actions Billing Documentation](https://docs.github.com/billing/managing-billing-for-github-actions/about-billing-for-github-actions)

---

## 2. Large File Handling

### 2.1 Can You Cache 5GB+ Files?

**Yes, technically possible** with caveats:

- The `actions/cache` action has been updated to handle files >2GB (previously had issues)
- Segment download timeout is configurable via `SEGMENT_DOWNLOAD_TIMEOUT_MINS` (default 10 minutes)
- Large caches significantly slow down workflow execution (restore/save times)

**Source:** [actions/cache Repository](https://github.com/actions/cache)

### 2.2 Practical Constraints

| Constraint | Impact on 5GB Cache |
|------------|---------------------|
| Network throughput | ~125 MB/s max; 5GB takes ~40 seconds minimum |
| Cache save/restore time | Adds 1-3 minutes per workflow run |
| Eviction risk | High if CI doesn't run weekly |
| Shared limit | 5GB uses 50% of your repository's cache budget |

### 2.3 What Happens When Cache Exceeds Limits

1. New cache is saved successfully
2. GitHub begins evicting oldest caches (by last access time)
3. Eviction continues until total size is under the limit
4. No workflow failure occurs; older caches simply become unavailable

---

## 3. Ollama Models in CI

### 3.1 Ollama Model Sizes

| Model | Size | Use Case |
|-------|------|----------|
| TinyLlama (1.1B) | ~637 MB | Testing, minimal resource |
| Llama 3.2 3B | ~2 GB | Light testing |
| Llama 3/3.1 8B | ~4-8 GB | Standard testing |
| Llama 3.1 70B | ~40+ GB | Not feasible for CI |

**Recommendation:** Use TinyLlama (~637MB) or Llama 3.2 3B (~2GB) for integration tests unless you specifically need larger model capabilities.

**Source:** [Ollama Model Library](https://ollama.com/library)

### 3.2 Existing GitHub Actions for Ollama

#### Option A: ai-action/ollama-action

```yaml
- uses: ai-action/ollama-action@v2
  with:
    model: tinyllama
    cache: true  # Uses actions/cache internally
```

**Source:** [ollama-action on GitHub Marketplace](https://github.com/marketplace/actions/ollama-action)

#### Option B: ai-action/setup-ollama with Manual Caching

```yaml
- uses: actions/cache@v4
  with:
    path: ~/.ollama
    key: ${{ runner.os }}-ollama-tinyllama

- uses: ai-action/setup-ollama@v1

- run: ollama pull tinyllama
```

**Source:** [setup-ollama Repository](https://github.com/ai-action/setup-ollama)

### 3.3 Pre-built Docker Images with Models

**The Problem:** Ollama downloads models on-demand, extending container startup time significantly.

**Solution:** Build custom Docker images with models pre-baked:

```dockerfile
# Multi-stage build to bake model into image
FROM gerke74/ollama-model-loader as downloader
RUN /ollama-pull tinyllama

FROM ollama/ollama
ENV OLLAMA_HOST "0.0.0.0"
COPY --from=downloader /root/.ollama /root/.ollama
```

**Benefits:**
- No model download during CI
- Consistent model versions
- Faster container startup
- Can be pushed to private registry

**Feature Request:** [GitHub Issue #2161 - Provide Docker images with pre-downloaded models](https://github.com/ollama/ollama/issues/2161)

**Source:** [Preloading Ollama Models - DEV Community](https://dev.to/jensgst/preloading-ollama-models-221k)

### 3.4 Testcontainers Approach

For .NET/C# integration tests, use Testcontainers with model caching:

```csharp
// Conceptual approach - actual API may vary
var ollama = new OllamaContainer("ollama/ollama:latest");
await ollama.StartAsync();
await ollama.ExecAsync("ollama", "pull", "tinyllama");
// After tests, commit container to new image for future runs
```

The Testcontainers Ollama module supports:
- Automatic model pulling
- Container commit for local image caching
- Shared volumes for model persistence across tests

**Sources:**
- [Testcontainers Ollama Module](https://testcontainers.com/modules/ollama/)
- [Ollama - Testcontainers for Java](https://java.testcontainers.org/modules/ollama/)

---

## 4. Alternative Approaches

### 4.1 S3-Backed Caching

Replace `actions/cache` with S3-compatible alternatives for unlimited cache size:

| Solution | Features |
|----------|----------|
| [runs-on/cache](https://runs-on.com/caching/s3-cache-for-github-actions/) | Drop-in replacement, 200+ MiB/s throughput |
| [tespkg/actions-cache](https://github.com/tespkg/actions-cache) | S3 with GitHub fallback |
| [step-security/s3-actions-cache](https://github.com/step-security/s3-actions-cache) | Secure S3 caching |

**Example with runs-on/cache:**

```yaml
- uses: runs-on/cache@v4
  with:
    path: ~/.ollama
    key: ${{ runner.os }}-ollama-models
```

**Benefits:**
- No size limits
- Faster throughput (200-500 MiB/s)
- Global cache sharing across repos

**Costs:** S3 storage costs (~$0.023/GB/month) + data transfer

### 4.2 Self-Hosted Runners

For production CI with GPU requirements:

| Option | Cost | GPU Support |
|--------|------|-------------|
| GitHub GPU Runners | $0.07/min (T4 GPU) | Yes |
| [RunsOn](https://runs-on.com/runners/gpu/) | ~$0.009/min (T4 GPU) | Yes |
| Self-hosted (AWS/GCP) | Variable | Yes |
| On-premise | Hardware cost | Yes |

**GPU Runner Setup:**

```yaml
jobs:
  test:
    runs-on: [self-hosted, gpu]
    steps:
      - name: Run Ollama with GPU
        run: |
          docker run --gpus all -d -p 11434:11434 ollama/ollama
          ollama pull tinyllama
```

**Sources:**
- [GitHub GPU Runners Announcement](https://github.blog/changelog/2024-07-08-github-actions-gpu-hosted-runners-are-now-generally-available/)
- [Run AI models with Ollama in CI](https://actuated.com/blog/ollama-in-github-actions)

### 4.3 External Model Registry

Host models on external storage and download during CI:

```yaml
- name: Download model from S3
  run: |
    aws s3 cp s3://my-bucket/models/tinyllama ~/.ollama/models/
```

**Pros:** Complete control, no cache eviction
**Cons:** Requires S3 setup, download time

---

## 5. Docker Layer Caching

### 5.1 GitHub Actions Docker Cache Backend

Docker Buildx supports GitHub Actions cache as a backend:

```yaml
- name: Set up Docker Buildx
  uses: docker/setup-buildx-action@v3

- name: Build with cache
  uses: docker/build-push-action@v5
  with:
    context: .
    cache-from: type=gha
    cache-to: type=gha,mode=max
```

**Important Notes:**
- Requires Buildx (not default docker driver)
- Cache is scoped to branch by default
- Subject to same 10GB repository limit

**Source:** [Docker Build Cache Documentation](https://docs.docker.com/build/cache/backends/gha/)

### 5.2 Interaction with Ollama Containers

Docker layer caching does **not** help with Ollama model downloads because:
1. Models are downloaded at runtime, not build time
2. Model data is stored in volumes, not layers
3. Each `ollama pull` occurs after container starts

**Solution:** Use custom Docker images with baked models (Section 3.3)

---

## 6. Practical Recommendations

### For Your Use Case: .NET MCP Server with PostgreSQL + Ollama Integration Tests

#### Recommended Approach: Tiered Strategy

**Tier 1: Unit Tests (Fast, No Models)**
```yaml
jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: dotnet test --filter "Category!=Integration"
```

**Tier 2: Integration Tests with Small Model**
```yaml
jobs:
  integration-tests:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:15
        # ... config
    steps:
      - uses: actions/cache@v4
        with:
          path: ~/.ollama
          key: ollama-tinyllama-v1

      - uses: ai-action/ollama-action@v2
        with:
          model: tinyllama  # 637MB, fits easily in cache

      - run: dotnet test --filter "Category=Integration"
```

**Tier 3: Full E2E with Larger Models (Weekly/Manual)**
```yaml
jobs:
  e2e-tests:
    if: github.event_name == 'schedule' || github.event_name == 'workflow_dispatch'
    runs-on: ubuntu-latest
    steps:
      # Use pre-built Docker image with model baked in
      - run: docker pull myregistry/ollama-with-llama3:latest
```

#### Model Size Recommendations

| Test Type | Recommended Model | Size | Cache Viable |
|-----------|-------------------|------|--------------|
| Smoke tests | TinyLlama | 637 MB | Yes |
| Integration tests | Llama 3.2 3B | 2 GB | Yes |
| Full capability tests | Llama 3.1 8B | 4-8 GB | Marginal |
| Production parity | Depends | Varies | Custom image |

#### Cost Summary

| Approach | Monthly Cost Estimate |
|----------|----------------------|
| Cache TinyLlama (~637MB) | Free (within 10GB limit) |
| Cache 2GB model | Free (within 10GB limit) |
| Cache 5GB model | ~$0.35/month if exceeding 10GB |
| S3-backed cache | ~$0.15-0.50/month + transfer |
| Custom Docker registry | ~$5-20/month (depends on provider) |
| Self-hosted GPU runner | Variable (hardware + electricity) |

---

## 7. Final Verdict

### Is Caching 5GB Ollama Models Feasible?

| Criterion | Assessment |
|-----------|------------|
| Technical feasibility | Yes, if within repository limit |
| Practical for daily CI | No, eviction after 7 days inactive |
| Cost-effective | Marginal; alternatives may be better |
| Recommended | No for 5GB; Yes for smaller models |

### Recommended Strategy

1. **Use TinyLlama (~637MB)** for most integration tests
2. **Cache with `actions/cache`** - fits easily in 10GB limit
3. **Build custom Docker image** with larger models for specific tests
4. **Run comprehensive tests weekly** with `workflow_dispatch` or schedule
5. **Consider S3 caching** if cache eviction becomes problematic

### Action Items

1. [ ] Create `ollama-tinyllama` Docker image for CI
2. [ ] Add caching workflow for Ollama models
3. [ ] Separate unit tests from integration tests
4. [ ] Set up scheduled workflow for full E2E tests
5. [ ] Monitor cache hit rates and adjust strategy

---

## References

### Official Documentation
- [GitHub Actions Limits](https://docs.github.com/en/actions/reference/limits)
- [GitHub Actions Billing](https://docs.github.com/billing/managing-billing-for-github-actions/about-billing-for-github-actions)
- [actions/cache Repository](https://github.com/actions/cache)
- [Docker Build Cache with GHA](https://docs.docker.com/build/cache/backends/gha/)

### Ollama Resources
- [Ollama Model Library](https://ollama.com/library)
- [ollama-action](https://github.com/marketplace/actions/ollama-action)
- [setup-ollama](https://github.com/ai-action/setup-ollama)
- [Ollama Docker Issue #2161](https://github.com/ollama/ollama/issues/2161)

### Alternative Approaches
- [RunsOn S3 Cache](https://runs-on.com/caching/s3-cache-for-github-actions/)
- [Actuated: Ollama in GitHub Actions](https://actuated.com/blog/ollama-in-github-actions)
- [Testcontainers Ollama Module](https://testcontainers.com/modules/ollama/)

### Community Discussions
- [Cache Size for Monorepos](https://github.com/orgs/community/discussions/66699)
- [Increase Cache Size Request](https://github.com/orgs/community/discussions/42506)
