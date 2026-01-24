using Amazon;
using CompoundDocs.Common.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenSearch.Client;
using OpenSearch.Net.Auth.AwsSigV4;

namespace CompoundDocs.Vector.DependencyInjection;

public static class VectorServiceCollectionExtensions
{
    public static IServiceCollection AddOpenSearchVector(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OpenSearchConfig>(
            configuration.GetSection("CompoundDocs:OpenSearch"));

        services.AddSingleton<IOpenSearchClient>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<OpenSearchConfig>>().Value;
            var region = configuration.GetValue<string>("CompoundDocs:Aws:Region") ?? "us-east-1";
            var connection = new AwsSigV4HttpConnection(
                RegionEndpoint.GetBySystemName(region),
                service: AwsSigV4HttpConnection.OpenSearchServerlessService);
            var settings = new ConnectionSettings(new Uri(config.CollectionEndpoint), connection)
                .DefaultIndex(config.IndexName);
            return new OpenSearchClient(settings);
        });

        services.AddSingleton<IVectorStore, OpenSearchVectorStore>();
        return services;
    }
}
