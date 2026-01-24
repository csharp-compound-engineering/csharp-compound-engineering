using CompoundDocs.Bedrock;
using CompoundDocs.Bedrock.DependencyInjection;
using CompoundDocs.Common.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CompoundDocs.Tests.Bedrock;

public sealed class BedrockServiceCollectionExtensionsTests
{
    [Fact]
    public void AddBedrockServices_ConfiguresBedrockConfig()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CompoundDocs:Bedrock:EmbeddingModelId"] = "custom-embed",
                ["CompoundDocs:Bedrock:SonnetModelId"] = "custom-sonnet",
                ["CompoundDocs:Bedrock:HaikuModelId"] = "custom-haiku",
                ["CompoundDocs:Bedrock:OpusModelId"] = "custom-opus"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBedrockServices(config);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<BedrockConfig>>().Value;

        options.EmbeddingModelId.ShouldBe("custom-embed");
        options.SonnetModelId.ShouldBe("custom-sonnet");
        options.HaikuModelId.ShouldBe("custom-haiku");
        options.OpusModelId.ShouldBe("custom-opus");
    }

    [Fact]
    public void AddBedrockServices_RegistersServices()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBedrockServices(config);

        // Verify registrations exist (they'll fail to construct without AWS credentials,
        // but the service descriptors should be present)
        var descriptors = services.ToList();
        descriptors.ShouldContain(d => d.ServiceType == typeof(IBedrockEmbeddingService));
        descriptors.ShouldContain(d => d.ServiceType == typeof(IBedrockLlmService));
    }
}
