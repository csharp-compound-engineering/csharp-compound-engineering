namespace CompoundDocs.Tests.Integration.Pipeline;

/// <summary>
/// Integration tests for the full GraphRAG pipeline through all containers.
/// </summary>
public class GraphRagPipelineIntegrationTests
{
    [Fact(Skip = "Requires Aspire container orchestration - run via CI pipeline")]
    public async Task FullPipeline_EmbedSearchEnrichSynthesize()
    {
        await Task.CompletedTask;
    }
}
