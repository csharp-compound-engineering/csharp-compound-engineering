using CompoundDocs.Vector;

namespace CompoundDocs.Tests.Vector;

public sealed class VectorModelsTests
{
    [Fact]
    public void VectorSearchResult_RequiredProperties()
    {
        var result = new VectorSearchResult
        {
            ChunkId = "chunk-1",
            Score = 0.95
        };

        result.ChunkId.ShouldBe("chunk-1");
        result.Score.ShouldBe(0.95);
    }

    [Fact]
    public void VectorSearchResult_DefaultMetadataIsEmpty()
    {
        var result = new VectorSearchResult
        {
            ChunkId = "chunk-1",
            Score = 0.5
        };

        result.Metadata.ShouldNotBeNull();
        result.Metadata.ShouldBeEmpty();
    }

    [Fact]
    public void VectorSearchResult_MetadataCanBePopulated()
    {
        var result = new VectorSearchResult
        {
            ChunkId = "chunk-1",
            Score = 0.5,
            Metadata = new Dictionary<string, string> { ["repo"] = "test" }
        };

        result.Metadata.Count.ShouldBe(1);
        result.Metadata["repo"].ShouldBe("test");
    }

    [Fact]
    public void VectorDocument_RequiredProperties()
    {
        var doc = new VectorDocument
        {
            ChunkId = "chunk-1",
            Embedding = new float[] { 0.1f, 0.2f, 0.3f }
        };

        doc.ChunkId.ShouldBe("chunk-1");
        doc.Embedding.Length.ShouldBe(3);
    }

    [Fact]
    public void VectorDocument_DefaultMetadataIsEmpty()
    {
        var doc = new VectorDocument
        {
            ChunkId = "chunk-1",
            Embedding = new float[] { 0.1f }
        };

        doc.Metadata.ShouldNotBeNull();
        doc.Metadata.ShouldBeEmpty();
    }
}
