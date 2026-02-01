using Microsoft.Extensions.Logging;
using Npgsql;
using CompoundDocs.McpServer.Options;
using Microsoft.Extensions.Options;

namespace CompoundDocs.McpServer.Hooks;

/// <summary>
/// Hook that executes when an MCP session starts.
/// Performs prerequisite checks for database connectivity and Ollama availability.
/// </summary>
public sealed class SessionStartHook : ISessionHook
{
    private readonly ILogger<SessionStartHook> _logger;
    private readonly CompoundDocsServerOptions _options;

    /// <inheritdoc/>
    public string Name => "SessionStart";

    /// <inheritdoc/>
    public int Order => 0;

    /// <inheritdoc/>
    public bool IsEnabled => true;

    /// <summary>
    /// Creates a new instance of SessionStartHook.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="options">Server options.</param>
    public SessionStartHook(
        ILogger<SessionStartHook> logger,
        IOptions<CompoundDocsServerOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public async Task<SessionHookResult> OnSessionStartAsync(
        SessionHookContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SessionStart hook: Checking MCP prerequisites");

        var warnings = new List<string>();

        // Check database connectivity
        var dbCheckResult = await CheckDatabaseConnectivityAsync(cancellationToken);
        if (!dbCheckResult.IsSuccess)
        {
            _logger.LogError("Database connectivity check failed: {Error}", dbCheckResult.ErrorMessage);
            return SessionHookResult.Failure(
                $"Database connectivity check failed: {dbCheckResult.ErrorMessage}. " +
                "Please ensure PostgreSQL is running and accessible.");
        }

        if (dbCheckResult.Warnings.Count > 0)
        {
            warnings.AddRange(dbCheckResult.Warnings);
        }

        // Check Ollama availability (graceful failure)
        var ollamaCheckResult = await CheckOllamaAvailabilityAsync(cancellationToken);
        if (!ollamaCheckResult.IsSuccess)
        {
            // Ollama is optional - log warning but don't fail
            _logger.LogWarning("Ollama availability check failed: {Error}", ollamaCheckResult.ErrorMessage);
            warnings.Add($"Ollama is unavailable: {ollamaCheckResult.ErrorMessage}. " +
                "RAG features will be limited.");
        }

        if (ollamaCheckResult.Warnings.Count > 0)
        {
            warnings.AddRange(ollamaCheckResult.Warnings);
        }

        // Check required directories exist
        var dirCheckResult = CheckRequiredDirectories();
        if (!dirCheckResult.IsSuccess)
        {
            _logger.LogWarning("Directory check failed: {Error}", dirCheckResult.ErrorMessage);
            warnings.Add(dirCheckResult.ErrorMessage!);
        }

        if (warnings.Count > 0)
        {
            _logger.LogInformation("SessionStart hook completed with {Count} warning(s)", warnings.Count);
            return SessionHookResult.Continue(warnings);
        }

        _logger.LogInformation("SessionStart hook: All prerequisites met");
        return SessionHookResult.Continue();
    }

    /// <inheritdoc/>
    public Task<SessionHookResult> OnSessionEndAsync(
        SessionHookContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SessionEnd hook: Cleaning up session resources");
        return Task.FromResult(SessionHookResult.Continue());
    }

    /// <summary>
    /// Checks PostgreSQL database connectivity.
    /// </summary>
    private async Task<SessionHookResult> CheckDatabaseConnectivityAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = BuildConnectionString();

            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            _logger.LogDebug("PostgreSQL connection successful");

            // Check if pgvector extension is available
            await using var cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM pg_extension WHERE extname = 'vector'",
                connection);

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            var hasVectorExtension = Convert.ToInt32(result) > 0;

            if (!hasVectorExtension)
            {
                var warning = "pgvector extension not found. Vector search functionality may be limited.";
                _logger.LogWarning(warning);
                return SessionHookResult.Continue([warning]);
            }

            return SessionHookResult.Continue();
        }
        catch (NpgsqlException ex)
        {
            return SessionHookResult.Failure(
                $"Failed to connect to PostgreSQL at {_options.Postgres.Host}:{_options.Postgres.Port}. " +
                $"Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return SessionHookResult.Failure($"Database connectivity check failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks Ollama service availability.
    /// </summary>
    private async Task<SessionHookResult> CheckOllamaAvailabilityAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };

            var ollamaUrl = $"http://{_options.Ollama.Host}:{_options.Ollama.Port}/api/tags";

            var response = await httpClient.GetAsync(ollamaUrl, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Ollama service is available at {Url}", ollamaUrl);
                return SessionHookResult.Continue();
            }
            else
            {
                return SessionHookResult.Failure(
                    $"Ollama returned status code {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            return SessionHookResult.Failure(
                $"Cannot connect to Ollama at {_options.Ollama.Host}:{_options.Ollama.Port}. " +
                $"Error: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return SessionHookResult.Failure(
                $"Ollama connection timed out at {_options.Ollama.Host}:{_options.Ollama.Port}");
        }
        catch (Exception ex)
        {
            return SessionHookResult.Failure($"Ollama availability check failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks that required directories exist.
    /// </summary>
    private SessionHookResult CheckRequiredDirectories()
    {
        var warnings = new List<string>();

        // Check for data directory (where documents might be stored)
        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        if (!Directory.Exists(dataDir))
        {
            try
            {
                Directory.CreateDirectory(dataDir);
                _logger.LogDebug("Created data directory: {Path}", dataDir);
            }
            catch (Exception ex)
            {
                warnings.Add($"Failed to create data directory: {ex.Message}");
            }
        }

        // Check for logs directory
        var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
        if (!Directory.Exists(logsDir))
        {
            try
            {
                Directory.CreateDirectory(logsDir);
                _logger.LogDebug("Created logs directory: {Path}", logsDir);
            }
            catch (Exception ex)
            {
                warnings.Add($"Failed to create logs directory: {ex.Message}");
            }
        }

        if (warnings.Count > 0)
        {
            return SessionHookResult.Continue(warnings);
        }

        return SessionHookResult.Continue();
    }

    /// <summary>
    /// Builds the PostgreSQL connection string from options.
    /// </summary>
    private string BuildConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = _options.Postgres.Host,
            Port = _options.Postgres.Port,
            Database = _options.Postgres.Database ?? "compound_docs",
            Username = _options.Postgres.Username ?? "postgres",
            Password = _options.Postgres.Password ?? "postgres",
            Timeout = 5 // 5 second timeout for connection check
        };

        return builder.ConnectionString;
    }
}
