using CompoundDocs.Common.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CompoundDocs.Tests.Configuration;

public sealed class CloudConfigExtensionsTests
{
    [Fact]
    public void AddCompoundDocsCloudConfig_RegistersIOptionsOfCloudConfig()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CompoundDocs:Aws:Region"] = "us-west-2"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddCompoundDocsCloudConfig(config);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<CompoundDocsCloudConfig>>().Value;

        options.ShouldNotBeNull();
        options.Aws.Region.ShouldBe("us-west-2");
    }

    [Fact]
    public void AddCompoundDocsCloudConfig_BindsNestedSections()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CompoundDocs:Neptune:Endpoint"] = "neptune.example.com",
                ["CompoundDocs:Neptune:Port"] = "8182",
                ["CompoundDocs:OpenSearch:CollectionEndpoint"] = "https://os.example.com",
                ["CompoundDocs:Bedrock:SonnetModelId"] = "test-model"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddCompoundDocsCloudConfig(config);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<CompoundDocsCloudConfig>>().Value;

        options.Neptune.Endpoint.ShouldBe("neptune.example.com");
        options.Neptune.Port.ShouldBe(8182);
        options.OpenSearch.CollectionEndpoint.ShouldBe("https://os.example.com");
        options.Bedrock.SonnetModelId.ShouldBe("test-model");
    }
}
