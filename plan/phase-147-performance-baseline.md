# Phase 147: Performance Baseline Establishment

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Testing Framework
> **Prerequisites**: Phase 143 (BenchmarkDotNet Setup)

---

## Spec References

This phase implements performance baseline documentation defined in:

- **spec/mcp-server/ollama-integration.md** - [Timeout Configuration](../spec/mcp-server/ollama-integration.md#connection) (5-minute timeout for embeddings)
- **spec/mcp-server/ollama-integration.md** - [Rate Limiting](../spec/mcp-server/ollama-integration.md#rate-limiting) (hardware-specific configurations)
- **spec/testing.md** - [Deferred for Post-MVP](../spec/testing.md#deferred-for-post-mvp) (performance benchmarks deferred)

---

## Objectives

1. Create performance benchmark classes for core operations using BenchmarkDotNet
2. Establish baseline measurements for RAG query response times
3. Establish baseline measurements for embedding generation times
4. Establish baseline measurements for document indexing throughput
5. Establish baseline measurements for vector search latency
6. Document baseline values without enforcing thresholds for MVP
7. Create infrastructure for future performance regression testing

---

## Acceptance Criteria

- [ ] RAG Query Benchmarks:
  - [ ] `RagQueryBenchmarks.cs` created in benchmark project
  - [ ] Measures end-to-end RAG query time (embedding + search + synthesis)
  - [ ] Tests with varying context sizes (small, medium, large)
  - [ ] Baseline values documented in markdown report
- [ ] Embedding Generation Benchmarks:
  - [ ] `EmbeddingBenchmarks.cs` created in benchmark project
  - [ ] Measures single document embedding generation time
  - [ ] Tests with varying document sizes (100 chars, 1KB, 10KB)
  - [ ] Measures batch embedding generation throughput
  - [ ] Baseline values documented in markdown report
- [ ] Indexing Throughput Benchmarks:
  - [ ] `IndexingBenchmarks.cs` created in benchmark project
  - [ ] Measures documents indexed per second
  - [ ] Tests with typical compound document sizes
  - [ ] Measures chunking + embedding + storage pipeline
  - [ ] Baseline values documented in markdown report
- [ ] Search Latency Benchmarks:
  - [ ] `VectorSearchBenchmarks.cs` created in benchmark project
  - [ ] Measures vector similarity search time (excluding embedding)
  - [ ] Tests with varying database sizes (100, 1000, 10000 documents)
  - [ ] Tests with different top_k values (5, 10, 20)
  - [ ] Baseline values documented in markdown report
- [ ] Documentation:
  - [ ] `docs/performance-baselines.md` created with all baseline values
  - [ ] Hardware specifications documented for baseline measurements
  - [ ] Clear disclaimer that baselines are informational, not enforced

---

## Implementation Notes

### Benchmark Project Structure

Add benchmark files to the existing benchmark project:

```
benchmarks/
└── CompoundDocs.Benchmarks/
    ├── RagQueryBenchmarks.cs
    ├── EmbeddingBenchmarks.cs
    ├── IndexingBenchmarks.cs
    ├── VectorSearchBenchmarks.cs
    └── BenchmarkConfig.cs
```

### RagQueryBenchmarks.cs

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;

namespace CompoundDocs.Benchmarks;

/// <summary>
/// Benchmarks for end-to-end RAG query performance.
/// These establish baseline measurements; thresholds are not enforced for MVP.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(BenchmarkConfig))]
public class RagQueryBenchmarks
{
    private IRagService _ragService = null!;
    private string _smallQuery = null!;
    private string _mediumQuery = null!;
    private string _largeQuery = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        // Initialize with real Ollama connection for accurate measurements
        // Requires Ollama running locally with mxbai-embed-large and mistral models
        _ragService = CreateRagService();

        _smallQuery = "What is the purpose of this class?";
        _mediumQuery = "Explain the architecture of the document indexing system and how it handles concurrent updates.";
        _largeQuery = "Provide a comprehensive overview of the RAG pipeline including embedding generation, vector storage, similarity search, and response synthesis. Include details about error handling and retry policies.";

        // Warm up the models
        await _ragService.QueryAsync(_smallQuery, topK: 5, CancellationToken.None);
    }

    [Benchmark(Baseline = true)]
    public async Task<string> SmallQuery_Top5()
    {
        return await _ragService.QueryAsync(_smallQuery, topK: 5, CancellationToken.None);
    }

    [Benchmark]
    public async Task<string> MediumQuery_Top10()
    {
        return await _ragService.QueryAsync(_mediumQuery, topK: 10, CancellationToken.None);
    }

    [Benchmark]
    public async Task<string> LargeQuery_Top20()
    {
        return await _ragService.QueryAsync(_largeQuery, topK: 20, CancellationToken.None);
    }

    private IRagService CreateRagService()
    {
        // Implementation to create real service with Ollama connection
        throw new NotImplementedException("Implement with actual service creation");
    }
}
```

### EmbeddingBenchmarks.cs

```csharp
using BenchmarkDotNet.Attributes;

namespace CompoundDocs.Benchmarks;

/// <summary>
/// Benchmarks for embedding generation performance.
/// Measures Ollama mxbai-embed-large embedding times for various document sizes.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(BenchmarkConfig))]
public class EmbeddingBenchmarks
{
    private IEmbeddingService _embeddingService = null!;
    private string _smallContent = null!;   // ~100 chars
    private string _mediumContent = null!;  // ~1KB
    private string _largeContent = null!;   // ~10KB
    private string[] _batchContent = null!; // 10 medium documents

    [GlobalSetup]
    public async Task Setup()
    {
        _embeddingService = CreateEmbeddingService();

        _smallContent = new string('x', 100);
        _mediumContent = new string('x', 1024);
        _largeContent = new string('x', 10240);
        _batchContent = Enumerable.Range(0, 10).Select(_ => _mediumContent).ToArray();

        // Warm up
        await _embeddingService.GenerateAsync(_smallContent, CancellationToken.None);
    }

    [Benchmark(Baseline = true)]
    public async Task<ReadOnlyMemory<float>> SmallDocument_100Chars()
    {
        return await _embeddingService.GenerateAsync(_smallContent, CancellationToken.None);
    }

    [Benchmark]
    public async Task<ReadOnlyMemory<float>> MediumDocument_1KB()
    {
        return await _embeddingService.GenerateAsync(_mediumContent, CancellationToken.None);
    }

    [Benchmark]
    public async Task<ReadOnlyMemory<float>> LargeDocument_10KB()
    {
        return await _embeddingService.GenerateAsync(_largeContent, CancellationToken.None);
    }

    [Benchmark]
    public async Task<ReadOnlyMemory<float>[]> BatchEmbedding_10Documents()
    {
        var tasks = _batchContent.Select(c => _embeddingService.GenerateAsync(c, CancellationToken.None));
        return await Task.WhenAll(tasks);
    }

    private IEmbeddingService CreateEmbeddingService()
    {
        throw new NotImplementedException("Implement with actual service creation");
    }
}
```

### IndexingBenchmarks.cs

```csharp
using BenchmarkDotNet.Attributes;

namespace CompoundDocs.Benchmarks;

/// <summary>
/// Benchmarks for document indexing throughput.
/// Measures the complete pipeline: chunking, embedding, and storage.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(BenchmarkConfig))]
public class IndexingBenchmarks
{
    private IIndexingService _indexingService = null!;
    private string _singleDocument = null!;
    private string[] _batchDocuments = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _indexingService = CreateIndexingService();

        // Typical compound document size (~5KB with frontmatter and content)
        _singleDocument = GenerateCompoundDocument(5120);
        _batchDocuments = Enumerable.Range(0, 10)
            .Select(_ => GenerateCompoundDocument(5120))
            .ToArray();

        // Warm up
        await _indexingService.IndexAsync("/test/warmup.md", _singleDocument, CancellationToken.None);
    }

    [Benchmark(Baseline = true)]
    public async Task IndexSingleDocument()
    {
        await _indexingService.IndexAsync($"/test/{Guid.NewGuid()}.md", _singleDocument, CancellationToken.None);
    }

    [Benchmark]
    [Arguments(5)]
    [Arguments(10)]
    public async Task IndexBatchDocuments(int count)
    {
        var tasks = _batchDocuments.Take(count)
            .Select((doc, i) => _indexingService.IndexAsync($"/test/batch-{Guid.NewGuid()}-{i}.md", doc, CancellationToken.None));
        await Task.WhenAll(tasks);
    }

    private string GenerateCompoundDocument(int size)
    {
        // Generate realistic compound document with frontmatter
        return $"""
            ---
            title: Test Document
            status: DRAFT
            parent: ../parent.md
            ---

            # Test Content

            {new string('x', size - 100)}
            """;
    }

    private IIndexingService CreateIndexingService()
    {
        throw new NotImplementedException("Implement with actual service creation");
    }
}
```

### VectorSearchBenchmarks.cs

```csharp
using BenchmarkDotNet.Attributes;

namespace CompoundDocs.Benchmarks;

/// <summary>
/// Benchmarks for vector similarity search performance.
/// Measures pgvector HNSW index search times (excluding embedding generation).
/// </summary>
[MemoryDiagnoser]
[Config(typeof(BenchmarkConfig))]
public class VectorSearchBenchmarks
{
    private IVectorSearchService _searchService = null!;
    private ReadOnlyMemory<float> _queryVector;

    [Params(100, 1000, 10000)]
    public int DatabaseSize { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _searchService = CreateSearchService();

        // Pre-generate query vector (1024 dimensions for mxbai-embed-large)
        _queryVector = new float[1024];
        var random = new Random(42);
        for (int i = 0; i < 1024; i++)
        {
            ((float[])_queryVector.ToArray())[i] = (float)random.NextDouble();
        }

        // Seed database with specified number of documents
        await SeedDatabase(DatabaseSize);
    }

    [Benchmark(Baseline = true)]
    public async Task<IReadOnlyList<SearchResult>> Search_Top5()
    {
        return await _searchService.SearchAsync(_queryVector, topK: 5, CancellationToken.None);
    }

    [Benchmark]
    public async Task<IReadOnlyList<SearchResult>> Search_Top10()
    {
        return await _searchService.SearchAsync(_queryVector, topK: 10, CancellationToken.None);
    }

    [Benchmark]
    public async Task<IReadOnlyList<SearchResult>> Search_Top20()
    {
        return await _searchService.SearchAsync(_queryVector, topK: 20, CancellationToken.None);
    }

    private IVectorSearchService CreateSearchService()
    {
        throw new NotImplementedException("Implement with actual service creation");
    }

    private async Task SeedDatabase(int count)
    {
        throw new NotImplementedException("Implement database seeding");
    }
}
```

### BenchmarkConfig.cs

```csharp
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;

namespace CompoundDocs.Benchmarks;

/// <summary>
/// Shared benchmark configuration for all performance baselines.
/// Configures warm-up, iterations, and export formats.
/// </summary>
public class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        // Use short runs for development, longer for official baselines
        AddJob(Job.ShortRun
            .WithWarmupCount(3)
            .WithIterationCount(5));

        // Export formats for baseline documentation
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(CsvExporter.Default);
        AddExporter(HtmlExporter.Default);

        // Output directory
        WithArtifactsPath("BenchmarkResults");
    }
}
```

### Performance Baselines Documentation

Create `docs/performance-baselines.md`:

```markdown
# Performance Baselines

