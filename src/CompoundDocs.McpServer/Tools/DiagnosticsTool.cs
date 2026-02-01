using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;
using CompoundDocs.McpServer.Observability;
using CompoundDocs.McpServer.Session;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CompoundDocs.McpServer.Tools;

/// <summary>
/// MCP tool for runtime diagnostics and system health monitoring.
/// Provides access to health checks, metrics, and operational status.
/// </summary>
[McpServerToolType]
public sealed class DiagnosticsTool
{
    private readonly HealthCheckService _healthCheckService;
    private readonly MetricsCollector _metricsCollector;
    private readonly ISessionContext _sessionContext;
    private readonly ILogger<DiagnosticsTool> _logger;

    /// <summary>
    /// Creates a new instance of DiagnosticsTool.
    /// </summary>
    public DiagnosticsTool(
        HealthCheckService healthCheckService,
        MetricsCollector metricsCollector,
        ISessionContext sessionContext,
        ILogger<DiagnosticsTool> logger)
    {
        _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get the health status of all system dependencies.
    /// </summary>
    /// <param name="forceRefresh">Force a fresh health check instead of using cached results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The health status of all dependencies.</returns>
    [McpServerTool(Name = "get_health")]
    [Description("Get the health status of all system dependencies including PostgreSQL, Ollama, and the vector store.")]
    public async Task<ToolResponse<HealthResult>> GetHealthAsync(
        [Description("Force a fresh health check instead of using cached results")] bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting health status, forceRefresh={ForceRefresh}", forceRefresh);

        try
        {
            SystemHealthReport report;

            if (forceRefresh)
            {
                report = await _healthCheckService.PerformHealthChecksAsync(cancellationToken);
            }
            else
            {
                report = _healthCheckService.LastHealthReport
                    ?? await _healthCheckService.PerformHealthChecksAsync(cancellationToken);
            }

            var result = new HealthResult
            {
                Status = report.OverallStatus.ToString().ToLowerInvariant(),
                IsHealthy = report.OverallStatus == HealthStatus.Healthy,
                CheckedAt = report.GeneratedAt,
                TotalDurationMs = report.TotalDurationMs,
                Dependencies = report.Checks.Select(c => new DependencyHealth
                {
                    Name = c.Name,
                    Status = c.Status.ToString().ToLowerInvariant(),
                    IsHealthy = c.Status == HealthStatus.Healthy,
                    Description = c.Description,
                    DurationMs = c.DurationMs
                }).ToList()
            };

            _logger.LogInformation("Health check returned: {Status}", result.Status);

            return ToolResponse<HealthResult>.Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Health check cancelled");
            return ToolResponse<HealthResult>.Fail(ToolErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting health status");
            return ToolResponse<HealthResult>.Fail(
                ToolErrors.UnexpectedError($"Failed to get health status: {ex.Message}"));
        }
    }

    /// <summary>
    /// Get operational metrics for the MCP server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current metrics snapshot.</returns>
    [McpServerTool(Name = "get_metrics")]
    [Description("Get operational metrics including query latency, document counts, cache hit rates, and embedding generation times.")]
    public Task<ToolResponse<MetricsResult>> GetMetricsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting metrics snapshot");

        try
        {
            var snapshot = _metricsCollector.GetSnapshot();

            var result = new MetricsResult
            {
                GeneratedAt = snapshot.GeneratedAt,
                ActiveProjects = snapshot.ActiveProjects,
                CacheHitRate = Math.Round(snapshot.CacheHitRate * 100, 2),
                QueryLatency = new LatencyMetrics
                {
                    P50Ms = Math.Round(snapshot.QueryLatency.P50, 2),
                    P95Ms = Math.Round(snapshot.QueryLatency.P95, 2),
                    P99Ms = Math.Round(snapshot.QueryLatency.P99, 2),
                    SampleCount = snapshot.QueryLatency.SampleCount
                },
                EmbeddingLatency = new LatencyMetrics
                {
                    P50Ms = Math.Round(snapshot.EmbeddingLatency.P50, 2),
                    P95Ms = Math.Round(snapshot.EmbeddingLatency.P95, 2),
                    P99Ms = Math.Round(snapshot.EmbeddingLatency.P99, 2),
                    SampleCount = snapshot.EmbeddingLatency.SampleCount
                },
                TenantStats = snapshot.TenantMetrics.Select(t => new TenantStats
                {
                    TenantKey = t.TenantKey,
                    DocumentCount = t.DocumentCount,
                    ChunkCount = t.ChunkCount,
                    QueryCount = t.QueryCount
                }).ToList()
            };

            _logger.LogInformation(
                "Metrics retrieved: {ActiveProjects} active projects, {CacheHitRate}% cache hit rate",
                result.ActiveProjects,
                result.CacheHitRate);

            return Task.FromResult(ToolResponse<MetricsResult>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting metrics");
            return Task.FromResult(ToolResponse<MetricsResult>.Fail(
                ToolErrors.UnexpectedError($"Failed to get metrics: {ex.Message}")));
        }
    }

    /// <summary>
    /// Get the overall status of the MCP server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Server status information.</returns>
    [McpServerTool(Name = "get_status")]
    [Description("Get the overall status of the MCP server including version, uptime, active project, and system health summary.")]
    public async Task<ToolResponse<StatusResult>> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting server status");

        try
        {
            // Get version info
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString(3) ?? "1.0.0";

            // Get health summary
            var healthReport = _healthCheckService.LastHealthReport
                ?? await _healthCheckService.PerformHealthChecksAsync(cancellationToken);

            // Get metrics summary
            var metricsSnapshot = _metricsCollector.GetSnapshot();

            var result = new StatusResult
            {
                ServerVersion = version,
                ServerName = "CSharp Compound Docs MCP Server",
                RuntimeVersion = Environment.Version.ToString(),
                Uptime = GetUptime(),
                IsProjectActive = _sessionContext.IsProjectActive,
                ActiveProject = _sessionContext.IsProjectActive
                    ? new ActiveProjectInfo
                    {
                        ProjectPath = _sessionContext.ActiveProjectPath!,
                        ProjectName = _sessionContext.ProjectName!,
                        Branch = _sessionContext.ActiveBranch!,
                        TenantKey = _sessionContext.TenantKey!
                    }
                    : null,
                HealthSummary = new HealthSummary
                {
                    OverallStatus = healthReport.OverallStatus.ToString().ToLowerInvariant(),
                    IsHealthy = healthReport.OverallStatus == HealthStatus.Healthy,
                    LastCheckedAt = healthReport.GeneratedAt,
                    UnhealthyDependencies = healthReport.Checks
                        .Where(c => c.Status != HealthStatus.Healthy)
                        .Select(c => c.Name)
                        .ToList()
                },
                MetricsSummary = new MetricsSummary
                {
                    ActiveProjects = metricsSnapshot.ActiveProjects,
                    TotalTenants = metricsSnapshot.TenantMetrics.Count,
                    CacheHitRate = Math.Round(metricsSnapshot.CacheHitRate * 100, 2),
                    QueryP50Ms = Math.Round(metricsSnapshot.QueryLatency.P50, 2)
                }
            };

            _logger.LogInformation(
                "Status retrieved: v{Version}, {HealthStatus}, {ActiveProjects} active projects",
                result.ServerVersion,
                result.HealthSummary.OverallStatus,
                result.MetricsSummary.ActiveProjects);

            return ToolResponse<StatusResult>.Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Status check cancelled");
            return ToolResponse<StatusResult>.Fail(ToolErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting server status");
            return ToolResponse<StatusResult>.Fail(
                ToolErrors.UnexpectedError($"Failed to get status: {ex.Message}"));
        }
    }

    private static string GetUptime()
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var uptime = DateTime.Now - process.StartTime;

        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        }
        if (uptime.TotalHours >= 1)
        {
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m {uptime.Seconds}s";
        }
        if (uptime.TotalMinutes >= 1)
        {
            return $"{(int)uptime.TotalMinutes}m {uptime.Seconds}s";
        }
        return $"{(int)uptime.TotalSeconds}s";
    }
}

