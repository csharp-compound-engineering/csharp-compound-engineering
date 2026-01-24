using Amazon;
using Amazon.BedrockRuntime;
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

        var region = configuration.GetValue<string>("CompoundDocs:Aws:Region") ?? "us-east-1";
        services.AddAWSService<IAmazonBedrockRuntime>(new Amazon.Extensions.NETCore.Setup.AWSOptions
        {
            Region = RegionEndpoint.GetBySystemName(region)
        });

        services.AddSingleton<IBedrockEmbeddingService, BedrockEmbeddingService>();
        services.AddSingleton<IBedrockLlmService, BedrockLlmService>();
        return services;
    }
}
