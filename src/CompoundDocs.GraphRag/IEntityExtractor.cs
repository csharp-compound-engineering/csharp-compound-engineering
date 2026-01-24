namespace CompoundDocs.GraphRag;

public interface IEntityExtractor
{
    Task<List<ExtractedEntity>> ExtractEntitiesAsync(string chunkText, CancellationToken ct = default);
}

public record ExtractedEntity
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public string? Description { get; init; }
    public List<string> Aliases { get; init; } = [];
}
