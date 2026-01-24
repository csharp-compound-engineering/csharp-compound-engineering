using CompoundDocs.Common.Configuration;
using CompoundDocs.Graph;
using CompoundDocs.Graph.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CompoundDocs.Tests.Graph;

public sealed class GraphServiceCollectionExtensionsTests
{
    [Fact]
    public void AddNeptuneGraph_ConfiguresNeptuneConfig()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CompoundDocs:Neptune:Endpoint"] = "neptune.test.com",
                ["CompoundDocs:Neptune:Port"] = "9999"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNeptuneGraph(config);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<NeptuneConfig>>().Value;

        options.Endpoint.ShouldBe("neptune.test.com");
        options.Port.ShouldBe(9999);
    }

    [Fact]
    public void AddNeptuneGraph_RegistersServices()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNeptuneGraph(config);

        var descriptors = services.ToList();
        descriptors.ShouldContain(d => d.ServiceType == typeof(INeptuneClient));
        descriptors.ShouldContain(d => d.ServiceType == typeof(IGraphRepository));
    }
}
