using CompoundDocs.McpServer.Models;

namespace CompoundDocs.McpServer.Events;

/// <summary>
/// Types of document lifecycle events.
/// </summary>
public enum DocumentLifecycleEventType
{
    /// <summary>A new document was created/indexed.</summary>
    Created,

    /// <summary>An existing document was updated.</summary>
    Updated,

    /// <summary>A document was deleted.</summary>
    Deleted,

    /// <summary>A document's promotion level was changed.</summary>
    Promoted,

    /// <summary>A document was superseded by another document.</summary>
    Superseded,

    /// <summary>A document's references were resolved.</summary>
    ReferencesResolved,

    /// <summary>A document validation completed.</summary>
    Validated
}

/// <summary>
/// Base class for document lifecycle event arguments.
/// </summary>
public abstract class DocumentLifecycleEventArgs : EventArgs
{
    /// <summary>
    /// The type of lifecycle event.
    /// </summary>
    public abstract DocumentLifecycleEventType EventType { get; }

    /// <summary>
    /// The file path of the document.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// The tenant key for the document.
    /// </summary>
    public required string TenantKey { get; init; }

    /// <summary>
    /// The timestamp when the event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional correlation ID for tracing related events.
    /// </summary>
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Event arguments for when a document is created.
/// </summary>
public sealed class DocumentCreatedEventArgs : DocumentLifecycleEventArgs
{
    /// <inheritdoc />
    public override DocumentLifecycleEventType EventType => DocumentLifecycleEventType.Created;

    /// <summary>
    /// The created document.
    /// </summary>
    public required CompoundDocument Document { get; init; }

    /// <summary>
    /// The document type identifier.
    /// </summary>
    public required string DocType { get; init; }

    /// <summary>
    /// The initial promotion level.
    /// </summary>
    public required string PromotionLevel { get; init; }

    /// <summary>
    /// Whether the document was indexed (had embeddings generated).
    /// </summary>
    public bool WasIndexed { get; init; }

    /// <summary>
    /// Creates event args for a document creation event.
    /// </summary>
    public static DocumentCreatedEventArgs Create(
        CompoundDocument document,
        string correlationId = null!)
    {
        return new DocumentCreatedEventArgs
        {
            Document = document,
            FilePath = document.FilePath,
            TenantKey = document.TenantKey,
            DocType = document.DocType,
            PromotionLevel = document.PromotionLevel,
            WasIndexed = document.Vector.HasValue,
            CorrelationId = correlationId
        };
    }
}

/// <summary>
/// Event arguments for when a document is updated.
/// </summary>
public sealed class DocumentUpdatedEventArgs : DocumentLifecycleEventArgs
{
    /// <inheritdoc />
    public override DocumentLifecycleEventType EventType => DocumentLifecycleEventType.Updated;

    /// <summary>
    /// The updated document.
    /// </summary>
    public required CompoundDocument Document { get; init; }

    /// <summary>
    /// The previous document type (if changed).
    /// </summary>
    public string? PreviousDocType { get; init; }

    /// <summary>
    /// The new document type.
    /// </summary>
    public required string NewDocType { get; init; }

    /// <summary>
    /// The previous promotion level (if changed).
    /// </summary>
    public string? PreviousPromotionLevel { get; init; }

    /// <summary>
    /// The new promotion level.
    /// </summary>
    public required string NewPromotionLevel { get; init; }

    /// <summary>
    /// Whether the content changed (requiring re-indexing).
    /// </summary>
    public bool ContentChanged { get; init; }

    /// <summary>
    /// Whether the document was re-indexed.
    /// </summary>
    public bool WasReIndexed { get; init; }

