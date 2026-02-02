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

        result.ChunkId.Should().Be("chunk1");
        result.Score.Should().Be(0.95);
        result.Metadata.Should().BeEmpty();
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

        result.Metadata.Should().ContainKey("doc_type");
        result.Metadata["doc_type"].Should().Be("insight");
    }

    [Fact]
    public void VectorDocument_RequiredProperties()
    {
        var doc = new VectorDocument
        {
            ChunkId = "chunk1",
            Embedding = new float[] { 0.1f, 0.2f, 0.3f }
        };

        doc.ChunkId.Should().Be("chunk1");
        doc.Embedding.Should().HaveCount(3);
        doc.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void VectorDocument_WithSameValues_AreEquivalent()
    {
        var embedding = new float[] { 0.1f, 0.2f };
        var metadata = new Dictionary<string, string> { ["key"] = "val" };
        var doc1 = new VectorDocument { ChunkId = "c1", Embedding = embedding, Metadata = metadata };
        var doc2 = new VectorDocument { ChunkId = "c1", Embedding = embedding, Metadata = metadata };

        doc1.Should().Be(doc2);
    }
}
