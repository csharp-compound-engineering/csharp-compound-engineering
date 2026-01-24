namespace CompoundDocs.Common.Models;

public record DocumentNode
{
    public required string Id { get; init; }
    public required string FilePath { get; init; }
    public required string Title { get; init; }
    public string? DocType { get; init; }
    public string PromotionLevel { get; init; } = "draft";
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
    public string? CommitHash { get; init; }
}

public record SectionNode
{
    public required string Id { get; init; }
    public required string DocumentId { get; init; }
    public required string Title { get; init; }
    public int Order { get; init; }
    public int HeadingLevel { get; init; }
}

public record ChunkNode
{
    public required string Id { get; init; }
    public required string SectionId { get; init; }
    public required string DocumentId { get; init; }
    public required string Content { get; init; }
    public int Order { get; init; }
    public int TokenCount { get; init; }
}

public record ConceptNode
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public List<string> Aliases { get; init; } = [];
}

public record CodeExampleNode
{
    public required string Id { get; init; }
    public required string ChunkId { get; init; }
    public required string Language { get; init; }
    public required string Code { get; init; }
    public string? Description { get; init; }
}

public record GraphRelationship
{
    public required string Type { get; init; }
    public required string SourceId { get; init; }
    public required string TargetId { get; init; }
    public Dictionary<string, object> Properties { get; init; } = [];
}
