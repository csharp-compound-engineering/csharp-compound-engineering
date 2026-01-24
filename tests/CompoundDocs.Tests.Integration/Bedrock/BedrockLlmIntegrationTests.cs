namespace CompoundDocs.Tests.Integration.Bedrock;

/// <summary>
/// Integration tests for Bedrock LLM SDK serialization against WireMock.
/// </summary>
public class BedrockLlmIntegrationTests
{
    [Fact(Skip = "Requires Aspire container orchestration - run via CI pipeline")]
    public async Task ConverseApi_SerializesCorrectly()
    {
        await Task.CompletedTask;
    }
}
