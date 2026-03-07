using OpenSearch.Client;

namespace CompoundDocs.Vector;

public record VectorSearchResult
{
    public required string ChunkId { get; init; }
    public required double Score { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = [];
}

public record VectorDocument
{
    public required string ChunkId { get; init; }
    public required float[] Embedding { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = [];
}

internal record OpenSearchChunkDocument
{
    [PropertyName("chunk_id")]
    public string ChunkId { get; init; } = string.Empty;

    [PropertyName("embedding")]
    public float[] Embedding { get; init; } = [];

    [PropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; init; } = [];
}
