using CompoundDocs.Vector;
using CompoundDocs.Vector.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompoundDocs.Tests.Integration.Vector;

/// <summary>
/// Integration tests for vector store k-NN operations against AWS OpenSearch Serverless.
/// These tests require real AWS infrastructure and are skipped in CI.
/// </summary>
public class VectorStoreTests
{
    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task IndexAndSearch_ReturnsRelevantResults()
    {
        // Arrange: build real services from environment configuration
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddOpenSearchVector(config);
        await using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IVectorStore>();
        var embedding = Enumerable.Range(0, 1024).Select(i => (float)i / 1024f).ToArray();
        var metadata = new Dictionary<string, string>
        {
            ["documentId"] = "test-doc-001",
            ["filePath"] = "docs/test.md"
        };

        // Act: index a document chunk then search with the same embedding
        await store.IndexAsync("chunk-001", embedding, metadata);
        var results = await store.SearchAsync(embedding, topK: 5);

        // Assert: the indexed chunk should be the top result with high similarity
        results.ShouldNotBeEmpty();
        results[0].ChunkId.ShouldBe("chunk-001");
        results[0].Score.ShouldBeGreaterThan(0.9);
        results[0].Metadata.ShouldContainKey("documentId");
        results[0].Metadata["documentId"].ShouldBe("test-doc-001");
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task BatchIndex_AllDocumentsRetrievable()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddOpenSearchVector(config);
        await using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IVectorStore>();
        var documents = Enumerable.Range(0, 5).Select(i => new VectorDocument
        {
            ChunkId = $"batch-chunk-{i:D3}",
            Embedding = Enumerable.Range(0, 1024).Select(j => (float)(i + j) / 2048f).ToArray(),
            Metadata = new Dictionary<string, string>
            {
                ["documentId"] = $"batch-doc-{i:D3}",
                ["filePath"] = $"docs/batch-{i}.md"
            }
        }).ToList();

        // Act: batch index all documents then search for the first one
        await store.BatchIndexAsync(documents);
        var results = await store.SearchAsync(documents[0].Embedding, topK: 5);

        // Assert: first document should appear in results
        results.ShouldNotBeEmpty();
        results.ShouldContain(r => r.ChunkId == "batch-chunk-000");
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task SearchWithFilters_ReturnsFilteredResults()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddOpenSearchVector(config);
        await using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IVectorStore>();
        var embedding = Enumerable.Range(0, 1024).Select(i => (float)i / 1024f).ToArray();
        var metadata = new Dictionary<string, string>
        {
            ["documentId"] = "filter-doc-001",
            ["filePath"] = "docs/filtered.md"
        };
        await store.IndexAsync("filter-chunk-001", embedding, metadata);

        var filters = new Dictionary<string, string>
        {
            ["documentId"] = "filter-doc-001"
        };

        // Act
        var results = await store.SearchAsync(embedding, topK: 5, filters: filters);

        // Assert: only filtered results should be returned
        results.ShouldNotBeEmpty();
        results.ShouldAllBe(r => r.Metadata["documentId"] == "filter-doc-001");
    }
}
