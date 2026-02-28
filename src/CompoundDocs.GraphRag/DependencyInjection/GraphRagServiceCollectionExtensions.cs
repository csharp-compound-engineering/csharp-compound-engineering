using Microsoft.Extensions.DependencyInjection;

namespace CompoundDocs.GraphRag.DependencyInjection;

public static class GraphRagServiceCollectionExtensions
{
    public static IServiceCollection AddGraphRag(this IServiceCollection services)
    {
        services.AddSingleton<IEntityExtractor, BedrockEntityExtractor>();
        services.AddSingleton<IDocumentIngestionService, DocumentIngestionService>();
        services.AddSingleton<IGraphRagPipeline, GraphRagPipeline>();
        services.AddSingleton<ICrossRepoEntityResolver, CrossRepoEntityResolver>();
        return services;
    }
}
