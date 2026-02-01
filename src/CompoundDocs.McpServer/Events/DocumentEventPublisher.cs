using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Events;

/// <summary>
/// Implementation of document event publisher using System.Threading.Channels for async dispatch.
/// </summary>
public sealed class DocumentEventPublisher : IDocumentEventPublisher, IDocumentEventSubscriber, IAsyncDisposable
{
    private readonly ILogger<DocumentEventPublisher> _logger;
    private readonly Channel<DocumentLifecycleEventArgs> _channel;
    private readonly List<HandlerRegistration> _handlers = [];
    private readonly object _handlersLock = new();
    private readonly CancellationTokenSource _shutdownCts = new();
    private bool _isDisposed;

    /// <summary>
    /// Creates a new instance of DocumentEventPublisher.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public DocumentEventPublisher(ILogger<DocumentEventPublisher> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Create an unbounded channel for events
        // In production, consider bounded channels with appropriate capacity
        _channel = Channel.CreateUnbounded<DocumentLifecycleEventArgs>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = false,
            AllowSynchronousContinuations = false
        });
    }

    #region IDocumentEventPublisher Implementation

    /// <inheritdoc />
    public Task PublishCreatedAsync(DocumentCreatedEventArgs eventArgs, CancellationToken cancellationToken = default)
    {
        return PublishAsync(eventArgs, cancellationToken);
    }

    /// <inheritdoc />
    public Task PublishUpdatedAsync(DocumentUpdatedEventArgs eventArgs, CancellationToken cancellationToken = default)
    {
        return PublishAsync(eventArgs, cancellationToken);
    }

    /// <inheritdoc />
    public Task PublishDeletedAsync(DocumentDeletedEventArgs eventArgs, CancellationToken cancellationToken = default)
    {
        return PublishAsync(eventArgs, cancellationToken);
    }

    /// <inheritdoc />
    public Task PublishPromotedAsync(DocumentPromotedEventArgs eventArgs, CancellationToken cancellationToken = default)
    {
        return PublishAsync(eventArgs, cancellationToken);
    }

    /// <inheritdoc />
    public Task PublishSupersededAsync(DocumentSupersededEventArgs eventArgs, CancellationToken cancellationToken = default)
    {
        return PublishAsync(eventArgs, cancellationToken);
    }

    /// <inheritdoc />
    public Task PublishReferencesResolvedAsync(ReferencesResolvedEventArgs eventArgs, CancellationToken cancellationToken = default)
    {
        return PublishAsync(eventArgs, cancellationToken);
    }

    /// <inheritdoc />
    public Task PublishValidatedAsync(DocumentValidatedEventArgs eventArgs, CancellationToken cancellationToken = default)
    {
        return PublishAsync(eventArgs, cancellationToken);
    }

    /// <inheritdoc />
    public async Task PublishAsync(DocumentLifecycleEventArgs eventArgs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (_isDisposed)
        {
            _logger.LogWarning("Attempted to publish event after disposal: {EventType}", eventArgs.EventType);
            return;
        }

        try
        {
            await _channel.Writer.WriteAsync(eventArgs, cancellationToken);
            _logger.LogDebug("Published event {EventType} for {FilePath}",
                eventArgs.EventType, eventArgs.FilePath);
        }
        catch (ChannelClosedException)
        {
            _logger.LogWarning("Channel closed while publishing event: {EventType}", eventArgs.EventType);
        }
    }

    #endregion

    #region IDocumentEventSubscriber Implementation

    /// <inheritdoc />
    public IDisposable OnCreated(Func<DocumentCreatedEventArgs, CancellationToken, Task> handler)
    {
        return Subscribe(DocumentLifecycleEventType.Created, WrapHandler(handler));
    }

    /// <inheritdoc />
    public IDisposable OnUpdated(Func<DocumentUpdatedEventArgs, CancellationToken, Task> handler)
    {
        return Subscribe(DocumentLifecycleEventType.Updated, WrapHandler(handler));
    }

    /// <inheritdoc />
    public IDisposable OnDeleted(Func<DocumentDeletedEventArgs, CancellationToken, Task> handler)
    {
        return Subscribe(DocumentLifecycleEventType.Deleted, WrapHandler(handler));
    }

    /// <inheritdoc />
    public IDisposable OnPromoted(Func<DocumentPromotedEventArgs, CancellationToken, Task> handler)
    {
        return Subscribe(DocumentLifecycleEventType.Promoted, WrapHandler(handler));
    }

    /// <inheritdoc />
    public IDisposable OnSuperseded(Func<DocumentSupersededEventArgs, CancellationToken, Task> handler)
    {
        return Subscribe(DocumentLifecycleEventType.Superseded, WrapHandler(handler));
    }

    /// <inheritdoc />
    public IDisposable OnReferencesResolved(Func<ReferencesResolvedEventArgs, CancellationToken, Task> handler)
    {
        return Subscribe(DocumentLifecycleEventType.ReferencesResolved, WrapHandler(handler));
    }

    /// <inheritdoc />
    public IDisposable OnValidated(Func<DocumentValidatedEventArgs, CancellationToken, Task> handler)
    {
        return Subscribe(DocumentLifecycleEventType.Validated, WrapHandler(handler));
    }

    /// <inheritdoc />
    public IDisposable OnAny(Func<DocumentLifecycleEventArgs, CancellationToken, Task> handler)
    {
        return Subscribe(null, handler);
    }

    #endregion

    #region Event Processing

    /// <summary>
    /// Starts the background event processing loop.
    /// Call this when the application starts.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when processing stops.</returns>
    public async Task StartProcessingAsync(CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _shutdownCts.Token);

        _logger.LogInformation("Starting document event processing");

        try
        {
            await foreach (var eventArgs in _channel.Reader.ReadAllAsync(linkedCts.Token))
            {
                await ProcessEventAsync(eventArgs, linkedCts.Token);
            }
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
        {
            _logger.LogInformation("Document event processing stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in document event processing loop");
            throw;
        }
    }

    /// <summary>
    /// Stops the background event processing.
    /// </summary>
    public void StopProcessing()
    {
        _shutdownCts.Cancel();
        _channel.Writer.Complete();
    }

    private async Task ProcessEventAsync(DocumentLifecycleEventArgs eventArgs, CancellationToken cancellationToken)
    {
        List<HandlerRegistration> matchingHandlers;

        lock (_handlersLock)
        {
            matchingHandlers = _handlers
                .Where(h => h.EventType == null || h.EventType == eventArgs.EventType)
                .ToList();
        }

        if (matchingHandlers.Count == 0)
        {
            _logger.LogDebug("No handlers registered for event type: {EventType}", eventArgs.EventType);
            return;
        }

        _logger.LogDebug("Processing event {EventType} with {HandlerCount} handlers",
            eventArgs.EventType, matchingHandlers.Count);

        // Process handlers in parallel with error isolation
        var tasks = matchingHandlers.Select(async handler =>
        {
            try
            {
                await handler.Handler(eventArgs, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error in event handler for {EventType}: {HandlerId}",
                    eventArgs.EventType, handler.Id);
            }
        });

        await Task.WhenAll(tasks);
    }

    #endregion

    #region Helper Methods

    private IDisposable Subscribe(
        DocumentLifecycleEventType? eventType,
        Func<DocumentLifecycleEventArgs, CancellationToken, Task> handler)
    {
        var registration = new HandlerRegistration
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            Handler = handler
        };

        lock (_handlersLock)
        {
            _handlers.Add(registration);
        }

        _logger.LogDebug("Subscribed handler {HandlerId} for event type: {EventType}",
            registration.Id, eventType?.ToString() ?? "All");

        return new Unsubscriber(() =>
        {
            lock (_handlersLock)
            {
                _handlers.Remove(registration);
            }
            _logger.LogDebug("Unsubscribed handler {HandlerId}", registration.Id);
        });
    }

    private static Func<DocumentLifecycleEventArgs, CancellationToken, Task> WrapHandler<T>(
        Func<T, CancellationToken, Task> handler) where T : DocumentLifecycleEventArgs
    {
        return async (eventArgs, ct) =>
        {
            if (eventArgs is T typedArgs)
            {
                await handler(typedArgs, ct);
            }
        };
    }

    #endregion

    #region IAsyncDisposable

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        StopProcessing();

        await _shutdownCts.CancelAsync();
        _shutdownCts.Dispose();

        lock (_handlersLock)
        {
            _handlers.Clear();
        }

        _logger.LogInformation("Document event publisher disposed");
    }

    #endregion

    #region Private Types

    private sealed class HandlerRegistration
    {
        public required Guid Id { get; init; }
        public DocumentLifecycleEventType? EventType { get; init; }
        public required Func<DocumentLifecycleEventArgs, CancellationToken, Task> Handler { get; init; }
    }

    private sealed class Unsubscriber : IDisposable
    {
        private readonly Action _unsubscribe;
        private bool _disposed;

        public Unsubscriber(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _unsubscribe();
                _disposed = true;
            }
        }
    }

    #endregion
}

/// <summary>
/// Background service that processes document events.
/// </summary>
public sealed class DocumentEventProcessingService : BackgroundService
{
    private readonly DocumentEventPublisher _publisher;
    private readonly ILogger<DocumentEventProcessingService> _logger;

    /// <summary>
    /// Creates a new instance of DocumentEventProcessingService.
    /// </summary>
    /// <param name="publisher">The document event publisher.</param>
    /// <param name="logger">Logger instance.</param>
    public DocumentEventProcessingService(
        DocumentEventPublisher publisher,
        ILogger<DocumentEventProcessingService> logger)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Document event processing service starting");

        try
        {
            await _publisher.StartProcessingAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document event processing service failed");
            throw;
        }

        _logger.LogInformation("Document event processing service stopped");
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Document event processing service stopping");
        _publisher.StopProcessing();
        await base.StopAsync(cancellationToken);
    }
}
