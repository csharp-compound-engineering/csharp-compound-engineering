using System.Text.Json;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using CompoundDocs.Common.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace CompoundDocs.Bedrock;

public sealed class BedrockLlmService : IBedrockLlmService
{
    private readonly IAmazonBedrockRuntime _client;
    private readonly BedrockConfig _config;
    private readonly ILogger<BedrockLlmService> _logger;
    private readonly ResiliencePipeline _retryPipeline;

    public BedrockLlmService(
        IOptions<BedrockConfig> options,
        ILogger<BedrockLlmService> logger)
    {
        _config = options.Value;
        _logger = logger;
        _client = new AmazonBedrockRuntimeClient();

        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex =>
                    ex is AmazonBedrockRuntimeException ||
                    ex is HttpRequestException ||
                    ex is TaskCanceledException)
            })
            .Build();
    }

    internal BedrockLlmService(
        IAmazonBedrockRuntime client,
        BedrockConfig config,
        ILogger<BedrockLlmService> logger,
        ResiliencePipeline? retryPipeline = null)
    {
        _client = client;
        _config = config;
        _logger = logger;
        _retryPipeline = retryPipeline ?? ResiliencePipeline.Empty;
    }

    public async Task<string> GenerateAsync(
        string systemPrompt,
        IReadOnlyList<BedrockMessage> messages,
        ModelTier tier = ModelTier.Sonnet,
        CancellationToken ct = default)
    {
        return await _retryPipeline.ExecuteAsync(async token =>
        {
            var modelId = GetModelId(tier);
            _logger.LogDebug("Generating with model {ModelId}, tier {Tier}", modelId, tier);

            var converseMessages = messages.Select(m => new Message
            {
                Role = m.Role == "user" ? ConversationRole.User : ConversationRole.Assistant,
                Content = [new ContentBlock { Text = m.Content }]
            }).ToList();

            var request = new ConverseRequest
            {
                ModelId = modelId,
                System = [new SystemContentBlock { Text = systemPrompt }],
                Messages = converseMessages
            };

            var response = await _client.ConverseAsync(request, token);

            return response.Output.Message.Content
                .Where(c => c.Text != null)
                .Select(c => c.Text)
                .FirstOrDefault() ?? string.Empty;
        }, ct);
    }

    public async Task<List<ExtractedEntity>> ExtractEntitiesAsync(
        string chunkText,
        CancellationToken ct = default)
    {
        var systemPrompt = """
            You are an entity extraction system. Extract named entities from the provided text.
            Return a JSON array of objects with the following structure:
            [{"name": "entity name", "type": "entity type", "description": "brief description", "aliases": ["alias1"]}]
            Valid types: Concept, Technology, Pattern, API, Library, Framework, Service, Protocol
            Return ONLY the JSON array, no other text.
            """;

        var response = await GenerateAsync(
            systemPrompt,
            [new BedrockMessage("user", chunkText)],
            ModelTier.Haiku,
            ct);

        try
        {
            var entities = JsonSerializer.Deserialize<List<ExtractedEntity>>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return entities ?? [];
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse entity extraction response");
            return [];
        }
    }

    internal string GetModelId(ModelTier tier) => tier switch
    {
        ModelTier.Haiku => _config.HaikuModelId,
        ModelTier.Sonnet => _config.SonnetModelId,
        ModelTier.Opus => _config.OpusModelId,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown model tier")
    };
}
