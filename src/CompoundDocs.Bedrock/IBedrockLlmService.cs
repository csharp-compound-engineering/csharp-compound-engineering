namespace CompoundDocs.Bedrock;

public interface IBedrockLlmService
{
    Task<string> GenerateAsync(
        string systemPrompt,
        IReadOnlyList<BedrockMessage> messages,
        ModelTier tier = ModelTier.Sonnet,
        CancellationToken ct = default);

    Task<List<ExtractedEntity>> ExtractEntitiesAsync(
        string chunkText,
        CancellationToken ct = default);
}

public record ExtractedEntity
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public string? Description { get; init; }
    public List<string> Aliases { get; init; } = [];
}
