using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompoundDocs.McpServer.Services.Queuing;

/// <summary>
/// In-memory implementation of the deferred indexing queue.
/// Thread-safe for concurrent access from file watcher and processor.
/// </summary>
public sealed class InMemoryDeferredIndexingQueue : IDeferredIndexingQueue
{
    private readonly ConcurrentQueue<DeferredDocument> _queue = new();
    private readonly ConcurrentDictionary<string, byte> _pathIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly DeferredIndexingOptions _options;
    private readonly ILogger<InMemoryDeferredIndexingQueue> _logger;
    private readonly object _overflowLock = new();
    private bool _warningLogged;

    public InMemoryDeferredIndexingQueue(
        IOptions<DeferredIndexingOptions> options,
        ILogger<InMemoryDeferredIndexingQueue> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public int Count => _queue.Count;
    public int MaxSize => _options.MaxQueueSize;
    public bool IsFull => Count >= MaxSize;

    public bool TryEnqueue(DeferredDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.FilePath);

        // Check for duplicates
        if (_pathIndex.ContainsKey(document.FilePath))
        {
            _logger.LogDebug(
                "Document {FilePath} already in deferred queue, skipping",
                document.FilePath);
            return true; // Already queued, consider success
        }

        lock (_overflowLock)
        {
            // Check capacity and handle overflow
            if (Count >= MaxSize)
            {
                return HandleOverflow(document);
            }

            // Check warning threshold
            var thresholdCount = (int)(MaxSize * (_options.WarningThresholdPercent / 100.0));
            if (Count >= thresholdCount && !_warningLogged)
            {
                _logger.LogWarning(
                    "Deferred indexing queue at {Percent}% capacity ({Count}/{Max})",
                    _options.WarningThresholdPercent,
                    Count,
                    MaxSize);
                _warningLogged = true;
            }

            // Enqueue the document
            _queue.Enqueue(document);
            _pathIndex.TryAdd(document.FilePath, 0);

            _logger.LogInformation(
                "Document {FilePath} added to deferred indexing queue (queue size: {Count})",
                document.FilePath,
                Count);

            return true;
        }
    }

    private bool HandleOverflow(DeferredDocument newDocument)
    {
        switch (_options.OverflowStrategy)
        {
            case OverflowStrategy.DropOldest:
                if (_queue.TryDequeue(out var dropped))
                {
                    _pathIndex.TryRemove(dropped.FilePath, out _);
                    _logger.LogWarning(
                        "Queue overflow: dropped oldest document {DroppedPath} to make room for {NewPath}",
                        dropped.FilePath,
                        newDocument.FilePath);

                    _queue.Enqueue(newDocument);
                    _pathIndex.TryAdd(newDocument.FilePath, 0);
                    return true;
                }
                return false;

            case OverflowStrategy.DropNewest:
                _logger.LogWarning(
                    "Queue overflow: rejecting new document {FilePath} (DropNewest strategy)",
                    newDocument.FilePath);
                return false;

            case OverflowStrategy.RejectNew:
            default:
                _logger.LogError(
                    "Queue overflow: cannot enqueue {FilePath}, queue is full ({Count}/{Max})",
                    newDocument.FilePath,
                    Count,
                    MaxSize);
                return false;
        }
    }

    public bool TryDequeue(out DeferredDocument? document)
    {
        if (_queue.TryDequeue(out document))
        {
            _pathIndex.TryRemove(document.FilePath, out _);

            // Reset warning flag if queue drops below threshold
            var thresholdCount = (int)(MaxSize * (_options.WarningThresholdPercent / 100.0));
            if (Count < thresholdCount)
            {
                _warningLogged = false;
            }

            return true;
        }

        document = null;
        return false;
    }

    public bool TryPeek(out DeferredDocument? document)
    {
        return _queue.TryPeek(out document);
    }

    public bool Contains(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return _pathIndex.ContainsKey(filePath);
    }

    public bool TryRemove(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // Note: ConcurrentQueue doesn't support removal by value.
        // We mark it as removed in the index; processor will skip it.
        if (_pathIndex.TryRemove(filePath, out _))
        {
            _logger.LogDebug(
                "Document {FilePath} marked for removal from deferred queue",
                filePath);
            return true;
        }
        return false;
    }

    public IReadOnlyList<DeferredDocument> GetSnapshot()
    {
        return _queue.ToArray();
    }

    public void Clear()
    {
        _queue.Clear();
        _pathIndex.Clear();
        _warningLogged = false;

        _logger.LogInformation("Deferred indexing queue cleared");
    }
}
