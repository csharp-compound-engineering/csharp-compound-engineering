using CompoundDocs.McpServer.Background;
using CompoundDocs.McpServer.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Shouldly;

namespace CompoundDocs.Tests.Unit.Health;

public sealed class GitSyncHealthCheckTests
{
    private readonly Mock<IGitSyncStatus> _statusMock = new();

    [Fact]
    public async Task CheckHealthAsync_NoRunYet_ReturnsDegraded()
    {
        _statusMock.Setup(s => s.LastRunFailed).Returns(false);
        _statusMock.Setup(s => s.LastSuccessfulRun).Returns((DateTimeOffset?)null);

        var sut = new GitSyncHealthCheck(_statusMock.Object);
        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldContain("not completed");
    }

    [Fact]
    public async Task CheckHealthAsync_RecentSuccessfulRun_ReturnsHealthy()
    {
        _statusMock.Setup(s => s.LastRunFailed).Returns(false);
        _statusMock.Setup(s => s.LastSuccessfulRun).Returns(DateTimeOffset.UtcNow);
        _statusMock.Setup(s => s.IntervalSeconds).Returns(21600);

        var sut = new GitSyncHealthCheck(_statusMock.Object);
        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_LastRunFailed_ReturnsUnhealthy()
    {
        _statusMock.Setup(s => s.LastRunFailed).Returns(true);

        var sut = new GitSyncHealthCheck(_statusMock.Object);
        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldContain("failures");
    }

    [Fact]
    public async Task CheckHealthAsync_OverdueRun_ReturnsDegraded()
    {
        _statusMock.Setup(s => s.LastRunFailed).Returns(false);
        // Last run was 13 hours ago, interval is 6 hours (21600s), threshold is 2x = 12 hours
        _statusMock.Setup(s => s.LastSuccessfulRun).Returns(DateTimeOffset.UtcNow.AddHours(-13));
        _statusMock.Setup(s => s.IntervalSeconds).Returns(21600);

        var sut = new GitSyncHealthCheck(_statusMock.Object);
        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldContain("minutes ago");
    }
}
