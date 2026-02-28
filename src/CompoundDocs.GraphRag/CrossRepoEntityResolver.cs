using CompoundDocs.Graph;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.GraphRag;

internal sealed partial class CrossRepoEntityResolver : ICrossRepoEntityResolver
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Resolving cross-repo entity for concept {ConceptName}")]
    private partial void LogResolving(string conceptName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "Resolved concept {ConceptName} in repository {Repository} with {RelatedCount} related concepts")]
    private partial void LogResolved(string conceptName, string repository, int relatedCount);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
        Message = "No concept found for name {ConceptName}")]
    private partial void LogNotFound(string conceptName);

    private readonly IGraphRepository _graphRepository;
    private readonly ILogger<CrossRepoEntityResolver> _logger;

    public CrossRepoEntityResolver(
        IGraphRepository graphRepository,
        ILogger<CrossRepoEntityResolver> logger)
    {
        _graphRepository = graphRepository;
        _logger = logger;
    }

    public async Task<ResolvedEntity?> ResolveAsync(string conceptName, CancellationToken ct = default)
    {
        LogResolving(conceptName);

        var concepts = await _graphRepository.FindConceptsByNameAsync(conceptName, ct);
        if (concepts.Count == 0)
        {
            LogNotFound(conceptName);
            return null;
        }

        var concept = concepts[0];

        // Parallel: get related concepts and chunks (for repository derivation)
        var relatedTask = _graphRepository.GetRelatedConceptsAsync(concept.Id, 1, ct);
        var chunksTask = _graphRepository.GetChunksByConceptAsync(concept.Id, ct);
        await Task.WhenAll(relatedTask, chunksTask);

        var related = relatedTask.Result;
        var chunks = chunksTask.Result;

        var repository = DeriveRepository(chunks);

        LogResolved(conceptName, repository, related.Count);

        return new ResolvedEntity
        {
            ConceptId = concept.Id,
            Name = concept.Name,
            Repository = repository,
            RelatedConceptIds = related.Select(c => c.Id).ToList(),
            RelatedConceptNames = related.Select(c => c.Name).ToList()
        };
    }

    internal static string DeriveRepository(List<Common.Models.ChunkNode> chunks)
    {
        if (chunks.Count == 0)
        {
            return string.Empty;
        }

        var documentId = chunks[0].DocumentId;
        var colonIndex = documentId.IndexOf(':');
        return colonIndex > 0 ? documentId[..colonIndex] : string.Empty;
    }
}
