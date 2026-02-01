using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace CompoundDocs.Common.Logging;

/// <summary>
/// Configures Serilog for MCP server (stderr only per MCP protocol constraints).
/// </summary>
public static class LoggingConfiguration
{
    /// <summary>
    /// Configures Serilog to write to stderr for MCP server compatibility.
    /// </summary>
    public static IHostBuilder UseMcpServerLogging(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseSerilog((context, services, configuration) =>
        {
            configuration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .Enrich.With<SensitiveDataMasker>()
                .Destructure.With<SensitiveDataDestructuringPolicy>()
                .WriteTo.Console(
                    standardErrorFromLevel: LogEventLevel.Verbose,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}");
        });
    }

    /// <summary>
    /// Configures structured JSON logging to stderr.
    /// </summary>
    public static IHostBuilder UseStructuredLogging(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseSerilog((context, services, configuration) =>
        {
            configuration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .Enrich.With<SensitiveDataMasker>()
                .Destructure.With<SensitiveDataDestructuringPolicy>()
                .WriteTo.Console(
                    new CompactJsonFormatter(),
                    standardErrorFromLevel: LogEventLevel.Verbose);
        });
    }
}
