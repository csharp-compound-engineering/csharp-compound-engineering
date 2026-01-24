namespace CompoundDocs.GraphRag;

public record GraphRagResult
{
    public required string Answer { get; init; }
    public List<GraphRagSource> Sources { get; init; } = [];
    public List<string> RelatedConcepts { get; init; } = [];
    public double Confidence { get; init; }
}

public record GraphRagSource
{
    public required string DocumentId { get; init; }
    public required string ChunkId { get; init; }
    public required string Repository { get; init; }
    public required string FilePath { get; init; }
    public double RelevanceScore { get; init; }
}

public record GraphRagOptions
{
    public int MaxChunks { get; init; } = 10;
    public int MaxTraversalSteps { get; init; } = 5;
    public double MinRelevanceScore { get; init; } = 0.7;
    public bool UseCrossRepoLinks { get; init; } = true;
    public string? RepositoryFilter { get; init; }
    public string? DocTypeFilter { get; init; }
}
