namespace CompoundDocs.McpServer.Events;

/// <summary>
/// Interface for publishing document lifecycle events.
/// </summary>
public interface IDocumentEventPublisher
{
    /// <summary>
    /// Publishes a document created event.
    /// </summary>
    /// <param name="eventArgs">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task PublishCreatedAsync(DocumentCreatedEventArgs eventArgs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a document updated event.
    /// </summary>
    /// <param name="eventArgs">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task PublishUpdatedAsync(DocumentUpdatedEventArgs eventArgs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a document deleted event.
    /// </summary>
    /// <param name="eventArgs">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task PublishDeletedAsync(DocumentDeletedEventArgs eventArgs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a document promoted event.
    /// </summary>
    /// <param name="eventArgs">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task PublishPromotedAsync(DocumentPromotedEventArgs eventArgs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a document superseded event.
    /// </summary>
    /// <param name="eventArgs">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task PublishSupersededAsync(DocumentSupersededEventArgs eventArgs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a references resolved event.
    /// </summary>
    /// <param name="eventArgs">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task PublishReferencesResolvedAsync(ReferencesResolvedEventArgs eventArgs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a document validated event.
    /// </summary>
    /// <param name="eventArgs">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task PublishValidatedAsync(DocumentValidatedEventArgs eventArgs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes any document lifecycle event.
    /// </summary>
    /// <param name="eventArgs">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task PublishAsync(DocumentLifecycleEventArgs eventArgs, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for subscribing to document lifecycle events.
/// </summary>
public interface IDocumentEventSubscriber
{
    /// <summary>
    /// Subscribes a handler for document created events.
    /// </summary>
    /// <param name="handler">The event handler.</param>
    /// <returns>A disposable to unsubscribe.</returns>
    IDisposable OnCreated(Func<DocumentCreatedEventArgs, CancellationToken, Task> handler);

    /// <summary>
    /// Subscribes a handler for document updated events.
    /// </summary>
    /// <param name="handler">The event handler.</param>
    /// <returns>A disposable to unsubscribe.</returns>
    IDisposable OnUpdated(Func<DocumentUpdatedEventArgs, CancellationToken, Task> handler);

    /// <summary>
    /// Subscribes a handler for document deleted events.
    /// </summary>
    /// <param name="handler">The event handler.</param>
    /// <returns>A disposable to unsubscribe.</returns>
    IDisposable OnDeleted(Func<DocumentDeletedEventArgs, CancellationToken, Task> handler);

    /// <summary>
    /// Subscribes a handler for document promoted events.
    /// </summary>
    /// <param name="handler">The event handler.</param>
    /// <returns>A disposable to unsubscribe.</returns>
    IDisposable OnPromoted(Func<DocumentPromotedEventArgs, CancellationToken, Task> handler);

    /// <summary>
    /// Subscribes a handler for document superseded events.
    /// </summary>
    /// <param name="handler">The event handler.</param>
    /// <returns>A disposable to unsubscribe.</returns>
    IDisposable OnSuperseded(Func<DocumentSupersededEventArgs, CancellationToken, Task> handler);

    /// <summary>
    /// Subscribes a handler for references resolved events.
    /// </summary>
    /// <param name="handler">The event handler.</param>
    /// <returns>A disposable to unsubscribe.</returns>
    IDisposable OnReferencesResolved(Func<ReferencesResolvedEventArgs, CancellationToken, Task> handler);

    /// <summary>
    /// Subscribes a handler for document validated events.
    /// </summary>
    /// <param name="handler">The event handler.</param>
    /// <returns>A disposable to unsubscribe.</returns>
    IDisposable OnValidated(Func<DocumentValidatedEventArgs, CancellationToken, Task> handler);

    /// <summary>
    /// Subscribes a handler for all document lifecycle events.
    /// </summary>
    /// <param name="handler">The event handler.</param>
    /// <returns>A disposable to unsubscribe.</returns>
    IDisposable OnAny(Func<DocumentLifecycleEventArgs, CancellationToken, Task> handler);
}
