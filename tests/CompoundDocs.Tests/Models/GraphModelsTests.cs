using CompoundDocs.Common.Models;

namespace CompoundDocs.Tests.Models;

public sealed class GraphModelsTests
{
    [Fact]
    public void DocumentNode_RequiredPropertiesAndDefaults()
    {
        var node = new DocumentNode
        {
            Id = "doc-1",
            FilePath = "docs/test.md",
            Title = "Test"
        };

        node.Id.ShouldBe("doc-1");
        node.FilePath.ShouldBe("docs/test.md");
        node.Title.ShouldBe("Test");
        node.PromotionLevel.ShouldBe("draft");
        node.DocType.ShouldBeNull();
        node.CommitHash.ShouldBeNull();
    }

    [Fact]
    public void SectionNode_RequiredPropertiesAndDefaults()
    {
        var node = new SectionNode
        {
            Id = "sec-1",
            DocumentId = "doc-1",
            Title = "Section"
        };

        node.Id.ShouldBe("sec-1");
        node.DocumentId.ShouldBe("doc-1");
        node.Title.ShouldBe("Section");
        node.Order.ShouldBe(0);
        node.HeadingLevel.ShouldBe(0);
    }

    [Fact]
    public void ChunkNode_RequiredPropertiesAndDefaults()
    {
        var node = new ChunkNode
        {
            Id = "chunk-1",
            SectionId = "sec-1",
            DocumentId = "doc-1",
            Content = "hello world"
        };

        node.Id.ShouldBe("chunk-1");
        node.SectionId.ShouldBe("sec-1");
        node.DocumentId.ShouldBe("doc-1");
        node.Content.ShouldBe("hello world");
        node.Order.ShouldBe(0);
        node.TokenCount.ShouldBe(0);
    }

    [Fact]
    public void ConceptNode_DefaultAliasesIsEmptyList()
    {
        var node = new ConceptNode
        {
            Id = "concept-1",
            Name = "React"
        };

        node.Aliases.ShouldNotBeNull();
        node.Aliases.ShouldBeEmpty();
        node.Description.ShouldBeNull();
        node.Category.ShouldBeNull();
    }

    [Fact]
    public void ConceptNode_WithAliases()
    {
        var node = new ConceptNode
        {
            Id = "concept-1",
            Name = "React",
            Aliases = ["ReactJS", "React.js"]
        };

        node.Aliases.Count.ShouldBe(2);
        node.Aliases.ShouldContain("ReactJS");
    }

    [Fact]
    public void CodeExampleNode_RequiredProperties()
    {
        var node = new CodeExampleNode
        {
            Id = "code-1",
            ChunkId = "chunk-1",
            Language = "csharp",
            Code = "Console.WriteLine(\"Hello\");"
        };

        node.Id.ShouldBe("code-1");
        node.ChunkId.ShouldBe("chunk-1");
        node.Language.ShouldBe("csharp");
        node.Code.ShouldBe("Console.WriteLine(\"Hello\");");
        node.Description.ShouldBeNull();
    }

    [Fact]
    public void GraphRelationship_DefaultPropertiesIsEmpty()
    {
        var rel = new GraphRelationship
        {
            Type = "LINKS_TO",
            SourceId = "doc-1",
            TargetId = "doc-2"
        };

        rel.Properties.ShouldNotBeNull();
        rel.Properties.ShouldBeEmpty();
    }

    [Fact]
    public void GraphRelationship_WithProperties()
    {
        var rel = new GraphRelationship
        {
            Type = "RELATES_TO",
            SourceId = "concept-1",
            TargetId = "concept-2",
            Properties = new Dictionary<string, object> { ["weight"] = 0.85 }
        };

        rel.Properties.Count.ShouldBe(1);
        rel.Properties["weight"].ShouldBe(0.85);
    }
}