#region Result Types

/// <summary>
/// Health check result data.
/// </summary>
public sealed class HealthResult
{
    /// <summary>
    /// Overall health status: healthy, degraded, or unhealthy.
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    /// Whether the system is considered healthy.
    /// </summary>
    [JsonPropertyName("is_healthy")]
    public required bool IsHealthy { get; init; }

    /// <summary>
    /// When the health check was performed.
    /// </summary>
    [JsonPropertyName("checked_at")]
    public required DateTimeOffset CheckedAt { get; init; }

    /// <summary>
    /// Total time to perform all health checks in milliseconds.
    /// </summary>
    [JsonPropertyName("total_duration_ms")]
    public required long TotalDurationMs { get; init; }

    /// <summary>
    /// Health status of individual dependencies.
    /// </summary>
    [JsonPropertyName("dependencies")]
    public required List<DependencyHealth> Dependencies { get; init; }
}

/// <summary>
/// Health status of a single dependency.
/// </summary>
public sealed class DependencyHealth
{
    /// <summary>
    /// Name of the dependency.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Health status: healthy, degraded, or unhealthy.
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    /// Whether this dependency is healthy.
    /// </summary>
    [JsonPropertyName("is_healthy")]
    public required bool IsHealthy { get; init; }

    /// <summary>
    /// Additional status description.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary>
    /// Time taken for this health check in milliseconds.
    /// </summary>
    [JsonPropertyName("duration_ms")]
    public required long DurationMs { get; init; }
}

