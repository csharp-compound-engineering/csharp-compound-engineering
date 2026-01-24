using CompoundDocs.GraphRag;

namespace CompoundDocs.Tests.GraphRag;

public sealed class CrossRepoEntityResolverModelsTests
{
    [Fact]
    public void ResolvedEntity_DefaultRelatedConceptIdsIsEmptyList()
    {
        var entity = new ResolvedEntity
        {
            ConceptId = "concept-1",
            Name = "React",
            Repository = "repo1"
        };

        entity.RelatedConceptIds.ShouldNotBeNull();
        entity.RelatedConceptIds.ShouldBeEmpty();
    }

    [Fact]
    public void ResolvedEntity_WithRelatedConceptIds()
    {
        var entity = new ResolvedEntity
        {
            ConceptId = "concept-1",
            Name = "React",
            Repository = "repo1",
            RelatedConceptIds = ["concept-2", "concept-3"]
        };

        entity.ConceptId.ShouldBe("concept-1");
        entity.Name.ShouldBe("React");
        entity.Repository.ShouldBe("repo1");
        entity.RelatedConceptIds.Count.ShouldBe(2);
    }
}
