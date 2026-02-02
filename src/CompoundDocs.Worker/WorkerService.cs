using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.Worker;

public sealed class WorkerService : BackgroundService
{
    private readonly ILogger<WorkerService> _logger;

    public WorkerService(ILogger<WorkerService> logger)
    {
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CompoundDocs Worker started. Full pipeline implementation pending.");
        return Task.CompletedTask;
    }
}