> **Status**: DRAFT
> **Last Updated**: [DATE]
> **Note**: These baselines are informational only and are not enforced for MVP.

## Overview

This document captures performance baseline measurements for the CSharp Compounding Docs plugin.
Baselines are established to provide reference points for future optimization work and to
detect significant performance regressions.

## Hardware Specifications

Baselines were measured on:

| Component | Specification |
|-----------|---------------|
| CPU | [TBD - e.g., Apple M2 Pro] |
| RAM | [TBD - e.g., 16GB] |
| Storage | [TBD - e.g., NVMe SSD] |
| OS | [TBD - e.g., macOS 14.x] |
| Ollama Version | [TBD] |
| PostgreSQL Version | 16.x with pgvector |

## RAG Query Performance

| Operation | Mean | StdDev | Allocated |
|-----------|------|--------|-----------|
| Small Query (top 5) | TBD | TBD | TBD |
| Medium Query (top 10) | TBD | TBD | TBD |
| Large Query (top 20) | TBD | TBD | TBD |

**Notes:**
- Includes embedding generation, vector search, and LLM synthesis
- Measured with Ollama running locally with mxbai-embed-large + mistral models
- 5-minute timeout configured per spec

## Embedding Generation Performance

| Document Size | Mean | StdDev | Allocated |
|---------------|------|--------|-----------|
| 100 chars | TBD | TBD | TBD |
| 1 KB | TBD | TBD | TBD |
| 10 KB | TBD | TBD | TBD |
| Batch (10 x 1KB) | TBD | TBD | TBD |

