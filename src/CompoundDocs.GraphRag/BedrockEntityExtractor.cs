using CompoundDocs.Bedrock;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.GraphRag;

internal sealed partial class BedrockEntityExtractor : IEntityExtractor
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Extracting entities from chunk ({ContentLength} chars)")]
    private partial void LogExtracting(int contentLength);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "Extracted {EntityCount} entities from chunk")]
    private partial void LogExtracted(int entityCount);

    private readonly IBedrockLlmService _llmService;
    private readonly ILogger<BedrockEntityExtractor> _logger;

    public BedrockEntityExtractor(
        IBedrockLlmService llmService,
        ILogger<BedrockEntityExtractor> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<List<ExtractedEntity>> ExtractEntitiesAsync(string chunkText, CancellationToken ct = default)
    {
        LogExtracting(chunkText.Length);

        var bedrockEntities = await _llmService.ExtractEntitiesAsync(chunkText, ct);

        var entities = bedrockEntities.Select(e => new ExtractedEntity
        {
            Name = e.Name,
            Type = e.Type,
            Description = e.Description,
            Aliases = e.Aliases
        }).ToList();

        LogExtracted(entities.Count);
        return entities;
    }
}
