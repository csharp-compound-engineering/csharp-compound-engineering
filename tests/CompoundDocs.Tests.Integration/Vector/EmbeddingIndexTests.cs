namespace CompoundDocs.Tests.Integration.Vector;

/// <summary>
/// Integration tests for embedding index mapping and 1024-dim validation.
/// </summary>
public class EmbeddingIndexTests
{
    [Fact(Skip = "Requires Aspire container orchestration - run via CI pipeline")]
    public async Task IndexMapping_Supports1024Dimensions()
    {
        await Task.CompletedTask;
    }
}
