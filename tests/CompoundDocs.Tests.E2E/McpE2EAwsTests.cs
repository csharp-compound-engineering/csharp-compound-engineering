using System.Net;
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
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    services.PostConfigure<ApiKeyAuthenticationOptions>(opts =>
                    {
                        opts.ApiKeys = "e2e-aws-test-key";
                        opts.Enabled = true;
                    });
                });
            });
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Healthy");
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task McpClient_Connects_WithRealServices()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    services.PostConfigure<ApiKeyAuthenticationOptions>(opts =>
                    {
                        opts.ApiKeys = "e2e-aws-test-key";
                        opts.Enabled = true;
                    });
                });
            });
        using var httpClient = factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/"),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["X-API-Key"] = "e2e-aws-test-key"
                },
                TransportMode = HttpTransportMode.StreamableHttp
            },
            httpClient,
            ownsHttpClient: false);

        await using var client = await McpClient.CreateAsync(
            transport,
            new McpClientOptions
            {
                ClientInfo = new() { Name = "e2e-aws-test", Version = "1.0.0" }
            },
            cancellationToken: cts.Token);

        client.ShouldNotBeNull();
        client.ServerInfo.ShouldNotBeNull();
        client.ServerInfo.Name.ShouldBe("csharp-compounding-docs");
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task ListTools_WithRealServices_Returns_RagQueryTool()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    services.PostConfigure<ApiKeyAuthenticationOptions>(opts =>
                    {
                        opts.ApiKeys = "e2e-aws-test-key";
                        opts.Enabled = true;
                    });
                });
            });
        using var httpClient = factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/"),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["X-API-Key"] = "e2e-aws-test-key"
                },
                TransportMode = HttpTransportMode.StreamableHttp
            },
            httpClient,
            ownsHttpClient: false);

        await using var client = await McpClient.CreateAsync(
            transport,
            new McpClientOptions
            {
                ClientInfo = new() { Name = "e2e-aws-test", Version = "1.0.0" }
            },
            cancellationToken: cts.Token);

        var tools = await client.ListToolsAsync(cancellationToken: cts.Token);

        tools.ShouldNotBeEmpty();
        var ragTool = tools.FirstOrDefault(t => t.Name == "rag_query");
        ragTool.ShouldNotBeNull();
        ragTool.Description.ShouldContain("RAG");
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task CallTool_RagQuery_WithRealServices_ReturnsAnswer()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    services.PostConfigure<ApiKeyAuthenticationOptions>(opts =>
                    {
                        opts.ApiKeys = "e2e-aws-test-key";
                        opts.Enabled = true;
                    });
                });
            });
        using var httpClient = factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/"),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["X-API-Key"] = "e2e-aws-test-key"
                },
                TransportMode = HttpTransportMode.StreamableHttp
            },
            httpClient,
            ownsHttpClient: false);

        await using var client = await McpClient.CreateAsync(
            transport,
            new McpClientOptions
            {
                ClientInfo = new() { Name = "e2e-aws-test", Version = "1.0.0" }
            },
            cancellationToken: cts.Token);

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
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    services.PostConfigure<ApiKeyAuthenticationOptions>(opts =>
                    {
                        opts.ApiKeys = "e2e-aws-test-key";
                        opts.Enabled = true;
                    });
                });
            });
        using var httpClient = factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/"),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["X-API-Key"] = "e2e-aws-test-key"
                },
                TransportMode = HttpTransportMode.StreamableHttp
            },
            httpClient,
            ownsHttpClient: false);

        await using var client = await McpClient.CreateAsync(
            transport,
            new McpClientOptions
            {
                ClientInfo = new() { Name = "e2e-aws-test", Version = "1.0.0" }
            },
            cancellationToken: cts.Token);

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
}
