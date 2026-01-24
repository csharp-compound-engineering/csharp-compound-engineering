using CompoundDocs.McpServer.Models;

namespace CompoundDocs.Tests.Unit.Models;

public class DocumentChunkTests
{
    [Fact]
    public void Constructor_DefaultValues_AreValid()
    {
        var chunk = new DocumentChunk();

        chunk.Id.ShouldNotBeNullOrEmpty();
        Guid.TryParse(chunk.Id, out _).ShouldBeTrue();
        chunk.DocumentId.ShouldBeEmpty();
        chunk.HeaderPath.ShouldBeEmpty();
        chunk.StartLine.ShouldBe(0);
        chunk.EndLine.ShouldBe(0);
        chunk.Content.ShouldBeEmpty();
    }

    [Fact]
    public void CreateFromParent_SetsCorrectValues()
    {
        var parent = new CompoundDocument { Id = "parent-doc-id" };

        var chunk = DocumentChunk.CreateFromParent(parent, "## Section", "content", 10, 20);

        chunk.DocumentId.ShouldBe("parent-doc-id");
        chunk.HeaderPath.ShouldBe("## Section");
        chunk.Content.ShouldBe("content");
        chunk.StartLine.ShouldBe(10);
        chunk.EndLine.ShouldBe(20);
    }

    [Fact]
    public void LineCount_CalculatesCorrectly()
    {
        var chunk = new DocumentChunk { StartLine = 5, EndLine = 15 };

        chunk.LineCount.ShouldBe(11);
    }

    [Fact]
    public void LineCount_SingleLine_ReturnsOne()
    {
        var chunk = new DocumentChunk { StartLine = 1, EndLine = 1 };

        chunk.LineCount.ShouldBe(1);
    }
}
