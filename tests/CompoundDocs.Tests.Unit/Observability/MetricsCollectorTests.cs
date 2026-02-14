using System.Diagnostics;
using CompoundDocs.McpServer.Observability;
using Shouldly;

namespace CompoundDocs.Tests.Unit.Observability;

public sealed class LatencyTrackerTests
{
    [Fact]
    public void GetPercentiles_WithNoSamples_ReturnsAllZerosAndZeroSampleCount()
    {
        // Arrange
        var tracker = new LatencyTracker();

        // Act
        var percentiles = tracker.GetPercentiles();

        // Assert
        percentiles.P50.ShouldBe(0);
        percentiles.P95.ShouldBe(0);
        percentiles.P99.ShouldBe(0);
        percentiles.SampleCount.ShouldBe(0);
    }

    [Fact]
    public void GetPercentiles_WithSingleSample_ReturnsAllPercentilesEqualToThatSample()
    {
        // Arrange
        var tracker = new LatencyTracker();
        tracker.Record(42.5);

        // Act
        var percentiles = tracker.GetPercentiles();

        // Assert
        percentiles.P50.ShouldBe(42.5);
        percentiles.P95.ShouldBe(42.5);
        percentiles.P99.ShouldBe(42.5);
        percentiles.SampleCount.ShouldBe(1);
    }

    [Fact]
    public void GetPercentiles_WithMultipleSamples_ReturnsReasonablePercentiles()
    {
        // Arrange
        var tracker = new LatencyTracker();

        // Record 100 samples: 1.0, 2.0, ..., 100.0
        for (int i = 1; i <= 100; i++)
        {
            tracker.Record(i);
        }

        // Act
        var percentiles = tracker.GetPercentiles();

        // Assert
        percentiles.SampleCount.ShouldBe(100);

        // P50 should be around the median (50th value)
        percentiles.P50.ShouldBe(50);

        // P95 should be around the 95th value
        percentiles.P95.ShouldBe(95);

        // P99 should be around the 99th value
        percentiles.P99.ShouldBe(99);

        // Verify ordering: P50 <= P95 <= P99
        percentiles.P50.ShouldBeLessThanOrEqualTo(percentiles.P95);
        percentiles.P95.ShouldBeLessThanOrEqualTo(percentiles.P99);
    }

    [Fact]
    public void Record_MoreThanMaxSamples_KeepsSampleCountAtMaxSamples()
    {
        // Arrange
        var tracker = new LatencyTracker();
        const int maxSamples = 10000;
        var totalToRecord = maxSamples + 500;

        // Act
        for (int i = 0; i < totalToRecord; i++)
        {
            tracker.Record(i);
        }

        var percentiles = tracker.GetPercentiles();

        // Assert - oldest samples should have been removed
        percentiles.SampleCount.ShouldBe(maxSamples);

        // The oldest 500 samples (0-499) should have been evicted,
        // so P50 should be based on values 500..10499
        percentiles.P50.ShouldBeGreaterThan(0);
    }
}

public sealed class MetricsCollectorTests : IDisposable
{
    private readonly MetricsCollector _collector;

    public MetricsCollectorTests()
    {
        _collector = new MetricsCollector();
    }

    [Fact]
    public void MeterName_HasExpectedValue()
    {
        MetricsCollector.MeterName.ShouldBe("CompoundDocs.McpServer");
    }

    [Fact]
    public void ActivitySourceName_HasExpectedValue()
    {
        MetricsCollector.ActivitySourceName.ShouldBe("CompoundDocs.McpServer");
    }

    [Fact]
    public void ActivitySource_IsNotNull()
    {
        _collector.ActivitySource.ShouldNotBeNull();
    }

    [Fact]
    public void RecordDocumentIndexed_IncrementsTotalDocumentsAndTotalChunksInSnapshot()
    {
        // Arrange & Act
        _collector.RecordDocumentIndexed(chunkCount: 5, durationMs: 100.0);
        _collector.RecordDocumentIndexed(chunkCount: 3, durationMs: 200.0);

        var snapshot = _collector.GetSnapshot();

        // Assert
        snapshot.TotalDocuments.ShouldBe(2);
        snapshot.TotalChunks.ShouldBe(8);
    }

    [Fact]
    public void RecordQuery_IncrementsTotalQueriesAndRecordsLatency()
    {
        // Arrange & Act
        _collector.RecordQuery(durationMs: 50.0, resultCount: 10);
        _collector.RecordQuery(durationMs: 150.0, resultCount: 5);

        var snapshot = _collector.GetSnapshot();

        // Assert
        snapshot.TotalQueries.ShouldBe(2);
        snapshot.QueryLatency.SampleCount.ShouldBe(2);
        snapshot.QueryLatency.P50.ShouldBeGreaterThanOrEqualTo(50.0);
        snapshot.QueryLatency.P99.ShouldBeLessThanOrEqualTo(150.0);
    }

    [Fact]
    public void RecordEmbeddingGeneration_RecordsLatencyInSnapshot()
    {
        // Arrange & Act
        _collector.RecordEmbeddingGeneration(durationMs: 25.0, contentLength: 1000);
        _collector.RecordEmbeddingGeneration(durationMs: 75.0, contentLength: 2000);

        var snapshot = _collector.GetSnapshot();

        // Assert
        snapshot.EmbeddingLatency.SampleCount.ShouldBe(2);
        snapshot.EmbeddingLatency.P50.ShouldBeGreaterThanOrEqualTo(25.0);
        snapshot.EmbeddingLatency.P99.ShouldBeLessThanOrEqualTo(75.0);
    }

    [Fact]
    public void RecordCacheHitAndMiss_CalculatesCacheHitRateCorrectly()
    {
        // Arrange & Act - 3 hits, 1 miss = 0.75 hit rate
        _collector.RecordCacheHit();
        _collector.RecordCacheHit();
        _collector.RecordCacheHit();
        _collector.RecordCacheMiss();

        var snapshot = _collector.GetSnapshot();

        // Assert
        snapshot.CacheHitRate.ShouldBe(0.75);
    }

    [Fact]
    public void CacheHitRate_ReturnsZero_WhenNoCacheOperations()
    {
        // Arrange & Act
        var snapshot = _collector.GetSnapshot();

        // Assert
        snapshot.CacheHitRate.ShouldBe(0.0);
    }

    [Fact]
    public void RecordError_DoesNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _collector.RecordError("TestError"));
        Should.NotThrow(() => _collector.RecordError("AnotherError"));
    }

    [Fact]
    public void StartActivity_ReturnsNull_WhenNoListenersRegistered()
    {
        // Act
        var activity = _collector.StartActivity("test-operation");

        // Assert
        activity.ShouldBeNull();
    }

    [Fact]
    public void GetSnapshot_HasReasonableGeneratedAtTimestamp()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;

        // Act
        var snapshot = _collector.GetSnapshot();

        var after = DateTimeOffset.UtcNow;

        // Assert
        snapshot.GeneratedAt.ShouldBeGreaterThanOrEqualTo(before);
        snapshot.GeneratedAt.ShouldBeLessThanOrEqualTo(after);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var collector = new MetricsCollector();

        // Act & Assert
        Should.NotThrow(() => collector.Dispose());
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_IsSafe()
    {
        // Arrange
        var collector = new MetricsCollector();

        // Act & Assert
        Should.NotThrow(() =>
        {
            collector.Dispose();
            collector.Dispose();
            collector.Dispose();
        });
    }

    public void Dispose()
    {
        _collector.Dispose();
    }
}
