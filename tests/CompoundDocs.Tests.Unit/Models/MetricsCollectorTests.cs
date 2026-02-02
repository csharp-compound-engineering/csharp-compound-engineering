using CompoundDocs.McpServer.Observability;

namespace CompoundDocs.Tests.Unit.Models;

public class MetricsCollectorTests : IDisposable
{
    private readonly MetricsCollector _collector = new();

    [Fact]
    public void GetSnapshot_Empty_ReturnsZeroValues()
    {
        var snapshot = _collector.GetSnapshot();

        snapshot.TotalDocuments.Should().Be(0);
        snapshot.TotalChunks.Should().Be(0);
        snapshot.TotalQueries.Should().Be(0);
        snapshot.CacheHitRate.Should().Be(0.0);
        snapshot.QueryLatency.SampleCount.Should().Be(0);
        snapshot.EmbeddingLatency.SampleCount.Should().Be(0);
    }

    [Fact]
    public void RecordDocumentIndexed_UpdatesSnapshot()
    {
        _collector.RecordDocumentIndexed(5, 100.0);
        _collector.RecordDocumentIndexed(3, 200.0);

        var snapshot = _collector.GetSnapshot();

        snapshot.TotalDocuments.Should().Be(2);
        snapshot.TotalChunks.Should().Be(8);
    }

    [Fact]
    public void RecordQuery_UpdatesSnapshot()
    {
        _collector.RecordQuery(50.0, 10);
        _collector.RecordQuery(100.0, 5);

        var snapshot = _collector.GetSnapshot();

        snapshot.TotalQueries.Should().Be(2);
        snapshot.QueryLatency.SampleCount.Should().Be(2);
    }

    [Fact]
    public void CacheHitRate_CalculatesCorrectly()
    {
        _collector.RecordCacheHit();
        _collector.RecordCacheHit();
        _collector.RecordCacheHit();
        _collector.RecordCacheMiss();

        var snapshot = _collector.GetSnapshot();

        snapshot.CacheHitRate.Should().BeApproximately(0.75, 0.01);
    }

    [Fact]
    public void StartActivity_ReturnsActivityOrNull()
    {
        // Without a listener, StartActivity returns null
        var activity = _collector.StartActivity("test-operation");
        // Activity may be null if no listener is registered
        // This verifies no exception is thrown
    }

    [Fact]
    public void ActivitySource_IsNotNull()
    {
        _collector.ActivitySource.Should().NotBeNull();
        _collector.ActivitySource.Name.Should().Be(MetricsCollector.ActivitySourceName);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        _collector.Dispose();
        _collector.Dispose(); // Should not throw
    }

    public void Dispose() => _collector.Dispose();
}
