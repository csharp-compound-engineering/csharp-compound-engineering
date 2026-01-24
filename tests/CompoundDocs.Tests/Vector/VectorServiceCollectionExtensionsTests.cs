using CompoundDocs.Common.Configuration;
using CompoundDocs.Vector;
using CompoundDocs.Vector.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenSearch.Client;

namespace CompoundDocs.Tests.Vector;

public sealed class VectorServiceCollectionExtensionsTests
{
    [Fact]
    public void AddOpenSearchVector_RegistersIVectorStore()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CompoundDocs:OpenSearch:CollectionEndpoint"] = "https://test.endpoint.com",
                ["CompoundDocs:OpenSearch:IndexName"] = "my-index",
                ["CompoundDocs:Aws:Region"] = "us-east-1"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOpenSearchVector(config);

        // Verify service descriptors are registered without resolving (avoids needing AWS creds)
        var vectorStoreDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IVectorStore));
        vectorStoreDescriptor.ShouldNotBeNull();
        vectorStoreDescriptor!.ImplementationType.ShouldBe(typeof(OpenSearchVectorStore));
        vectorStoreDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);

        var clientDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IOpenSearchClient));
        clientDescriptor.ShouldNotBeNull();
        clientDescriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddOpenSearchVector_ConfiguresOpenSearchConfig()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CompoundDocs:OpenSearch:CollectionEndpoint"] = "https://custom.endpoint.com",
                ["CompoundDocs:OpenSearch:IndexName"] = "custom-index",
                ["CompoundDocs:Aws:Region"] = "us-west-2"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOpenSearchVector(config);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OpenSearchConfig>>().Value;
        options.CollectionEndpoint.ShouldBe("https://custom.endpoint.com");
        options.IndexName.ShouldBe("custom-index");
    }
}