    /// <summary>
    /// Creates event args for a document update event.
    /// </summary>
    public static DocumentUpdatedEventArgs Create(
        CompoundDocument document,
        string? previousDocType = null,
        string? previousPromotionLevel = null,
        bool contentChanged = true,
        bool wasReIndexed = true,
        string correlationId = null!)
    {
        return new DocumentUpdatedEventArgs
        {
            Document = document,
            FilePath = document.FilePath,
            TenantKey = document.TenantKey,
            PreviousDocType = previousDocType,
            NewDocType = document.DocType,
            PreviousPromotionLevel = previousPromotionLevel,
            NewPromotionLevel = document.PromotionLevel,
            ContentChanged = contentChanged,
            WasReIndexed = wasReIndexed,
            CorrelationId = correlationId
        };
    }
}

/// <summary>
/// Event arguments for when a document is deleted.
/// </summary>
public sealed class DocumentDeletedEventArgs : DocumentLifecycleEventArgs
{
    /// <inheritdoc />
    public override DocumentLifecycleEventType EventType => DocumentLifecycleEventType.Deleted;

    /// <summary>
    /// The document ID that was deleted.
    /// </summary>
    public required string DocumentId { get; init; }

    /// <summary>
    /// The document type of the deleted document.
    /// </summary>
    public string? DocType { get; init; }

    /// <summary>
    /// The title of the deleted document.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Whether the deletion was due to file deletion (vs. explicit removal).
    /// </summary>
    public bool IsFileSystemDeletion { get; init; }

    /// <summary>
    /// Creates event args for a document deletion event.
    /// </summary>
    public static DocumentDeletedEventArgs Create(
        string documentId,
        string filePath,
        string tenantKey,
        string? docType = null,
        string? title = null,
        bool isFileSystemDeletion = false,
        string correlationId = null!)
    {
        return new DocumentDeletedEventArgs
        {
            DocumentId = documentId,
            FilePath = filePath,
            TenantKey = tenantKey,
            DocType = docType,
            Title = title,
            IsFileSystemDeletion = isFileSystemDeletion,
            CorrelationId = correlationId
        };
    }
}

/// <summary>
/// Event arguments for when a document's promotion level changes.
/// </summary>
public sealed class DocumentPromotedEventArgs : DocumentLifecycleEventArgs
{
    /// <inheritdoc />
    public override DocumentLifecycleEventType EventType => DocumentLifecycleEventType.Promoted;

    /// <summary>
    /// The document ID.
    /// </summary>
    public required string DocumentId { get; init; }

    /// <summary>
    /// The previous promotion level.
    /// </summary>
    public required string PreviousLevel { get; init; }

    /// <summary>
    /// The new promotion level.
    /// </summary>
    public required string NewLevel { get; init; }

    /// <summary>
    /// The reason for the promotion change (if provided).
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Whether this was an automatic promotion (vs. manual).
    /// </summary>
    public bool IsAutomatic { get; init; }

    /// <summary>
    /// Creates event args for a document promotion event.
    /// </summary>
    public static DocumentPromotedEventArgs Create(
        string documentId,
        string filePath,
        string tenantKey,
        string previousLevel,
        string newLevel,
        string? reason = null,
        bool isAutomatic = false,
        string correlationId = null!)
    {
        return new DocumentPromotedEventArgs
        {
            DocumentId = documentId,
            FilePath = filePath,
            TenantKey = tenantKey,
            PreviousLevel = previousLevel,
            NewLevel = newLevel,
            Reason = reason,
            IsAutomatic = isAutomatic,
            CorrelationId = correlationId
        };
    }
}

/// <summary>
/// Event arguments for when a document is superseded by another.
/// </summary>
public sealed class DocumentSupersededEventArgs : DocumentLifecycleEventArgs
{
    /// <inheritdoc />
    public override DocumentLifecycleEventType EventType => DocumentLifecycleEventType.Superseded;

    /// <summary>
    /// The document ID of the superseded document.
    /// </summary>
    public required string SupersededDocumentId { get; init; }

    /// <summary>
    /// The file path of the superseding document.
    /// </summary>
    public required string SupersedingFilePath { get; init; }

