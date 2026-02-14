using CompoundDocs.Common.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CompoundDocs.Tests.Unit.Configuration;

public sealed class CloudConfigTests
{
    [Fact]
    public void DefaultValues_MatchPlanSpec()
    {
        // Arrange & Act
        var config = new CompoundDocsCloudConfig();

        // Assert
        config.Aws.Region.ShouldBe("us-east-1");
        config.Neptune.Port.ShouldBe(8182);
        config.OpenSearch.IndexName.ShouldBe("compound-docs");
        config.Bedrock.EmbeddingModelId.ShouldBe("amazon.titan-embed-text-v2:0");
        config.Bedrock.SonnetModelId.ShouldBe("anthropic.claude-sonnet-4-5-v1:0");
        config.Bedrock.HaikuModelId.ShouldBe("anthropic.claude-haiku-4-5-v1:0");
        config.Bedrock.OpusModelId.ShouldBe("anthropic.claude-opus-4-5-v1:0");
    }

    [Fact]
    public void BedrockConfig_DefaultsToClaudeFortyFiveModels()
    {
        var config = new BedrockConfig();

        config.SonnetModelId.ShouldContain("4-5");
        config.HaikuModelId.ShouldContain("4-5");
        config.OpusModelId.ShouldContain("4-5");
    }

    [Fact]
    public void CompoundDocsCloudConfig_BindsFromConfiguration()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["CompoundDocs:Aws:Region"] = "eu-west-1",
            ["CompoundDocs:Neptune:Endpoint"] = "neptune.example.com",
            ["CompoundDocs:Neptune:Port"] = "9182",
            ["CompoundDocs:OpenSearch:CollectionEndpoint"] = "https://opensearch.example.com",
            ["CompoundDocs:OpenSearch:IndexName"] = "custom-index",
            ["CompoundDocs:Bedrock:EmbeddingModelId"] = "custom-embedding-model",
            ["CompoundDocs:Bedrock:SonnetModelId"] = "custom-sonnet",
            ["CompoundDocs:GraphRag:MaxTraversalSteps"] = "10",
            ["CompoundDocs:GraphRag:MinRelevanceScore"] = "0.8"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddCompoundDocsCloudConfig(configuration);
        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<CompoundDocsCloudConfig>>().Value;

        // Assert
        options.Aws.Region.ShouldBe("eu-west-1");
        options.Neptune.Endpoint.ShouldBe("neptune.example.com");
        options.Neptune.Port.ShouldBe(9182);
        options.OpenSearch.CollectionEndpoint.ShouldBe("https://opensearch.example.com");
        options.OpenSearch.IndexName.ShouldBe("custom-index");
        options.Bedrock.EmbeddingModelId.ShouldBe("custom-embedding-model");
        options.Bedrock.SonnetModelId.ShouldBe("custom-sonnet");
        options.GraphRag.MaxTraversalSteps.ShouldBe(10);
        options.GraphRag.MinRelevanceScore.ShouldBe(0.8);
    }

    [Fact]
    public void GraphRagConfig_HasCorrectDefaults()
    {
        var config = new GraphRagConfig();

        config.MaxTraversalSteps.ShouldBe(5);
        config.MaxChunksPerQuery.ShouldBe(10);
        config.MinRelevanceScore.ShouldBe(0.7);
        config.UseCrossRepoLinks.ShouldBeTrue();
    }

    [Fact]
    public void RepositoryConfig_Defaults()
    {
        var config = new RepositoryConfig();

        config.Branch.ShouldBe("main");
        config.PollIntervalMinutes.ShouldBe(5);
        config.MonitoredPaths.ShouldNotBeNull();
        config.MonitoredPaths.ShouldBeEmpty();
        config.Url.ShouldBe(string.Empty);
        config.Name.ShouldBe(string.Empty);
    }

    [Fact]
    public void AuthConfig_DefaultApiKeysIsEmpty()
    {
        var config = new AuthConfig();

        config.ApiKeys.ShouldNotBeNull();
        config.ApiKeys.ShouldBeEmpty();
    }

    [Fact]
    public void AwsConfig_DefaultRegion()
    {
        var config = new AwsConfig();
        config.Region.ShouldBe("us-east-1");
    }

    [Fact]
    public void NeptuneConfig_DefaultEndpointIsEmpty()
    {
        var config = new NeptuneConfig();
        config.Endpoint.ShouldBe(string.Empty);
        config.Port.ShouldBe(8182);
    }

    [Fact]
    public void OpenSearchConfig_DefaultEndpointIsEmpty()
    {
        var config = new OpenSearchConfig();
        config.CollectionEndpoint.ShouldBe(string.Empty);
        config.IndexName.ShouldBe("compound-docs");
    }

    [Fact]
    public void CompoundDocsCloudConfig_DefaultRepositoriesIsEmpty()
    {
        var config = new CompoundDocsCloudConfig();
        config.Repositories.ShouldNotBeNull();
        config.Repositories.ShouldBeEmpty();
    }
}
