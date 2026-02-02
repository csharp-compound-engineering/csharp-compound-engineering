using CompoundDocs.Worker;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompoundDocs.Tests.Worker;

public sealed class WorkerServiceTests
{
    [Fact]
    public async Task ExecuteAsync_CompletesWithoutError()
    {
        var logger = NullLogger<WorkerService>.Instance;
        var service = new WorkerService(logger);

        // WorkerService.ExecuteAsync is protected, so we use StartAsync/StopAsync
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);
        await service.StopAsync(cts.Token);
    }

    [Fact]
    public async Task ExecuteAsync_CompletesImmediately()
    {
        var logger = NullLogger<WorkerService>.Instance;
        var service = new WorkerService(logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);

        // The service should complete almost immediately since ExecuteAsync returns Task.CompletedTask
        await Task.Delay(100, cts.Token);

        // StopAsync should not hang
        await service.StopAsync(CancellationToken.None);
    }
}
