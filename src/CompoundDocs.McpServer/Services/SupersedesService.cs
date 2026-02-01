using System.Collections.Concurrent;
using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.Events;
using CompoundDocs.McpServer.Models;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Service for handling document supersession relationships.
/// When a document has a `supersedes` frontmatter field, this service manages
/// the relationship and adjusts promotion levels accordingly.
/// </summary>
public interface ISupersedesService
{
    /// <summary>
    /// Processes the supersedes relationship for a document.
    /// </summary>
    /// <param name="document">The document with potential supersedes frontmatter.</param>
    /// <param name="frontmatter">The parsed frontmatter dictionary.</param>
    /// <param name="tenantKey">The tenant key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of processing the supersession.</returns>
    Task<SupersessionResult> ProcessSupersessionAsync(
        CompoundDocument document,
        IDictionary<string, object?> frontmatter,
        string tenantKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all documents that a given document supersedes.
    /// </summary>
    /// <param name="documentPath">The file path of the superseding document.</param>
    /// <param name="tenantKey">The tenant key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of superseded document paths.</returns>
    Task<IReadOnlyList<string>> GetSupersededDocumentsAsync(
        string documentPath,
        string tenantKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the document that supersedes a given document.
    /// </summary>
    /// <param name="documentPath">The file path of the potentially superseded document.</param>
    /// <param name="tenantKey">The tenant key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The superseding document path, or null if not superseded.</returns>
    Task<string?> GetSupersedingDocumentAsync(
        string documentPath,
        string tenantKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full supersession chain for a document.
    /// </summary>
    /// <param name="documentPath">The starting document path.</param>
    /// <param name="tenantKey">The tenant key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The supersession chain from oldest to newest.</returns>
    Task<SupersessionChain> GetSupersessionChainAsync(
        string documentPath,
        string tenantKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a document from the supersession tracking.
    /// Called when a document is deleted.
    /// </summary>
    /// <param name="documentPath">The document path being removed.</param>
    /// <param name="tenantKey">The tenant key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveFromChainAsync(
        string documentPath,
        string tenantKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a document is superseded by another.
    /// </summary>
    /// <param name="documentPath">The document path to check.</param>
    /// <param name="tenantKey">The tenant key.</param>
    /// <returns>True if the document has been superseded.</returns>
    bool IsSuperseded(string documentPath, string tenantKey);

    /// <summary>
    /// Adjusts search result scores based on supersession status.
    /// Superseded documents receive lower scores.
    /// </summary>
    /// <param name="documents">The documents to adjust.</param>
    /// <param name="tenantKey">The tenant key.</param>
    /// <returns>Documents with adjusted scores.</returns>
    IReadOnlyList<ScoredDocument> AdjustScoresForSupersession(
        IReadOnlyList<ScoredDocument> documents,
        string tenantKey);
}

/// <summary>
/// Result of processing a supersession relationship.
/// </summary>
public sealed class SupersessionResult
{
    /// <summary>
    /// Whether a supersession relationship was found and processed.
    /// </summary>
    public bool HasSupersession { get; init; }

    /// <summary>
    /// The paths of documents that were superseded.
    /// </summary>
    public IReadOnlyList<string> SupersededPaths { get; init; } = [];

    /// <summary>
    /// Documents that couldn't be found (broken references).
    /// </summary>
    public IReadOnlyList<string> NotFoundPaths { get; init; } = [];

    /// <summary>
    /// Any errors that occurred during processing.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Creates a result indicating no supersession.
    /// </summary>
    public static SupersessionResult None() => new() { HasSupersession = false };

    /// <summary>
    /// Creates a successful supersession result.
    /// </summary>
    public static SupersessionResult Success(
        IReadOnlyList<string> supersededPaths,
        IReadOnlyList<string>? notFoundPaths = null)
    {
        return new SupersessionResult
        {
            HasSupersession = true,
            SupersededPaths = supersededPaths,
            NotFoundPaths = notFoundPaths ?? []
        };
    }

    /// <summary>
    /// Creates an error result.
    /// </summary>
    public static SupersessionResult Error(params string[] errors)
    {
        return new SupersessionResult
        {
            HasSupersession = false,
            Errors = errors
        };
    }
}

/// <summary>
/// Represents a chain of document supersessions.
/// </summary>
public sealed class SupersessionChain
{
    /// <summary>
    /// The documents in the chain, ordered from oldest to newest.
    /// </summary>
    public IReadOnlyList<SupersessionEntry> Entries { get; init; } = [];

    /// <summary>
    /// The most current document in the chain.
    /// </summary>
    public string? CurrentDocument => Entries.LastOrDefault()?.DocumentPath;

    /// <summary>
    /// The original document that started the chain.
    /// </summary>
    public string? OriginalDocument => Entries.FirstOrDefault()?.DocumentPath;

    /// <summary>
    /// The total length of the chain.
    /// </summary>
    public int Length => Entries.Count;
}

/// <summary>
/// An entry in a supersession chain.
/// </summary>
public sealed class SupersessionEntry
{
    /// <summary>
    /// The document path.
    /// </summary>
    public required string DocumentPath { get; init; }

    /// <summary>
    /// The document ID.
    /// </summary>
    public string? DocumentId { get; init; }

    /// <summary>
    /// The document title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// When the supersession was recorded.
    /// </summary>
    public DateTimeOffset SupersededAt { get; init; }

    /// <summary>
    /// The path of the document that supersedes this one (if any).
    /// </summary>
    public string? SupersededBy { get; init; }
}

/// <summary>
/// A document with an associated relevance score.
/// </summary>
public sealed class ScoredDocument
{
    /// <summary>
    /// The document.
    /// </summary>
    public required CompoundDocument Document { get; init; }

    /// <summary>
    /// The relevance score (0.0 to 1.0).
    /// </summary>
    public float Score { get; set; }

    /// <summary>
    /// Whether this document has been superseded.
    /// </summary>
    public bool IsSuperseded { get; set; }

    /// <summary>
    /// The path of the superseding document, if any.
    /// </summary>
    public string? SupersededBy { get; set; }
}

/// <summary>
/// Implementation of ISupersedesService.
/// </summary>
public sealed class SupersedesService : ISupersedesService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentEventPublisher _eventPublisher;
    private readonly ILogger<SupersedesService> _logger;

    /// <summary>
    /// Tracks supersession relationships: superseding document -> superseded documents.
    /// </summary>
    private readonly ConcurrentDictionary<string, HashSet<string>> _supersedes = new();

    /// <summary>
    /// Tracks reverse supersession: superseded document -> superseding document.
    /// </summary>
    private readonly ConcurrentDictionary<string, string> _supersededBy = new();

    /// <summary>
    /// Lock for thread-safe modifications.
    /// </summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Score penalty for superseded documents (multiplier).
    /// </summary>
    private const float SupersededScorePenalty = 0.5f;

    /// <summary>
    /// Creates a new instance of SupersedesService.
    /// </summary>
    /// <param name="documentRepository">The document repository.</param>
    /// <param name="eventPublisher">The event publisher for lifecycle events.</param>
    /// <param name="logger">Logger instance.</param>
    public SupersedesService(
        IDocumentRepository documentRepository,
        IDocumentEventPublisher eventPublisher,
        ILogger<SupersedesService> logger)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<SupersessionResult> ProcessSupersessionAsync(
        CompoundDocument document,
        IDictionary<string, object?> frontmatter,
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(frontmatter);

        // Check for supersedes field in frontmatter
        var supersedes = ExtractSupersedesValue(frontmatter);
        if (supersedes.Count == 0)
        {
            return SupersessionResult.None();
        }

        _logger.LogInformation(
            "Processing supersession: {Document} supersedes {Count} document(s)",
            document.FilePath, supersedes.Count);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var supersededPaths = new List<string>();
            var notFoundPaths = new List<string>();

            foreach (var supersededPath in supersedes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Try to find the superseded document
                var supersededDoc = await _documentRepository.GetByTenantKeyAsync(
                    tenantKey, supersededPath, cancellationToken);

                if (supersededDoc == null)
                {
                    _logger.LogWarning(
                        "Superseded document not found: {Path}",
                        supersededPath);
                    notFoundPaths.Add(supersededPath);
                    continue;
                }

                // Update the tracking dictionaries
                var key = CreateKey(document.FilePath, tenantKey);
                var supersededKey = CreateKey(supersededPath, tenantKey);

                if (!_supersedes.TryGetValue(key, out var supersededSet))
                {
                    supersededSet = [];
                    _supersedes[key] = supersededSet;
                }
                supersededSet.Add(supersededPath);

                _supersededBy[supersededKey] = document.FilePath;
                supersededPaths.Add(supersededPath);

                // Update the superseded document's promotion level if needed
                await UpdateSupersededDocumentAsync(
                    supersededDoc, document, tenantKey, cancellationToken);

                // Publish supersession event
                await _eventPublisher.PublishSupersededAsync(
                    DocumentSupersededEventArgs.Create(
                        supersededDocumentId: supersededDoc.Id,
                        supersededFilePath: supersededPath,
                        tenantKey: tenantKey,
                        supersedingFilePath: document.FilePath,
                        supersedingDocumentId: document.Id,
                        newPromotionLevel: PromotionLevels.Standard),
                    cancellationToken);
            }

            _logger.LogInformation(
                "Supersession processed: {SuccessCount} succeeded, {NotFoundCount} not found",
                supersededPaths.Count, notFoundPaths.Count);

            return SupersessionResult.Success(supersededPaths, notFoundPaths);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetSupersededDocumentsAsync(
        string documentPath,
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        var key = CreateKey(documentPath, tenantKey);

        if (_supersedes.TryGetValue(key, out var supersededSet))
        {
            return Task.FromResult<IReadOnlyList<string>>(supersededSet.ToList());
        }

        return Task.FromResult<IReadOnlyList<string>>([]);
    }

    /// <inheritdoc />
    public Task<string?> GetSupersedingDocumentAsync(
        string documentPath,
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        var key = CreateKey(documentPath, tenantKey);

        if (_supersededBy.TryGetValue(key, out var supersedingPath))
        {
            return Task.FromResult<string?>(supersedingPath);
        }

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc />
    public async Task<SupersessionChain> GetSupersessionChainAsync(
        string documentPath,
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<SupersessionEntry>();
        var visited = new HashSet<string>();

        // First, find the oldest document in the chain by walking backwards
        var current = documentPath;
        var oldest = documentPath;

        while (true)
        {
            var supersedingKey = CreateKey(current, tenantKey);

            // Find if this document superseded something (walk backwards)
            var previousDoc = await FindPreviousInChainAsync(current, tenantKey, cancellationToken);

            if (previousDoc == null || visited.Contains(previousDoc))
                break;

            visited.Add(current);
            oldest = previousDoc;
            current = previousDoc;
        }

        // Now walk forward from the oldest to build the chain
        visited.Clear();
        current = oldest;

        while (current != null && !visited.Contains(current))
        {
            visited.Add(current);

            var supersedingDoc = await GetSupersedingDocumentAsync(current, tenantKey, cancellationToken);

            entries.Add(new SupersessionEntry
            {
                DocumentPath = current,
                SupersededBy = supersedingDoc,
                SupersededAt = DateTimeOffset.UtcNow // Would need to track this separately for accuracy
            });

            current = supersedingDoc;
        }

        return new SupersessionChain { Entries = entries };
    }

    /// <inheritdoc />
    public async Task RemoveFromChainAsync(
        string documentPath,
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        var key = CreateKey(documentPath, tenantKey);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Remove from supersedes list
            _supersedes.TryRemove(key, out _);

            // Remove from supersededBy
            _supersededBy.TryRemove(key, out _);

            // Update any documents that were tracking this as superseding them
            var keysToUpdate = _supersededBy
                .Where(kvp => kvp.Value == documentPath)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var updateKey in keysToUpdate)
            {
                _supersededBy.TryRemove(updateKey, out _);
            }

            // Update any documents that had this document in their supersedes list
            foreach (var kvp in _supersedes)
            {
                kvp.Value.Remove(documentPath);
            }

            _logger.LogDebug("Removed document from supersession tracking: {Path}", documentPath);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public bool IsSuperseded(string documentPath, string tenantKey)
    {
        var key = CreateKey(documentPath, tenantKey);
        return _supersededBy.ContainsKey(key);
    }

    /// <inheritdoc />
    public IReadOnlyList<ScoredDocument> AdjustScoresForSupersession(
        IReadOnlyList<ScoredDocument> documents,
        string tenantKey)
    {
        foreach (var scoredDoc in documents)
        {
            var key = CreateKey(scoredDoc.Document.FilePath, tenantKey);

            if (_supersededBy.TryGetValue(key, out var supersedingPath))
            {
                scoredDoc.IsSuperseded = true;
                scoredDoc.SupersededBy = supersedingPath;
                scoredDoc.Score *= SupersededScorePenalty;
            }
        }

        // Re-sort by adjusted score
        return documents.OrderByDescending(d => d.Score).ToList();
    }

    #region Private Methods

    private static List<string> ExtractSupersedesValue(IDictionary<string, object?> frontmatter)
    {
        var result = new List<string>();

        // Check for 'supersedes' field
        if (!frontmatter.TryGetValue("supersedes", out var value) || value == null)
            return result;

        // Handle single string value
        if (value is string stringValue)
        {
            if (!string.IsNullOrWhiteSpace(stringValue))
                result.Add(stringValue);
            return result;
        }

        // Handle array of strings
        if (value is IEnumerable<object> enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is string str && !string.IsNullOrWhiteSpace(str))
                    result.Add(str);
            }
        }

        return result;
    }

    private async Task UpdateSupersededDocumentAsync(
        CompoundDocument supersededDoc,
        CompoundDocument supersedingDoc,
        string tenantKey,
        CancellationToken cancellationToken)
    {
        // Lower the promotion level of the superseded document
        if (supersededDoc.PromotionLevel != PromotionLevels.Standard)
        {
            var previousLevel = supersededDoc.PromotionLevel;

            // Use the repository's dedicated method for updating promotion level
            await _documentRepository.UpdatePromotionLevelAsync(
                supersededDoc.Id, PromotionLevels.Standard, cancellationToken);

            // Publish promotion event
            await _eventPublisher.PublishPromotedAsync(
                DocumentPromotedEventArgs.Create(
                    documentId: supersededDoc.Id,
                    filePath: supersededDoc.FilePath,
                    tenantKey: tenantKey,
                    previousLevel: previousLevel,
                    newLevel: PromotionLevels.Standard,
                    reason: $"Superseded by {supersedingDoc.FilePath}",
                    isAutomatic: true),
                cancellationToken);

            _logger.LogInformation(
                "Lowered promotion level of superseded document {Path} from {Previous} to {New}",
                supersededDoc.FilePath, previousLevel, PromotionLevels.Standard);
        }
    }

    private Task<string?> FindPreviousInChainAsync(
        string documentPath,
        string tenantKey,
        CancellationToken cancellationToken)
    {
        // Find documents that this document supersedes
        var key = CreateKey(documentPath, tenantKey);

        if (_supersedes.TryGetValue(key, out var supersededSet) && supersededSet.Count > 0)
        {
            // Return the first superseded document (oldest in chain)
            return Task.FromResult<string?>(supersededSet.First());
        }

        return Task.FromResult<string?>(null);
    }

    private static string CreateKey(string documentPath, string tenantKey)
    {
        return $"{tenantKey}:{documentPath}";
    }

    #endregion
}
