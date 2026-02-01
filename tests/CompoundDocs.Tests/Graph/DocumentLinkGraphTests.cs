using CompoundDocs.Common.Graph;

namespace CompoundDocs.Tests.Graph;

/// <summary>
/// Unit tests for DocumentLinkGraph.
/// </summary>
public sealed class DocumentLinkGraphTests
{
    private readonly DocumentLinkGraph _sut;

    public DocumentLinkGraphTests()
    {
        _sut = new DocumentLinkGraph();
    }

    #region AddDocument Tests

    [Fact]
    public void AddDocument_WithNewDocument_IncreasesDocumentCount()
    {
        // Arrange
        var initialCount = _sut.DocumentCount;

        // Act
        _sut.AddDocument("docs/readme.md");

        // Assert
        _sut.DocumentCount.ShouldBe(initialCount + 1);
    }

    [Fact]
    public void AddDocument_WithDuplicateDocument_DoesNotDuplicate()
    {
        // Arrange
        _sut.AddDocument("docs/readme.md");
        var countAfterFirst = _sut.DocumentCount;

        // Act
        _sut.AddDocument("docs/readme.md");

        // Assert
        _sut.DocumentCount.ShouldBe(countAfterFirst);
    }

    [Fact]
    public void AddDocument_WithMultipleDocuments_TracksAll()
    {
        // Arrange & Act
        _sut.AddDocument("docs/one.md");
        _sut.AddDocument("docs/two.md");
        _sut.AddDocument("docs/three.md");

        // Assert
        _sut.DocumentCount.ShouldBe(3);
    }

    #endregion

    #region AddLink Tests

    [Fact]
    public void AddLink_WithNewLink_IncreasesLinkCount()
    {
        // Arrange
        _sut.AddDocument("docs/source.md");
        _sut.AddDocument("docs/target.md");
        var initialCount = _sut.LinkCount;

        // Act
        _sut.AddLink("docs/source.md", "docs/target.md");

        // Assert
        _sut.LinkCount.ShouldBe(initialCount + 1);
    }

    [Fact]
    public void AddLink_AutomaticallyAddsVertices()
    {
        // Arrange
        var initialCount = _sut.DocumentCount;

        // Act
        _sut.AddLink("docs/new-source.md", "docs/new-target.md");

        // Assert
        _sut.DocumentCount.ShouldBe(initialCount + 2);
        _sut.LinkCount.ShouldBe(1);
    }

    [Fact]
    public void AddLink_WithSelfReference_CreatesLink()
    {
        // Arrange
        _sut.AddDocument("docs/self.md");

        // Act
        _sut.AddLink("docs/self.md", "docs/self.md");

        // Assert
        _sut.LinkCount.ShouldBe(1);
        var links = _sut.GetLinkedDocuments("docs/self.md");
        links.ShouldContain("docs/self.md");
    }

    [Fact]
    public void AddLink_WithMultipleLinks_TracksAll()
    {
        // Arrange & Act
        _sut.AddLink("docs/a.md", "docs/b.md");
        _sut.AddLink("docs/a.md", "docs/c.md");
        _sut.AddLink("docs/b.md", "docs/c.md");

        // Assert
        _sut.LinkCount.ShouldBe(3);
    }

    #endregion

    #region GetLinkedDocuments Tests

    [Fact]
    public void GetLinkedDocuments_WithExistingLinks_ReturnsTargets()
    {
        // Arrange
        _sut.AddLink("docs/source.md", "docs/target1.md");
        _sut.AddLink("docs/source.md", "docs/target2.md");

        // Act
        var links = _sut.GetLinkedDocuments("docs/source.md");

        // Assert
        links.ShouldNotBeNull();
        links.Count.ShouldBe(2);
        links.ShouldContain("docs/target1.md");
        links.ShouldContain("docs/target2.md");
    }

    [Fact]
    public void GetLinkedDocuments_WithNoOutgoingLinks_ReturnsEmptyList()
    {
        // Arrange
        _sut.AddDocument("docs/isolated.md");

        // Act
        var links = _sut.GetLinkedDocuments("docs/isolated.md");

        // Assert
        links.ShouldNotBeNull();
        links.Count.ShouldBe(0);
    }

