using CompoundDocs.Bedrock;
using CompoundDocs.Bedrock.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompoundDocs.Tests.Integration.Bedrock;

/// <summary>
/// Integration tests for Amazon Bedrock Titan Embed V2 embedding generation.
/// These tests require real AWS infrastructure and are skipped in CI.
/// </summary>
public class BedrockEmbeddingIntegrationTests
{
    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task TitanEmbed_ReturnsValidEmbeddingArray()
    {
        // Arrange: configure real Bedrock embedding service
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddBedrockServices(config);
        await using var provider = services.BuildServiceProvider();

        var embeddingService = provider.GetRequiredService<IBedrockEmbeddingService>();

        // Act: generate embedding for a technical question
        var embedding = await embeddingService.GenerateEmbeddingAsync(
            "What is dependency injection in .NET?");

        // Assert: Titan Embed V2 should return a 1024-dimensional vector
        embedding.ShouldNotBeNull();
        embedding.Length.ShouldBe(1024);
        embedding.ShouldAllBe(v => !float.IsNaN(v));
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task SimilarTexts_ProduceHighCosineSimilarity()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddBedrockServices(config);
        await using var provider = services.BuildServiceProvider();

        var embeddingService = provider.GetRequiredService<IBedrockEmbeddingService>();

        // Act: generate embeddings for semantically similar texts
        var embedding1 = await embeddingService.GenerateEmbeddingAsync(
            "How to configure dependency injection in ASP.NET Core");
        var embedding2 = await embeddingService.GenerateEmbeddingAsync(
            "Setting up DI container in ASP.NET Core applications");
        var embeddingUnrelated = await embeddingService.GenerateEmbeddingAsync(
            "Chocolate cake recipe with frosting");

        // Calculate cosine similarity
        var similarityRelated = CosineSimilarity(embedding1, embedding2);
        var similarityUnrelated = CosineSimilarity(embedding1, embeddingUnrelated);

        // Assert: related texts should be more similar than unrelated
        similarityRelated.ShouldBeGreaterThan(similarityUnrelated);
        similarityRelated.ShouldBeGreaterThan(0.7);
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task BatchEmbeddings_ReturnCorrectCount()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddBedrockServices(config);
        await using var provider = services.BuildServiceProvider();

        var embeddingService = provider.GetRequiredService<IBedrockEmbeddingService>();

        var texts = new[]
        {
            "What is dependency injection?",
            "How does middleware work in ASP.NET Core?",
            "Explain the repository pattern."
        };

        // Act: generate embeddings for multiple texts
        var embeddings = await embeddingService.GenerateEmbeddingsAsync(texts);

        // Assert: should return one embedding per input text
        embeddings.ShouldNotBeNull();
        embeddings.Count.ShouldBe(3);
        embeddings.ShouldAllBe(e => e.Length == 1024);
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task EmptyText_ReturnsEmbeddingWithoutError()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddBedrockServices(config);
        await using var provider = services.BuildServiceProvider();

        var embeddingService = provider.GetRequiredService<IBedrockEmbeddingService>();

        // Act: generate embedding for minimal text input
        var embedding = await embeddingService.GenerateEmbeddingAsync(" ");

        // Assert: should still produce a valid 1024-dim vector
        embedding.ShouldNotBeNull();
        embedding.Length.ShouldBe(1024);
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dotProduct = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
