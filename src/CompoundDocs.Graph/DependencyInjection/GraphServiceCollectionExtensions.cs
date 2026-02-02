using CompoundDocs.Common.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompoundDocs.Graph.DependencyInjection;

public static class GraphServiceCollectionExtensions
{
    public static IServiceCollection AddNeptuneGraph(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<NeptuneConfig>(
            configuration.GetSection("CompoundDocs:Neptune"));
        services.AddSingleton<INeptuneClient, NeptuneClient>();
        services.AddSingleton<IGraphRepository, NeptuneGraphRepository>();
        return services;
    }
}
