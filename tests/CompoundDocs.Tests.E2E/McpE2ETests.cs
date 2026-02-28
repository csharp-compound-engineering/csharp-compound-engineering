using System.Net;
using CompoundDocs.Bedrock;
using CompoundDocs.Graph;
using CompoundDocs.GraphRag;
using CompoundDocs.McpServer.Options;
using CompoundDocs.Vector;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Moq;
using OpenSearch.Client;

namespace CompoundDocs.Tests.E2E;

public class McpE2ETests
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
                        opts.ApiKeys = "e2e-test-key";
                        opts.Enabled = true;
                    });
                    services.AddSingleton(new Mock<IGraphRagPipeline>().Object);
                    services.AddSingleton(new Mock<IVectorStore>().Object);
                    services.AddSingleton(new Mock<IGraphRepository>().Object);
                    services.AddSingleton(embeddingServiceMock.Object);
                    services.AddSingleton(new Mock<IBedrockLlmService>().Object);
                    services.AddSingleton(neptuneClientMock.Object);
                    services.AddSingleton(new Mock<IOpenSearchClient>().Object);

                });
            });
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Healthy");
    }

    [Fact]
    public async Task McpEndpoint_WithoutAuth_Returns401()
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
                        opts.ApiKeys = "e2e-test-key";
                        opts.Enabled = true;
                    });
                    services.AddSingleton(new Mock<IGraphRagPipeline>().Object);
                    services.AddSingleton(new Mock<IVectorStore>().Object);
                    services.AddSingleton(new Mock<IGraphRepository>().Object);
                    services.AddSingleton(embeddingServiceMock.Object);
                    services.AddSingleton(new Mock<IBedrockLlmService>().Object);
                    services.AddSingleton(neptuneClientMock.Object);
                    services.AddSingleton(new Mock<IOpenSearchClient>().Object);

                });
            });
        using var httpClient = factory.CreateClient();

        // POST to the root MCP endpoint without an API key
        var request = new HttpRequestMessage(HttpMethod.Post, "/")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };

        var response = await httpClient.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task McpClient_Connects_Successfully()
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
                        opts.ApiKeys = "e2e-test-key";
                        opts.Enabled = true;
                    });
                    services.AddSingleton(new Mock<IGraphRagPipeline>().Object);
                    services.AddSingleton(new Mock<IVectorStore>().Object);
                    services.AddSingleton(new Mock<IGraphRepository>().Object);
                    services.AddSingleton(embeddingServiceMock.Object);
                    services.AddSingleton(new Mock<IBedrockLlmService>().Object);
                    services.AddSingleton(neptuneClientMock.Object);
                    services.AddSingleton(new Mock<IOpenSearchClient>().Object);

                });
            });
        using var httpClient = factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/"),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["X-API-Key"] = "e2e-test-key"
                },
                TransportMode = HttpTransportMode.StreamableHttp
            },
            httpClient,
            ownsHttpClient: false);

        await using var client = await McpClient.CreateAsync(
            transport,
            new McpClientOptions
            {
                ClientInfo = new() { Name = "e2e-test", Version = "1.0.0" }
            },
            cancellationToken: cts.Token);

        client.ShouldNotBeNull();
        client.ServerInfo.ShouldNotBeNull();
        client.ServerInfo.Name.ShouldBe("csharp-compounding-docs");
    }

    [Fact]
    public async Task ListTools_Returns_RagQueryTool()
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
                        opts.ApiKeys = "e2e-test-key";
                        opts.Enabled = true;
                    });
                    services.AddSingleton(new Mock<IGraphRagPipeline>().Object);
                    services.AddSingleton(new Mock<IVectorStore>().Object);
                    services.AddSingleton(new Mock<IGraphRepository>().Object);
                    services.AddSingleton(embeddingServiceMock.Object);
                    services.AddSingleton(new Mock<IBedrockLlmService>().Object);
                    services.AddSingleton(neptuneClientMock.Object);
                    services.AddSingleton(new Mock<IOpenSearchClient>().Object);

                });
            });
        using var httpClient = factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/"),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["X-API-Key"] = "e2e-test-key"
                },
                TransportMode = HttpTransportMode.StreamableHttp
            },
            httpClient,
            ownsHttpClient: false);

        await using var client = await McpClient.CreateAsync(
            transport,
            new McpClientOptions
            {
                ClientInfo = new() { Name = "e2e-test", Version = "1.0.0" }
            },
            cancellationToken: cts.Token);

        var tools = await client.ListToolsAsync(cancellationToken: cts.Token);

        tools.ShouldNotBeEmpty();
        var ragTool = tools.FirstOrDefault(t => t.Name == "rag_query");
        ragTool.ShouldNotBeNull();
        ragTool.Description.ShouldContain("RAG");
    }

    [Fact]
    public async Task CallTool_RagQuery_ReturnsAnswer()
    {
        var pipelineMock = new Mock<IGraphRagPipeline>();
        pipelineMock
            .Setup(p => p.QueryAsync(
                It.IsAny<string>(),
                It.IsAny<GraphRagOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphRagResult
            {
                Answer = "Test answer from mocked pipeline",
                Sources =
                [
                    new GraphRagSource
                    {
                        DocumentId = "doc-001",
                        ChunkId = "chunk-001",
                        Repository = "test-repo",
                        FilePath = "docs/getting-started.md",
                        RelevanceScore = 0.95
                    }
                ],
                RelatedConcepts = ["dependency injection", "MCP protocol"],
                Confidence = 0.92
            });

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
                        opts.ApiKeys = "e2e-test-key";
                        opts.Enabled = true;
                    });
                    services.AddSingleton(pipelineMock.Object);
                    services.AddSingleton(new Mock<IVectorStore>().Object);
                    services.AddSingleton(new Mock<IGraphRepository>().Object);
                    services.AddSingleton(embeddingServiceMock.Object);
                    services.AddSingleton(new Mock<IBedrockLlmService>().Object);
                    services.AddSingleton(neptuneClientMock.Object);
                    services.AddSingleton(new Mock<IOpenSearchClient>().Object);

                });
            });
        using var httpClient = factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/"),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["X-API-Key"] = "e2e-test-key"
                },
                TransportMode = HttpTransportMode.StreamableHttp
            },
            httpClient,
            ownsHttpClient: false);

        await using var client = await McpClient.CreateAsync(
            transport,
            new McpClientOptions
            {
                ClientInfo = new() { Name = "e2e-test", Version = "1.0.0" }
            },
            cancellationToken: cts.Token);

        var result = await client.CallToolAsync(
            "rag_query",
            new Dictionary<string, object?> { ["query"] = "How does dependency injection work?" },
            cancellationToken: cts.Token);

        result.ShouldNotBeNull();
        result.Content.ShouldNotBeEmpty();
        var text = result.Content.OfType<TextContentBlock>().First().Text;
        text.ShouldNotBeNull();
        text.ShouldContain("Test answer from mocked pipeline");
    }

    [Fact]
    public async Task CallTool_RagQuery_EmptyQuery_ReturnsError()
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
                        opts.ApiKeys = "e2e-test-key";
                        opts.Enabled = true;
                    });
                    services.AddSingleton(new Mock<IGraphRagPipeline>().Object);
                    services.AddSingleton(new Mock<IVectorStore>().Object);
                    services.AddSingleton(new Mock<IGraphRepository>().Object);
                    services.AddSingleton(embeddingServiceMock.Object);
                    services.AddSingleton(new Mock<IBedrockLlmService>().Object);
                    services.AddSingleton(neptuneClientMock.Object);
                    services.AddSingleton(new Mock<IOpenSearchClient>().Object);

                });
            });
        using var httpClient = factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/"),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["X-API-Key"] = "e2e-test-key"
                },
                TransportMode = HttpTransportMode.StreamableHttp
            },
            httpClient,
            ownsHttpClient: false);

        await using var client = await McpClient.CreateAsync(
            transport,
            new McpClientOptions
            {
                ClientInfo = new() { Name = "e2e-test", Version = "1.0.0" }
            },
            cancellationToken: cts.Token);

        var result = await client.CallToolAsync(
            "rag_query",
            new Dictionary<string, object?> { ["query"] = "" },
            cancellationToken: cts.Token);

        result.ShouldNotBeNull();
        result.Content.ShouldNotBeEmpty();
        var text = result.Content.OfType<TextContentBlock>().First().Text;
        text.ShouldNotBeNull();
        // The tool returns a ToolResponse with success=false containing an error message
        text.ShouldContain("\"success\":false");
    }

    [Fact]
    public async Task CallTool_NonexistentTool_ThrowsMcpProtocolException()
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
                        opts.ApiKeys = "e2e-test-key";
                        opts.Enabled = true;
                    });
                    services.AddSingleton(new Mock<IGraphRagPipeline>().Object);
                    services.AddSingleton(new Mock<IVectorStore>().Object);
                    services.AddSingleton(new Mock<IGraphRepository>().Object);
                    services.AddSingleton(embeddingServiceMock.Object);
                    services.AddSingleton(new Mock<IBedrockLlmService>().Object);
                    services.AddSingleton(neptuneClientMock.Object);
                    services.AddSingleton(new Mock<IOpenSearchClient>().Object);

                });
            });
        using var httpClient = factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/"),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["X-API-Key"] = "e2e-test-key"
                },
                TransportMode = HttpTransportMode.StreamableHttp
            },
            httpClient,
            ownsHttpClient: false);

        await using var client = await McpClient.CreateAsync(
            transport,
            new McpClientOptions
            {
                ClientInfo = new() { Name = "e2e-test", Version = "1.0.0" }
            },
            cancellationToken: cts.Token);

        var ex = await Should.ThrowAsync<McpProtocolException>(async () =>
            await client.CallToolAsync(
                "nonexistent_tool",
                new Dictionary<string, object?> { ["param"] = "value" },
                cancellationToken: cts.Token));

        ex.ErrorCode.ShouldBe(McpErrorCode.InvalidParams);
        ex.Message.ShouldContain("nonexistent_tool");
    }

    [Fact]
    public async Task CallTool_RagQuery_PassesMaxResultsOption()
    {
        var pipelineMock = new Mock<IGraphRagPipeline>();
        GraphRagOptions? capturedOptions = null;

        pipelineMock
            .Setup(p => p.QueryAsync(
                It.IsAny<string>(),
                It.IsAny<GraphRagOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, GraphRagOptions?, CancellationToken>((_, opts, _) =>
                capturedOptions = opts)
            .ReturnsAsync(new GraphRagResult
            {
                Answer = "Answer with limited results",
                Sources = [],
                RelatedConcepts = [],
                Confidence = 0.8
            });

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
                        opts.ApiKeys = "e2e-test-key";
                        opts.Enabled = true;
                    });
                    services.AddSingleton(pipelineMock.Object);
                    services.AddSingleton(new Mock<IVectorStore>().Object);
                    services.AddSingleton(new Mock<IGraphRepository>().Object);
                    services.AddSingleton(embeddingServiceMock.Object);
                    services.AddSingleton(new Mock<IBedrockLlmService>().Object);
                    services.AddSingleton(neptuneClientMock.Object);
                    services.AddSingleton(new Mock<IOpenSearchClient>().Object);

                });
            });
        using var httpClient = factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/"),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["X-API-Key"] = "e2e-test-key"
                },
                TransportMode = HttpTransportMode.StreamableHttp
            },
            httpClient,
            ownsHttpClient: false);

        await using var client = await McpClient.CreateAsync(
            transport,
            new McpClientOptions
            {
                ClientInfo = new() { Name = "e2e-test", Version = "1.0.0" }
            },
            cancellationToken: cts.Token);

        await client.CallToolAsync(
            "rag_query",
            new Dictionary<string, object?>
            {
                ["query"] = "test query",
                ["maxResults"] = 3
            },
            cancellationToken: cts.Token);

        capturedOptions.ShouldNotBeNull();
        capturedOptions.MaxChunks.ShouldBe(3);
    }
}
