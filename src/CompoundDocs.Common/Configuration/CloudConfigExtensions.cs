using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompoundDocs.Common.Configuration;

public static class CloudConfigExtensions
{
    public static IServiceCollection AddCompoundDocsCloudConfig(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CompoundDocsCloudConfig>(
            configuration.GetSection("CompoundDocs"));
        return services;
    }
}
