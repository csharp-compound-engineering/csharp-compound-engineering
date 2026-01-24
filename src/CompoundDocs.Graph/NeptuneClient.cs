using System.Text.Json;
using Amazon.Neptunedata;
using Amazon.Neptunedata.Model;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace CompoundDocs.Graph;

public sealed class NeptuneClient : INeptuneClient
{
    private readonly IAmazonNeptunedata _client;
    private readonly ILogger<NeptuneClient> _logger;
    private readonly ResiliencePipeline _retryPipeline;

    public NeptuneClient(
        IAmazonNeptunedata client,
        ILogger<NeptuneClient> logger)
    {
        _client = client;
        _logger = logger;

        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex =>
                    ex is AmazonNeptunedataException ||
                    ex is HttpRequestException ||
                    ex is TaskCanceledException)
            })
            .Build();
    }

    internal NeptuneClient(
        IAmazonNeptunedata client,
        ILogger<NeptuneClient> logger,
        ResiliencePipeline? retryPipeline = null)
    {
        _client = client;
        _logger = logger;
        _retryPipeline = retryPipeline ?? ResiliencePipeline.Empty;
    }

    public async Task<JsonElement> ExecuteOpenCypherAsync(
        string query,
        Dictionary<string, object>? parameters,
        CancellationToken ct)
    {
        return await _retryPipeline.ExecuteAsync(async token =>
        {
            _logger.LogDebug("Executing openCypher query: {Query}", query);

            var request = new ExecuteOpenCypherQueryRequest
            {
                OpenCypherQuery = query
            };

            if (parameters is { Count: > 0 })
            {
                request.Parameters = JsonSerializer.Serialize(parameters);
            }

            var response = await _client.ExecuteOpenCypherQueryAsync(request, token);

            using var doc = JsonDocument.Parse(response.Results.ToString()!);
            return doc.RootElement.Clone();
        }, ct);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct)
    {
        try
        {
            await ExecuteOpenCypherAsync("RETURN 1", null, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Neptune connection test failed");
            return false;
        }
    }
}
