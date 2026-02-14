using CompoundDocs.Vector;
using CompoundDocs.Vector.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompoundDocs.Tests.Integration.Vector;

/// <summary>
/// Integration tests for embedding index mapping and 1024-dimension validation
/// against AWS OpenSearch Serverless.
/// </summary>
public class EmbeddingIndexTests
{
    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task IndexMapping_Supports1024Dimensions()
    {
        // Arrange: configure real OpenSearch vector store
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddOpenSearchVector(config);
        await using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IVectorStore>();
        var embedding = new float[1024];
        Array.Fill(embedding, 0.5f);
        var metadata = new Dictionary<string, string>
        {
            ["documentId"] = "dim-test-doc",
            ["filePath"] = "docs/dimensions.md"
        };

        // Act: index a 1024-dimensional embedding and retrieve it
        await store.IndexAsync("dim-chunk-001", embedding, metadata);
        var results = await store.SearchAsync(embedding, topK: 1);

        // Assert: the exact embedding should produce a near-perfect match
        results.ShouldNotBeEmpty();
        results[0].ChunkId.ShouldBe("dim-chunk-001");
        results[0].Score.ShouldBeGreaterThan(0.99);
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task ZeroEmbedding_IndexesWithoutError()
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
        var zeroEmbedding = new float[1024];
        var metadata = new Dictionary<string, string>
        {
            ["documentId"] = "zero-test-doc",
            ["filePath"] = "docs/zero-embedding.md"
        };

        // Act: index a zero-vector embedding (edge case for normalization)
        await store.IndexAsync("zero-chunk-001", zeroEmbedding, metadata);
        var results = await store.SearchAsync(zeroEmbedding, topK: 1);

        // Assert: should still be retrievable
        results.ShouldNotBeNull();
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task ReindexSameChunkId_UpdatesExistingRecord()
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
        var embeddingV1 = Enumerable.Range(0, 1024).Select(i => (float)i / 1024f).ToArray();
        var embeddingV2 = Enumerable.Range(0, 1024).Select(i => (float)(1024 - i) / 1024f).ToArray();
        var metadata = new Dictionary<string, string>
        {
            ["documentId"] = "reindex-doc",
            ["filePath"] = "docs/reindex.md"
        };

        // Act: index same chunk ID twice with different embeddings
        await store.IndexAsync("reindex-chunk-001", embeddingV1, metadata);
        await store.IndexAsync("reindex-chunk-001", embeddingV2, metadata);
        var results = await store.SearchAsync(embeddingV2, topK: 1);

        // Assert: searching with V2 embedding should find the updated record
        results.ShouldNotBeEmpty();
        results[0].ChunkId.ShouldBe("reindex-chunk-001");
        results[0].Score.ShouldBeGreaterThan(0.9);
    }
}
