using System.Text.Json;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using CompoundDocs.Common.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace CompoundDocs.Bedrock;

public sealed class BedrockEmbeddingService : IBedrockEmbeddingService
{
    private readonly IAmazonBedrockRuntime _client;
    private readonly BedrockConfig _config;
    private readonly ILogger<BedrockEmbeddingService> _logger;
    private readonly ResiliencePipeline _retryPipeline;

    public BedrockEmbeddingService(
        IOptions<BedrockConfig> options,
        ILogger<BedrockEmbeddingService> logger)
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

    internal BedrockEmbeddingService(
        IAmazonBedrockRuntime client,
        BedrockConfig config,
        ILogger<BedrockEmbeddingService> logger,
        ResiliencePipeline? retryPipeline = null)
    {
        _client = client;
        _config = config;
        _logger = logger;
        _retryPipeline = retryPipeline ?? ResiliencePipeline.Empty;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        return await _retryPipeline.ExecuteAsync(async token =>
        {
            _logger.LogDebug("Generating embedding for text of length {Length}", text.Length);

            var requestBody = JsonSerializer.Serialize(new
            {
                inputText = text,
                dimensions = 1024,
                normalize = true
            });

            var request = new InvokeModelRequest
            {
                ModelId = _config.EmbeddingModelId,
                ContentType = "application/json",
                Accept = "application/json",
                Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(requestBody))
            };

            var response = await _client.InvokeModelAsync(request, token);

            using var doc = await JsonDocument.ParseAsync(response.Body, cancellationToken: token);
            var embeddingElement = doc.RootElement.GetProperty("embedding");

            var embedding = new float[embeddingElement.GetArrayLength()];
            var index = 0;
            foreach (var element in embeddingElement.EnumerateArray())
            {
                embedding[index++] = element.GetSingle();
            }

            return embedding;
        }, ct);
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default)
    {
        var results = new List<float[]>();
        foreach (var text in texts)
        {
            var embedding = await GenerateEmbeddingAsync(text, ct);
            results.Add(embedding);
        }
        return results;
    }
}
