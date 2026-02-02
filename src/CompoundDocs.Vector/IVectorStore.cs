namespace CompoundDocs.Vector;

public interface IVectorStore
{
    Task IndexAsync(
        string chunkId,
        float[] embedding,
        Dictionary<string, string> metadata,
        CancellationToken ct = default);

    Task DeleteByDocumentIdAsync(
        string documentId,
        CancellationToken ct = default);

    Task<List<VectorSearchResult>> SearchAsync(
        float[] queryEmbedding,
        int topK = 10,
        Dictionary<string, string>? filters = null,
        CancellationToken ct = default);

    Task BatchIndexAsync(
        IEnumerable<VectorDocument> documents,
        CancellationToken ct = default);
}
