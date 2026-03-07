using Amazon.Runtime.Documents;

namespace CompoundDocs.Graph;

public interface INeptuneClient
{
    Task<Document> ExecuteOpenCypherAsync(
        string query,
        Dictionary<string, object>? parameters,
        CancellationToken ct);

    Task<bool> TestConnectionAsync(CancellationToken ct);
}
