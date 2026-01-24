using CompoundDocs.Vector;

namespace CompoundDocs.Tests.Unit.Models;

public class VectorModelsTests
{
    [Fact]
    public void VectorSearchResult_RequiredProperties()
    {
        var result = new VectorSearchResult
        {
            ChunkId = "chunk1",
            Score = 0.95
        };

        result.ChunkId.ShouldBe("chunk1");
        result.Score.ShouldBe(0.95);
        result.Metadata.ShouldBeEmpty();
    }

    [Fact]
    public void VectorSearchResult_WithMetadata()
    {
        var result = new VectorSearchResult
        {
            ChunkId = "chunk1",
            Score = 0.9,
            Metadata = new Dictionary<string, string> { ["doc_type"] = "insight" }
        };

        result.Metadata.ShouldContainKey("doc_type");
        result.Metadata["doc_type"].ShouldBe("insight");
    }

    [Fact]
    public void VectorDocument_RequiredProperties()
    {
        var doc = new VectorDocument
        {
            ChunkId = "chunk1",
            Embedding = new float[] { 0.1f, 0.2f, 0.3f }
        };

        doc.ChunkId.ShouldBe("chunk1");
        doc.Embedding.Length.ShouldBe(3);
        doc.Metadata.ShouldBeEmpty();
    }

    [Fact]
    public void VectorDocument_WithSameValues_AreEquivalent()
    {
        var embedding = new float[] { 0.1f, 0.2f };
        var metadata = new Dictionary<string, string> { ["key"] = "val" };
        var doc1 = new VectorDocument { ChunkId = "c1", Embedding = embedding, Metadata = metadata };
        var doc2 = new VectorDocument { ChunkId = "c1", Embedding = embedding, Metadata = metadata };

        doc1.ShouldBe(doc2);
    }
}
