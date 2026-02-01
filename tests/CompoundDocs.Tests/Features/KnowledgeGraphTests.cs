using CompoundDocs.Common.Graph;

namespace CompoundDocs.Tests.Features;

/// <summary>
/// Unit tests for knowledge graph traversal functionality.
/// Tests the enhanced DocumentLinkGraph with relationship types.
/// </summary>
public sealed class KnowledgeGraphTests
{
    private readonly DocumentLinkGraph _graph;

    public KnowledgeGraphTests()
    {
        _graph = new DocumentLinkGraph();
    }

    #region FindRelatedDocuments Tests

    [Fact]
    public void FindRelatedDocuments_WithTypedLinks_ReturnsRelationships()
    {
        // Arrange
        _graph.AddTypedLink("docs/main.md", "docs/child1.md", DocumentRelationshipType.Parent);
        _graph.AddTypedLink("docs/main.md", "docs/child2.md", DocumentRelationshipType.Parent);

        // Act
        var related = _graph.FindRelatedDocuments("docs/main.md", maxDepth: 1);

        // Assert
        related.Count.ShouldBe(2);
        related.ShouldAllBe(r => r.RelationshipType == DocumentRelationshipType.Parent);
    }

    [Fact]
    public void FindRelatedDocuments_WithDepth_TraversesMultipleLevels()
    {
        // Arrange - Create a chain: main -> child1 -> grandchild
        _graph.AddTypedLink("docs/main.md", "docs/child1.md", DocumentRelationshipType.Parent);
        _graph.AddTypedLink("docs/child1.md", "docs/grandchild.md", DocumentRelationshipType.Parent);

        // Act
        var related = _graph.FindRelatedDocuments("docs/main.md", maxDepth: 2);

        // Assert
        related.Count.ShouldBe(2);
        related.ShouldContain(r => r.TargetDocument == "docs/child1.md");
        related.ShouldContain(r => r.TargetDocument == "docs/grandchild.md");
    }

    [Fact]
    public void FindRelatedDocuments_WithMaxDocuments_LimitsResults()
    {
        // Arrange - Create multiple links
        _graph.AddTypedLink("docs/main.md", "docs/child1.md", DocumentRelationshipType.References);
        _graph.AddTypedLink("docs/main.md", "docs/child2.md", DocumentRelationshipType.References);
        _graph.AddTypedLink("docs/main.md", "docs/child3.md", DocumentRelationshipType.References);
        _graph.AddTypedLink("docs/main.md", "docs/child4.md", DocumentRelationshipType.References);

        // Act
        var related = _graph.FindRelatedDocuments("docs/main.md", maxDepth: 1, maxDocuments: 2);

        // Assert
        related.Count.ShouldBe(2);
    }

    [Fact]
    public void FindRelatedDocuments_WithRelationshipFilter_FiltersCorrectly()
    {
        // Arrange
        _graph.AddTypedLink("docs/main.md", "docs/parent.md", DocumentRelationshipType.Parent);
        _graph.AddTypedLink("docs/main.md", "docs/reference.md", DocumentRelationshipType.References);
        _graph.AddTypedLink("docs/main.md", "docs/dependency.md", DocumentRelationshipType.DependsOn);

        // Act - Filter to only References
        var related = _graph.FindRelatedDocuments(
            "docs/main.md",
            maxDepth: 1,
            relationshipTypes: new[] { DocumentRelationshipType.References });

        // Assert
        related.Count.ShouldBe(1);
        related[0].TargetDocument.ShouldBe("docs/reference.md");
        related[0].RelationshipType.ShouldBe(DocumentRelationshipType.References);
    }

    [Fact]
    public void FindRelatedDocuments_WithMultipleRelationshipFilters_FiltersCorrectly()
    {
        // Arrange
        _graph.AddTypedLink("docs/main.md", "docs/parent.md", DocumentRelationshipType.Parent);
        _graph.AddTypedLink("docs/main.md", "docs/reference.md", DocumentRelationshipType.References);
        _graph.AddTypedLink("docs/main.md", "docs/dependency.md", DocumentRelationshipType.DependsOn);
        _graph.AddTypedLink("docs/main.md", "docs/related.md", DocumentRelationshipType.Related);

        // Act - Filter to References and Related
        var related = _graph.FindRelatedDocuments(
            "docs/main.md",
            maxDepth: 1,
            relationshipTypes: new[] { DocumentRelationshipType.References, DocumentRelationshipType.Related });

        // Assert
        related.Count.ShouldBe(2);
        related.ShouldContain(r => r.TargetDocument == "docs/reference.md");
        related.ShouldContain(r => r.TargetDocument == "docs/related.md");
    }

