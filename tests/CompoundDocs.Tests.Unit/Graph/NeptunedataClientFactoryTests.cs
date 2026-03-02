using Amazon.Neptunedata;
using CompoundDocs.Common.Configuration;
using CompoundDocs.Graph;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace CompoundDocs.Tests.Unit.Graph;

public sealed class NeptunedataClientFactoryTests
{
    [Fact]
    public void GetClient_ReturnsClient_WhenEndpointConfigured()
    {
        // Arrange
        var config = new NeptuneConfig { Endpoint = "neptune.example.com", Port = 8182 };
        var mockMonitor = new Mock<IOptionsMonitor<NeptuneConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<NeptuneConfig, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        var mockNeptune = new Mock<IAmazonNeptunedata>();
        var sut = new Mock<NeptunedataClientFactory>(
            mockMonitor.Object,
            NullLogger<NeptunedataClientFactory>.Instance) { CallBase = true };
        sut.Setup(f => f.CreateClient(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(mockNeptune.Object);

        // Act
        var client = sut.Object.GetClient();

        // Assert
        client.ShouldBe(mockNeptune.Object);
        sut.Verify(f => f.CreateClient("neptune.example.com", 8182), Times.Once);
    }

    [Fact]
    public void GetClient_CachesClient_WhenCalledMultipleTimes()
    {
        // Arrange
        var config = new NeptuneConfig { Endpoint = "neptune.example.com", Port = 8182 };
        var mockMonitor = new Mock<IOptionsMonitor<NeptuneConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<NeptuneConfig, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        var mockNeptune = new Mock<IAmazonNeptunedata>();
        var sut = new Mock<NeptunedataClientFactory>(
            mockMonitor.Object,
            NullLogger<NeptunedataClientFactory>.Instance) { CallBase = true };
        sut.Setup(f => f.CreateClient(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(mockNeptune.Object);

        // Act
        var client1 = sut.Object.GetClient();
        var client2 = sut.Object.GetClient();

        // Assert
        client1.ShouldBeSameAs(client2);
        sut.Verify(f => f.CreateClient(It.IsAny<string>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public void GetClient_ThrowsInvalidOperationException_WhenEndpointEmpty()
    {
        // Arrange
        var config = new NeptuneConfig { Endpoint = "", Port = 8182 };
        var mockMonitor = new Mock<IOptionsMonitor<NeptuneConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<NeptuneConfig, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        var sut = new Mock<NeptunedataClientFactory>(
            mockMonitor.Object,
            NullLogger<NeptunedataClientFactory>.Instance) { CallBase = true };

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() => sut.Object.GetClient());
        ex.Message.ShouldContain("Neptune endpoint is not configured");
    }

    [Fact]
    public void GetClient_ThrowsInvalidOperationException_WhenEndpointNull()
    {
        // Arrange
        var config = new NeptuneConfig { Endpoint = null!, Port = 8182 };
        var mockMonitor = new Mock<IOptionsMonitor<NeptuneConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<NeptuneConfig, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        var sut = new Mock<NeptunedataClientFactory>(
            mockMonitor.Object,
            NullLogger<NeptunedataClientFactory>.Instance) { CallBase = true };

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() => sut.Object.GetClient());
        ex.Message.ShouldContain("Neptune endpoint is not configured");
    }

    [Fact]
    public void GetClient_ThrowsObjectDisposedException_AfterDispose()
    {
        // Arrange
        var config = new NeptuneConfig { Endpoint = "neptune.example.com", Port = 8182 };
        var mockMonitor = new Mock<IOptionsMonitor<NeptuneConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<NeptuneConfig, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        var sut = new Mock<NeptunedataClientFactory>(
            mockMonitor.Object,
            NullLogger<NeptunedataClientFactory>.Instance) { CallBase = true };

        sut.Object.Dispose();

        // Act & Assert
        Should.Throw<ObjectDisposedException>(() => sut.Object.GetClient());
    }

    [Fact]
    public void GetClient_CreatesNewClient_WhenEndpointChanges()
    {
        // Arrange
        var config1 = new NeptuneConfig { Endpoint = "neptune-a.example.com", Port = 8182 };
        var mockMonitor = new Mock<IOptionsMonitor<NeptuneConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config1);
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<NeptuneConfig, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        var mockNeptune1 = new Mock<IAmazonNeptunedata>();
        var mockNeptune2 = new Mock<IAmazonNeptunedata>();
        var sut = new Mock<NeptunedataClientFactory>(
            mockMonitor.Object,
            NullLogger<NeptunedataClientFactory>.Instance) { CallBase = true };
        sut.SetupSequence(f => f.CreateClient(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(mockNeptune1.Object)
            .Returns(mockNeptune2.Object);

        var client1 = sut.Object.GetClient();

        // Act — change endpoint
        var config2 = new NeptuneConfig { Endpoint = "neptune-b.example.com", Port = 8182 };
        mockMonitor.Setup(m => m.CurrentValue).Returns(config2);

        var client2 = sut.Object.GetClient();

        // Assert
        client1.ShouldBeSameAs(mockNeptune1.Object);
        client2.ShouldBeSameAs(mockNeptune2.Object);
        client1.ShouldNotBeSameAs(client2);
    }

    [Fact]
    public void GetClient_CreatesNewClient_WhenPortChanges()
    {
        // Arrange
        var config1 = new NeptuneConfig { Endpoint = "neptune.example.com", Port = 8182 };
        var mockMonitor = new Mock<IOptionsMonitor<NeptuneConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config1);
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<NeptuneConfig, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        var mockNeptune1 = new Mock<IAmazonNeptunedata>();
        var mockNeptune2 = new Mock<IAmazonNeptunedata>();
        var sut = new Mock<NeptunedataClientFactory>(
            mockMonitor.Object,
            NullLogger<NeptunedataClientFactory>.Instance) { CallBase = true };
        sut.SetupSequence(f => f.CreateClient(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(mockNeptune1.Object)
            .Returns(mockNeptune2.Object);

        var client1 = sut.Object.GetClient();

        // Act — change port
        var config2 = new NeptuneConfig { Endpoint = "neptune.example.com", Port = 9182 };
        mockMonitor.Setup(m => m.CurrentValue).Returns(config2);

        var client2 = sut.Object.GetClient();

        // Assert
        client1.ShouldBeSameAs(mockNeptune1.Object);
        client2.ShouldBeSameAs(mockNeptune2.Object);
        client1.ShouldNotBeSameAs(client2);
    }

    [Fact]
    public void OnConfigChanged_ClearsClient_WhenEndpointDiffers()
    {
        // Arrange
        var config = new NeptuneConfig { Endpoint = "neptune-a.example.com", Port = 8182 };
        var mockMonitor = new Mock<IOptionsMonitor<NeptuneConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);

        Action<NeptuneConfig, string?>? onChangeCallback = null;
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<NeptuneConfig, string?>>()))
            .Callback<Action<NeptuneConfig, string?>>(cb => onChangeCallback = cb)
            .Returns(Mock.Of<IDisposable>());

        var mockNeptune1 = new Mock<IAmazonNeptunedata>();
        var mockNeptune2 = new Mock<IAmazonNeptunedata>();
        var sut = new Mock<NeptunedataClientFactory>(
            mockMonitor.Object,
            NullLogger<NeptunedataClientFactory>.Instance) { CallBase = true };
        sut.SetupSequence(f => f.CreateClient(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(mockNeptune1.Object)
            .Returns(mockNeptune2.Object);

        var client1 = sut.Object.GetClient();
        onChangeCallback.ShouldNotBeNull();

        // Act — trigger config change callback with different endpoint
        var newConfig = new NeptuneConfig { Endpoint = "neptune-b.example.com", Port = 8182 };
        onChangeCallback(newConfig, null);

        // GetClient should create a new client because OnConfigChanged cleared _currentEndpoint
        var client2 = sut.Object.GetClient();

        // Assert
        client1.ShouldNotBeSameAs(client2);
        sut.Verify(f => f.CreateClient(It.IsAny<string>(), It.IsAny<int>()), Times.Exactly(2));
    }

    [Fact]
    public void OnConfigChanged_NoOp_WhenEndpointEmpty()
    {
        // Arrange
        var config = new NeptuneConfig { Endpoint = "neptune.example.com", Port = 8182 };
        var mockMonitor = new Mock<IOptionsMonitor<NeptuneConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);

        Action<NeptuneConfig, string?>? onChangeCallback = null;
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<NeptuneConfig, string?>>()))
            .Callback<Action<NeptuneConfig, string?>>(cb => onChangeCallback = cb)
            .Returns(Mock.Of<IDisposable>());

        var mockNeptune = new Mock<IAmazonNeptunedata>();
        var sut = new Mock<NeptunedataClientFactory>(
            mockMonitor.Object,
            NullLogger<NeptunedataClientFactory>.Instance) { CallBase = true };
        sut.Setup(f => f.CreateClient(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(mockNeptune.Object);

        var client1 = sut.Object.GetClient();
        onChangeCallback.ShouldNotBeNull();

        // Act — trigger config change with empty endpoint (guard clause should skip)
        var emptyConfig = new NeptuneConfig { Endpoint = "", Port = 8182 };
        onChangeCallback(emptyConfig, null);

        // GetClient should return cached client — OnConfigChanged was a no-op
        var client2 = sut.Object.GetClient();

        // Assert
        client1.ShouldBeSameAs(client2);
        sut.Verify(f => f.CreateClient(It.IsAny<string>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public void OnConfigChanged_NoOp_WhenEndpointSame()
    {
        // Arrange
        var config = new NeptuneConfig { Endpoint = "neptune.example.com", Port = 8182 };
        var mockMonitor = new Mock<IOptionsMonitor<NeptuneConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);

        Action<NeptuneConfig, string?>? onChangeCallback = null;
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<NeptuneConfig, string?>>()))
            .Callback<Action<NeptuneConfig, string?>>(cb => onChangeCallback = cb)
            .Returns(Mock.Of<IDisposable>());

        var mockNeptune = new Mock<IAmazonNeptunedata>();
        var sut = new Mock<NeptunedataClientFactory>(
            mockMonitor.Object,
            NullLogger<NeptunedataClientFactory>.Instance) { CallBase = true };
        sut.Setup(f => f.CreateClient(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(mockNeptune.Object);

        var client1 = sut.Object.GetClient();
        onChangeCallback.ShouldNotBeNull();

        // Act — trigger config change with same endpoint and port
        var sameConfig = new NeptuneConfig { Endpoint = "neptune.example.com", Port = 8182 };
        onChangeCallback(sameConfig, null);

        // GetClient should return cached client
        var client2 = sut.Object.GetClient();

        // Assert
        client1.ShouldBeSameAs(client2);
        sut.Verify(f => f.CreateClient(It.IsAny<string>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public void Dispose_DisposesClient()
    {
        // Arrange
        var config = new NeptuneConfig { Endpoint = "neptune.example.com", Port = 8182 };
        var mockMonitor = new Mock<IOptionsMonitor<NeptuneConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);

        var mockSubscription = new Mock<IDisposable>();
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<NeptuneConfig, string?>>()))
            .Returns(mockSubscription.Object);

        var mockNeptune = new Mock<IAmazonNeptunedata>();
        var sut = new Mock<NeptunedataClientFactory>(
            mockMonitor.Object,
            NullLogger<NeptunedataClientFactory>.Instance) { CallBase = true };
        sut.Setup(f => f.CreateClient(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(mockNeptune.Object);

        _ = sut.Object.GetClient();

        // Act
        sut.Object.Dispose();

        // Assert
        mockNeptune.Verify(c => c.Dispose(), Times.Once);
        mockSubscription.Verify(s => s.Dispose(), Times.Once);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        // Arrange
        var config = new NeptuneConfig { Endpoint = "neptune.example.com", Port = 8182 };
        var mockMonitor = new Mock<IOptionsMonitor<NeptuneConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);

        var mockSubscription = new Mock<IDisposable>();
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<NeptuneConfig, string?>>()))
            .Returns(mockSubscription.Object);

        var mockNeptune = new Mock<IAmazonNeptunedata>();
        var sut = new Mock<NeptunedataClientFactory>(
            mockMonitor.Object,
            NullLogger<NeptunedataClientFactory>.Instance) { CallBase = true };
        sut.Setup(f => f.CreateClient(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(mockNeptune.Object);

        _ = sut.Object.GetClient();

        // Act — dispose twice
        sut.Object.Dispose();
        sut.Object.Dispose();

        // Assert — client disposed only once
        mockNeptune.Verify(c => c.Dispose(), Times.Once);
        mockSubscription.Verify(s => s.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GetClient_ReturnsCachedClient_WhenConcurrentCallEntersLockAfterCreation()
    {
        // Covers the inner double-check lock path: thread 2 enters the lock
        // after thread 1 has already created the client.
        var config = new NeptuneConfig { Endpoint = "neptune.example.com", Port = 8182 };
        var mockMonitor = new Mock<IOptionsMonitor<NeptuneConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<NeptuneConfig, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        var mockNeptune = new Mock<IAmazonNeptunedata>();
        using var createClientEntered = new ManualResetEventSlim(false);
        using var createClientGo = new ManualResetEventSlim(false);

        var sut = new Mock<NeptunedataClientFactory>(
            mockMonitor.Object,
            NullLogger<NeptunedataClientFactory>.Instance) { CallBase = true };
        sut.Setup(f => f.CreateClient(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(() =>
            {
                createClientEntered.Set();
                createClientGo.Wait();
                return mockNeptune.Object;
            });

        // Thread 1: passes outer check, enters lock, calls CreateClient (pauses inside)
        var task1 = Task.Run(() => sut.Object.GetClient());
        createClientEntered.Wait();

        // Thread 2: passes outer check (_client still null), blocks on lock
        var task2 = Task.Run(() => sut.Object.GetClient());
        await Task.Delay(50);

        // Release thread 1 — sets _client/_currentEndpoint, exits lock
        // Thread 2 enters lock — inner check finds _client set → returns cached
        createClientGo.Set();

        var client1 = await task1;
        var client2 = await task2;

        client1.ShouldBeSameAs(mockNeptune.Object);
        client2.ShouldBeSameAs(mockNeptune.Object);
        sut.Verify(f => f.CreateClient(It.IsAny<string>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public void Dispose_WhenNoClientCreated_DoesNotThrow()
    {
        // Covers null branches of _onChangeSubscription?.Dispose() and _client?.Dispose()
        var config = new NeptuneConfig { Endpoint = "neptune.example.com", Port = 8182 };
        var mockMonitor = new Mock<IOptionsMonitor<NeptuneConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<NeptuneConfig, string?>>()))
            .Returns((IDisposable?)null);

        var sut = new NeptunedataClientFactory(
            mockMonitor.Object,
            NullLogger<NeptunedataClientFactory>.Instance);

        // Act — dispose without ever calling GetClient
        Should.NotThrow(() => sut.Dispose());
    }

    [Fact]
    public void CreateClient_ReturnsAmazonNeptunedataClient()
    {
        // Arrange — use real factory (not mocked) to test the actual CreateClient method
        var config = new NeptuneConfig { Endpoint = "neptune.example.com", Port = 8182 };
        var mockMonitor = new Mock<IOptionsMonitor<NeptuneConfig>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(config);
        mockMonitor.Setup(m => m.OnChange(It.IsAny<Action<NeptuneConfig, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        var sut = new NeptunedataClientFactory(
            mockMonitor.Object,
            NullLogger<NeptunedataClientFactory>.Instance);

        // Act — call the real CreateClient (no network call, just constructs the SDK client)
        var client = sut.CreateClient("neptune.example.com", 8182);

        // Assert
        client.ShouldNotBeNull();
        client.ShouldBeOfType<AmazonNeptunedataClient>();

        // Cleanup
        client.Dispose();
        sut.Dispose();
    }
}
