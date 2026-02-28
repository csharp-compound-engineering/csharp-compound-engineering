using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.Common.Logging;

/// <summary>
/// Configures logging for MCP server and worker processes.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Logging builder configuration; side-effect heavy")]
public static class LoggingConfiguration
{
    /// <summary>
    /// Configures simple console logging for MCP server compatibility.
    /// </summary>
    public static ILoggingBuilder ConfigureMcpServerLogging(this ILoggingBuilder builder)
    {
        builder.ClearProviders();
        builder.SetMinimumLevel(LogLevel.Information);
        builder.AddFilter("Microsoft", LogLevel.Warning);
        builder.AddFilter("System", LogLevel.Warning);
        builder.AddSimpleConsole(options =>
        {
            options.TimestampFormat = "HH:mm:ss ";
            options.SingleLine = true;
        });
        return builder;
    }

    /// <summary>
    /// Configures structured JSON logging.
    /// </summary>
    public static ILoggingBuilder ConfigureStructuredLogging(this ILoggingBuilder builder)
    {
        builder.ClearProviders();
        builder.SetMinimumLevel(LogLevel.Information);
        builder.AddFilter("Microsoft", LogLevel.Warning);
        builder.AddFilter("System", LogLevel.Warning);
        builder.AddJsonConsole();
        return builder;
    }
}
