using CompoundDocs.Common.Graph;

namespace CompoundDocs.Tests.Features;

/// <summary>
/// Unit tests for document relationship types and typed edges.
/// Tests the DocumentRelationshipType enum, TypedDocumentEdge, and DocumentRelationship classes.
/// </summary>
public sealed class DocumentRelationshipTests
{
    #region DocumentRelationshipType Enum Tests

    [Fact]
    public void DocumentRelationshipType_HasAllExpectedValues()
    {
        // Assert
        var values = Enum.GetValues<DocumentRelationshipType>();

        values.ShouldContain(DocumentRelationshipType.References);
        values.ShouldContain(DocumentRelationshipType.Parent);
        values.ShouldContain(DocumentRelationshipType.Child);
        values.ShouldContain(DocumentRelationshipType.Related);
        values.ShouldContain(DocumentRelationshipType.Supersedes);
        values.ShouldContain(DocumentRelationshipType.DependsOn);
    }

    [Theory]
    [InlineData("references", DocumentRelationshipType.References)]
    [InlineData("References", DocumentRelationshipType.References)]
    [InlineData("REFERENCES", DocumentRelationshipType.References)]
    [InlineData("parent", DocumentRelationshipType.Parent)]
    [InlineData("child", DocumentRelationshipType.Child)]
    [InlineData("related", DocumentRelationshipType.Related)]
    [InlineData("supersedes", DocumentRelationshipType.Supersedes)]
    [InlineData("dependson", DocumentRelationshipType.DependsOn)]
    public void DocumentRelationshipType_ParsesCaseInsensitively(string input, DocumentRelationshipType expected)
    {
        // Act
        var parsed = Enum.TryParse<DocumentRelationshipType>(input, ignoreCase: true, out var result);

        // Assert
        parsed.ShouldBeTrue();
        result.ShouldBe(expected);
    }

    [Fact]
    public void DocumentRelationshipType_References_IsDefaultValue()
    {
        // Assert
        ((int)DocumentRelationshipType.References).ShouldBe(0);
        default(DocumentRelationshipType).ShouldBe(DocumentRelationshipType.References);
    }

    #endregion

    #region TypedDocumentEdge Tests

    [Fact]
    public void TypedDocumentEdge_Constructor_SetsSourceAndTarget()
    {
        // Arrange & Act
        var edge = new TypedDocumentEdge("docs/source.md", "docs/target.md");

        // Assert
        edge.Source.ShouldBe("docs/source.md");
        edge.Target.ShouldBe("docs/target.md");
    }

    [Fact]
    public void TypedDocumentEdge_Constructor_DefaultsToReferences()
    {
        // Arrange & Act
        var edge = new TypedDocumentEdge("docs/source.md", "docs/target.md");

        // Assert
        edge.RelationshipType.ShouldBe(DocumentRelationshipType.References);
    }

    [Theory]
    [InlineData(DocumentRelationshipType.References)]
    [InlineData(DocumentRelationshipType.Parent)]
    [InlineData(DocumentRelationshipType.Child)]
    [InlineData(DocumentRelationshipType.Related)]
    [InlineData(DocumentRelationshipType.Supersedes)]
    [InlineData(DocumentRelationshipType.DependsOn)]
    public void TypedDocumentEdge_Constructor_SetsRelationshipType(DocumentRelationshipType type)
    {
        // Arrange & Act
        var edge = new TypedDocumentEdge("docs/source.md", "docs/target.md", type);

        // Assert
        edge.RelationshipType.ShouldBe(type);
    }

    [Fact]
    public void TypedDocumentEdge_InheritsFromEdge()
    {
        // Arrange & Act
        var edge = new TypedDocumentEdge("docs/a.md", "docs/b.md", DocumentRelationshipType.Parent);

        // Assert - Should inherit from QuikGraph Edge
        edge.ShouldBeAssignableTo<QuikGraph.Edge<string>>();
    }

    #endregion

    #region DocumentRelationship Tests

