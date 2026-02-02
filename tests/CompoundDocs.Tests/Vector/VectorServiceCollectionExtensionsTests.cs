using CompoundDocs.Common.Configuration;
using CompoundDocs.Vector;
using CompoundDocs.Vector.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
                ["CompoundDocs:OpenSearch:IndexName"] = "my-index"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOpenSearchVector(config);

        var provider = services.BuildServiceProvider();
        var vectorStore = provider.GetService<IVectorStore>();
        vectorStore.ShouldNotBeNull();
        vectorStore.ShouldBeOfType<OpenSearchVectorStore>();
    }

    [Fact]
    public void AddOpenSearchVector_ConfiguresOpenSearchConfig()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CompoundDocs:OpenSearch:CollectionEndpoint"] = "https://custom.endpoint.com",
                ["CompoundDocs:OpenSearch:IndexName"] = "custom-index"
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
