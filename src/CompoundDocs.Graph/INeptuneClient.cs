using System.Text.Json;

namespace CompoundDocs.Graph;

public interface INeptuneClient
{
    Task<JsonElement> ExecuteOpenCypherAsync(
        string query,
        Dictionary<string, object>? parameters,
        CancellationToken ct);

    Task<bool> TestConnectionAsync(CancellationToken ct);
}
