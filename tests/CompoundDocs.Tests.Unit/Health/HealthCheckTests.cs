using CompoundDocs.Bedrock;
using CompoundDocs.Graph;
using CompoundDocs.McpServer.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using OpenSearch.Client;
using Shouldly;

namespace CompoundDocs.Tests.Unit.Health;

public sealed class NeptuneHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenConnectionSucceeds()
    {
        var mockClient = new Mock<INeptuneClient>();
        mockClient
            .Setup(c => c.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new NeptuneHealthCheck(mockClient.Object);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenConnectionReturnsFalse()
    {
        var mockClient = new Mock<INeptuneClient>();
        mockClient
            .Setup(c => c.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new NeptuneHealthCheck(mockClient.Object);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenConnectionThrows()
    {
        var mockClient = new Mock<INeptuneClient>();
        mockClient
            .Setup(c => c.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        var sut = new NeptuneHealthCheck(mockClient.Object);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Exception.ShouldNotBeNull();
    }
}

public sealed class OpenSearchHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenPingSucceeds()
    {
        var mockClient = new Mock<IOpenSearchClient>();
        mockClient
            .Setup(c => c.PingAsync(It.IsAny<Func<PingDescriptor, IPingRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PingResponse());

        var sut = new OpenSearchHealthCheck(mockClient.Object);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenPingThrows()
    {
        var mockClient = new Mock<IOpenSearchClient>();
        mockClient
            .Setup(c => c.PingAsync(It.IsAny<Func<PingDescriptor, IPingRequest>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection refused"));

        var sut = new OpenSearchHealthCheck(mockClient.Object);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Exception.ShouldNotBeNull();
    }
}

public sealed class BedrockHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenEmbeddingSucceeds()
    {
        var mockEmbedding = new Mock<IBedrockEmbeddingService>();
        mockEmbedding
            .Setup(s => s.GenerateEmbeddingAsync("health", It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f]);

        var sut = new BedrockHealthCheck(mockEmbedding.Object);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenEmbeddingThrows()
    {
        var mockEmbedding = new Mock<IBedrockEmbeddingService>();
        mockEmbedding
            .Setup(s => s.GenerateEmbeddingAsync("health", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Bedrock not reachable"));

        var sut = new BedrockHealthCheck(mockEmbedding.Object);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Exception.ShouldNotBeNull();
    }
}