/// <summary>
/// Metrics result data.
/// </summary>
public sealed class MetricsResult
{
    /// <summary>
    /// When the metrics snapshot was generated.
    /// </summary>
    [JsonPropertyName("generated_at")]
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// Number of currently active projects.
    /// </summary>
    [JsonPropertyName("active_projects")]
    public required int ActiveProjects { get; init; }

    /// <summary>
    /// Cache hit rate as a percentage (0-100).
    /// </summary>
    [JsonPropertyName("cache_hit_rate_percent")]
    public required double CacheHitRate { get; init; }

    /// <summary>
    /// Query latency statistics.
    /// </summary>
    [JsonPropertyName("query_latency")]
    public required LatencyMetrics QueryLatency { get; init; }

    /// <summary>
    /// Embedding generation latency statistics.
    /// </summary>
    [JsonPropertyName("embedding_latency")]
    public required LatencyMetrics EmbeddingLatency { get; init; }

    /// <summary>
    /// Per-tenant statistics.
    /// </summary>
    [JsonPropertyName("tenant_stats")]
    public required List<TenantStats> TenantStats { get; init; }
}

/// <summary>
/// Latency percentile metrics.
/// </summary>
public sealed class LatencyMetrics
{
    /// <summary>
    /// 50th percentile (median) latency in milliseconds.
    /// </summary>
    [JsonPropertyName("p50_ms")]
    public required double P50Ms { get; init; }

    /// <summary>
    /// 95th percentile latency in milliseconds.
    /// </summary>
    [JsonPropertyName("p95_ms")]
    public required double P95Ms { get; init; }

    /// <summary>
    /// 99th percentile latency in milliseconds.
    /// </summary>
    [JsonPropertyName("p99_ms")]
    public required double P99Ms { get; init; }

    /// <summary>
    /// Number of samples used to calculate percentiles.
    /// </summary>
    [JsonPropertyName("sample_count")]
    public required int SampleCount { get; init; }
}

/// <summary>
/// Per-tenant statistics.
/// </summary>
public sealed class TenantStats
{
    /// <summary>
    /// The tenant key.
    /// </summary>
    [JsonPropertyName("tenant_key")]
    public required string TenantKey { get; init; }

    /// <summary>
    /// Number of documents indexed.
    /// </summary>
    [JsonPropertyName("document_count")]
    public required long DocumentCount { get; init; }

