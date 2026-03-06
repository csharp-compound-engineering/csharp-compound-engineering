using System.Text.Json;
using Amazon.Neptunedata;
using Amazon.Neptunedata.Model;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace CompoundDocs.Graph;

public sealed partial class NeptuneClient : INeptuneClient
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Executing openCypher query: {Query}")]
    private partial void LogExecutingQuery(string query);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Neptune connection test failed: {ExceptionType} - {ExceptionMessage}")]
    private partial void LogConnectionTestFailed(string exceptionType, string exceptionMessage, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "Neptune retry attempt {AttemptNumber}: {ExceptionType} - {ExceptionMessage}")]
    private partial void LogRetryAttempt(int attemptNumber, string exceptionType, string exceptionMessage);

    private readonly INeptunedataClientFactory _clientFactory;
    private readonly ILogger<NeptuneClient> _logger;
    private readonly ResiliencePipeline _retryPipeline;

    public NeptuneClient(
        INeptunedataClientFactory clientFactory,
        ILogger<NeptuneClient> logger)
    {
        _clientFactory = clientFactory;
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
                    ex is TaskCanceledException),
                OnRetry = args =>
                {
                    LogRetryAttempt(args.AttemptNumber, args.Outcome.Exception?.GetType().Name ?? "unknown",
                        args.Outcome.Exception?.Message ?? "no message");
                    return default;
                }
            })
            .Build();
    }

    internal NeptuneClient(
        INeptunedataClientFactory clientFactory,
        ILogger<NeptuneClient> logger,
        ResiliencePipeline? retryPipeline = null)
    {
        _clientFactory = clientFactory;
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
            LogExecutingQuery(query);

            var request = new ExecuteOpenCypherQueryRequest
            {
                OpenCypherQuery = query
            };

            if (parameters is { Count: > 0 })
            {
                request.Parameters = JsonSerializer.Serialize(parameters);
            }

            var response = await _clientFactory.GetClient().ExecuteOpenCypherQueryAsync(request, token);

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
            LogConnectionTestFailed(ex.GetType().Name, ex.Message, ex);
            return false;
        }
    }
}