    [Fact]
    public void DocumentRelationship_RequiredProperties_MustBeSet()
    {
        // Arrange & Act
        var relationship = new DocumentRelationship
        {
            SourceDocument = "docs/source.md",
            TargetDocument = "docs/target.md",
            RelationshipType = DocumentRelationshipType.References
        };

        // Assert
        relationship.SourceDocument.ShouldBe("docs/source.md");
        relationship.TargetDocument.ShouldBe("docs/target.md");
        relationship.RelationshipType.ShouldBe(DocumentRelationshipType.References);
    }

    [Fact]
    public void DocumentRelationship_CanRepresentParentChild()
    {
        // Arrange & Act
        var relationship = new DocumentRelationship
        {
            SourceDocument = "docs/parent.md",
            TargetDocument = "docs/child.md",
            RelationshipType = DocumentRelationshipType.Parent
        };

        // Assert
        relationship.RelationshipType.ShouldBe(DocumentRelationshipType.Parent);
    }

    [Fact]
    public void DocumentRelationship_CanRepresentDependency()
    {
        // Arrange & Act
        var relationship = new DocumentRelationship
        {
            SourceDocument = "docs/consumer.md",
            TargetDocument = "docs/library.md",
            RelationshipType = DocumentRelationshipType.DependsOn
        };

        // Assert
        relationship.RelationshipType.ShouldBe(DocumentRelationshipType.DependsOn);
    }

    [Fact]
    public void DocumentRelationship_CanRepresentSupersedes()
    {
        // Arrange & Act
        var relationship = new DocumentRelationship
        {
            SourceDocument = "docs/adr-002.md",
            TargetDocument = "docs/adr-001.md",
            RelationshipType = DocumentRelationshipType.Supersedes
        };

        // Assert
        relationship.RelationshipType.ShouldBe(DocumentRelationshipType.Supersedes);
    }

    #endregion

    #region Graph Integration Tests

    [Fact]
    public void DocumentLinkGraph_AddTypedLink_AddsToTypedGraph()
    {
        // Arrange
        var graph = new DocumentLinkGraph();

        // Act
        graph.AddTypedLink("docs/a.md", "docs/b.md", DocumentRelationshipType.References);

        // Assert
        graph.TypedRelationshipCount.ShouldBe(1);
    }

    [Fact]
    public void DocumentLinkGraph_AddTypedLink_AlsoAddsToBothGraphs()
    {
        // Arrange
        var graph = new DocumentLinkGraph();

        // Act
        graph.AddTypedLink("docs/a.md", "docs/b.md", DocumentRelationshipType.Parent);

        // Assert - Should be in both typed and untyped graphs
        graph.TypedRelationshipCount.ShouldBe(1);
        graph.LinkCount.ShouldBe(1);
    }

    [Fact]
    public void DocumentLinkGraph_GetTypedRelationships_ReturnsAllRelationships()
    {
        // Arrange
        var graph = new DocumentLinkGraph();
        graph.AddTypedLink("docs/main.md", "docs/ref1.md", DocumentRelationshipType.References);
        graph.AddTypedLink("docs/main.md", "docs/child.md", DocumentRelationshipType.Parent);
        graph.AddTypedLink("docs/main.md", "docs/dep.md", DocumentRelationshipType.DependsOn);

        // Act
        var relationships = graph.GetTypedRelationships("docs/main.md");

        // Assert
        relationships.Count.ShouldBe(3);
        relationships.ShouldContain(r => r.RelationshipType == DocumentRelationshipType.References);
        relationships.ShouldContain(r => r.RelationshipType == DocumentRelationshipType.Parent);
        relationships.ShouldContain(r => r.RelationshipType == DocumentRelationshipType.DependsOn);
    }

    [Fact]
    public void DocumentLinkGraph_GetIncomingTypedRelationships_ReturnsIncoming()
    {
        // Arrange
        var graph = new DocumentLinkGraph();
        graph.AddTypedLink("docs/source1.md", "docs/target.md", DocumentRelationshipType.References);
        graph.AddTypedLink("docs/source2.md", "docs/target.md", DocumentRelationshipType.Parent);

        // Act
        var incoming = graph.GetIncomingTypedRelationships("docs/target.md");

        // Assert
        incoming.Count.ShouldBe(2);
        incoming.ShouldAllBe(r => r.TargetDocument == "docs/target.md");
    }

