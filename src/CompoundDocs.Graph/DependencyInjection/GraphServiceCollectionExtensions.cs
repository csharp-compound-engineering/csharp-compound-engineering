using Amazon.Neptunedata;
using CompoundDocs.Common.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CompoundDocs.Graph.DependencyInjection;

public static class GraphServiceCollectionExtensions
{
    public static IServiceCollection AddNeptuneGraph(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<NeptuneConfig>(
            configuration.GetSection("CompoundDocs:Neptune"));

        services.AddSingleton<IAmazonNeptunedata>(sp =>
        {
            var cfg = sp.GetRequiredService<IOptions<NeptuneConfig>>().Value;
            return new AmazonNeptunedataClient(new AmazonNeptunedataConfig
            {
                ServiceURL = $"https://{cfg.Endpoint}:{cfg.Port}"
            });
        });

        services.AddSingleton<INeptuneClient, NeptuneClient>();
        services.AddSingleton<IGraphRepository, NeptuneGraphRepository>();
        return services;
    }
}
