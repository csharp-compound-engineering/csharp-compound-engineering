using CompoundDocs.McpServer.Models;

namespace CompoundDocs.Tests.Models;

public class CompoundDocumentTests
{
    [Fact]
    public void Constructor_DefaultValues_AreValid()
    {
        var doc = new CompoundDocument();

        doc.Id.ShouldNotBeNullOrEmpty();
        Guid.TryParse(doc.Id, out _).ShouldBeTrue();
        doc.Title.ShouldBeEmpty();
        doc.Content.ShouldBeEmpty();
        doc.DocType.ShouldBeNull();
        doc.FilePath.ShouldBeEmpty();
        (DateTimeOffset.UtcNow - doc.LastModified).Duration().TotalSeconds.ShouldBeLessThan(5);
        doc.Links.ShouldBeNull();
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

        doc.Id.ShouldBe("custom-id");
        doc.Title.ShouldBe("Test Document");
        doc.Content.ShouldContain("# Test");
        doc.DocType.ShouldBe("insight");
        doc.FilePath.ShouldBe("docs/test.md");
        doc.LastModified.ShouldBe(now);
        doc.Links.ShouldContain("doc2");
    }
}
