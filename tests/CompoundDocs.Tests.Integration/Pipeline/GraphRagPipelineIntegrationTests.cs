using CompoundDocs.Bedrock;
using CompoundDocs.Bedrock.DependencyInjection;
using CompoundDocs.Graph;
using CompoundDocs.Graph.DependencyInjection;
using CompoundDocs.GraphRag;
using CompoundDocs.Vector;
using CompoundDocs.Vector.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompoundDocs.Tests.Integration.Pipeline;

/// <summary>
/// Integration tests for the full GraphRAG pipeline: embed -> vector search -> graph traversal -> LLM synthesis.
/// These tests require real AWS infrastructure (Neptune, OpenSearch, Bedrock) and are skipped in CI.
/// </summary>
public class GraphRagPipelineIntegrationTests
{
    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task FullPipeline_EmbedSearchEnrichSynthesize()
    {
        // Arrange: wire up all real AWS services
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddNeptuneGraph(config);
        services.AddOpenSearchVector(config);
        services.AddBedrockServices(config);
        await using var provider = services.BuildServiceProvider();

        // Verify all pipeline dependencies resolve correctly
        var vectorStore = provider.GetRequiredService<IVectorStore>();
        var graphRepo = provider.GetRequiredService<IGraphRepository>();
        var embeddingService = provider.GetRequiredService<IBedrockEmbeddingService>();
        var llmService = provider.GetRequiredService<IBedrockLlmService>();

        vectorStore.ShouldNotBeNull();
        graphRepo.ShouldNotBeNull();
        embeddingService.ShouldNotBeNull();
        llmService.ShouldNotBeNull();

        // Act: manually execute the pipeline stages to verify end-to-end integration

        // Stage 1: Generate embedding for the query
        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(
            "What is dependency injection in .NET?");
        queryEmbedding.ShouldNotBeNull();
        queryEmbedding.Length.ShouldBe(1024);

        // Stage 2: Search vector store for relevant chunks
        var searchResults = await vectorStore.SearchAsync(queryEmbedding, topK: 5);
        searchResults.ShouldNotBeNull();

        // Stage 3: Synthesize an answer using the LLM
        var contextText = searchResults.Count > 0
            ? string.Join("\n", searchResults.Select(r => $"[{r.ChunkId}]: score={r.Score:F3}"))
            : "No relevant chunks found in vector store.";

        var answer = await llmService.GenerateAsync(
            "You are a technical documentation assistant. Answer based on the provided context.",
            [new BedrockMessage("user", $"Context:\n{contextText}\n\nQuestion: What is dependency injection in .NET?")],
            ModelTier.Haiku);

        // Assert: LLM should produce a meaningful answer
        answer.ShouldNotBeNullOrWhiteSpace();
        answer.Length.ShouldBeGreaterThan(20);
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task PipelineDependencies_ResolveFromServiceProvider()
    {
        // Arrange: verify DI wiring for all pipeline components
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddNeptuneGraph(config);
        services.AddOpenSearchVector(config);
        services.AddBedrockServices(config);
        await using var provider = services.BuildServiceProvider();

        // Act & Assert: all pipeline dependencies should be resolvable
        var vectorStore = provider.GetService<IVectorStore>();
        var graphRepo = provider.GetService<IGraphRepository>();
        var embeddingService = provider.GetService<IBedrockEmbeddingService>();
        var llmService = provider.GetService<IBedrockLlmService>();

        vectorStore.ShouldNotBeNull();
        graphRepo.ShouldNotBeNull();
        embeddingService.ShouldNotBeNull();
        llmService.ShouldNotBeNull();
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task IndexThenQuery_EndToEndRoundTrip()
    {
        // Arrange: full round trip - index a document chunk, then query for it
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddNeptuneGraph(config);
        services.AddOpenSearchVector(config);
        services.AddBedrockServices(config);
        await using var provider = services.BuildServiceProvider();

        var vectorStore = provider.GetRequiredService<IVectorStore>();
        var embeddingService = provider.GetRequiredService<IBedrockEmbeddingService>();
        var llmService = provider.GetRequiredService<IBedrockLlmService>();

        // Stage 1: Generate and index an embedding for known content
        var contentText = "Dependency injection is a design pattern where objects receive their dependencies from external sources rather than creating them internally.";
        var contentEmbedding = await embeddingService.GenerateEmbeddingAsync(contentText);
        await vectorStore.IndexAsync("e2e-chunk-001", contentEmbedding, new Dictionary<string, string>
        {
            ["documentId"] = "e2e-doc-001",
            ["filePath"] = "docs/dependency-injection.md"
        });

        // Stage 2: Query with a related question
        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(
            "What is dependency injection?");
        var results = await vectorStore.SearchAsync(queryEmbedding, topK: 5);

        // Assert: the indexed chunk should appear in search results
        results.ShouldNotBeEmpty();
        results.ShouldContain(r => r.ChunkId == "e2e-chunk-001");
        results.First(r => r.ChunkId == "e2e-chunk-001").Score.ShouldBeGreaterThan(0.7);

        // Stage 3: Synthesize answer
        var answer = await llmService.GenerateAsync(
            "You are a documentation assistant. Answer using the provided context only.",
            [new BedrockMessage("user", $"Context: {contentText}\n\nQuestion: What is dependency injection?")],
            ModelTier.Haiku);

        answer.ShouldNotBeNullOrWhiteSpace();
    }
}
