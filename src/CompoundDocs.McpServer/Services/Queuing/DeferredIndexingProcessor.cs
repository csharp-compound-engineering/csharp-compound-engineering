using CompoundDocs.McpServer.Observability;
using CompoundDocs.McpServer.Processing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompoundDocs.McpServer.Services.Queuing;

/// <summary>
/// Background service that processes the deferred indexing queue when Ollama becomes available.
/// </summary>
public sealed class DeferredIndexingProcessor : BackgroundService
{
    private readonly IDeferredIndexingQueue _queue;
    private readonly HealthCheckService _healthService;
    private readonly Func<IDocumentIndexer> _indexerFactory;
    private readonly DeferredIndexingOptions _options;
    private readonly ILogger<DeferredIndexingProcessor> _logger;

    private bool _wasOllamaAvailable = true;

    public DeferredIndexingProcessor(
        IDeferredIndexingQueue queue,
        HealthCheckService healthService,
        Func<IDocumentIndexer> indexerFactory,
        IOptions<DeferredIndexingOptions> options,
        ILogger<DeferredIndexingProcessor> logger)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _healthService = healthService ?? throw new ArgumentNullException(nameof(healthService));
        _indexerFactory = indexerFactory ?? throw new ArgumentNullException(nameof(indexerFactory));
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Deferred indexing processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueueIfOllamaAvailableAsync(stoppingToken);
                await Task.Delay(_options.HealthCheckInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in deferred indexing processor loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        LogShutdownState();
    }

    private async Task ProcessQueueIfOllamaAvailableAsync(CancellationToken ct)
    {
        // Check if queue has items
        if (_queue.Count == 0)
        {
            return;
        }

        // Check Ollama availability via the health check service
        var isAvailable = IsOllamaAvailable();

        // Detect recovery (transition from unavailable to available)
        if (isAvailable && !_wasOllamaAvailable)
        {
            _logger.LogInformation(
                "Ollama recovered, starting deferred queue processing ({Count} documents)",
                _queue.Count);
        }

        _wasOllamaAvailable = isAvailable;

        if (!isAvailable)
        {
            _logger.LogDebug(
                "Ollama unavailable, deferring queue processing ({Count} documents pending)",
                _queue.Count);
            return;
        }

        // Process queue in batches
        await ProcessBatchAsync(ct);
    }

    private bool IsOllamaAvailable()
    {
        var report = _healthService.LastHealthReport;
        if (report == null)
        {
            return false;
        }

        var ollamaCheck = report.Checks.FirstOrDefault(c => c.Name == "Ollama");
        return ollamaCheck?.Status == HealthStatus.Healthy;
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        var processed = 0;
        var failed = 0;
        var skipped = 0;

        var indexer = _indexerFactory();

        while (processed < _options.ProcessingBatchSize && _queue.TryDequeue(out var document))
        {
            if (document == null)
            {
                continue;
            }

            // Skip if file no longer exists or was removed from path index
            if (!File.Exists(document.FilePath))
            {
                _logger.LogDebug(
                    "Skipping deferred document {FilePath}: file no longer exists",
                    document.FilePath);
                skipped++;
                continue;
            }

            // Check if content has changed since queueing
            var currentHash = await ComputeContentHashAsync(document.FilePath, ct);
            if (currentHash != document.ContentHash)
            {
                _logger.LogDebug(
                    "Skipping deferred document {FilePath}: content changed since queueing",
                    document.FilePath);
                skipped++;
                continue;
            }

            try
            {
                var result = await indexer.IndexAsync(document.FilePath, ct);
                if (result.IsSuccess)
                {
                    processed++;
                    _logger.LogInformation(
                        "Successfully indexed deferred document {FilePath}",
                        document.FilePath);
                }
                else
                {
                    failed++;
                    await HandleRetryAsync(document, new Exception(string.Join("; ", result.Errors)));
                }
            }
            catch (Exception ex) when (IsRetryableError(ex))
            {
                failed++;
                await HandleRetryAsync(document, ex);
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex,
                    "Non-retryable error indexing deferred document {FilePath}, dropping",
                    document.FilePath);
            }
        }

        if (processed > 0 || failed > 0 || skipped > 0)
        {
            _logger.LogInformation(
                "Deferred queue batch complete: {Processed} indexed, {Failed} failed, {Skipped} skipped, {Remaining} remaining",
                processed, failed, skipped, _queue.Count);
        }

        // Delay before next batch to avoid overwhelming Ollama
        if (_queue.Count > 0)
        {
            await Task.Delay(_options.ProcessingBatchDelay, ct);
        }
    }

    private Task HandleRetryAsync(DeferredDocument document, Exception ex)
    {
        if (document.RetryCount >= _options.MaxRetryAttempts)
        {
            _logger.LogWarning(
                "Document {FilePath} exceeded max retry attempts ({Max}), dropping from queue",
                document.FilePath,
                _options.MaxRetryAttempts);
            return Task.CompletedTask;
        }

        var updatedDocument = document.WithRetry();
        var backoffDelay = CalculateBackoffDelay(updatedDocument.RetryCount);

        _logger.LogWarning(ex,
            "Retryable error indexing {FilePath} (attempt {Attempt}/{Max}), re-queueing with {Delay}s delay",
            document.FilePath,
            updatedDocument.RetryCount,
            _options.MaxRetryAttempts,
            backoffDelay.TotalSeconds);

        // Re-queue with updated retry count
        _queue.TryEnqueue(updatedDocument);

        return Task.CompletedTask;
    }

    private TimeSpan CalculateBackoffDelay(int retryCount)
    {
        // Exponential backoff: base * 2^(retry-1) with max cap
        var multiplier = Math.Pow(2, retryCount - 1);
        var delay = TimeSpan.FromTicks((long)(_options.RetryBaseDelay.Ticks * multiplier));

        // Cap at 5 minutes
        return delay > TimeSpan.FromMinutes(5) ? TimeSpan.FromMinutes(5) : delay;
    }

    private static bool IsRetryableError(Exception ex)
    {
        return ex is HttpRequestException or TaskCanceledException or TimeoutException;
    }

    private static async Task<string> ComputeContentHashAsync(string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var hashBytes = await System.Security.Cryptography.SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hashBytes);
    }

    private void LogShutdownState()
    {
        if (_queue.Count > 0)
        {
            _logger.LogWarning(
                "Deferred indexing processor shutting down with {Count} documents still in queue. " +
                "These will be re-indexed on next startup via reconciliation.",
                _queue.Count);
        }
        else
        {
            _logger.LogInformation("Deferred indexing processor stopped (queue empty)");
        }
    }
}
