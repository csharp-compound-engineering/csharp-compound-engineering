using CompoundDocs.Common.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CompoundDocs.Graph.DependencyInjection;

public static class GraphServiceCollectionExtensions
{
    public static IServiceCollection AddNeptuneGraph(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton(configuration);
        services.AddOptions<NeptuneConfig>()
            .BindConfiguration("CompoundDocs:Neptune");

        services.AddSingleton<INeptunedataClientFactory, NeptunedataClientFactory>();
        services.AddSingleton<INeptuneClient, NeptuneClient>();
        services.AddSingleton<IGraphRepository, NeptuneGraphRepository>();
        return services;
    }
}
