using CompoundDocs.Common.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CompoundDocs.Vector.DependencyInjection;

public static class VectorServiceCollectionExtensions
{
    public static IServiceCollection AddOpenSearchVector(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton(configuration);
        services.AddOptions<OpenSearchConfig>()
            .BindConfiguration("CompoundDocs:OpenSearch");

        services.AddSingleton<IOpenSearchClientFactory, OpenSearchClientFactory>();
        services.AddSingleton<IVectorStore, OpenSearchVectorStore>();
        return services;
    }
}
