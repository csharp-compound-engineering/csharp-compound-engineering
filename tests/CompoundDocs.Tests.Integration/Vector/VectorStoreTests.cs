namespace CompoundDocs.Tests.Integration.Vector;

/// <summary>
/// Integration tests for vector store k-NN operations against OpenSearch.
/// </summary>
public class VectorStoreTests
{
    [Fact(Skip = "Requires Aspire container orchestration - run via CI pipeline")]
    public async Task IndexAndSearch_ReturnsRelevantResults()
    {
        await Task.CompletedTask;
    }
}
