using CompoundDocs.McpServer.Models;

namespace CompoundDocs.Tests.Unit.Models;

public class CompoundDocumentTests
{
    [Fact]
    public void Constructor_DefaultValues_AreValid()
    {
        var doc = new CompoundDocument();

        doc.Id.Should().NotBeNullOrEmpty();
        Guid.TryParse(doc.Id, out _).Should().BeTrue();
        doc.Title.Should().BeEmpty();
        doc.Content.Should().BeEmpty();
        doc.DocType.Should().BeNull();
        doc.FilePath.Should().BeEmpty();
        doc.LastModified.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        doc.Links.Should().BeNull();
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var now = DateTimeOffset.UtcNow;
        var doc = new CompoundDocument
        {
            Id = "custom-id",
            Title = "Test Document",
            Content = "# Test\nContent here",
            DocType = "insight",
            FilePath = "docs/test.md",
            LastModified = now,
            Links = "[\"doc2\",\"doc3\"]"
        };

        doc.Id.Should().Be("custom-id");
        doc.Title.Should().Be("Test Document");
        doc.Content.Should().Contain("# Test");
        doc.DocType.Should().Be("insight");
        doc.FilePath.Should().Be("docs/test.md");
        doc.LastModified.Should().Be(now);
        doc.Links.Should().Contain("doc2");
    }
}
