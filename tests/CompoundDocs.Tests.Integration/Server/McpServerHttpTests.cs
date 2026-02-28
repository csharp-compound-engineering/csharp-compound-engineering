using System.Net;
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

namespace CompoundDocs.Tests.Integration.Server;

public class McpServerHttpTests
{
    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var neptuneClientMock = new Mock<INeptuneClient>();
        neptuneClientMock
            .Setup(c => c.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var embeddingServiceMock = new Mock<IBedrockEmbeddingService>();
        embeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync("health", It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f]);

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

                });
            });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthEndpoint_WithNoAuth_ReturnsOk()
    {
        var neptuneClientMock = new Mock<INeptuneClient>();
        neptuneClientMock
            .Setup(c => c.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var embeddingServiceMock = new Mock<IBedrockEmbeddingService>();
        embeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync("health", It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f]);

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

                });
            });
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        // Explicitly send no authentication headers

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SseEndpoint_WithNoAuth_Returns401()
    {
        var neptuneClientMock = new Mock<INeptuneClient>();
        neptuneClientMock
            .Setup(c => c.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var embeddingServiceMock = new Mock<IBedrockEmbeddingService>();
        embeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync("health", It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f]);

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

                });
            });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/sse");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SseEndpoint_WithValidApiKey_DoesNotReturn401()
    {
        var neptuneClientMock = new Mock<INeptuneClient>();
        neptuneClientMock
            .Setup(c => c.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var embeddingServiceMock = new Mock<IBedrockEmbeddingService>();
        embeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync("health", It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f]);

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

                });
            });
        using var client = factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var request = new HttpRequestMessage(HttpMethod.Get, "/sse");
        request.Headers.Add("X-API-Key", "test-key-1");

        var response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SseEndpoint_WithValidBearerToken_DoesNotReturn401()
    {
        var neptuneClientMock = new Mock<INeptuneClient>();
        neptuneClientMock
            .Setup(c => c.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var embeddingServiceMock = new Mock<IBedrockEmbeddingService>();
        embeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync("health", It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f]);

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

                });
            });
        using var client = factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var request = new HttpRequestMessage(HttpMethod.Get, "/sse");
        request.Headers.Add("Authorization", "Bearer test-key-1");

        var response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SseEndpoint_WithInvalidKey_Returns401()
    {
        var neptuneClientMock = new Mock<INeptuneClient>();
        neptuneClientMock
            .Setup(c => c.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var embeddingServiceMock = new Mock<IBedrockEmbeddingService>();
        embeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync("health", It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f]);

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

                });
            });
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/sse");
        request.Headers.Add("X-API-Key", "invalid-key-that-should-fail");

        var response = await client.GetAsync("/sse");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task McpEndpoint_SseContentType_ReturnsTextEventStream()
    {
        var neptuneClientMock = new Mock<INeptuneClient>();
        neptuneClientMock
            .Setup(c => c.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var embeddingServiceMock = new Mock<IBedrockEmbeddingService>();
        embeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync("health", It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f]);

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

                });
            });
        using var client = factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var request = new HttpRequestMessage(HttpMethod.Get, "/sse");
        request.Headers.Add("X-API-Key", "test-key-1");

        var response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        response.Content.Headers.ContentType?.ToString().ShouldContain("text/event-stream");
    }
}