    [Fact]
    public void GetLinkedDocuments_WithNonExistentDocument_ReturnsEmptyList()
    {
        // Act
        var links = _sut.GetLinkedDocuments("docs/nonexistent.md");

        // Assert
        links.ShouldNotBeNull();
        links.Count.ShouldBe(0);
    }

    [Fact]
    public void GetLinkedDocuments_DoesNotReturnIncomingLinks()
    {
        // Arrange
        _sut.AddLink("docs/source.md", "docs/target.md");
        _sut.AddLink("docs/other.md", "docs/target.md");

        // Act
        var links = _sut.GetLinkedDocuments("docs/target.md");

        // Assert
        links.Count.ShouldBe(0); // target.md has no outgoing links
    }

    #endregion

    #region GetIncomingLinks Tests

    [Fact]
    public void GetIncomingLinks_ReturnsSourcesLinkingToDocument()
    {
        // Arrange
        _sut.AddLink("docs/source1.md", "docs/target.md");
        _sut.AddLink("docs/source2.md", "docs/target.md");

        // Act
        var incoming = _sut.GetIncomingLinks("docs/target.md");

        // Assert
        incoming.Count.ShouldBe(2);
        incoming.ShouldContain("docs/source1.md");
        incoming.ShouldContain("docs/source2.md");
    }

    #endregion

    #region DetectCycle / FindCycle Tests

    [Fact]
    public void IsAcyclic_WithNoCycles_ReturnsTrue()
    {
        // Arrange - Linear chain: a -> b -> c
        _sut.AddLink("docs/a.md", "docs/b.md");
        _sut.AddLink("docs/b.md", "docs/c.md");

        // Act
        var isAcyclic = _sut.IsAcyclic();

        // Assert
        isAcyclic.ShouldBeTrue();
    }

    [Fact]
    public void IsAcyclic_WithCycle_ReturnsFalse()
    {
        // Arrange - Cycle: a -> b -> c -> a
        _sut.AddLink("docs/a.md", "docs/b.md");
        _sut.AddLink("docs/b.md", "docs/c.md");
        _sut.AddLink("docs/c.md", "docs/a.md");

        // Act
        var isAcyclic = _sut.IsAcyclic();

        // Assert
        isAcyclic.ShouldBeFalse();
    }

    [Fact]
    public void FindCycle_WithCycle_ReturnsCyclePath()
    {
        // Arrange - Cycle: a -> b -> c -> a
        _sut.AddLink("docs/a.md", "docs/b.md");
        _sut.AddLink("docs/b.md", "docs/c.md");
        _sut.AddLink("docs/c.md", "docs/a.md");

        // Act
        var cycle = _sut.FindCycle("docs/a.md");

        // Assert
        cycle.ShouldNotBeNull();
        cycle.Count.ShouldBeGreaterThan(1);
        // The cycle should contain all three nodes
        cycle.ShouldContain("docs/a.md");
    }

    [Fact]
    public void FindCycle_WithNoCycle_ReturnsNull()
    {
        // Arrange - Linear chain: a -> b -> c
        _sut.AddLink("docs/a.md", "docs/b.md");
        _sut.AddLink("docs/b.md", "docs/c.md");

        // Act
        var cycle = _sut.FindCycle("docs/a.md");

        // Assert
        cycle.ShouldBeNull();
    }

    [Fact]
    public void FindCycle_WithSelfLoop_DetectsCycle()
    {
        // Arrange
        _sut.AddLink("docs/self.md", "docs/self.md");

        // Act
        var cycle = _sut.FindCycle("docs/self.md");

        // Assert
        cycle.ShouldNotBeNull();
        cycle.ShouldContain("docs/self.md");
    }

    [Fact]
    public void FindCycle_WithNonExistentDocument_ReturnsNull()
    {
        // Act
        var cycle = _sut.FindCycle("docs/nonexistent.md");

        // Assert
        cycle.ShouldBeNull();
    }

    #endregion

    #region RemoveDocument Tests

    [Fact]
    public void RemoveDocument_DecreasesDocumentCount()
    {
        // Arrange
        _sut.AddDocument("docs/toremove.md");
        var countBefore = _sut.DocumentCount;

        // Act
        _sut.RemoveDocument("docs/toremove.md");

        // Assert
        _sut.DocumentCount.ShouldBe(countBefore - 1);
    }

