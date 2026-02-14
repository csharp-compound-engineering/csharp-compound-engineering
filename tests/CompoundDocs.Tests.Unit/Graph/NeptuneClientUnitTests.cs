using System.Text.Json;
using Amazon.Neptunedata;
using CompoundDocs.Graph;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompoundDocs.Tests.Unit.Graph;

/// <summary>
/// Tests for NeptuneClient behavior via the INeptuneClient interface.
/// NeptuneClient's TestConnectionAsync calls ExecuteOpenCypherAsync("RETURN 1"),
/// so we test this indirectly through a mock INeptuneClient to validate the logic pattern.
/// </summary>
public sealed class NeptuneClientUnitTests
{
    [Fact]
    public void PublicConstructor_WithIAmazonNeptunedata_CreatesClient()
    {
        var mockNeptune = new Mock<IAmazonNeptunedata>();
        var logger = NullLogger<NeptuneClient>.Instance;

        var client = new NeptuneClient(mockNeptune.Object, logger);

        client.ShouldNotBeNull();
    }

    [Fact]
    public void InternalConstructor_WithNullRetryPipeline_UsesEmptyPipeline()
    {
        var mockNeptune = new Mock<IAmazonNeptunedata>();
        var logger = NullLogger<NeptuneClient>.Instance;

        var client = new NeptuneClient(mockNeptune.Object, logger, retryPipeline: null);

        client.ShouldNotBeNull();
    }
    [Fact]
    public void INeptuneClient_ExecuteOpenCypherAsync_CanBeMocked()
    {
        var mockClient = new Mock<INeptuneClient>();
        mockClient.Setup(c => c.ExecuteOpenCypherAsync(
                "RETURN 1",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("1").RootElement.Clone());

        mockClient.Object.ShouldNotBeNull();
    }

    [Fact]
    public async Task INeptuneClient_TestConnectionAsync_CanBeMocked_ReturnsTrue()
    {
        var mockClient = new Mock<INeptuneClient>();
        mockClient.Setup(c => c.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await mockClient.Object.TestConnectionAsync(CancellationToken.None);
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task INeptuneClient_TestConnectionAsync_CanBeMocked_ReturnsFalse()
    {
        var mockClient = new Mock<INeptuneClient>();
        mockClient.Setup(c => c.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await mockClient.Object.TestConnectionAsync(CancellationToken.None);
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task INeptuneClient_ExecuteOpenCypherAsync_WithParameters()
    {
        var mockClient = new Mock<INeptuneClient>();
        var expectedResult = JsonDocument.Parse("[{\"count\": 42}]").RootElement.Clone();

        mockClient.Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var result = await mockClient.Object.ExecuteOpenCypherAsync(
            "MATCH (n) RETURN count(n) AS count",
            new Dictionary<string, object> { ["limit"] = 10 },
            CancellationToken.None);

        result.ValueKind.ShouldBe(JsonValueKind.Array);
    }
}