    /// <summary>
    /// The document ID of the superseding document.
    /// </summary>
    public required string SupersedingDocumentId { get; init; }

    /// <summary>
    /// The new promotion level applied to the superseded document.
    /// </summary>
    public string? NewPromotionLevel { get; init; }

    /// <summary>
    /// Creates event args for a document supersession event.
    /// </summary>
    public static DocumentSupersededEventArgs Create(
        string supersededDocumentId,
        string supersededFilePath,
        string tenantKey,
        string supersedingFilePath,
        string supersedingDocumentId,
        string? newPromotionLevel = null,
        string correlationId = null!)
    {
        return new DocumentSupersededEventArgs
        {
            SupersededDocumentId = supersededDocumentId,
            FilePath = supersededFilePath,
            TenantKey = tenantKey,
            SupersedingFilePath = supersedingFilePath,
            SupersedingDocumentId = supersedingDocumentId,
            NewPromotionLevel = newPromotionLevel,
            CorrelationId = correlationId
        };
    }
}

/// <summary>
/// Event arguments for when document references are resolved.
/// </summary>
public sealed class ReferencesResolvedEventArgs : DocumentLifecycleEventArgs
{
    /// <inheritdoc />
    public override DocumentLifecycleEventType EventType => DocumentLifecycleEventType.ReferencesResolved;

    /// <summary>
    /// The document ID.
    /// </summary>
    public required string DocumentId { get; init; }

    /// <summary>
    /// The total number of references found.
    /// </summary>
    public int TotalReferences { get; init; }

    /// <summary>
    /// The number of references successfully resolved.
    /// </summary>
    public int ResolvedCount { get; init; }

    /// <summary>
    /// The number of broken/unresolved references.
    /// </summary>
    public int BrokenCount { get; init; }

    /// <summary>
    /// The paths of broken references.
    /// </summary>
    public IReadOnlyList<string> BrokenPaths { get; init; } = [];

    /// <summary>
    /// Creates event args for a references resolved event.
    /// </summary>
    public static ReferencesResolvedEventArgs Create(
        string documentId,
        string filePath,
        string tenantKey,
        int totalReferences,
        int resolvedCount,
        IReadOnlyList<string>? brokenPaths = null,
        string correlationId = null!)
    {
        var brokenList = brokenPaths ?? [];
        return new ReferencesResolvedEventArgs
        {
            DocumentId = documentId,
            FilePath = filePath,
            TenantKey = tenantKey,
            TotalReferences = totalReferences,
            ResolvedCount = resolvedCount,
            BrokenCount = brokenList.Count,
            BrokenPaths = brokenList,
            CorrelationId = correlationId
        };
    }
}

/// <summary>
/// Event arguments for when document validation completes.
/// </summary>
public sealed class DocumentValidatedEventArgs : DocumentLifecycleEventArgs
{
    /// <inheritdoc />
    public override DocumentLifecycleEventType EventType => DocumentLifecycleEventType.Validated;

    /// <summary>
    /// The document ID.
    /// </summary>
    public required string DocumentId { get; init; }

    /// <summary>
    /// The document type being validated against.
    /// </summary>
    public required string DocType { get; init; }

    /// <summary>
    /// Whether validation was successful.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Validation errors, if any.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Validation warnings, if any.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Creates event args for a document validation event.
    /// </summary>
    public static DocumentValidatedEventArgs Create(
        string documentId,
        string filePath,
        string tenantKey,
        string docType,
        bool isValid,
        IReadOnlyList<string>? errors = null,
        IReadOnlyList<string>? warnings = null,
        string correlationId = null!)
    {
        return new DocumentValidatedEventArgs
        {
            DocumentId = documentId,
            FilePath = filePath,
            TenantKey = tenantKey,
            DocType = docType,
            IsValid = isValid,
            Errors = errors ?? [],
            Warnings = warnings ?? [],
            CorrelationId = correlationId
        };
    }
}