    [Fact]
    public void RemoveDocument_AlsoRemovesAssociatedLinks()
    {
        // Arrange
        _sut.AddLink("docs/source.md", "docs/target.md");
        _sut.AddLink("docs/source.md", "docs/other.md");

        // Act
        _sut.RemoveDocument("docs/source.md");

        // Assert
        _sut.LinkCount.ShouldBe(0);
    }

    #endregion

    #region ClearLinksFrom Tests

    [Fact]
    public void ClearLinksFrom_RemovesOutgoingLinks()
    {
        // Arrange
        _sut.AddLink("docs/source.md", "docs/target1.md");
        _sut.AddLink("docs/source.md", "docs/target2.md");
        _sut.AddLink("docs/other.md", "docs/target1.md");

        // Act
        _sut.ClearLinksFrom("docs/source.md");

        // Assert
        var links = _sut.GetLinkedDocuments("docs/source.md");
        links.Count.ShouldBe(0);

        // Other links should remain
        _sut.LinkCount.ShouldBe(1);
    }

    [Fact]
    public void ClearLinksFrom_KeepsDocumentVertex()
    {
        // Arrange
        _sut.AddLink("docs/source.md", "docs/target.md");
        var countBefore = _sut.DocumentCount;

        // Act
        _sut.ClearLinksFrom("docs/source.md");

        // Assert
        _sut.DocumentCount.ShouldBe(countBefore);
    }

    #endregion

    #region GetLinkedDocumentsWithDepth Tests

    [Fact]
    public void GetLinkedDocumentsWithDepth_ReturnsTransitiveLinks()
    {
        // Arrange - Chain: a -> b -> c -> d
        _sut.AddLink("docs/a.md", "docs/b.md");
        _sut.AddLink("docs/b.md", "docs/c.md");
        _sut.AddLink("docs/c.md", "docs/d.md");

        // Act
        var links = _sut.GetLinkedDocumentsWithDepth("docs/a.md", maxDepth: 3);

        // Assert
        links.Count.ShouldBe(3);
        links.ShouldContain("docs/b.md");
        links.ShouldContain("docs/c.md");
        links.ShouldContain("docs/d.md");
    }

    [Fact]
    public void GetLinkedDocumentsWithDepth_RespectsMaxDepth()
    {
        // Arrange - Chain: a -> b -> c -> d
        _sut.AddLink("docs/a.md", "docs/b.md");
        _sut.AddLink("docs/b.md", "docs/c.md");
        _sut.AddLink("docs/c.md", "docs/d.md");

        // Act
        var links = _sut.GetLinkedDocumentsWithDepth("docs/a.md", maxDepth: 1);

        // Assert
        links.Count.ShouldBe(1);
        links.ShouldContain("docs/b.md");
    }

    [Fact]
    public void GetLinkedDocumentsWithDepth_RespectsMaxDocuments()
    {
        // Arrange - Star pattern: center -> 1, 2, 3, 4, 5
        _sut.AddLink("docs/center.md", "docs/1.md");
        _sut.AddLink("docs/center.md", "docs/2.md");
        _sut.AddLink("docs/center.md", "docs/3.md");
        _sut.AddLink("docs/center.md", "docs/4.md");
        _sut.AddLink("docs/center.md", "docs/5.md");

        // Act
        var links = _sut.GetLinkedDocumentsWithDepth("docs/center.md", maxDepth: 1, maxDocuments: 2);

        // Assert
        links.Count.ShouldBe(2);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ConcurrentAddDocument_DoesNotThrow()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() => _sut.AddDocument($"docs/doc{index}.md")));
        }

        await Task.WhenAll(tasks);

        // Assert
        _sut.DocumentCount.ShouldBe(100);
    }

    [Fact]
    public async Task ConcurrentAddLink_DoesNotThrow()
    {
        // Arrange
        _sut.AddDocument("docs/source.md");
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() => _sut.AddLink("docs/source.md", $"docs/target{index}.md")));
        }

        await Task.WhenAll(tasks);

        // Assert - Should not throw and all links should be added
        _sut.LinkCount.ShouldBe(100);
    }

    #endregion
}