    [Fact]
    public void FindRelatedDocuments_WithNonExistentDocument_ReturnsEmpty()
    {
        // Arrange - Empty graph

        // Act
        var related = _graph.FindRelatedDocuments("docs/nonexistent.md", maxDepth: 1);

        // Assert
        related.ShouldBeEmpty();
    }

    [Fact]
    public void FindRelatedDocuments_AvoidsCycles()
    {
        // Arrange - Create a cycle: a -> b -> c -> a
        _graph.AddTypedLink("docs/a.md", "docs/b.md", DocumentRelationshipType.References);
        _graph.AddTypedLink("docs/b.md", "docs/c.md", DocumentRelationshipType.References);
        _graph.AddTypedLink("docs/c.md", "docs/a.md", DocumentRelationshipType.References);

        // Act
        var related = _graph.FindRelatedDocuments("docs/a.md", maxDepth: 5);

        // Assert - Should return unique documents only
        related.Count.ShouldBe(2); // b and c, not a again
        related.ShouldContain(r => r.TargetDocument == "docs/b.md");
        related.ShouldContain(r => r.TargetDocument == "docs/c.md");
    }

    [Fact]
    public void FindRelatedDocuments_RespectsDepthLimit()
    {
        // Arrange - Create a chain: a -> b -> c -> d -> e
        _graph.AddTypedLink("docs/a.md", "docs/b.md", DocumentRelationshipType.References);
        _graph.AddTypedLink("docs/b.md", "docs/c.md", DocumentRelationshipType.References);
        _graph.AddTypedLink("docs/c.md", "docs/d.md", DocumentRelationshipType.References);
        _graph.AddTypedLink("docs/d.md", "docs/e.md", DocumentRelationshipType.References);

        // Act - Only go 2 levels deep
        var related = _graph.FindRelatedDocuments("docs/a.md", maxDepth: 2);

        // Assert
        related.Count.ShouldBe(2); // Only b and c
        related.ShouldContain(r => r.TargetDocument == "docs/b.md");
        related.ShouldContain(r => r.TargetDocument == "docs/c.md");
        related.ShouldNotContain(r => r.TargetDocument == "docs/d.md");
        related.ShouldNotContain(r => r.TargetDocument == "docs/e.md");
    }

    #endregion

    #region Hierarchy Traversal Tests

    [Fact]
    public void GetChildDocuments_ReturnsChildrenOnly()
    {
        // Arrange
        _graph.AddTypedLink("docs/parent.md", "docs/child1.md", DocumentRelationshipType.Parent);
        _graph.AddTypedLink("docs/parent.md", "docs/child2.md", DocumentRelationshipType.Parent);
        _graph.AddTypedLink("docs/parent.md", "docs/reference.md", DocumentRelationshipType.References);

        // Act
        var children = _graph.GetChildDocuments("docs/parent.md");

        // Assert
        children.Count.ShouldBe(2);
        children.ShouldContain("docs/child1.md");
        children.ShouldContain("docs/child2.md");
        children.ShouldNotContain("docs/reference.md");
    }

    [Fact]
    public void GetParentDocuments_ReturnsParentsOnly()
    {
        // Arrange
        _graph.AddTypedLink("docs/parent1.md", "docs/child.md", DocumentRelationshipType.Parent);
        _graph.AddTypedLink("docs/parent2.md", "docs/child.md", DocumentRelationshipType.Parent);
        _graph.AddTypedLink("docs/reference.md", "docs/child.md", DocumentRelationshipType.References);

        // Act
        var parents = _graph.GetParentDocuments("docs/child.md");

        // Assert
        parents.Count.ShouldBe(2);
        parents.ShouldContain("docs/parent1.md");
        parents.ShouldContain("docs/parent2.md");
        parents.ShouldNotContain("docs/reference.md");
    }

