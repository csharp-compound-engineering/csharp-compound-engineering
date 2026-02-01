# Phase 141: Document Supersedes Handling

> **Status**: NOT_STARTED
> **Effort Estimate**: 6-8 hours
> **Category**: Document Processing
> **Prerequisites**: Phase 059 (YAML Frontmatter Parsing)

---

## Spec References

This phase implements the supersedes functionality defined in:

- **spec/doc-types.md** - [Common Optional Fields](../spec/doc-types.md#common-optional-fields) (line 97: `supersedes` field)
- **plan/phase-059-frontmatter-parsing.md** - BaseFrontmatter model with `Supersedes` property

---

## Objectives

1. Implement supersession chain tracking for document relationships
2. Create relevance downgrade logic for superseded documents in search
3. Build supersession validation to detect circular references and orphaned chains
4. Add UI-ready indicators for superseded document status
5. Implement superseded document filtering in search operations
6. Support supersession chain traversal for document lineage queries
7. Handle cascading updates when superseding documents are deleted

---

## Acceptance Criteria

### Supersedes Field Processing
- [ ] Parse `supersedes` field from YAML frontmatter as relative path
- [ ] Validate supersedes path resolves to existing document
- [ ] Store normalized supersedes relationship in database
- [ ] Handle missing superseded document gracefully (warn, don't fail)
- [ ] Support both forward slash and backslash path separators

### Supersession Chain Tracking
- [ ] Create `ISupersessionService` for chain operations
- [ ] Track supersession chains in `supersession_chain` database table
- [ ] Detect supersession chains up to configurable depth (default: 10)
- [ ] Query "what superseded this document?" (predecessors)
- [ ] Query "what does this document supersede?" (successors)
- [ ] Return full chain from original to current document

### Relevance Downgrade for Superseded Documents
- [ ] Apply 0.5x relevance multiplier to superseded documents in search
- [ ] Ensure newest document in chain has full relevance
- [ ] Progressive downgrade for multi-level supersession (0.5^depth)
- [ ] Downgrade configurable via project configuration
- [ ] Downgrade only applied to standard searches, not explicit lookups

### Search Integration
- [ ] Add `exclude_superseded` filter option (default: true)
- [ ] Add `include_all_versions` filter for version history queries
- [ ] Superseded documents excluded from RAG by default
- [ ] Superseded documents still findable via direct path/ID lookup
- [ ] Search results include `is_superseded` and `superseded_by` fields

### UI Indication Support
- [ ] Return `SupersessionStatus` object with search results
- [ ] Include superseding document path for "view current" links
- [ ] Include supersession depth for chain visualization
- [ ] Provide human-readable status: "superseded", "current", "original"
- [ ] Include supersession date when available

### Circular Reference Detection
- [ ] Detect circular supersession references during indexing
- [ ] Log warning and break cycle at detection point
- [ ] Mark documents in cycle with `has_circular_reference` flag
- [ ] Validation tool to scan for circular references

### Cascading Updates
- [ ] When superseding document deleted, update chain pointers
- [ ] Option to "unsupersede" and restore relevance to predecessor
- [ ] Handle orphaned chains (middle document deleted)
- [ ] Maintain chain integrity during document updates

### Testing
- [ ] Unit tests for supersession chain detection
- [ ] Unit tests for relevance downgrade calculations
- [ ] Unit tests for circular reference detection
- [ ] Unit tests for cascading delete handling
- [ ] Integration tests for search with superseded documents
- [ ] Test coverage meets 100% requirement

---

## Implementation Notes

### 1. Supersession Status Enum

```csharp
namespace CompoundDocs.Common.Models.Enums;

/// <summary>
/// Represents the supersession status of a document.
/// </summary>
public enum SupersessionStatus
{
    /// <summary>
    /// Document is the current/latest version in its chain.
    /// </summary>
    Current,

    /// <summary>
    /// Document has been superseded by a newer document.
    /// </summary>
    Superseded,

    /// <summary>
    /// Document is the original in a supersession chain (has successors but no predecessors).
    /// </summary>
    Original,

    /// <summary>
    /// Document has circular supersession reference (error state).
    /// </summary>
    CircularReference
}
```

### 2. Supersession Info Model

```csharp
namespace CompoundDocs.Common.Models;

/// <summary>
/// Detailed supersession information for a document.
/// </summary>
public sealed record SupersessionInfo
{
    /// <summary>
    /// Current supersession status.
    /// </summary>
    public required SupersessionStatus Status { get; init; }

    /// <summary>
    /// Path to the document that supersedes this one (if superseded).
    /// </summary>
    public string? SupersededBy { get; init; }

    /// <summary>
    /// Path to the document this supersedes (if any).
    /// </summary>
    public string? Supersedes { get; init; }

    /// <summary>
    /// Depth in supersession chain (0 = current/no chain, 1+ = superseded).
    /// </summary>
    public int ChainDepth { get; init; }

    /// <summary>
    /// Path to the current/latest document in this chain.
    /// </summary>
    public string? CurrentVersionPath { get; init; }

    /// <summary>
    /// Whether this document has a circular reference in its chain.
    /// </summary>
    public bool HasCircularReference { get; init; }

    /// <summary>
    /// Relevance multiplier based on chain depth (1.0 for current, 0.5^depth for superseded).
    /// </summary>
    public double RelevanceMultiplier => Status == SupersessionStatus.Current
        ? 1.0
        : Math.Pow(0.5, ChainDepth);

    /// <summary>
    /// Human-readable status description.
    /// </summary>
    public string StatusDescription => Status switch
    {
        SupersessionStatus.Current => "Current version",
        SupersessionStatus.Superseded => $"Superseded by newer version (depth: {ChainDepth})",
        SupersessionStatus.Original => "Original version in chain",
        SupersessionStatus.CircularReference => "Warning: Circular reference detected",
        _ => "Unknown status"
    };

    /// <summary>
    /// Creates info for a current (non-superseded) document.
    /// </summary>
    public static SupersessionInfo Current() => new()
    {
        Status = SupersessionStatus.Current,
        ChainDepth = 0
    };

    /// <summary>
    /// Creates info for a superseded document.
    /// </summary>
    public static SupersessionInfo Superseded(string supersededBy, int chainDepth, string currentVersionPath) => new()
    {
        Status = SupersessionStatus.Superseded,
        SupersededBy = supersededBy,
        ChainDepth = chainDepth,
        CurrentVersionPath = currentVersionPath
    };
}
```

### 3. ISupersessionService Interface

```csharp
namespace CompoundDocs.Common.Services.Abstractions;

/// <summary>
/// Service for managing document supersession relationships.
/// </summary>
public interface ISupersessionService
{
    /// <summary>
    /// Maximum chain depth to traverse (prevents infinite loops).
    /// </summary>
    const int DefaultMaxChainDepth = 10;

    /// <summary>
    /// Registers a supersession relationship when a document is indexed.
    /// </summary>
    /// <param name="documentId">The new document that supersedes another.</param>
    /// <param name="supersededPath">Relative path to the superseded document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success and any validation warnings.</returns>
    Task<SupersessionRegistrationResult> RegisterSupersessionAsync(
        string documentId,
        string supersededPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the supersession info for a document.
    /// </summary>
    /// <param name="documentId">Document ID to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Supersession info or null if document not found.</returns>
    Task<SupersessionInfo?> GetSupersessionInfoAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full supersession chain for a document.
    /// </summary>
    /// <param name="documentId">Any document in the chain.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Chain from oldest to newest document.</returns>
    Task<SupersessionChain> GetSupersessionChainAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the current (non-superseded) version of a document.
    /// </summary>
    /// <param name="documentId">Any document in the chain.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current version document ID, or input if already current.</returns>
    Task<string> GetCurrentVersionAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes supersession relationships when a document is deleted.
    /// </summary>
    /// <param name="documentId">Document being deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating how chain was updated.</returns>
    Task<SupersessionRemovalResult> RemoveFromChainAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates supersession chains for integrity issues.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation results with any issues found.</returns>
    Task<SupersessionValidationResult> ValidateChainsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the relevance multiplier for a document based on supersession status.
    /// </summary>
    /// <param name="documentId">Document to calculate multiplier for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Multiplier (1.0 for current, 0.5^depth for superseded).</returns>
    Task<double> GetRelevanceMultiplierAsync(
        string documentId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of registering a supersession relationship.
/// </summary>
public sealed record SupersessionRegistrationResult(
    bool Success,
    string? Warning,
    bool SupersededDocumentExists,
    int NewChainDepth);

/// <summary>
/// Represents a complete supersession chain.
/// </summary>
public sealed record SupersessionChain(
    IReadOnlyList<SupersessionChainNode> Nodes,
    bool HasCircularReference,
    string? CircularReferenceDocumentId);

/// <summary>
/// A single node in a supersession chain.
/// </summary>
public sealed record SupersessionChainNode(
    string DocumentId,
    string RelativePath,
    string Title,
    int ChainPosition,
    DateTime? Date,
    bool IsCurrent);

/// <summary>
/// Result of removing a document from supersession chain.
/// </summary>
public sealed record SupersessionRemovalResult(
    bool Success,
    bool ChainUpdated,
    string? NewPredecessorLink);

/// <summary>
/// Result of validating supersession chains.
/// </summary>
public sealed record SupersessionValidationResult(
    bool IsValid,
    IReadOnlyList<SupersessionValidationIssue> Issues);

/// <summary>
/// A supersession validation issue.
/// </summary>
public sealed record SupersessionValidationIssue(
    SupersessionIssueType Type,
    string DocumentId,
    string Message);

/// <summary>
/// Types of supersession validation issues.
/// </summary>
public enum SupersessionIssueType
{
    CircularReference,
    OrphanedChain,
    MissingSupersededDocument,
    ExcessiveChainDepth
}
```

### 4. SupersessionService Implementation

```csharp
using Microsoft.Extensions.Logging;
using CompoundDocs.Common.Models;
using CompoundDocs.Common.Models.Enums;
using CompoundDocs.Common.Services.Abstractions;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Implementation of document supersession management.
/// </summary>
public sealed class SupersessionService : ISupersessionService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ISupersessionRepository _supersessionRepository;
    private readonly ILogger<SupersessionService> _logger;

    public SupersessionService(
        IDocumentRepository documentRepository,
        ISupersessionRepository supersessionRepository,
        ILogger<SupersessionService> logger)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _supersessionRepository = supersessionRepository ?? throw new ArgumentNullException(nameof(supersessionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SupersessionRegistrationResult> RegisterSupersessionAsync(
        string documentId,
        string supersededPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(supersededPath);

        _logger.LogDebug(
            "Registering supersession: {DocumentId} supersedes {SupersededPath}",
            documentId,
            supersededPath);

        // Normalize path separators
        supersededPath = supersededPath.Replace('\\', '/');

        // Check if superseded document exists
        var supersededDoc = await _documentRepository.GetByRelativePathAsync(supersededPath, cancellationToken);
        var supersededExists = supersededDoc is not null;

        if (!supersededExists)
        {
            _logger.LogWarning(
                "Superseded document not found: {SupersededPath}. Relationship stored but may be orphaned.",
                supersededPath);
        }

        // Check for circular reference
        if (supersededDoc is not null)
        {
            var wouldCreateCircle = await WouldCreateCircularReferenceAsync(
                documentId,
                supersededDoc.Id,
                cancellationToken);

            if (wouldCreateCircle)
            {
                _logger.LogWarning(
                    "Circular supersession reference detected: {DocumentId} -> {SupersededPath}",
                    documentId,
                    supersededPath);

                return new SupersessionRegistrationResult(
                    Success: false,
                    Warning: "Circular reference would be created",
                    SupersededDocumentExists: supersededExists,
                    NewChainDepth: -1);
            }
        }

        // Calculate chain depth
        var chainDepth = 1;
        if (supersededDoc is not null)
        {
            var existingInfo = await GetSupersessionInfoAsync(supersededDoc.Id, cancellationToken);
            if (existingInfo is not null)
            {
                chainDepth = existingInfo.ChainDepth + 1;
            }
        }

        // Store the relationship
        await _supersessionRepository.CreateRelationshipAsync(
            documentId,
            supersededPath,
            supersededDoc?.Id,
            cancellationToken);

        // Update superseded document status
        if (supersededDoc is not null)
        {
            await _supersessionRepository.MarkAsSupersededAsync(
                supersededDoc.Id,
                documentId,
                cancellationToken);
        }

        _logger.LogInformation(
            "Supersession registered: {DocumentId} supersedes {SupersededPath} (chain depth: {Depth})",
            documentId,
            supersededPath,
            chainDepth);

        return new SupersessionRegistrationResult(
            Success: true,
            Warning: supersededExists ? null : "Superseded document not found",
            SupersededDocumentExists: supersededExists,
            NewChainDepth: chainDepth);
    }

    public async Task<SupersessionInfo?> GetSupersessionInfoAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        var relationship = await _supersessionRepository.GetRelationshipAsync(documentId, cancellationToken);

        if (relationship is null)
        {
            // Check if this document is superseded by another
            var supersededBy = await _supersessionRepository.GetSupersededByAsync(documentId, cancellationToken);

            if (supersededBy is null)
            {
                return SupersessionInfo.Current();
            }

            // Calculate chain depth by walking up the chain
            var depth = await CalculateChainDepthAsync(documentId, cancellationToken);
            var currentVersion = await GetCurrentVersionAsync(documentId, cancellationToken);

            return SupersessionInfo.Superseded(supersededBy, depth, currentVersion);
        }

        // This document supersedes another
        var supersededByDoc = await _supersessionRepository.GetSupersededByAsync(documentId, cancellationToken);

        if (supersededByDoc is null)
        {
            // This is the current document
            return new SupersessionInfo
            {
                Status = SupersessionStatus.Current,
                Supersedes = relationship.SupersededPath,
                ChainDepth = 0
            };
        }

        // This document both supersedes and is superseded
        var chainDepth = await CalculateChainDepthAsync(documentId, cancellationToken);
        var currentDoc = await GetCurrentVersionAsync(documentId, cancellationToken);

        return new SupersessionInfo
        {
            Status = SupersessionStatus.Superseded,
            Supersedes = relationship.SupersededPath,
            SupersededBy = supersededByDoc,
            ChainDepth = chainDepth,
            CurrentVersionPath = currentDoc
        };
    }

    public async Task<SupersessionChain> GetSupersessionChainAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        var nodes = new List<SupersessionChainNode>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var circularRef = false;
        string? circularDocId = null;

        // Walk to the beginning of the chain (oldest document)
        var currentId = documentId;
        while (true)
        {
            if (!visited.Add(currentId))
            {
                circularRef = true;
                circularDocId = currentId;
                _logger.LogWarning("Circular reference detected at {DocumentId}", currentId);
                break;
            }

            var relationship = await _supersessionRepository.GetRelationshipAsync(currentId, cancellationToken);
            if (relationship?.SupersededDocumentId is null)
            {
                break; // Reached the original document
            }

            currentId = relationship.SupersededDocumentId;

            if (visited.Count > ISupersessionService.DefaultMaxChainDepth)
            {
                _logger.LogWarning(
                    "Chain depth exceeds maximum ({Max}) at {DocumentId}",
                    ISupersessionService.DefaultMaxChainDepth,
                    currentId);
                break;
            }
        }

        // Now walk forward from oldest to newest, building the chain
        visited.Clear();
        var position = 0;
        currentId = await GetOldestInChainAsync(documentId, cancellationToken);

        while (currentId is not null)
        {
            if (!visited.Add(currentId))
            {
                break; // Already visited (circular)
            }

            var doc = await _documentRepository.GetAsync(currentId, cancellationToken);
            if (doc is not null)
            {
                var supersededBy = await _supersessionRepository.GetSupersededByAsync(currentId, cancellationToken);

                nodes.Add(new SupersessionChainNode(
                    DocumentId: currentId,
                    RelativePath: doc.RelativePath,
                    Title: doc.Title,
                    ChainPosition: position++,
                    Date: doc.Date,
                    IsCurrent: supersededBy is null));
            }

            // Get next in chain (document that supersedes this one)
            currentId = await _supersessionRepository.GetSupersededByAsync(currentId, cancellationToken);

            if (visited.Count > ISupersessionService.DefaultMaxChainDepth)
            {
                break;
            }
        }

        return new SupersessionChain(nodes.AsReadOnly(), circularRef, circularDocId);
    }

    public async Task<string> GetCurrentVersionAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentId = documentId;

        while (true)
        {
            if (!visited.Add(currentId))
            {
                _logger.LogWarning("Circular reference during GetCurrentVersion at {DocumentId}", currentId);
                return documentId; // Return original on circular reference
            }

            var supersededBy = await _supersessionRepository.GetSupersededByAsync(currentId, cancellationToken);
            if (supersededBy is null)
            {
                return currentId;
            }

            currentId = supersededBy;

            if (visited.Count > ISupersessionService.DefaultMaxChainDepth)
            {
                _logger.LogWarning("Max chain depth exceeded during GetCurrentVersion");
                return currentId;
            }
        }
    }

    public async Task<SupersessionRemovalResult> RemoveFromChainAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        _logger.LogInformation("Removing document {DocumentId} from supersession chain", documentId);

        // Get the document's relationships
        var relationship = await _supersessionRepository.GetRelationshipAsync(documentId, cancellationToken);
        var supersededBy = await _supersessionRepository.GetSupersededByAsync(documentId, cancellationToken);

        // Remove the document's relationships
        await _supersessionRepository.RemoveRelationshipsAsync(documentId, cancellationToken);

        // If this document was in the middle of a chain, reconnect the chain
        if (relationship?.SupersededDocumentId is not null && supersededBy is not null)
        {
            // The document that superseded us should now supersede what we superseded
            await _supersessionRepository.UpdateSupersessionTargetAsync(
                supersededBy,
                relationship.SupersededDocumentId,
                cancellationToken);

            _logger.LogInformation(
                "Chain reconnected: {Successor} now supersedes {Predecessor}",
                supersededBy,
                relationship.SupersededDocumentId);

            return new SupersessionRemovalResult(
                Success: true,
                ChainUpdated: true,
                NewPredecessorLink: relationship.SupersededDocumentId);
        }

        // If this was the current version, the predecessor becomes current
        if (relationship?.SupersededDocumentId is not null && supersededBy is null)
        {
            _logger.LogInformation(
                "Predecessor {PredecessorId} is now current version",
                relationship.SupersededDocumentId);
        }

        return new SupersessionRemovalResult(
            Success: true,
            ChainUpdated: relationship is not null || supersededBy is not null,
            NewPredecessorLink: null);
    }

    public async Task<SupersessionValidationResult> ValidateChainsAsync(
        CancellationToken cancellationToken = default)
    {
        var issues = new List<SupersessionValidationIssue>();

        var allRelationships = await _supersessionRepository.GetAllRelationshipsAsync(cancellationToken);

        foreach (var rel in allRelationships)
        {
            // Check for circular references
            if (await WouldCreateCircularReferenceAsync(
                rel.SupersededDocumentId ?? string.Empty,
                rel.DocumentId,
                cancellationToken))
            {
                issues.Add(new SupersessionValidationIssue(
                    SupersessionIssueType.CircularReference,
                    rel.DocumentId,
                    $"Circular reference involving {rel.DocumentId}"));
            }

            // Check for missing superseded documents
            if (rel.SupersededDocumentId is null)
            {
                var exists = await _documentRepository.ExistsByRelativePathAsync(
                    rel.SupersededPath,
                    cancellationToken);

                if (!exists)
                {
                    issues.Add(new SupersessionValidationIssue(
                        SupersessionIssueType.MissingSupersededDocument,
                        rel.DocumentId,
                        $"Superseded document not found: {rel.SupersededPath}"));
                }
            }

            // Check for excessive chain depth
            var depth = await CalculateChainDepthAsync(rel.DocumentId, cancellationToken);
            if (depth > ISupersessionService.DefaultMaxChainDepth)
            {
                issues.Add(new SupersessionValidationIssue(
                    SupersessionIssueType.ExcessiveChainDepth,
                    rel.DocumentId,
                    $"Chain depth ({depth}) exceeds maximum ({ISupersessionService.DefaultMaxChainDepth})"));
            }
        }

        return new SupersessionValidationResult(issues.Count == 0, issues.AsReadOnly());
    }

    public async Task<double> GetRelevanceMultiplierAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var info = await GetSupersessionInfoAsync(documentId, cancellationToken);
        return info?.RelevanceMultiplier ?? 1.0;
    }

    #region Private Helpers

    private async Task<bool> WouldCreateCircularReferenceAsync(
        string sourceId,
        string targetId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(sourceId) || string.IsNullOrEmpty(targetId))
        {
            return false;
        }

        if (sourceId.Equals(targetId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Walk the chain from target to see if we reach source
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentId = targetId;

        while (currentId is not null)
        {
            if (!visited.Add(currentId))
            {
                return true; // Already have a circular reference
            }

            if (currentId.Equals(sourceId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var relationship = await _supersessionRepository.GetRelationshipAsync(currentId, cancellationToken);
            currentId = relationship?.SupersededDocumentId;

            if (visited.Count > ISupersessionService.DefaultMaxChainDepth)
            {
                return false; // Too deep, assume no circular
            }
        }

        return false;
    }

    private async Task<int> CalculateChainDepthAsync(
        string documentId,
        CancellationToken cancellationToken)
    {
        var depth = 0;
        var currentId = documentId;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (true)
        {
            if (!visited.Add(currentId))
            {
                return depth; // Circular reference
            }

            var supersededBy = await _supersessionRepository.GetSupersededByAsync(currentId, cancellationToken);
            if (supersededBy is null)
            {
                return depth;
            }

            depth++;
            currentId = supersededBy;

            if (depth > ISupersessionService.DefaultMaxChainDepth)
            {
                return depth;
            }
        }
    }

    private async Task<string> GetOldestInChainAsync(
        string documentId,
        CancellationToken cancellationToken)
    {
        var currentId = documentId;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (true)
        {
            if (!visited.Add(currentId))
            {
                return currentId; // Circular reference
            }

            var relationship = await _supersessionRepository.GetRelationshipAsync(currentId, cancellationToken);
            if (relationship?.SupersededDocumentId is null)
            {
                return currentId;
            }

            currentId = relationship.SupersededDocumentId;

            if (visited.Count > ISupersessionService.DefaultMaxChainDepth)
            {
                return currentId;
            }
        }
    }

    #endregion
}
```

### 5. ISupersessionRepository Interface

```csharp
namespace CompoundDocs.Common.Repositories.Abstractions;

/// <summary>
/// Repository for supersession relationship storage.
/// </summary>
public interface ISupersessionRepository
{
    /// <summary>
    /// Creates a supersession relationship.
    /// </summary>
    Task CreateRelationshipAsync(
        string documentId,
        string supersededPath,
        string? supersededDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the supersession relationship for a document.
    /// </summary>
    Task<SupersessionRelationship?> GetRelationshipAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the document ID that supersedes the given document.
    /// </summary>
    Task<string?> GetSupersededByAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a document as superseded.
    /// </summary>
    Task MarkAsSupersededAsync(
        string documentId,
        string supersededByDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all supersession relationships for a document.
    /// </summary>
    Task RemoveRelationshipsAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the supersession target for a document.
    /// </summary>
    Task UpdateSupersessionTargetAsync(
        string documentId,
        string newSupersededDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all supersession relationships.
    /// </summary>
    Task<IReadOnlyList<SupersessionRelationship>> GetAllRelationshipsAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a stored supersession relationship.
/// </summary>
public sealed record SupersessionRelationship(
    string DocumentId,
    string SupersededPath,
    string? SupersededDocumentId);
```

### 6. Search Filter Extensions for Supersession

```csharp
namespace CompoundDocs.Common.Search;

/// <summary>
/// Extension methods for supersession filtering in searches.
/// </summary>
public static class SupersessionSearchFilterExtensions
{
    /// <summary>
    /// Adds a filter to exclude superseded documents.
    /// </summary>
    public static VectorSearchFilter ExcludeSuperseded(this VectorSearchFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return filter with
        {
            ExcludeSupersededDocuments = true
        };
    }

    /// <summary>
    /// Includes all document versions (superseded and current).
    /// </summary>
    public static VectorSearchFilter IncludeAllVersions(this VectorSearchFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return filter with
        {
            ExcludeSupersededDocuments = false
        };
    }
}
```

### 7. VectorSearchFilter Enhancement

Add to `VectorSearchFilter` class:

```csharp
/// <summary>
/// Whether to exclude superseded documents from results. Default: true.
/// </summary>
public bool ExcludeSupersededDocuments { get; init; } = true;
```

### 8. Search Result Enhancement

Add to `VectorSearchResult`:

```csharp
/// <summary>
/// Supersession status of this document.
/// </summary>
public SupersessionInfo? SupersessionInfo { get; init; }

/// <summary>
/// Whether this document has been superseded.
/// </summary>
public bool IsSuperseded => SupersessionInfo?.Status == SupersessionStatus.Superseded;
```

### 9. Relevance Downgrade Integration

Update `IVectorSearchService.SearchDocumentsAsync` to apply supersession multiplier:

```csharp
// In VectorSearchService implementation
private async Task<VectorSearchResults> ApplySupersessionAdjustments(
    VectorSearchResults rawResults,
    CancellationToken cancellationToken)
{
    var adjustedResults = new List<VectorSearchResult>();

    foreach (var result in rawResults.Results)
    {
        var supersessionInfo = await _supersessionService.GetSupersessionInfoAsync(
            result.Id,
            cancellationToken);

        var adjustedScore = result.RelevanceScore * (supersessionInfo?.RelevanceMultiplier ?? 1.0);

        adjustedResults.Add(result with
        {
            RelevanceScore = adjustedScore,
            SupersessionInfo = supersessionInfo
        });
    }

    // Re-sort by adjusted relevance
    var sortedResults = adjustedResults
        .OrderByDescending(r => r.RelevanceScore)
        .ToList();

    return rawResults with
    {
        Results = sortedResults
    };
}
```

### 10. Database Schema for Supersession

Add Liquibase changeset for supersession table:

```xml
<changeSet id="add-supersession-table" author="compound-docs">
    <createTable tableName="supersession_chain" schemaName="compounding">
        <column name="document_id" type="VARCHAR(64)">
            <constraints primaryKey="true" nullable="false"/>
        </column>
        <column name="supersedes_path" type="VARCHAR(512)">
            <constraints nullable="false"/>
        </column>
        <column name="supersedes_document_id" type="VARCHAR(64)">
            <constraints nullable="true"/>
        </column>
        <column name="superseded_by_document_id" type="VARCHAR(64)">
            <constraints nullable="true"/>
        </column>
        <column name="created_at" type="TIMESTAMP WITH TIME ZONE" defaultValueComputed="CURRENT_TIMESTAMP">
            <constraints nullable="false"/>
        </column>
    </createTable>

    <createIndex tableName="supersession_chain" indexName="idx_supersession_supersedes_doc" schemaName="compounding">
        <column name="supersedes_document_id"/>
    </createIndex>

    <createIndex tableName="supersession_chain" indexName="idx_supersession_superseded_by" schemaName="compounding">
        <column name="superseded_by_document_id"/>
    </createIndex>
</changeSet>
```

### 11. DI Registration

```csharp
namespace CompoundDocs.McpServer.Extensions;

public static class SupersessionServiceCollectionExtensions
{
    public static IServiceCollection AddSupersessionServices(this IServiceCollection services)
    {
        services.AddScoped<ISupersessionService, SupersessionService>();
        services.AddScoped<ISupersessionRepository, PostgresSupersessionRepository>();

        return services;
    }
}
```

---

## File Structure

After completion, the following files should exist:

```
src/CompoundDocs.Common/
├── Models/
│   ├── Enums/
│   │   └── SupersessionStatus.cs
│   └── SupersessionInfo.cs
├── Services/
│   └── Abstractions/
│       └── ISupersessionService.cs
├── Repositories/
│   └── Abstractions/
│       └── ISupersessionRepository.cs
└── Search/
    └── SupersessionSearchFilterExtensions.cs

src/CompoundDocs.McpServer/
├── Services/
│   └── SupersessionService.cs
├── Repositories/
│   └── PostgresSupersessionRepository.cs
└── Extensions/
    └── SupersessionServiceCollectionExtensions.cs

src/CompoundDocs.Database/
└── changelog/
    └── add-supersession-table.xml

tests/CompoundDocs.Tests/
└── Services/
    ├── SupersessionServiceTests.cs
    ├── SupersessionChainTests.cs
    └── SupersessionRelevanceTests.cs
```

---

## Dependencies

### Depends On

- **Phase 059**: YAML Frontmatter Parsing - Provides `Supersedes` field in BaseFrontmatter
- **Phase 048**: Document Repository - For document lookups by path/ID
- **Phase 050**: Vector Search Service - For search result integration

### Blocks

- **Phase 142+**: Document Version History UI - Requires chain traversal
- **Phase 143+**: Bulk Supersession Tool - Requires supersession service
- **Phase 144+**: Migration from legacy docs - May need supersession support

---

## Verification Steps

After completing this phase, verify:

1. **Build**: `dotnet build` completes without errors
2. **Tests pass**: `dotnet test` runs all supersession tests successfully
3. **Coverage**: Code coverage report shows 100% for all supersession code
4. **Database**: Supersession table created with proper indexes
5. **Search**: Superseded documents excluded by default
6. **Relevance**: Superseded docs have reduced relevance scores

### Manual Verification

```bash
# 1. Create a document
echo "---
doc_type: problem
title: Original Issue
date: 2025-01-01
summary: First version
significance: correctness
---
Original content" > original-issue.md

# 2. Create superseding document
echo "---
doc_type: problem
title: Updated Issue
date: 2025-01-15
summary: Updated version
significance: correctness
supersedes: ./original-issue.md
---
Updated content" > updated-issue.md

# 3. Search and verify only updated-issue appears by default
# 4. Search with include_all_versions and verify both appear
# 5. Check relevance scores (original should be 0.5x)
```

---

## Notes

- The 0.5x relevance multiplier per supersession level is configurable but defaults to spec behavior
- Circular reference detection is critical to prevent infinite loops in chain traversal
- Documents can be both superseding and superseded (middle of chain)
- When a document is deleted, the chain is reconnected to maintain relationships
- The `supersedes` field stores relative paths, not document IDs, for portability
- UI components should use `SupersessionInfo.CurrentVersionPath` to link to latest version
- RAG queries exclude superseded documents by default to avoid outdated information
