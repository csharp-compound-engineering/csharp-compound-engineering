using Amazon;
using CompoundDocs.Common.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenSearch.Client;
using OpenSearch.Net.Auth.AwsSigV4;

namespace CompoundDocs.Vector;

public interface IOpenSearchClientFactory
{
    IOpenSearchClient GetClient();
}

internal sealed partial class OpenSearchClientFactory : IOpenSearchClientFactory
{
    [LoggerMessage(EventId = 1, Level = Microsoft.Extensions.Logging.LogLevel.Information,
        Message = "Creating new OpenSearch client for endpoint {Endpoint}")]
    private partial void LogCreatingClient(string endpoint);

    [LoggerMessage(EventId = 2, Level = Microsoft.Extensions.Logging.LogLevel.Information,
        Message = "OpenSearch endpoint changed from {OldEndpoint} to {NewEndpoint}, recreating client")]
    private partial void LogEndpointChanged(string oldEndpoint, string newEndpoint);

    private readonly object _lock = new();
    private readonly string _region;
    private readonly ILogger<OpenSearchClientFactory> _logger;
    private IOpenSearchClient? _client;
    private string _currentEndpoint = string.Empty;
    private readonly IOptionsMonitor<OpenSearchConfig> _optionsMonitor;

    public OpenSearchClientFactory(
        IOptionsMonitor<OpenSearchConfig> optionsMonitor,
        IConfiguration configuration,
        ILogger<OpenSearchClientFactory> logger)
    {
        _optionsMonitor = optionsMonitor;
        _region = configuration.GetValue<string>("CompoundDocs:Aws:Region") ?? "us-east-1";
        _logger = logger;

        _optionsMonitor.OnChange(OnConfigChanged);
    }

    private void OnConfigChanged(OpenSearchConfig config)
    {
        lock (_lock)
        {
            var newEndpoint = config.CollectionEndpoint;
            if (!string.IsNullOrEmpty(newEndpoint) && newEndpoint != _currentEndpoint)
            {
                LogEndpointChanged(_currentEndpoint, newEndpoint);
                _client = null;
            }
        }
    }

    public IOpenSearchClient GetClient()
    {
        var config = _optionsMonitor.CurrentValue;
        var endpoint = config.CollectionEndpoint;

        if (string.IsNullOrEmpty(endpoint))
        {
            throw new InvalidOperationException(
                "OpenSearch endpoint is not configured. Set CompoundDocs:OpenSearch:CollectionEndpoint.");
        }

        if (_client is not null && endpoint == _currentEndpoint)
        {
            return _client;
        }

        lock (_lock)
        {
            if (_client is not null && endpoint == _currentEndpoint)
            {
                return _client;
            }

            LogCreatingClient(endpoint);

            var connection = new AwsSigV4HttpConnection(
                RegionEndpoint.GetBySystemName(_region));
            var settings = new ConnectionSettings(new Uri(endpoint), connection)
                .DefaultIndex(config.IndexName);
            _client = new OpenSearchClient(settings);
            _currentEndpoint = endpoint;
            return _client;
        }
    }
}
