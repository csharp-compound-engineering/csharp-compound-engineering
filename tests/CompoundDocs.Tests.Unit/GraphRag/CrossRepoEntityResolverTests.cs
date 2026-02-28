using CompoundDocs.Common.Models;
using CompoundDocs.Graph;
using CompoundDocs.GraphRag;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace CompoundDocs.Tests.Unit.GraphRag;

public sealed class CrossRepoEntityResolverTests
{
    #region Happy path

    [Fact]
    public async Task ResolveAsync_ConceptFoundWithRelatedConcepts_ReturnsResolvedEntity()
    {
        // Arrange
        var graphMock = new Mock<IGraphRepository>();
        var sut = new CrossRepoEntityResolver(
            graphMock.Object,
            NullLogger<CrossRepoEntityResolver>.Instance);

        var concept = new ConceptNode { Id = "concept:react", Name = "React", Description = "UI library", Category = "framework" };
        graphMock
            .Setup(g => g.FindConceptsByNameAsync("React", It.IsAny<CancellationToken>()))
            .ReturnsAsync([concept]);

        graphMock
            .Setup(g => g.GetRelatedConceptsAsync("concept:react", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ConceptNode { Id = "concept:jsx", Name = "JSX" },
                new ConceptNode { Id = "concept:hooks", Name = "Hooks" }
            ]);

        graphMock
            .Setup(g => g.GetChunksByConceptAsync("concept:react", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChunkNode { Id = "c1", SectionId = "s1", DocumentId = "frontend-repo:docs/react.md", Content = "content", Order = 0, TokenCount = 10 }
            ]);

        // Act
        var result = await sut.ResolveAsync("React");

        // Assert
        result.ShouldNotBeNull();
        result.ConceptId.ShouldBe("concept:react");
        result.Name.ShouldBe("React");
        result.Repository.ShouldBe("frontend-repo");
        result.RelatedConceptIds.ShouldBe(["concept:jsx", "concept:hooks"]);
        result.RelatedConceptNames.ShouldBe(["JSX", "Hooks"]);
    }

    #endregion

    #region Concept not found

    [Fact]
    public async Task ResolveAsync_ConceptNotFound_ReturnsNull()
    {
        // Arrange
        var graphMock = new Mock<IGraphRepository>();
        var sut = new CrossRepoEntityResolver(
            graphMock.Object,
            NullLogger<CrossRepoEntityResolver>.Instance);

        graphMock
            .Setup(g => g.FindConceptsByNameAsync("Unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await sut.ResolveAsync("Unknown");

        // Assert
        result.ShouldBeNull();

        graphMock.Verify(
            g => g.GetRelatedConceptsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Empty related concepts

    [Fact]
    public async Task ResolveAsync_NoRelatedConcepts_ReturnsEntityWithEmptyLists()
    {
        // Arrange
        var graphMock = new Mock<IGraphRepository>();
        var sut = new CrossRepoEntityResolver(
            graphMock.Object,
            NullLogger<CrossRepoEntityResolver>.Instance);

        var concept = new ConceptNode { Id = "concept:solo", Name = "Solo" };
        graphMock
            .Setup(g => g.FindConceptsByNameAsync("Solo", It.IsAny<CancellationToken>()))
            .ReturnsAsync([concept]);

        graphMock
            .Setup(g => g.GetRelatedConceptsAsync("concept:solo", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        graphMock
            .Setup(g => g.GetChunksByConceptAsync("concept:solo", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await sut.ResolveAsync("Solo");

        // Assert
        result.ShouldNotBeNull();
        result.ConceptId.ShouldBe("concept:solo");
        result.RelatedConceptIds.ShouldBeEmpty();
        result.RelatedConceptNames.ShouldBeEmpty();
        result.Repository.ShouldBeEmpty();
    }

    #endregion

    #region CancellationToken forwarding

    [Fact]
    public async Task ResolveAsync_ForwardsCancellationToken()
    {
        // Arrange
        var graphMock = new Mock<IGraphRepository>();
        var sut = new CrossRepoEntityResolver(
            graphMock.Object,
            NullLogger<CrossRepoEntityResolver>.Instance);

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var concept = new ConceptNode { Id = "concept:test", Name = "Test" };
        graphMock
            .Setup(g => g.FindConceptsByNameAsync("Test", token))
            .ReturnsAsync([concept]);

        graphMock
            .Setup(g => g.GetRelatedConceptsAsync("concept:test", 1, token))
            .ReturnsAsync([]);

        graphMock
            .Setup(g => g.GetChunksByConceptAsync("concept:test", token))
            .ReturnsAsync([]);

        // Act
        await sut.ResolveAsync("Test", token);

        // Assert
        graphMock.Verify(g => g.FindConceptsByNameAsync("Test", token), Times.Once);
        graphMock.Verify(g => g.GetRelatedConceptsAsync("concept:test", 1, token), Times.Once);
        graphMock.Verify(g => g.GetChunksByConceptAsync("concept:test", token), Times.Once);
    }

    #endregion

    #region DeriveRepository

    [Theory]
    [InlineData("my-repo:docs/file.md", "my-repo")]
    [InlineData("org-name:path/to/doc.md", "org-name")]
    [InlineData("no-colon-here", "")]
    [InlineData("", "")]
    public void DeriveRepository_ExtractsRepoFromDocumentId(string documentId, string expected)
    {
        var chunks = new List<ChunkNode>
        {
            new() { Id = "c1", SectionId = "s1", DocumentId = documentId, Content = "content", Order = 0, TokenCount = 5 }
        };

        var result = CrossRepoEntityResolver.DeriveRepository(chunks);

        result.ShouldBe(expected);
    }

    [Fact]
    public void DeriveRepository_EmptyChunks_ReturnsEmpty()
    {
        var result = CrossRepoEntityResolver.DeriveRepository([]);

        result.ShouldBeEmpty();
    }

    #endregion
}