**Notes:**
- Using mxbai-embed-large model (1024 dimensions)
- Rate limited to 2 concurrent requests per spec

## Indexing Throughput

| Operation | Mean | StdDev | Docs/sec |
|-----------|------|--------|----------|
| Single Document (5KB) | TBD | TBD | TBD |
| Batch (5 documents) | TBD | TBD | TBD |
| Batch (10 documents) | TBD | TBD | TBD |

**Notes:**
- Includes chunking, embedding generation, and database storage
- Typical compound document with frontmatter

## Vector Search Latency

| Database Size | Top K | Mean | StdDev |
|---------------|-------|------|--------|
| 100 docs | 5 | TBD | TBD |
| 100 docs | 10 | TBD | TBD |
| 100 docs | 20 | TBD | TBD |
| 1,000 docs | 5 | TBD | TBD |
| 1,000 docs | 10 | TBD | TBD |
| 1,000 docs | 20 | TBD | TBD |
| 10,000 docs | 5 | TBD | TBD |
| 10,000 docs | 10 | TBD | TBD |
| 10,000 docs | 20 | TBD | TBD |

**Notes:**
- Search time only (excludes embedding generation for query)
- Using pgvector HNSW index with default parameters
- ef_search = 40 (default)

## Future Considerations

These baselines are established for documentation purposes only. Post-MVP, consider:

