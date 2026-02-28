using CompoundDocs.Common.Models;
using CompoundDocs.Graph;
using CompoundDocs.GraphRag;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace CompoundDocs.Tests.Unit.GraphRag;

public sealed class CrossRepoEntityResolverTests
{
    private readonly Mock<IGraphRepository> _graphMock = new();
    private readonly CrossRepoEntityResolver _sut;

    public CrossRepoEntityResolverTests()
    {
        _sut = new CrossRepoEntityResolver(
            _graphMock.Object,
            NullLogger<CrossRepoEntityResolver>.Instance);
    }

    #region Happy path

    [Fact]
    public async Task ResolveAsync_ConceptFoundWithRelatedConcepts_ReturnsResolvedEntity()
    {
        // Arrange
        var concept = new ConceptNode { Id = "concept:react", Name = "React", Description = "UI library", Category = "framework" };
        _graphMock
            .Setup(g => g.FindConceptsByNameAsync("React", It.IsAny<CancellationToken>()))
            .ReturnsAsync([concept]);

        _graphMock
            .Setup(g => g.GetRelatedConceptsAsync("concept:react", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ConceptNode { Id = "concept:jsx", Name = "JSX" },
                new ConceptNode { Id = "concept:hooks", Name = "Hooks" }
            ]);

        _graphMock
            .Setup(g => g.GetChunksByConceptAsync("concept:react", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChunkNode { Id = "c1", SectionId = "s1", DocumentId = "frontend-repo:docs/react.md", Content = "content", Order = 0, TokenCount = 10 }
            ]);

        // Act
        var result = await _sut.ResolveAsync("React");

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
        _graphMock
            .Setup(g => g.FindConceptsByNameAsync("Unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _sut.ResolveAsync("Unknown");

        // Assert
        result.ShouldBeNull();

        _graphMock.Verify(
            g => g.GetRelatedConceptsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Empty related concepts

    [Fact]
    public async Task ResolveAsync_NoRelatedConcepts_ReturnsEntityWithEmptyLists()
    {
        // Arrange
        var concept = new ConceptNode { Id = "concept:solo", Name = "Solo" };
        _graphMock
            .Setup(g => g.FindConceptsByNameAsync("Solo", It.IsAny<CancellationToken>()))
            .ReturnsAsync([concept]);

        _graphMock
            .Setup(g => g.GetRelatedConceptsAsync("concept:solo", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _graphMock
            .Setup(g => g.GetChunksByConceptAsync("concept:solo", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _sut.ResolveAsync("Solo");

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
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var concept = new ConceptNode { Id = "concept:test", Name = "Test" };
        _graphMock
            .Setup(g => g.FindConceptsByNameAsync("Test", token))
            .ReturnsAsync([concept]);

        _graphMock
            .Setup(g => g.GetRelatedConceptsAsync("concept:test", 1, token))
            .ReturnsAsync([]);

        _graphMock
            .Setup(g => g.GetChunksByConceptAsync("concept:test", token))
            .ReturnsAsync([]);

        // Act
        await _sut.ResolveAsync("Test", token);

        // Assert
        _graphMock.Verify(g => g.FindConceptsByNameAsync("Test", token), Times.Once);
        _graphMock.Verify(g => g.GetRelatedConceptsAsync("concept:test", 1, token), Times.Once);
        _graphMock.Verify(g => g.GetChunksByConceptAsync("concept:test", token), Times.Once);
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
