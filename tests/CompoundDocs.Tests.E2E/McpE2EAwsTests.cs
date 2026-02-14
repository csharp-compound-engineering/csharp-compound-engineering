using System.Net;
using CompoundDocs.GraphRag;
using CompoundDocs.McpServer.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CompoundDocs.Tests.E2E;

/// <summary>
/// End-to-end tests that use real AWS services (Neptune, OpenSearch, Bedrock).
/// These mirror the mocked McpE2ETests but exercise the full production pipeline.
/// </summary>
public class McpE2EAwsTests
{
    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task HealthEndpoint_ReturnsOk_WithRealServices()
    {
        await using var server = new McpAwsTestServer();
        using var httpClient = server.CreateHttpClient();

        var response = await httpClient.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Healthy");
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task McpClient_Connects_WithRealServices()
    {
        await using var server = new McpAwsTestServer();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var client = await server.ConnectMcpClientAsync(cts.Token);

        client.ShouldNotBeNull();
        client.ServerInfo.ShouldNotBeNull();
        client.ServerInfo.Name.ShouldBe("csharp-compounding-docs");
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task ListTools_WithRealServices_Returns_RagQueryTool()
    {
        await using var server = new McpAwsTestServer();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var client = await server.ConnectMcpClientAsync(cts.Token);

        var tools = await client.ListToolsAsync(cancellationToken: cts.Token);

        tools.ShouldNotBeEmpty();
        var ragTool = tools.FirstOrDefault(t => t.Name == "rag_query");
        ragTool.ShouldNotBeNull();
        ragTool.Description.ShouldContain("RAG");
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task CallTool_RagQuery_WithRealServices_ReturnsAnswer()
    {
        await using var server = new McpAwsTestServer();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var client = await server.ConnectMcpClientAsync(cts.Token);

        var result = await client.CallToolAsync(
            "rag_query",
            new Dictionary<string, object?> { ["query"] = "What is dependency injection in .NET?" },
            cancellationToken: cts.Token);

        result.ShouldNotBeNull();
        result.Content.ShouldNotBeEmpty();
        var text = result.Content.OfType<TextContentBlock>().First().Text;
        text.ShouldNotBeNull();
        text.ShouldContain("\"success\":true");
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task CallTool_RagQuery_WithRealServices_PassesMaxResultsOption()
    {
        await using var server = new McpAwsTestServer();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var client = await server.ConnectMcpClientAsync(cts.Token);

        var result = await client.CallToolAsync(
            "rag_query",
            new Dictionary<string, object?>
            {
                ["query"] = "How does the RAG pipeline work?",
                ["maxResults"] = 3
            },
            cancellationToken: cts.Token);

        result.ShouldNotBeNull();
        result.Content.ShouldNotBeEmpty();
        var text = result.Content.OfType<TextContentBlock>().First().Text;
        text.ShouldNotBeNull();
        text.ShouldContain("\"success\":true");
    }

    /// <summary>
    /// Test server that uses real AWS service registrations instead of mocks.
    /// Only overrides authentication for test access.
    /// </summary>
    private sealed class McpAwsTestServer : IAsyncDisposable
    {
        private const string TestApiKey = "e2e-aws-test-key";
        private readonly WebApplicationFactory<Program> _factory;
        private McpClient? _client;
        private HttpClient? _httpClient;

        public McpAwsTestServer()
        {
            _factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Testing");
                    builder.ConfigureServices(services =>
                    {
                        services.PostConfigure<ApiKeyAuthenticationOptions>(opts =>
                        {
                            opts.ApiKeys = TestApiKey;
                            opts.Enabled = true;
                        });
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
                    ClientInfo = new() { Name = "e2e-aws-test", Version = "1.0.0" }
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
