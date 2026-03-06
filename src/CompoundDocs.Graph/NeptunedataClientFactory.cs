using Amazon.Neptunedata;
using CompoundDocs.Common.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompoundDocs.Graph;

public interface INeptunedataClientFactory : IDisposable
{
    IAmazonNeptunedata GetClient();
}

public partial class NeptunedataClientFactory : INeptunedataClientFactory
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Neptune endpoint changed from {OldEndpoint} to {NewEndpoint}, creating new client")]
    private partial void LogEndpointChanged(string oldEndpoint, string newEndpoint);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "Creating Neptune client for endpoint {Endpoint}:{Port}")]
    private partial void LogCreatingClient(string endpoint, int port);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error,
        Message = "Neptune endpoint is not configured")]
    private partial void LogEndpointNotConfigured();

    private readonly IOptionsMonitor<NeptuneConfig> _optionsMonitor;
    private readonly ILogger<NeptunedataClientFactory> _logger;
    private readonly IDisposable? _onChangeSubscription;
    private readonly object _lock = new();

    private IAmazonNeptunedata? _client;
    private string _currentEndpoint = string.Empty;
    private int _currentPort;
    private bool _disposed;

    public NeptunedataClientFactory(
        IOptionsMonitor<NeptuneConfig> optionsMonitor,
        ILogger<NeptunedataClientFactory> logger)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;

        _onChangeSubscription = _optionsMonitor.OnChange(OnConfigChanged);
    }

    public IAmazonNeptunedata GetClient()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var config = _optionsMonitor.CurrentValue;
        var endpoint = config.Endpoint;
        var port = config.Port;

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            LogEndpointNotConfigured();
            throw new InvalidOperationException(
                "Neptune endpoint is not configured. Set CompoundDocs:Neptune:Endpoint in configuration.");
        }

        if (_client is not null && _currentEndpoint == endpoint && _currentPort == port)
        {
            return _client;
        }

        lock (_lock)
        {
            if (_client is not null && _currentEndpoint == endpoint && _currentPort == port)
            {
                return _client;
            }

            var oldEndpoint = _currentEndpoint;
            var oldClient = _client;

            LogCreatingClient(endpoint, port);

            _client = CreateClient(endpoint, port);
            _currentEndpoint = endpoint;
            _currentPort = port;

            if (oldClient is not null)
            {
                LogEndpointChanged(oldEndpoint, endpoint);
                _ = DisposeAfterDelayAsync(oldClient);
            }

            return _client;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _onChangeSubscription?.Dispose();

        lock (_lock)
        {
            _client?.Dispose();
            _client = null;
        }
    }

    internal virtual IAmazonNeptunedata CreateClient(string endpoint, int port)
    {
        if (!endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            endpoint = $"https://{endpoint}";
        }

        return new AmazonNeptunedataClient(new AmazonNeptunedataConfig
        {
            ServiceURL = $"{endpoint.TrimEnd('/')}:{port}"
        });
    }

    private void OnConfigChanged(NeptuneConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Endpoint))
        {
            return;
        }

        if (config.Endpoint != _currentEndpoint || config.Port != _currentPort)
        {
            // Force re-creation on next GetClient() call by clearing the current client reference.
            // The actual client creation happens lazily in GetClient().
            lock (_lock)
            {
                _currentEndpoint = string.Empty;
            }
        }
    }

    private static async Task DisposeAfterDelayAsync(IAmazonNeptunedata client)
    {
        await Task.Delay(TimeSpan.FromSeconds(30));
        client.Dispose();
    }
}
