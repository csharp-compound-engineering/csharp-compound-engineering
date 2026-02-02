using CompoundDocs.Common.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompoundDocs.Vector.DependencyInjection;

public static class VectorServiceCollectionExtensions
{
    public static IServiceCollection AddOpenSearchVector(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OpenSearchConfig>(
            configuration.GetSection("CompoundDocs:OpenSearch"));
        services.AddHttpClient<IVectorStore, OpenSearchVectorStore>();
        return services;
    }
}
