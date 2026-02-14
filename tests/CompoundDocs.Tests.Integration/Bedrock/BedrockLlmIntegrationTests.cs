using CompoundDocs.Bedrock;
using CompoundDocs.Bedrock.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompoundDocs.Tests.Integration.Bedrock;

/// <summary>
/// Integration tests for Amazon Bedrock LLM Converse API serialization and response handling.
/// These tests require real AWS infrastructure and are skipped in CI.
/// </summary>
public class BedrockLlmIntegrationTests
{
    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task ConverseApi_SerializesCorrectly()
    {
        // Arrange: configure real Bedrock LLM service
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddBedrockServices(config);
        await using var provider = services.BuildServiceProvider();

        var llmService = provider.GetRequiredService<IBedrockLlmService>();

        // Act: send a minimal prompt to verify Converse API serialization
        var response = await llmService.GenerateAsync(
            "You are a helpful assistant.",
            [new BedrockMessage("user", "Say hello in one word.")],
            ModelTier.Haiku);

        // Assert: response should be non-empty text
        response.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task GenerateWithSystemPrompt_RespectsInstructions()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddBedrockServices(config);
        await using var provider = services.BuildServiceProvider();

        var llmService = provider.GetRequiredService<IBedrockLlmService>();

        // Act: use a system prompt that constrains the output format
        var response = await llmService.GenerateAsync(
            "You are a JSON-only assistant. Always respond with valid JSON. No other text.",
            [new BedrockMessage("user", "Return a JSON object with a single key 'status' set to 'ok'.")],
            ModelTier.Haiku);

        // Assert: response should contain JSON-like content
        response.ShouldNotBeNullOrWhiteSpace();
        response.ShouldContain("status");
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task MultiTurnConversation_MaintainsContext()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddBedrockServices(config);
        await using var provider = services.BuildServiceProvider();

        var llmService = provider.GetRequiredService<IBedrockLlmService>();

        var messages = new List<BedrockMessage>
        {
            new("user", "My name is TestUser."),
            new("assistant", "Hello TestUser! How can I help you?"),
            new("user", "What is my name?")
        };

        // Act: send a multi-turn conversation to verify context handling
        var response = await llmService.GenerateAsync(
            "You are a helpful assistant.",
            messages,
            ModelTier.Haiku);

        // Assert: the model should reference the name from earlier context
        response.ShouldNotBeNullOrWhiteSpace();
        response.ShouldContain("TestUser");
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task SonnetTier_ReturnsResponse()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddBedrockServices(config);
        await using var provider = services.BuildServiceProvider();

        var llmService = provider.GetRequiredService<IBedrockLlmService>();

        // Act: verify that the Sonnet model tier resolves correctly
        var response = await llmService.GenerateAsync(
            "You are a technical documentation assistant.",
            [new BedrockMessage("user", "Explain what a record type is in C# in one sentence.")],
            ModelTier.Sonnet);

        // Assert
        response.ShouldNotBeNullOrWhiteSpace();
        response.Length.ShouldBeGreaterThan(10);
    }
}
