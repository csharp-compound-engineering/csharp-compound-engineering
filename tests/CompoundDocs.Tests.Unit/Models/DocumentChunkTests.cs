using CompoundDocs.McpServer.Models;

namespace CompoundDocs.Tests.Unit.Models;

public class DocumentChunkTests
{
    [Fact]
    public void Constructor_DefaultValues_AreValid()
    {
        var chunk = new DocumentChunk();

        chunk.Id.Should().NotBeNullOrEmpty();
        Guid.TryParse(chunk.Id, out _).Should().BeTrue();
        chunk.DocumentId.Should().BeEmpty();
        chunk.HeaderPath.Should().BeEmpty();
        chunk.StartLine.Should().Be(0);
        chunk.EndLine.Should().Be(0);
        chunk.Content.Should().BeEmpty();
    }

    [Fact]
    public void CreateFromParent_SetsCorrectValues()
    {
        var parent = new CompoundDocument { Id = "parent-doc-id" };

        var chunk = DocumentChunk.CreateFromParent(parent, "## Section", "content", 10, 20);

        chunk.DocumentId.Should().Be("parent-doc-id");
        chunk.HeaderPath.Should().Be("## Section");
        chunk.Content.Should().Be("content");
        chunk.StartLine.Should().Be(10);
        chunk.EndLine.Should().Be(20);
    }

    [Fact]
    public void LineCount_CalculatesCorrectly()
    {
        var chunk = new DocumentChunk { StartLine = 5, EndLine = 15 };

        chunk.LineCount.Should().Be(11);
    }

    [Fact]
    public void LineCount_SingleLine_ReturnsOne()
    {
        var chunk = new DocumentChunk { StartLine = 1, EndLine = 1 };

        chunk.LineCount.Should().Be(1);
    }
}