1. **Automated Regression Detection**: Add BenchmarkDotNet to CI with threshold alerts
2. **Hardware-Specific Profiles**: Document baselines for different hardware tiers
3. **Optimization Targets**: Set specific improvement goals based on user feedback
4. **Load Testing**: Add multi-user concurrent access scenarios
```

### Running Benchmarks

Add a script to run all performance baselines:

```bash
#!/bin/bash
# scripts/run-performance-baselines.sh

echo "Running Performance Baseline Benchmarks"
echo "========================================"
echo ""
echo "Prerequisites:"
echo "  - Ollama running locally with mxbai-embed-large and mistral models"
echo "  - PostgreSQL with pgvector running"
echo ""

cd benchmarks/CompoundDocs.Benchmarks

# Run all benchmarks
dotnet run -c Release -- --filter "*Benchmarks*" --exporters markdown html csv

echo ""
echo "Results saved to BenchmarkResults/"
echo "Update docs/performance-baselines.md with the results"
```

---

## Dependencies

### Depends On
- **Phase 143**: BenchmarkDotNet Setup (benchmark project structure and configuration)

### Blocks
- Future performance regression testing phases
- Performance optimization phases (post-MVP)

---

## Verification Steps

After completing this phase, verify:

1. **Benchmark files exist**:
   ```bash
   ls -la benchmarks/CompoundDocs.Benchmarks/*.cs
   ```

2. **Benchmarks compile**:
   ```bash
   dotnet build benchmarks/CompoundDocs.Benchmarks -c Release
   ```

3. **Benchmarks run** (requires Ollama and PostgreSQL):
   ```bash
   cd benchmarks/CompoundDocs.Benchmarks
   dotnet run -c Release -- --filter "EmbeddingBenchmarks" --list
   ```

4. **Documentation created**:
   ```bash
   cat docs/performance-baselines.md
   ```

5. **Export formats work**:
   ```bash
   cd benchmarks/CompoundDocs.Benchmarks
   dotnet run -c Release -- --filter "VectorSearchBenchmarks.Search_Top5" --exporters markdown
   ```

---

## Notes

- Per spec/testing.md, performance benchmarks are deferred for post-MVP as "Embedding performance is Ollama-dependent; not a differentiator for MVP"
- This phase establishes the infrastructure and baseline measurements without enforcing thresholds
- Benchmarks require real Ollama and PostgreSQL connections for accurate measurements
- The 5-minute timeout for embedding operations is specified in spec/mcp-server/ollama-integration.md
- Rate limiting (2 concurrent requests) should be reflected in benchmark configuration
- Hardware specifications must be documented alongside baseline values for reproducibility
- Baselines will vary significantly based on hardware (Apple Silicon vs NVIDIA GPU vs CPU-only)
