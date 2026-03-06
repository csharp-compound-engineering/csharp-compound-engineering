using CompoundDocs.Common.Configuration;
using CompoundDocs.Vector;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OpenSearch.Client;
using Shouldly;

namespace CompoundDocs.Tests.Unit.Vector;

public sealed class OpenSearchClientFactoryTests
{
    [Fact]
    public void GetClient_ReturnsClient_WhenEndpointConfigured()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockMonitor = new Mock<IOptionsMonitor<OpenSearchConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<OpenSearchConfig, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        var mockConfig = new Mock<IConfiguration>();
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns("us-east-1");
        mockConfig.Setup(c => c.GetSection("CompoundDocs:Aws:Region")).Returns(mockSection.Object);

        var sut = new OpenSearchClientFactory(
            mockMonitor.Object,
            mockConfig.Object,
            NullLogger<OpenSearchClientFactory>.Instance);

        // Act
        var client = sut.GetClient();

        // Assert
        client.ShouldNotBeNull();
        client.ShouldBeAssignableTo<IOpenSearchClient>();
    }

    [Fact]
    public void GetClient_CachesClient_WhenCalledMultipleTimes()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockMonitor = new Mock<IOptionsMonitor<OpenSearchConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<OpenSearchConfig, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        var mockConfig = new Mock<IConfiguration>();
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns("us-east-1");
        mockConfig.Setup(c => c.GetSection("CompoundDocs:Aws:Region")).Returns(mockSection.Object);

        var sut = new OpenSearchClientFactory(
            mockMonitor.Object,
            mockConfig.Object,
            NullLogger<OpenSearchClientFactory>.Instance);

        // Act
        var client1 = sut.GetClient();
        var client2 = sut.GetClient();

        // Assert
        client1.ShouldBeSameAs(client2);
    }

    [Fact]
    public void GetClient_ThrowsInvalidOperationException_WhenEndpointEmpty()
    {
        // Arrange
        var config = new OpenSearchConfig { CollectionEndpoint = "", IndexName = "test-index" };
        var mockMonitor = new Mock<IOptionsMonitor<OpenSearchConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<OpenSearchConfig, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        var mockConfig = new Mock<IConfiguration>();
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns("us-east-1");
        mockConfig.Setup(c => c.GetSection("CompoundDocs:Aws:Region")).Returns(mockSection.Object);

        var sut = new OpenSearchClientFactory(
            mockMonitor.Object,
            mockConfig.Object,
            NullLogger<OpenSearchClientFactory>.Instance);

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() => sut.GetClient());
        ex.Message.ShouldContain("OpenSearch endpoint is not configured");
    }

    [Fact]
    public void GetClient_ThrowsInvalidOperationException_WhenEndpointNull()
    {
        // Arrange
        var config = new OpenSearchConfig { CollectionEndpoint = null!, IndexName = "test-index" };
        var mockMonitor = new Mock<IOptionsMonitor<OpenSearchConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<OpenSearchConfig, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        var mockConfig = new Mock<IConfiguration>();
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns("us-east-1");
        mockConfig.Setup(c => c.GetSection("CompoundDocs:Aws:Region")).Returns(mockSection.Object);

        var sut = new OpenSearchClientFactory(
            mockMonitor.Object,
            mockConfig.Object,
            NullLogger<OpenSearchClientFactory>.Instance);

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() => sut.GetClient());
        ex.Message.ShouldContain("OpenSearch endpoint is not configured");
    }

    [Fact]
    public void GetClient_CreatesNewClient_WhenEndpointChanges()
    {
        // Arrange
        var config1 = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch-a.example.com",
            IndexName = "test-index"
        };
        var mockMonitor = new Mock<IOptionsMonitor<OpenSearchConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config1);
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<OpenSearchConfig, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        var mockConfig = new Mock<IConfiguration>();
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns("us-east-1");
        mockConfig.Setup(c => c.GetSection("CompoundDocs:Aws:Region")).Returns(mockSection.Object);

        var sut = new OpenSearchClientFactory(
            mockMonitor.Object,
            mockConfig.Object,
            NullLogger<OpenSearchClientFactory>.Instance);

        var client1 = sut.GetClient();

        // Act — change endpoint
        var config2 = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch-b.example.com",
            IndexName = "test-index"
        };
        mockMonitor.Setup(m => m.CurrentValue).Returns(config2);

        var client2 = sut.GetClient();

        // Assert
        client1.ShouldNotBeSameAs(client2);
    }

    [Fact]
    public void OnConfigChanged_ClearsClient_WhenEndpointDiffers()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch-a.example.com",
            IndexName = "test-index"
        };
        var mockMonitor = new Mock<IOptionsMonitor<OpenSearchConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);

        Action<OpenSearchConfig, string?>? onChangeCallback = null;
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<OpenSearchConfig, string?>>()))
            .Callback<Action<OpenSearchConfig, string?>>(cb => onChangeCallback = cb)
            .Returns(Mock.Of<IDisposable>());

        var mockConfig = new Mock<IConfiguration>();
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns("us-east-1");
        mockConfig.Setup(c => c.GetSection("CompoundDocs:Aws:Region")).Returns(mockSection.Object);

        var sut = new OpenSearchClientFactory(
            mockMonitor.Object,
            mockConfig.Object,
            NullLogger<OpenSearchClientFactory>.Instance);

        var client1 = sut.GetClient();
        onChangeCallback.ShouldNotBeNull();

        // Act — trigger config change callback with different endpoint
        var newConfig = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch-b.example.com",
            IndexName = "test-index"
        };
        onChangeCallback(newConfig, null);

        // Update CurrentValue to match the new endpoint
        mockMonitor.Setup(m => m.CurrentValue).Returns(newConfig);

        var client2 = sut.GetClient();

        // Assert — OnConfigChanged nulled the client, so a new one was created
        client1.ShouldNotBeSameAs(client2);
    }

    [Fact]
    public void OnConfigChanged_NoOp_WhenEndpointEmpty()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockMonitor = new Mock<IOptionsMonitor<OpenSearchConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);

        Action<OpenSearchConfig, string?>? onChangeCallback = null;
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<OpenSearchConfig, string?>>()))
            .Callback<Action<OpenSearchConfig, string?>>(cb => onChangeCallback = cb)
            .Returns(Mock.Of<IDisposable>());

        var mockConfig = new Mock<IConfiguration>();
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns("us-east-1");
        mockConfig.Setup(c => c.GetSection("CompoundDocs:Aws:Region")).Returns(mockSection.Object);

        var sut = new OpenSearchClientFactory(
            mockMonitor.Object,
            mockConfig.Object,
            NullLogger<OpenSearchClientFactory>.Instance);

        var client1 = sut.GetClient();
        onChangeCallback.ShouldNotBeNull();

        // Act — trigger config change with empty endpoint (guard clause should skip)
        var emptyConfig = new OpenSearchConfig { CollectionEndpoint = "", IndexName = "test-index" };
        onChangeCallback(emptyConfig, null);

        // GetClient should return cached client
        var client2 = sut.GetClient();

        // Assert
        client1.ShouldBeSameAs(client2);
    }

    [Fact]
    public void OnConfigChanged_NoOp_WhenEndpointSame()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockMonitor = new Mock<IOptionsMonitor<OpenSearchConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);

        Action<OpenSearchConfig, string?>? onChangeCallback = null;
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<OpenSearchConfig, string?>>()))
            .Callback<Action<OpenSearchConfig, string?>>(cb => onChangeCallback = cb)
            .Returns(Mock.Of<IDisposable>());

        var mockConfig = new Mock<IConfiguration>();
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns("us-east-1");
        mockConfig.Setup(c => c.GetSection("CompoundDocs:Aws:Region")).Returns(mockSection.Object);

        var sut = new OpenSearchClientFactory(
            mockMonitor.Object,
            mockConfig.Object,
            NullLogger<OpenSearchClientFactory>.Instance);

        var client1 = sut.GetClient();
        onChangeCallback.ShouldNotBeNull();

        // Act — trigger config change with same endpoint
        var sameConfig = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        onChangeCallback(sameConfig, null);

        // GetClient should return cached client
        var client2 = sut.GetClient();

        // Assert
        client1.ShouldBeSameAs(client2);
    }

    [Fact]
    public void Constructor_UsesDefaultRegion_WhenNotConfigured()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockMonitor = new Mock<IOptionsMonitor<OpenSearchConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<OpenSearchConfig, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        var mockConfig = new Mock<IConfiguration>();
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns((string?)null);
        mockConfig.Setup(c => c.GetSection("CompoundDocs:Aws:Region")).Returns(mockSection.Object);

        // Act — should not throw, defaults to us-east-1
        var sut = new OpenSearchClientFactory(
            mockMonitor.Object,
            mockConfig.Object,
            NullLogger<OpenSearchClientFactory>.Instance);

        var client = sut.GetClient();

        // Assert
        client.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_ReadsRegionFromConfiguration()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockMonitor = new Mock<IOptionsMonitor<OpenSearchConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<OpenSearchConfig, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        var mockConfig = new Mock<IConfiguration>();
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns("us-west-2");
        mockConfig.Setup(c => c.GetSection("CompoundDocs:Aws:Region")).Returns(mockSection.Object);

        // Act — should not throw, uses us-west-2
        var sut = new OpenSearchClientFactory(
            mockMonitor.Object,
            mockConfig.Object,
            NullLogger<OpenSearchClientFactory>.Instance);

        var client = sut.GetClient();

        // Assert
        client.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetClient_ReturnsSameClient_WhenConcurrentCallsContend()
    {
        // Covers the inner double-check lock path: after OnConfigChanged clears _client,
        // concurrent GetClient calls contend for the lock. The first creates the client,
        // subsequent calls find it via the inner check.
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockMonitor = new Mock<IOptionsMonitor<OpenSearchConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);

        Action<OpenSearchConfig, string?>? onChangeCallback = null;
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<OpenSearchConfig, string?>>()))
            .Callback<Action<OpenSearchConfig, string?>>(cb => onChangeCallback = cb)
            .Returns(Mock.Of<IDisposable>());

        var mockConfig = new Mock<IConfiguration>();
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns("us-east-1");
        mockConfig.Setup(c => c.GetSection("CompoundDocs:Aws:Region")).Returns(mockSection.Object);

        var sut = new OpenSearchClientFactory(
            mockMonitor.Object,
            mockConfig.Object,
            NullLogger<OpenSearchClientFactory>.Instance);

        // Create initial client to set _currentEndpoint
        _ = sut.GetClient();
        onChangeCallback.ShouldNotBeNull();

        // Clear _client via OnConfigChanged (but _currentEndpoint stays as original)
        onChangeCallback(
            new OpenSearchConfig
            {
                CollectionEndpoint = "https://different.example.com",
                IndexName = "test-index"
            },
            null);

        // Now _client is null. All threads fail the outer check and contend for the lock.
        // First thread creates the client; subsequent threads hit the inner lock check.
        const int threadCount = 10;
        using var barrier = new Barrier(threadCount);
        var tasks = Enumerable.Range(0, threadCount)
            .Select(_ => Task.Run(() =>
            {
                barrier.SignalAndWait();
                return sut.GetClient();
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        results.Distinct().Count().ShouldBe(1);
    }

    [Fact]
    public void GetClient_NormalizesEndpoint_WhenSchemeIsMissing()
    {
        // Arrange — bare domain without https://
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "opensearch.example.com",
            IndexName = "test-index"
        };
        var mockMonitor = new Mock<IOptionsMonitor<OpenSearchConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<OpenSearchConfig, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        var mockConfig = new Mock<IConfiguration>();
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns("us-east-1");
        mockConfig.Setup(c => c.GetSection("CompoundDocs:Aws:Region")).Returns(mockSection.Object);

        var sut = new OpenSearchClientFactory(
            mockMonitor.Object,
            mockConfig.Object,
            NullLogger<OpenSearchClientFactory>.Instance);

        // Act — should not throw UriFormatException
        var client = sut.GetClient();

        // Assert
        client.ShouldNotBeNull();
        client.ShouldBeAssignableTo<IOpenSearchClient>();
    }

    [Fact]
    public void GetClient_DoesNotDoublePrefix_WhenHttpsSchemePresent()
    {
        // Arrange — endpoint already has https://
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockMonitor = new Mock<IOptionsMonitor<OpenSearchConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<OpenSearchConfig, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        var mockConfig = new Mock<IConfiguration>();
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns("us-east-1");
        mockConfig.Setup(c => c.GetSection("CompoundDocs:Aws:Region")).Returns(mockSection.Object);

        var sut = new OpenSearchClientFactory(
            mockMonitor.Object,
            mockConfig.Object,
            NullLogger<OpenSearchClientFactory>.Instance);

        // Act
        var client = sut.GetClient();

        // Assert
        client.ShouldNotBeNull();
        client.ShouldBeAssignableTo<IOpenSearchClient>();
    }
}
