using CompoundDocs.Bedrock;
using CompoundDocs.Graph;
using CompoundDocs.GraphRag;
using CompoundDocs.McpServer.Options;
using CompoundDocs.Vector;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenSearch.Client;

namespace CompoundDocs.Tests.Integration.Fixtures;

/// <summary>
/// Shared WebApplicationFactory fixture for all integration tests.
/// Uses xUnit collection to ensure a single instance per test run.
/// </summary>
public class McpServerFixture : WebApplicationFactory<Program>
{
    public Mock<IVectorStore> VectorStoreMock { get; } = new();
    public Mock<IGraphRepository> GraphRepositoryMock { get; } = new();
    public Mock<IBedrockEmbeddingService> EmbeddingServiceMock { get; } = new();
    public Mock<IBedrockLlmService> LlmServiceMock { get; } = new();
    public Mock<IGraphRagPipeline> GraphRagPipelineMock { get; } = new();
    public Mock<INeptuneClient> NeptuneClientMock { get; } = new();
    public Mock<IOpenSearchClient> OpenSearchClientMock { get; } = new();

    public IVectorStore VectorStore => VectorStoreMock.Object;
    public IGraphRepository GraphRepository => GraphRepositoryMock.Object;
    public IBedrockEmbeddingService EmbeddingService => EmbeddingServiceMock.Object;
    public IBedrockLlmService LlmService => LlmServiceMock.Object;
    public IGraphRagPipeline GraphRagPipeline => GraphRagPipelineMock.Object;

    public const string ValidApiKey1 = "test-key-1";
    public const string ValidApiKey2 = "test-key-2";

    public McpServerFixture()
    {
        // Set up healthy defaults for health check dependencies
        NeptuneClientMock
            .Setup(c => c.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        EmbeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync("health", It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f]);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<ApiKeyAuthenticationOptions>(opts =>
            {
                opts.ApiKeys = $"{ValidApiKey1},{ValidApiKey2}";
                opts.Enabled = true;
            });

            services.AddSingleton(VectorStore);
            services.AddSingleton(GraphRepository);
            services.AddSingleton(EmbeddingService);
            services.AddSingleton(LlmService);
            services.AddSingleton(GraphRagPipeline);
            services.AddSingleton(NeptuneClientMock.Object);
            services.AddSingleton(OpenSearchClientMock.Object);
        });
    }
}

[CollectionDefinition("McpServer")]
public class McpServerCollection : ICollectionFixture<McpServerFixture>;
