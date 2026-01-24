namespace CompoundDocs.GraphRag;

public interface IDocumentIngestionService
{
    Task IngestDocumentAsync(string content, DocumentIngestionMetadata metadata, CancellationToken ct = default);
    Task DeleteDocumentAsync(string documentId, CancellationToken ct = default);
}

public record DocumentIngestionMetadata
{
    public required string DocumentId { get; init; }
    public required string Repository { get; init; }
    public required string FilePath { get; init; }
    public required string Title { get; init; }
    public string? DocType { get; init; }
    public string PromotionLevel { get; init; } = "draft";
    public string? CommitHash { get; init; }
}
