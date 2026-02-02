using CompoundDocs.Common.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompoundDocs.Bedrock.DependencyInjection;

public static class BedrockServiceCollectionExtensions
{
    public static IServiceCollection AddBedrockServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<BedrockConfig>(
            configuration.GetSection("CompoundDocs:Bedrock"));
        services.AddSingleton<IBedrockEmbeddingService, BedrockEmbeddingService>();
        services.AddSingleton<IBedrockLlmService, BedrockLlmService>();
        return services;
    }
}
