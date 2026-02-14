using CompoundDocs.GraphRag;

namespace CompoundDocs.Tests.Unit.GraphRag;

public sealed class EntityExtractorModelsTests
{
    [Fact]
    public void ExtractedEntity_DefaultAliasesIsEmptyList()
    {
        var entity = new ExtractedEntity
        {
            Name = "React",
            Type = "Framework"
        };

        entity.Aliases.ShouldNotBeNull();
        entity.Aliases.ShouldBeEmpty();
        entity.Description.ShouldBeNull();
    }

    [Fact]
    public void ExtractedEntity_WithAllProperties()
    {
        var entity = new ExtractedEntity
        {
            Name = "React",
            Type = "Framework",
            Description = "UI library",
            Aliases = ["ReactJS", "React.js"]
        };

        entity.Name.ShouldBe("React");
        entity.Type.ShouldBe("Framework");
        entity.Description.ShouldBe("UI library");
        entity.Aliases.Count.ShouldBe(2);
    }
}
