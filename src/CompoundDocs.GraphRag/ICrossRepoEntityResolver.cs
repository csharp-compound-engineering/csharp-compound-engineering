namespace CompoundDocs.GraphRag;

public interface ICrossRepoEntityResolver
{
    Task<ResolvedEntity?> ResolveAsync(string conceptName, CancellationToken ct = default);
}

public record ResolvedEntity
{
    public required string ConceptId { get; init; }
    public required string Name { get; init; }
    public required string Repository { get; init; }
    public List<string> RelatedConceptIds { get; init; } = [];
    public List<string> RelatedConceptNames { get; init; } = [];
}
