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
using Microsoft.Extensions.Options;
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
        await using var server = new McpTestServer();
        using var httpClient = server.CreateHttpClient();

        var response = await httpClient.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Healthy");
    }

    [Fact]
    public async Task McpEndpoint_WithoutAuth_Returns401()
    {
        await using var server = new McpTestServer();
        using var httpClient = server.CreateHttpClient();

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
        await using var server = new McpTestServer();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var client = await server.ConnectMcpClientAsync(cts.Token);

        client.ShouldNotBeNull();
        client.ServerInfo.ShouldNotBeNull();
        client.ServerInfo.Name.ShouldBe("csharp-compounding-docs");
    }

    [Fact]
    public async Task ListTools_Returns_RagQueryTool()
    {
        await using var server = new McpTestServer();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var client = await server.ConnectMcpClientAsync(cts.Token);

        var tools = await client.ListToolsAsync(cancellationToken: cts.Token);

        tools.ShouldNotBeEmpty();
        var ragTool = tools.FirstOrDefault(t => t.Name == "rag_query");
        ragTool.ShouldNotBeNull();
        ragTool.Description.ShouldContain("RAG");
    }

    [Fact]
    public async Task CallTool_RagQuery_ReturnsAnswer()
    {
        await using var server = new McpTestServer();
        server.PipelineMock
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

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var client = await server.ConnectMcpClientAsync(cts.Token);

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
        await using var server = new McpTestServer();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var client = await server.ConnectMcpClientAsync(cts.Token);

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
        await using var server = new McpTestServer();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var client = await server.ConnectMcpClientAsync(cts.Token);

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
        await using var server = new McpTestServer();
        GraphRagOptions? capturedOptions = null;

        server.PipelineMock
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

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var client = await server.ConnectMcpClientAsync(cts.Token);

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

    private sealed class McpTestServer : IAsyncDisposable
    {
        private const string TestApiKey = "e2e-test-key";
        private readonly WebApplicationFactory<Program> _factory;
        private McpClient? _client;
        private HttpClient? _httpClient;

        public Mock<IGraphRagPipeline> PipelineMock { get; } = new();
        public Mock<IVectorStore> VectorStoreMock { get; } = new();
        public Mock<IGraphRepository> GraphRepositoryMock { get; } = new();
        public Mock<IBedrockEmbeddingService> EmbeddingServiceMock { get; } = new();
        public Mock<IBedrockLlmService> LlmServiceMock { get; } = new();
        public Mock<INeptuneClient> NeptuneClientMock { get; } = new();
        public Mock<IOpenSearchClient> OpenSearchClientMock { get; } = new();
        public Mock<IGitSyncStatus> GitSyncStatusMock { get; } = new();

        public McpTestServer()
        {
            // Set up healthy defaults for health check dependencies
            NeptuneClientMock
                .Setup(c => c.TestConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            EmbeddingServiceMock
                .Setup(s => s.GenerateEmbeddingAsync("health", It.IsAny<CancellationToken>()))
                .ReturnsAsync([0.1f, 0.2f]);
            GitSyncStatusMock
                .Setup(s => s.LastRunFailed).Returns(false);
            GitSyncStatusMock
                .Setup(s => s.LastSuccessfulRun).Returns(DateTimeOffset.UtcNow);
            GitSyncStatusMock
                .Setup(s => s.IntervalSeconds).Returns(21600);

            _factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Testing");
                    builder.ConfigureServices(services =>
                    {
                        // Override API key auth options to use our test key
                        services.PostConfigure<ApiKeyAuthenticationOptions>(opts =>
                        {
                            opts.ApiKeys = TestApiKey;
                            opts.Enabled = true;
                        });

                        services.AddSingleton(PipelineMock.Object);
                        services.AddSingleton(VectorStoreMock.Object);
                        services.AddSingleton(GraphRepositoryMock.Object);
                        services.AddSingleton(EmbeddingServiceMock.Object);
                        services.AddSingleton(LlmServiceMock.Object);
                        services.AddSingleton(NeptuneClientMock.Object);
                        services.AddSingleton(OpenSearchClientMock.Object);
                        services.AddSingleton(GitSyncStatusMock.Object);
                    });
                });
        }

        public HttpClient CreateHttpClient()
        {
            _httpClient = _factory.CreateClient();
            return _httpClient;
        }

        public async Task<McpClient> ConnectMcpClientAsync(CancellationToken ct = default)
        {
            _httpClient = _factory.CreateClient();
            var transport = new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = new Uri(_httpClient.BaseAddress!, "/"),
                    AdditionalHeaders = new Dictionary<string, string>
                    {
                        ["X-API-Key"] = TestApiKey
                    },
                    TransportMode = HttpTransportMode.StreamableHttp
                },
                _httpClient,
                ownsHttpClient: false);

            _client = await McpClient.CreateAsync(
                transport,
                new McpClientOptions
                {
                    ClientInfo = new() { Name = "e2e-test", Version = "1.0.0" }
                },
                cancellationToken: ct);
            return _client;
        }

        public async ValueTask DisposeAsync()
        {
            if (_client is not null) await _client.DisposeAsync();
            _httpClient?.Dispose();
            await _factory.DisposeAsync();
        }
    }
}
