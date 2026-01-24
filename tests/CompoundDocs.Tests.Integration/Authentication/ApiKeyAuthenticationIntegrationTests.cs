using System.Net;
using CompoundDocs.Tests.Integration.Fixtures;

namespace CompoundDocs.Tests.Integration.Authentication;

[Collection("McpServer")]
public class ApiKeyAuthenticationIntegrationTests
{
    private readonly McpServerFixture _fixture;

    public ApiKeyAuthenticationIntegrationTests(McpServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task HealthEndpoint_WithNoKey_Returns200()
    {
        using var client = _fixture.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task McpEndpoint_WithNoKey_Returns401()
    {
        using var client = _fixture.CreateClient();
        var response = await client.GetAsync("/sse");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task McpEndpoint_WithInvalidKey_Returns401()
    {
        using var client = _fixture.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/sse");
        request.Headers.Add("X-API-Key", "invalid-key");

        var response = await client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task McpEndpoint_WithValidXApiKey_DoesNotReturn401()
    {
        using var client = _fixture.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var request = new HttpRequestMessage(HttpMethod.Get, "/sse");
        request.Headers.Add("X-API-Key", McpServerFixture.ValidApiKey1);

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task McpEndpoint_WithValidBearerToken_DoesNotReturn401()
    {
        using var client = _fixture.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var request = new HttpRequestMessage(HttpMethod.Get, "/sse");
        request.Headers.Add("Authorization", $"Bearer {McpServerFixture.ValidApiKey2}");

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }
}