    /// <summary>
    /// Number of chunks indexed.
    /// </summary>
    [JsonPropertyName("chunk_count")]
    public required long ChunkCount { get; init; }

    /// <summary>
    /// Number of queries executed.
    /// </summary>
    [JsonPropertyName("query_count")]
    public required long QueryCount { get; init; }
}

/// <summary>
/// Server status result data.
/// </summary>
public sealed class StatusResult
{
    /// <summary>
    /// Server version.
    /// </summary>
    [JsonPropertyName("server_version")]
    public required string ServerVersion { get; init; }

    /// <summary>
    /// Server name.
    /// </summary>
    [JsonPropertyName("server_name")]
    public required string ServerName { get; init; }

    /// <summary>
    /// .NET runtime version.
    /// </summary>
    [JsonPropertyName("runtime_version")]
    public required string RuntimeVersion { get; init; }

    /// <summary>
    /// Server uptime as a human-readable string.
    /// </summary>
    [JsonPropertyName("uptime")]
    public required string Uptime { get; init; }

    /// <summary>
    /// Whether a project is currently active.
    /// </summary>
    [JsonPropertyName("is_project_active")]
    public required bool IsProjectActive { get; init; }

    /// <summary>
    /// Information about the active project, if any.
    /// </summary>
    [JsonPropertyName("active_project")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ActiveProjectInfo? ActiveProject { get; init; }

    /// <summary>
    /// Summary of system health.
    /// </summary>
    [JsonPropertyName("health_summary")]
    public required HealthSummary HealthSummary { get; init; }

    /// <summary>
    /// Summary of system metrics.
    /// </summary>
    [JsonPropertyName("metrics_summary")]
    public required MetricsSummary MetricsSummary { get; init; }
}

/// <summary>
/// Active project information.
/// </summary>
public sealed class ActiveProjectInfo
{
    /// <summary>
    /// Full path to the project.
    /// </summary>
    [JsonPropertyName("project_path")]
    public required string ProjectPath { get; init; }

    /// <summary>
    /// Project name.
    /// </summary>
    [JsonPropertyName("project_name")]
    public required string ProjectName { get; init; }

    /// <summary>
    /// Active git branch.
    /// </summary>
    [JsonPropertyName("branch")]
    public required string Branch { get; init; }

    /// <summary>
    /// Tenant key for the active project.
    /// </summary>
    [JsonPropertyName("tenant_key")]
    public required string TenantKey { get; init; }
}

/// <summary>
/// Health summary information.
/// </summary>
public sealed class HealthSummary
{
    /// <summary>
    /// Overall health status.
    /// </summary>
    [JsonPropertyName("overall_status")]
    public required string OverallStatus { get; init; }

    /// <summary>
    /// Whether the system is healthy.
    /// </summary>
    [JsonPropertyName("is_healthy")]
    public required bool IsHealthy { get; init; }

    /// <summary>
    /// When the health was last checked.
    /// </summary>
    [JsonPropertyName("last_checked_at")]
    public required DateTimeOffset LastCheckedAt { get; init; }

    /// <summary>
    /// List of unhealthy dependency names.
    /// </summary>
    [JsonPropertyName("unhealthy_dependencies")]
    public required List<string> UnhealthyDependencies { get; init; }
}

/// <summary>
/// Metrics summary information.
/// </summary>
public sealed class MetricsSummary
{
    /// <summary>
    /// Number of active projects.
    /// </summary>
    [JsonPropertyName("active_projects")]
    public required int ActiveProjects { get; init; }

    /// <summary>
    /// Total number of tenants with data.
    /// </summary>
    [JsonPropertyName("total_tenants")]
    public required int TotalTenants { get; init; }

    /// <summary>
    /// Cache hit rate as a percentage.
    /// </summary>
    [JsonPropertyName("cache_hit_rate_percent")]
    public required double CacheHitRate { get; init; }

    /// <summary>
    /// Query P50 latency in milliseconds.
    /// </summary>
    [JsonPropertyName("query_p50_ms")]
    public required double QueryP50Ms { get; init; }
}

#endregion
