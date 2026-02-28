using System.Net;
using CompoundDocs.Bedrock;
using CompoundDocs.Graph;
using CompoundDocs.GraphRag;
using CompoundDocs.McpServer.Background;
using CompoundDocs.McpServer.Options;
using CompoundDocs.Vector;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenSearch.Client;

namespace CompoundDocs.Tests.Integration.Authentication;

public class ApiKeyAuthenticationIntegrationTests
{
    [Fact]
    public async Task HealthEndpoint_WithNoKey_Returns200()
    {
        var neptuneClientMock = new Mock<INeptuneClient>();
        neptuneClientMock
            .Setup(c => c.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var embeddingServiceMock = new Mock<IBedrockEmbeddingService>();
        embeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync("health", It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f]);
        var gitSyncStatusMock = new Mock<IGitSyncStatus>();
        gitSyncStatusMock.Setup(s => s.LastRunFailed).Returns(false);
        gitSyncStatusMock.Setup(s => s.LastSuccessfulRun).Returns(DateTimeOffset.UtcNow);
        gitSyncStatusMock.Setup(s => s.IntervalSeconds).Returns(21600);

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    services.PostConfigure<ApiKeyAuthenticationOptions>(opts =>
                    {
                        opts.ApiKeys = "test-key-1,test-key-2";
                        opts.Enabled = true;
                    });
                    services.AddSingleton(neptuneClientMock.Object);
                    services.AddSingleton(embeddingServiceMock.Object);
                    services.AddSingleton(new Mock<IGraphRagPipeline>().Object);
                    services.AddSingleton(new Mock<IVectorStore>().Object);
                    services.AddSingleton(new Mock<IGraphRepository>().Object);
                    services.AddSingleton(new Mock<IBedrockLlmService>().Object);
                    services.AddSingleton(new Mock<IOpenSearchClient>().Object);
                    services.AddSingleton(gitSyncStatusMock.Object);
                });
            });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task McpEndpoint_WithNoKey_Returns401()
    {
        var neptuneClientMock = new Mock<INeptuneClient>();
        neptuneClientMock
            .Setup(c => c.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var embeddingServiceMock = new Mock<IBedrockEmbeddingService>();
        embeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync("health", It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f]);
        var gitSyncStatusMock = new Mock<IGitSyncStatus>();
        gitSyncStatusMock.Setup(s => s.LastRunFailed).Returns(false);
        gitSyncStatusMock.Setup(s => s.LastSuccessfulRun).Returns(DateTimeOffset.UtcNow);
        gitSyncStatusMock.Setup(s => s.IntervalSeconds).Returns(21600);

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    services.PostConfigure<ApiKeyAuthenticationOptions>(opts =>
                    {
                        opts.ApiKeys = "test-key-1,test-key-2";
                        opts.Enabled = true;
                    });
                    services.AddSingleton(neptuneClientMock.Object);
                    services.AddSingleton(embeddingServiceMock.Object);
                    services.AddSingleton(new Mock<IGraphRagPipeline>().Object);
                    services.AddSingleton(new Mock<IVectorStore>().Object);
                    services.AddSingleton(new Mock<IGraphRepository>().Object);
                    services.AddSingleton(new Mock<IBedrockLlmService>().Object);
                    services.AddSingleton(new Mock<IOpenSearchClient>().Object);
                    services.AddSingleton(gitSyncStatusMock.Object);
                });
            });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/sse");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task McpEndpoint_WithInvalidKey_Returns401()
    {
        var neptuneClientMock = new Mock<INeptuneClient>();
        neptuneClientMock
            .Setup(c => c.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var embeddingServiceMock = new Mock<IBedrockEmbeddingService>();
        embeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync("health", It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f]);
        var gitSyncStatusMock = new Mock<IGitSyncStatus>();
        gitSyncStatusMock.Setup(s => s.LastRunFailed).Returns(false);
        gitSyncStatusMock.Setup(s => s.LastSuccessfulRun).Returns(DateTimeOffset.UtcNow);
        gitSyncStatusMock.Setup(s => s.IntervalSeconds).Returns(21600);

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    services.PostConfigure<ApiKeyAuthenticationOptions>(opts =>
                    {
                        opts.ApiKeys = "test-key-1,test-key-2";
                        opts.Enabled = true;
                    });
                    services.AddSingleton(neptuneClientMock.Object);
                    services.AddSingleton(embeddingServiceMock.Object);
                    services.AddSingleton(new Mock<IGraphRagPipeline>().Object);
                    services.AddSingleton(new Mock<IVectorStore>().Object);
                    services.AddSingleton(new Mock<IGraphRepository>().Object);
                    services.AddSingleton(new Mock<IBedrockLlmService>().Object);
                    services.AddSingleton(new Mock<IOpenSearchClient>().Object);
                    services.AddSingleton(gitSyncStatusMock.Object);
                });
            });
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/sse");
        request.Headers.Add("X-API-Key", "invalid-key");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task McpEndpoint_WithValidXApiKey_DoesNotReturn401()
    {
        var neptuneClientMock = new Mock<INeptuneClient>();
        neptuneClientMock
            .Setup(c => c.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var embeddingServiceMock = new Mock<IBedrockEmbeddingService>();
        embeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync("health", It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f]);
        var gitSyncStatusMock = new Mock<IGitSyncStatus>();
        gitSyncStatusMock.Setup(s => s.LastRunFailed).Returns(false);
        gitSyncStatusMock.Setup(s => s.LastSuccessfulRun).Returns(DateTimeOffset.UtcNow);
        gitSyncStatusMock.Setup(s => s.IntervalSeconds).Returns(21600);

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    services.PostConfigure<ApiKeyAuthenticationOptions>(opts =>
                    {
                        opts.ApiKeys = "test-key-1,test-key-2";
                        opts.Enabled = true;
                    });
                    services.AddSingleton(neptuneClientMock.Object);
                    services.AddSingleton(embeddingServiceMock.Object);
                    services.AddSingleton(new Mock<IGraphRagPipeline>().Object);
                    services.AddSingleton(new Mock<IVectorStore>().Object);
                    services.AddSingleton(new Mock<IGraphRepository>().Object);
                    services.AddSingleton(new Mock<IBedrockLlmService>().Object);
                    services.AddSingleton(new Mock<IOpenSearchClient>().Object);
                    services.AddSingleton(gitSyncStatusMock.Object);
                });
            });
        using var client = factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var request = new HttpRequestMessage(HttpMethod.Get, "/sse");
        request.Headers.Add("X-API-Key", "test-key-1");

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task McpEndpoint_WithValidBearerToken_DoesNotReturn401()
    {
        var neptuneClientMock = new Mock<INeptuneClient>();
        neptuneClientMock
            .Setup(c => c.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var embeddingServiceMock = new Mock<IBedrockEmbeddingService>();
        embeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync("health", It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f]);
        var gitSyncStatusMock = new Mock<IGitSyncStatus>();
        gitSyncStatusMock.Setup(s => s.LastRunFailed).Returns(false);
        gitSyncStatusMock.Setup(s => s.LastSuccessfulRun).Returns(DateTimeOffset.UtcNow);
        gitSyncStatusMock.Setup(s => s.IntervalSeconds).Returns(21600);

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    services.PostConfigure<ApiKeyAuthenticationOptions>(opts =>
                    {
                        opts.ApiKeys = "test-key-1,test-key-2";
                        opts.Enabled = true;
                    });
                    services.AddSingleton(neptuneClientMock.Object);
                    services.AddSingleton(embeddingServiceMock.Object);
                    services.AddSingleton(new Mock<IGraphRagPipeline>().Object);
                    services.AddSingleton(new Mock<IVectorStore>().Object);
                    services.AddSingleton(new Mock<IGraphRepository>().Object);
                    services.AddSingleton(new Mock<IBedrockLlmService>().Object);
                    services.AddSingleton(new Mock<IOpenSearchClient>().Object);
                    services.AddSingleton(gitSyncStatusMock.Object);
                });
            });
        using var client = factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var request = new HttpRequestMessage(HttpMethod.Get, "/sse");
        request.Headers.Add("Authorization", "Bearer test-key-2");

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }
}
