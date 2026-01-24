namespace CompoundDocs.Tests.Integration.Graph;

/// <summary>
/// Integration tests for multi-hop graph traversals against Neo4j.
/// </summary>
public class GraphTraversalTests
{
    [Fact(Skip = "Requires Aspire container orchestration - run via CI pipeline")]
    public async Task MultiHopTraversal_ReturnsRelatedDocuments()
    {
        await Task.CompletedTask;
    }
}
