using CompoundDocs.GraphRag;

namespace CompoundDocs.Tests.GraphRag;

public sealed class DocumentIngestionMetadataTests
{
    [Fact]
    public void DocumentIngestionMetadata_Defaults()
    {
        var metadata = new DocumentIngestionMetadata
        {
            DocumentId = "doc-1",
            Repository = "repo",
            FilePath = "docs/test.md",
            Title = "Test"
        };

        metadata.DocumentId.ShouldBe("doc-1");
        metadata.Repository.ShouldBe("repo");
        metadata.FilePath.ShouldBe("docs/test.md");
        metadata.Title.ShouldBe("Test");
        metadata.PromotionLevel.ShouldBe("draft");
        metadata.DocType.ShouldBeNull();
        metadata.CommitHash.ShouldBeNull();
    }

    [Fact]
    public void DocumentIngestionMetadata_CustomValues()
    {
        var metadata = new DocumentIngestionMetadata
        {
            DocumentId = "doc-2",
            Repository = "repo2",
            FilePath = "specs/api.md",
            Title = "API Spec",
            DocType = "spec",
            PromotionLevel = "standard",
            CommitHash = "abc123"
        };

        metadata.DocType.ShouldBe("spec");
        metadata.PromotionLevel.ShouldBe("standard");
        metadata.CommitHash.ShouldBe("abc123");
    }
}
