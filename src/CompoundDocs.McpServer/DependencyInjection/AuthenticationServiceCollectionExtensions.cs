using CompoundDocs.McpServer.Authentication;
using CompoundDocs.McpServer.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompoundDocs.McpServer.DependencyInjection;

/// <summary>
/// Extension methods for registering API key authentication.
/// </summary>
public static class AuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Adds API key authentication and authorization to the service collection.
    /// Binds <see cref="ApiKeyAuthenticationOptions"/> from the "Authentication" configuration section.
    /// </summary>
    public static IServiceCollection AddApiKeyAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ApiKeyAuthenticationOptions>(
            configuration.GetSection("Authentication"));

        services.AddAuthentication(ApiKeyAuthenticationDefaults.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.AuthenticationScheme, null);

        services.AddAuthorization();

        return services;
    }
}
