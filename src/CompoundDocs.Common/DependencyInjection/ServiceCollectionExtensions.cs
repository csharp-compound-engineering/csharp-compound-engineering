using CompoundDocs.Common.Configuration;
using CompoundDocs.Common.Logging;
using CompoundDocs.Common.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace CompoundDocs.Common.DependencyInjection;

/// <summary>
/// Extension methods for configuring common services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all CompoundDocs.Common services to the container.
    /// </summary>
    public static IServiceCollection AddCompoundDocsCommon(this IServiceCollection services)
    {
        // Configuration
        services.AddSingleton<ConfigurationLoader>();

        // Parsing services
        services.AddSingleton<MarkdownParser>();
        services.AddSingleton<FrontmatterParser>();
        services.AddSingleton<SchemaValidator>();

        return services;
    }

    /// <summary>
    /// Adds correlation ID support to logging.
    /// </summary>
    public static IServiceCollection AddCorrelationLogging(this IServiceCollection services)
    {
        services.AddScoped<CorrelationContext>();
        return services;
    }

    /// <summary>
    /// Configures Serilog with sensitive data masking.
    /// </summary>
    public static LoggerConfiguration WithSensitiveDataMasking(this LoggerConfiguration config)
    {
        return config
            .Enrich.With<SensitiveDataMasker>()
            .Destructure.With<SensitiveDataDestructuringPolicy>();
    }
}
