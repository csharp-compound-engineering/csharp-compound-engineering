using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Agents;

/// <summary>
/// Hosted service that initializes the agent registry on application startup.
/// </summary>
public sealed class AgentInitializationService : IHostedService
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly ILogger<AgentInitializationService> _logger;

    /// <summary>
    /// Creates a new instance of AgentInitializationService.
    /// </summary>
    public AgentInitializationService(
        IAgentRegistry agentRegistry,
        ILogger<AgentInitializationService> logger)
    {
        _agentRegistry = agentRegistry ?? throw new ArgumentNullException(nameof(agentRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing agent registry...");

        try
        {
            if (_agentRegistry is AgentRegistry registry)
            {
                await registry.InitializeAsync(cancellationToken);
                _logger.LogInformation("Agent registry initialized with {Count} agents", registry.AgentCount);
            }
            else
            {
                _logger.LogWarning("AgentRegistry is not the expected implementation type");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize agent registry");
            // Don't throw - allow the application to continue even if agent loading fails
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Agent initialization service stopping");
        return Task.CompletedTask;
    }
}