    #endregion

    #region Dependency Traversal Tests

    [Fact]
    public void GetDependencies_ReturnsDependenciesOnly()
    {
        // Arrange
        _graph.AddTypedLink("docs/main.md", "docs/lib1.md", DocumentRelationshipType.DependsOn);
        _graph.AddTypedLink("docs/main.md", "docs/lib2.md", DocumentRelationshipType.DependsOn);
        _graph.AddTypedLink("docs/main.md", "docs/related.md", DocumentRelationshipType.Related);

        // Act
        var dependencies = _graph.GetDependencies("docs/main.md");

        // Assert
        dependencies.Count.ShouldBe(2);
        dependencies.ShouldContain("docs/lib1.md");
        dependencies.ShouldContain("docs/lib2.md");
        dependencies.ShouldNotContain("docs/related.md");
    }

    [Fact]
    public void GetDependents_ReturnsDependentsOnly()
    {
        // Arrange
        _graph.AddTypedLink("docs/consumer1.md", "docs/library.md", DocumentRelationshipType.DependsOn);
        _graph.AddTypedLink("docs/consumer2.md", "docs/library.md", DocumentRelationshipType.DependsOn);
        _graph.AddTypedLink("docs/reference.md", "docs/library.md", DocumentRelationshipType.References);

        // Act
        var dependents = _graph.GetDependents("docs/library.md");

        // Assert
        dependents.Count.ShouldBe(2);
        dependents.ShouldContain("docs/consumer1.md");
        dependents.ShouldContain("docs/consumer2.md");
        dependents.ShouldNotContain("docs/reference.md");
    }

    #endregion

    #region Complex Graph Scenario Tests

    [Fact]
    public void KnowledgeGraph_ComplexStructure_TraversesCorrectly()
    {
        // Arrange - Build a complex knowledge graph
        // Architecture doc is parent of multiple specs
        _graph.AddTypedLink("docs/architecture.md", "docs/api-spec.md", DocumentRelationshipType.Parent);
        _graph.AddTypedLink("docs/architecture.md", "docs/data-spec.md", DocumentRelationshipType.Parent);

        // API spec references data spec
        _graph.AddTypedLink("docs/api-spec.md", "docs/data-spec.md", DocumentRelationshipType.References);

        // Implementation guide depends on specs
        _graph.AddTypedLink("docs/impl-guide.md", "docs/api-spec.md", DocumentRelationshipType.DependsOn);
        _graph.AddTypedLink("docs/impl-guide.md", "docs/data-spec.md", DocumentRelationshipType.DependsOn);

        // ADR supersedes old ADR
        _graph.AddTypedLink("docs/adr-002.md", "docs/adr-001.md", DocumentRelationshipType.Supersedes);

        // Act - Find all related from architecture
        var related = _graph.FindRelatedDocuments("docs/architecture.md", maxDepth: 2);

        // Assert
        related.Count.ShouldBeGreaterThan(0);
        related.ShouldContain(r => r.TargetDocument == "docs/api-spec.md");
        related.ShouldContain(r => r.TargetDocument == "docs/data-spec.md");
    }

    [Fact]
    public void KnowledgeGraph_GetRelationshipTypeCounts_ReturnsCorrectCounts()
    {
        // Arrange
        _graph.AddTypedLink("docs/a.md", "docs/b.md", DocumentRelationshipType.References);
        _graph.AddTypedLink("docs/a.md", "docs/c.md", DocumentRelationshipType.References);
        _graph.AddTypedLink("docs/a.md", "docs/d.md", DocumentRelationshipType.Parent);
        _graph.AddTypedLink("docs/e.md", "docs/f.md", DocumentRelationshipType.DependsOn);

        // Act
        var counts = _graph.GetRelationshipTypeCounts();

        // Assert
        counts[DocumentRelationshipType.References].ShouldBe(2);
        counts[DocumentRelationshipType.Parent].ShouldBe(1);
        counts[DocumentRelationshipType.DependsOn].ShouldBe(1);
    }

    #endregion
}