    [Fact]
    public void DocumentLinkGraph_GetDocumentsByRelationshipType_FiltersCorrectly()
    {
        // Arrange
        var graph = new DocumentLinkGraph();
        graph.AddTypedLink("docs/main.md", "docs/ref.md", DocumentRelationshipType.References);
        graph.AddTypedLink("docs/main.md", "docs/related.md", DocumentRelationshipType.Related);
        graph.AddTypedLink("docs/main.md", "docs/parent.md", DocumentRelationshipType.Parent);

        // Act
        var references = graph.GetDocumentsByRelationshipType("docs/main.md", DocumentRelationshipType.References);

        // Assert
        references.Count.ShouldBe(1);
        references.ShouldContain("docs/ref.md");
    }

    [Fact]
    public void DocumentLinkGraph_ClearTypedLinksFrom_RemovesTypedLinks()
    {
        // Arrange
        var graph = new DocumentLinkGraph();
        graph.AddTypedLink("docs/main.md", "docs/a.md", DocumentRelationshipType.References);
        graph.AddTypedLink("docs/main.md", "docs/b.md", DocumentRelationshipType.Parent);
        graph.AddTypedLink("docs/other.md", "docs/c.md", DocumentRelationshipType.References);

        // Act
        graph.ClearTypedLinksFrom("docs/main.md");

        // Assert
        var mainRelationships = graph.GetTypedRelationships("docs/main.md");
        mainRelationships.ShouldBeEmpty();

        // Other document's links should remain
        var otherRelationships = graph.GetTypedRelationships("docs/other.md");
        otherRelationships.Count.ShouldBe(1);
    }

    [Fact]
    public void DocumentLinkGraph_TypedRelationshipCount_ReturnsCorrectCount()
    {
        // Arrange
        var graph = new DocumentLinkGraph();

        // Act
        graph.AddTypedLink("docs/a.md", "docs/b.md", DocumentRelationshipType.References);
        graph.AddTypedLink("docs/b.md", "docs/c.md", DocumentRelationshipType.Parent);
        graph.AddTypedLink("docs/c.md", "docs/d.md", DocumentRelationshipType.DependsOn);

        // Assert
        graph.TypedRelationshipCount.ShouldBe(3);
    }

    #endregion

    #region Relationship Semantics Tests

    [Fact]
    public void Parent_Relationship_ImpliesHierarchy()
    {
        // Arrange
        var graph = new DocumentLinkGraph();
        graph.AddTypedLink("docs/architecture.md", "docs/api-spec.md", DocumentRelationshipType.Parent);
        graph.AddTypedLink("docs/architecture.md", "docs/db-spec.md", DocumentRelationshipType.Parent);

        // Act
        var children = graph.GetChildDocuments("docs/architecture.md");

        // Assert - Parent relationship means source is parent, targets are children
        children.Count.ShouldBe(2);
        children.ShouldContain("docs/api-spec.md");
        children.ShouldContain("docs/db-spec.md");
    }

    [Fact]
    public void DependsOn_Relationship_ImpliesDependency()
    {
        // Arrange
        var graph = new DocumentLinkGraph();
        graph.AddTypedLink("docs/impl.md", "docs/spec.md", DocumentRelationshipType.DependsOn);
        graph.AddTypedLink("docs/test.md", "docs/spec.md", DocumentRelationshipType.DependsOn);

        // Act
        var dependents = graph.GetDependents("docs/spec.md");

        // Assert - Multiple documents depend on the spec
        dependents.Count.ShouldBe(2);
        dependents.ShouldContain("docs/impl.md");
        dependents.ShouldContain("docs/test.md");
    }

    [Fact]
    public void Supersedes_Relationship_ImpliesVersioning()
    {
        // Arrange
        var graph = new DocumentLinkGraph();
        graph.AddTypedLink("docs/adr-003.md", "docs/adr-002.md", DocumentRelationshipType.Supersedes);
        graph.AddTypedLink("docs/adr-002.md", "docs/adr-001.md", DocumentRelationshipType.Supersedes);

        // Act
        var superseded = graph.GetDocumentsByRelationshipType("docs/adr-003.md", DocumentRelationshipType.Supersedes);

        // Assert
        superseded.Count.ShouldBe(1);
        superseded.ShouldContain("docs/adr-002.md");
    }

    #endregion
}
