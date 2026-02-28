using Moq;
using CompoundDocs.Graph;
using CompoundDocs.Common.Models;

namespace CompoundDocs.Tests.Integration.Graph;

/// <summary>
/// Mock-based integration tests for IGraphRepository operations.
/// Validates document upsert and concept traversal contracts without requiring Neptune infrastructure.
/// </summary>
public class GraphRepositoryMockTests
{
    [Fact]
    public async Task CreateDocumentNode_WithMockedNeptune_Succeeds()
    {
        // Arrange
        var graphRepoMock = new Mock<IGraphRepository>(MockBehavior.Strict);

        var document = new DocumentNode
        {
            Id = "doc-graph-001",
            FilePath = "docs/architecture/overview.md",
            Title = "Architecture Overview",
            DocType = "architecture",
            PromotionLevel = "published",
            LastUpdated = DateTime.UtcNow,
            CommitHash = "abc123def456"
        };

        graphRepoMock
            .Setup(g => g.UpsertDocumentAsync(
                It.IsAny<DocumentNode>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var repo = graphRepoMock.Object;

        // Act
        await repo.UpsertDocumentAsync(document);

        // Assert
        graphRepoMock.Verify(
            g => g.UpsertDocumentAsync(
                It.Is<DocumentNode>(d =>
                    d.Id == "doc-graph-001" &&
                    d.FilePath == "docs/architecture/overview.md" &&
                    d.Title == "Architecture Overview" &&
                    d.DocType == "architecture" &&
                    d.PromotionLevel == "published" &&
                    d.CommitHash == "abc123def456"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRelatedConcepts_WithMockedNeptune_ReturnsNodes()
    {
        // Arrange
        var graphRepoMock = new Mock<IGraphRepository>(MockBehavior.Strict);

        var conceptId = "concept-di";
        var expectedConcepts = new List<ConceptNode>
        {
            new()
            {
                Id = "concept-ioc",
                Name = "Inversion of Control",
                Description = "A design principle for decoupling components",
                Category = "Design Pattern",
                Aliases = ["IoC"]
            },
            new()
            {
                Id = "concept-service-locator",
                Name = "Service Locator",
                Description = "A pattern for obtaining service references",
                Category = "Design Pattern",
                Aliases = ["ServiceLocator"]
            },
            new()
            {
                Id = "concept-composition-root",
                Name = "Composition Root",
                Description = "The single location where DI container is configured",
                Category = "Architecture",
                Aliases = []
            }
        };

        graphRepoMock
            .Setup(g => g.GetRelatedConceptsAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedConcepts)
            .Verifiable();

        var repo = graphRepoMock.Object;

        // Act
        var results = await repo.GetRelatedConceptsAsync(conceptId, hops: 2);

        // Assert
        results.ShouldNotBeNull();
        results.ShouldNotBeEmpty();
        results.Count.ShouldBe(3);

        results[0].Id.ShouldBe("concept-ioc");
        results[0].Name.ShouldBe("Inversion of Control");
        results[0].Description.ShouldNotBeNull();
        results[0].Category.ShouldBe("Design Pattern");
        results[0].Aliases.ShouldContain("IoC");

        results[1].Id.ShouldBe("concept-service-locator");
        results[1].Name.ShouldBe("Service Locator");

        results[2].Id.ShouldBe("concept-composition-root");
        results[2].Category.ShouldBe("Architecture");
        results[2].Aliases.ShouldBeEmpty();

        graphRepoMock.Verify(
            g => g.GetRelatedConceptsAsync(conceptId, 2, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
