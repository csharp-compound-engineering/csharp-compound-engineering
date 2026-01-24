namespace CompoundDocs.GraphRag;

public interface IGraphRagPipeline
{
    Task<GraphRagResult> QueryAsync(string query, GraphRagOptions? options = null, CancellationToken ct = default);
}
