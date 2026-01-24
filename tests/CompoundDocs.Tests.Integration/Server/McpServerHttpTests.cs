using System.Net;
using CompoundDocs.Tests.Integration.Fixtures;

namespace CompoundDocs.Tests.Integration.Server;

[Collection("McpServer")]
public class McpServerHttpTests
{
    private readonly McpServerFixture _fixture;

    public McpServerHttpTests(McpServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthEndpoint_WithNoAuth_ReturnsOk()
    {
        using var client = _fixture.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        // Explicitly send no authentication headers

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SseEndpoint_WithNoAuth_Returns401()
    {
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync("/sse");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SseEndpoint_WithValidApiKey_DoesNotReturn401()
    {
        using var client = _fixture.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var request = new HttpRequestMessage(HttpMethod.Get, "/sse");
        request.Headers.Add("X-API-Key", McpServerFixture.ValidApiKey1);

        var response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SseEndpoint_WithValidBearerToken_DoesNotReturn401()
    {
        using var client = _fixture.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var request = new HttpRequestMessage(HttpMethod.Get, "/sse");
        request.Headers.Add("Authorization", $"Bearer {McpServerFixture.ValidApiKey1}");

        var response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SseEndpoint_WithInvalidKey_Returns401()
    {
        using var client = _fixture.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/sse");
        request.Headers.Add("X-API-Key", "invalid-key-that-should-fail");

        var response = await client.GetAsync("/sse");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task McpEndpoint_SseContentType_ReturnsTextEventStream()
    {
        using var client = _fixture.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var request = new HttpRequestMessage(HttpMethod.Get, "/sse");
        request.Headers.Add("X-API-Key", McpServerFixture.ValidApiKey1);

        var response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        response.Content.Headers.ContentType?.ToString().ShouldContain("text/event-stream");
    }
}
