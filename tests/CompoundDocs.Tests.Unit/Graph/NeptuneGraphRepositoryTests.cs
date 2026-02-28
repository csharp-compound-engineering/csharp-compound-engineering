using System.Text.Json;
using CompoundDocs.Common.Models;
using CompoundDocs.Graph;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace CompoundDocs.Tests.Unit.Graph;

public class NeptuneGraphRepositoryTests
{
    #region UpsertDocumentAsync

    [Fact]
    public async Task UpsertDocumentAsync_CallsExecuteOpenCypherAsync_WithCorrectParameters()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("{}").RootElement.Clone());

        var document = new DocumentNode
        {
            Id = "doc-1",
            FilePath = "/docs/guide.md",
            Title = "Getting Started",
            DocType = "guide",
            PromotionLevel = "published",
            LastUpdated = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            CommitHash = "abc123"
        };

        // Act
        await sut.UpsertDocumentAsync(document);

        // Assert
        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.Is<string>(q => q.Contains("MERGE (d:Document {id: $id})")),
            It.Is<Dictionary<string, object>?>(p =>
                p != null &&
                (string)p["id"] == "doc-1" &&
                (string)p["filePath"] == "/docs/guide.md" &&
                (string)p["title"] == "Getting Started" &&
                (string)p["docType"] == "guide" &&
                (string)p["promotionLevel"] == "published" &&
                (string)p["commitHash"] == "abc123"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpsertDocumentAsync_NullOptionalFields_PassesEmptyStrings()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("{}").RootElement.Clone());

        var document = new DocumentNode
        {
            Id = "doc-2",
            FilePath = "/docs/readme.md",
            Title = "Readme",
            DocType = null,
            CommitHash = null
        };

        // Act
        await sut.UpsertDocumentAsync(document);

        // Assert
        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.IsAny<string>(),
            It.Is<Dictionary<string, object>?>(p =>
                p != null &&
                (string)p["docType"] == string.Empty &&
                (string)p["commitHash"] == string.Empty),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region UpsertSectionAsync

    [Fact]
    public async Task UpsertSectionAsync_CallsExecuteOpenCypherAsync_WithCorrectParameters()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("{}").RootElement.Clone());

        var section = new SectionNode
        {
            Id = "sec-1",
            DocumentId = "doc-1",
            Title = "Introduction",
            Order = 1,
            HeadingLevel = 2
        };

        // Act
        await sut.UpsertSectionAsync(section);

        // Assert
        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.Is<string>(q =>
                q.Contains("MERGE (s:Section {id: $id})") &&
                q.Contains("MERGE (d)-[:HAS_SECTION]->(s)")),
            It.Is<Dictionary<string, object>?>(p =>
                p != null &&
                (string)p["id"] == "sec-1" &&
                (string)p["documentId"] == "doc-1" &&
                (string)p["title"] == "Introduction" &&
                (int)p["order"] == 1 &&
                (int)p["headingLevel"] == 2),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region UpsertChunkAsync

    [Fact]
    public async Task UpsertChunkAsync_CallsExecuteOpenCypherAsync_WithCorrectParameters()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("{}").RootElement.Clone());

        var chunk = new ChunkNode
        {
            Id = "chunk-1",
            SectionId = "sec-1",
            DocumentId = "doc-1",
            Content = "This is a test chunk.",
            Order = 3,
            TokenCount = 42
        };

        // Act
        await sut.UpsertChunkAsync(chunk);

        // Assert
        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.Is<string>(q =>
                q.Contains("MERGE (c:Chunk {id: $id})") &&
                q.Contains("MERGE (s)-[:HAS_CHUNK]->(c)")),
            It.Is<Dictionary<string, object>?>(p =>
                p != null &&
                (string)p["id"] == "chunk-1" &&
                (string)p["sectionId"] == "sec-1" &&
                (string)p["documentId"] == "doc-1" &&
                (string)p["content"] == "This is a test chunk." &&
                (int)p["order"] == 3 &&
                (int)p["tokenCount"] == 42),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region UpsertConceptAsync

    [Fact]
    public async Task UpsertConceptAsync_SerializesAliasesToJson()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("{}").RootElement.Clone());

        var concept = new ConceptNode
        {
            Id = "concept-1",
            Name = "Dependency Injection",
            Description = "A design pattern",
            Category = "patterns",
            Aliases = ["DI", "IoC"]
        };

        // Act
        await sut.UpsertConceptAsync(concept);

        // Assert
        var expectedAliasesJson = JsonSerializer.Serialize(new List<string> { "DI", "IoC" });
        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.Is<string>(q => q.Contains("MERGE (c:Concept {id: $id})")),
            It.Is<Dictionary<string, object>?>(p =>
                p != null &&
                (string)p["id"] == "concept-1" &&
                (string)p["name"] == "Dependency Injection" &&
                (string)p["description"] == "A design pattern" &&
                (string)p["category"] == "patterns" &&
                (string)p["aliases"] == expectedAliasesJson),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpsertConceptAsync_NullOptionalFields_PassesEmptyStrings()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("{}").RootElement.Clone());

        var concept = new ConceptNode
        {
            Id = "concept-2",
            Name = "Testing",
            Description = null,
            Category = null
        };

        // Act
        await sut.UpsertConceptAsync(concept);

        // Assert
        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.IsAny<string>(),
            It.Is<Dictionary<string, object>?>(p =>
                p != null &&
                (string)p["description"] == string.Empty &&
                (string)p["category"] == string.Empty),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region CreateRelationshipAsync

    [Fact]
    public async Task CreateRelationshipAsync_UsesRelationshipTypeInQuery()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("{}").RootElement.Clone());

        var relationship = new GraphRelationship
        {
            Type = "MENTIONS",
            SourceId = "chunk-1",
            TargetId = "concept-1",
            Properties = new Dictionary<string, object> { ["weight"] = 0.95 }
        };

        // Act
        await sut.CreateRelationshipAsync(relationship);

        // Assert
        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.Is<string>(q => q.Contains("MERGE (a)-[r:MENTIONS]->(b)")),
            It.Is<Dictionary<string, object>?>(p =>
                p != null &&
                (string)p["sourceId"] == "chunk-1" &&
                (string)p["targetId"] == "concept-1"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region DeleteDocumentCascadeAsync

    [Fact]
    public async Task DeleteDocumentCascadeAsync_PassesDocumentId()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("{}").RootElement.Clone());

        // Act
        await sut.DeleteDocumentCascadeAsync("doc-to-delete");

        // Assert
        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.Is<string>(q =>
                q.Contains("MATCH (d:Document {id: $documentId})") &&
                q.Contains("DETACH DELETE")),
            It.Is<Dictionary<string, object>?>(p =>
                p != null &&
                (string)p["documentId"] == "doc-to-delete"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region FindConceptsByNameAsync

    [Fact]
    public async Task FindConceptsByNameAsync_ParsesArrayResult_IntoConceptNodeList()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        var json = JsonDocument.Parse("""
            [
                {"id": "c1", "name": "Dependency Injection", "description": "DI pattern", "category": "patterns"}
            ]
            """).RootElement.Clone();

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        // Act
        var result = await sut.FindConceptsByNameAsync("Dependency Injection");

        // Assert
        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe("c1");
        result[0].Name.ShouldBe("Dependency Injection");
        result[0].Description.ShouldBe("DI pattern");
        result[0].Category.ShouldBe("patterns");

        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.Is<string>(q =>
                q.Contains("MATCH (c:Concept)") &&
                q.Contains("WHERE c.name = $name")),
            It.Is<Dictionary<string, object>?>(p =>
                p != null &&
                (string)p["name"] == "Dependency Injection"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FindConceptsByNameAsync_NoMatches_ReturnsEmptyList()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("[]").RootElement.Clone());

        // Act
        var result = await sut.FindConceptsByNameAsync("NonExistent");

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task FindConceptsByNameAsync_NonArrayResult_ReturnsEmptyList()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("{}").RootElement.Clone());

        // Act
        var result = await sut.FindConceptsByNameAsync("Test");

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task FindConceptsByNameAsync_ForwardsCancellationToken()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("[]").RootElement.Clone());

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        // Act
        await sut.FindConceptsByNameAsync("Test", token);

        // Assert
        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>?>(),
            token),
            Times.Once);
    }

    #endregion

    #region GetRelatedConceptsAsync

    [Fact]
    public async Task GetRelatedConceptsAsync_ParsesArrayResult_IntoConceptNodeList()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        var json = JsonDocument.Parse("""
            [
                {"id": "c1", "name": "Concept One", "description": "First concept", "category": "arch"},
                {"id": "c2", "name": "Concept Two", "description": "Second concept", "category": "design"}
            ]
            """).RootElement.Clone();

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        // Act
        var result = await sut.GetRelatedConceptsAsync("c0", hops: 3);

        // Assert
        result.Count.ShouldBe(2);

        result[0].Id.ShouldBe("c1");
        result[0].Name.ShouldBe("Concept One");
        result[0].Description.ShouldBe("First concept");
        result[0].Category.ShouldBe("arch");

        result[1].Id.ShouldBe("c2");
        result[1].Name.ShouldBe("Concept Two");
        result[1].Description.ShouldBe("Second concept");
        result[1].Category.ShouldBe("design");
    }

    [Fact]
    public async Task GetRelatedConceptsAsync_NonArrayResult_ReturnsEmptyList()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("{}").RootElement.Clone());

        // Act
        var result = await sut.GetRelatedConceptsAsync("c0");

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetRelatedConceptsAsync_MissingOptionalProperties_ReturnsDefaults()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        var json = JsonDocument.Parse("""
            [{"id": "c1", "name": "Minimal Concept"}]
            """).RootElement.Clone();

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        // Act
        var result = await sut.GetRelatedConceptsAsync("c0");

        // Assert
        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe("c1");
        result[0].Name.ShouldBe("Minimal Concept");
        result[0].Description.ShouldBeNull();
        result[0].Category.ShouldBeNull();
    }

    #endregion

    #region GetChunksByConceptAsync

    [Fact]
    public async Task GetChunksByConceptAsync_ParsesArrayResult_IntoChunkNodeList()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        var json = JsonDocument.Parse("""
            [
                {
                    "id": "chunk-1",
                    "sectionId": "sec-1",
                    "documentId": "doc-1",
                    "content": "Some chunk content",
                    "order": 2,
                    "tokenCount": 150
                }
            ]
            """).RootElement.Clone();

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        // Act
        var result = await sut.GetChunksByConceptAsync("concept-1");

        // Assert
        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe("chunk-1");
        result[0].SectionId.ShouldBe("sec-1");
        result[0].DocumentId.ShouldBe("doc-1");
        result[0].Content.ShouldBe("Some chunk content");
        result[0].Order.ShouldBe(2);
        result[0].TokenCount.ShouldBe(150);
    }

    [Fact]
    public async Task GetChunksByConceptAsync_NonArrayResult_ReturnsEmptyList()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("\"not an array\"").RootElement.Clone());

        // Act
        var result = await sut.GetChunksByConceptAsync("concept-1");

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetChunksByConceptAsync_MissingOptionalProperties_ReturnsDefaults()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        var json = JsonDocument.Parse("""
            [
                {
                    "id": "chunk-1",
                    "sectionId": "sec-1",
                    "documentId": "doc-1",
                    "content": "Content without order or tokenCount"
                }
            ]
            """).RootElement.Clone();

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        // Act
        var result = await sut.GetChunksByConceptAsync("concept-1");

        // Assert
        result.Count.ShouldBe(1);
        result[0].Order.ShouldBe(0);
        result[0].TokenCount.ShouldBe(0);
    }

    #endregion

    #region GetLinkedDocumentsAsync

    [Fact]
    public async Task GetLinkedDocumentsAsync_ParsesArrayResult_IntoDocumentNodeList()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        var json = JsonDocument.Parse("""
            [
                {
                    "id": "doc-2",
                    "filePath": "/docs/linked.md",
                    "title": "Linked Document",
                    "docType": "reference",
                    "promotionLevel": "published",
                    "commitHash": "def456"
                }
            ]
            """).RootElement.Clone();

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        // Act
        var result = await sut.GetLinkedDocumentsAsync("doc-1");

        // Assert
        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe("doc-2");
        result[0].FilePath.ShouldBe("/docs/linked.md");
        result[0].Title.ShouldBe("Linked Document");
        result[0].DocType.ShouldBe("reference");
        result[0].PromotionLevel.ShouldBe("published");
        result[0].CommitHash.ShouldBe("def456");
    }

    [Fact]
    public async Task GetLinkedDocumentsAsync_NonArrayResult_ReturnsEmptyList()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("42").RootElement.Clone());

        // Act
        var result = await sut.GetLinkedDocumentsAsync("doc-1");

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetLinkedDocumentsAsync_MissingOptionalProperties_ReturnsDefaults()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        var json = JsonDocument.Parse("""
            [
                {
                    "id": "doc-2",
                    "filePath": "/docs/minimal.md",
                    "title": "Minimal Doc"
                }
            ]
            """).RootElement.Clone();

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        // Act
        var result = await sut.GetLinkedDocumentsAsync("doc-1");

        // Assert
        result.Count.ShouldBe(1);
        result[0].DocType.ShouldBeNull();
        result[0].PromotionLevel.ShouldBe("draft");
        result[0].CommitHash.ShouldBeNull();
    }

    [Fact]
    public async Task GetLinkedDocumentsAsync_NullPromotionLevel_DefaultsToDraft()
    {
        // Arrange — promotionLevel property exists but has a null value
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        var json = JsonDocument.Parse("""
            [
                {
                    "id": "doc-3",
                    "filePath": "/docs/null-promo.md",
                    "title": "Null Promo",
                    "docType": null,
                    "promotionLevel": null,
                    "commitHash": null
                }
            ]
            """).RootElement.Clone();

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        // Act
        var result = await sut.GetLinkedDocumentsAsync("doc-1");

        // Assert
        result.Count.ShouldBe(1);
        result[0].PromotionLevel.ShouldBe("draft");
        result[0].DocType.ShouldBeNull();
        result[0].CommitHash.ShouldBeNull();
    }

    #endregion

    #region GetChunksByIdsAsync

    [Fact]
    public async Task GetChunksByIdsAsync_ReturnsMatchingChunks()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        var json = JsonDocument.Parse("""
            [
                {
                    "id": "chunk-1",
                    "sectionId": "sec-1",
                    "documentId": "doc-1",
                    "content": "First chunk content",
                    "order": 0,
                    "tokenCount": 50
                },
                {
                    "id": "chunk-2",
                    "sectionId": "sec-1",
                    "documentId": "doc-1",
                    "content": "Second chunk content",
                    "order": 1,
                    "tokenCount": 60
                }
            ]
            """).RootElement.Clone();

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        // Act
        var result = await sut.GetChunksByIdsAsync(["chunk-1", "chunk-2"]);

        // Assert
        result.Count.ShouldBe(2);
        result[0].Id.ShouldBe("chunk-1");
        result[0].Content.ShouldBe("First chunk content");
        result[1].Id.ShouldBe("chunk-2");
        result[1].Content.ShouldBe("Second chunk content");

        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.Is<string>(q => q.Contains("MATCH (c:Chunk)") && q.Contains("WHERE c.id IN $ids")),
            It.Is<Dictionary<string, object>?>(p => p != null && p.ContainsKey("ids")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetChunksByIdsAsync_EmptyInput_ReturnsEmptyList()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        // Act
        var result = await sut.GetChunksByIdsAsync([]);

        // Assert
        result.ShouldBeEmpty();

        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>?>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region GetConceptsByChunkIdsAsync

    [Fact]
    public async Task GetConceptsByChunkIdsAsync_ReturnsDistinctConcepts()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        var json = JsonDocument.Parse("""
            [
                {"id": "c1", "name": "Dependency Injection", "description": "DI pattern", "category": "patterns"},
                {"id": "c2", "name": "GraphRAG", "description": "Graph RAG pipeline", "category": "architecture"}
            ]
            """).RootElement.Clone();

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        // Act
        var result = await sut.GetConceptsByChunkIdsAsync(["chunk-1", "chunk-2"]);

        // Assert
        result.Count.ShouldBe(2);
        result[0].Id.ShouldBe("c1");
        result[0].Name.ShouldBe("Dependency Injection");
        result[1].Id.ShouldBe("c2");
        result[1].Name.ShouldBe("GraphRAG");

        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.Is<string>(q =>
                q.Contains("MATCH (c:Chunk)-[:MENTIONS]->(concept:Concept)") &&
                q.Contains("WHERE c.id IN $ids") &&
                q.Contains("RETURN DISTINCT")),
            It.Is<Dictionary<string, object>?>(p => p != null && p.ContainsKey("ids")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetConceptsByChunkIdsAsync_EmptyInput_ReturnsEmptyList()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        // Act
        var result = await sut.GetConceptsByChunkIdsAsync([]);

        // Assert
        result.ShouldBeEmpty();

        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>?>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region UpsertCodeExampleAsync

    [Fact]
    public async Task UpsertCodeExampleAsync_CallsExecuteOpenCypherAsync_WithCorrectParameters()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("{}").RootElement.Clone());

        var codeExample = new CodeExampleNode
        {
            Id = "code-1",
            ChunkId = "chunk-1",
            Language = "csharp",
            Code = "var x = 1;",
            Description = "A simple assignment"
        };

        // Act
        await sut.UpsertCodeExampleAsync(codeExample, "chunk-1");

        // Assert
        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.Is<string>(q =>
                q.Contains("MERGE (ce:CodeExample {id: $id})") &&
                q.Contains("MERGE (c)-[:HAS_CODE_EXAMPLE]->(ce)")),
            It.Is<Dictionary<string, object>?>(p =>
                p != null &&
                (string)p["id"] == "code-1" &&
                (string)p["chunkId"] == "chunk-1" &&
                (string)p["language"] == "csharp" &&
                (string)p["code"] == "var x = 1;" &&
                (string)p["description"] == "A simple assignment"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpsertCodeExampleAsync_NullDescription_PassesEmptyString()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("{}").RootElement.Clone());

        var codeExample = new CodeExampleNode
        {
            Id = "code-2",
            ChunkId = "chunk-1",
            Language = "python",
            Code = "print('hi')",
            Description = null
        };

        // Act
        await sut.UpsertCodeExampleAsync(codeExample, "chunk-1");

        // Assert
        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.IsAny<string>(),
            It.Is<Dictionary<string, object>?>(p =>
                p != null &&
                (string)p["description"] == string.Empty),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetSyncStateAsync

    [Fact]
    public async Task GetSyncStateAsync_ReturnsCommitHash_WhenFound()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        var json = JsonDocument.Parse("""
            [{"commitHash": "abc123def456"}]
            """).RootElement.Clone();

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        // Act
        var result = await sut.GetSyncStateAsync("my-repo");

        // Assert
        result.ShouldBe("abc123def456");

        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.Is<string>(q => q.Contains("MATCH (s:SyncState {repoName: $repoName})")),
            It.Is<Dictionary<string, object>?>(p =>
                p != null &&
                (string)p["repoName"] == "my-repo"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetSyncStateAsync_ReturnsNull_WhenEmptyArray()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("[]").RootElement.Clone());

        // Act
        var result = await sut.GetSyncStateAsync("my-repo");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetSyncStateAsync_ReturnsNull_WhenNonArrayResult()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("{}").RootElement.Clone());

        // Act
        var result = await sut.GetSyncStateAsync("my-repo");

        // Assert
        result.ShouldBeNull();
    }

    #endregion

    #region SetSyncStateAsync

    [Fact]
    public async Task SetSyncStateAsync_CallsExecuteOpenCypherAsync_WithCorrectParameters()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("{}").RootElement.Clone());

        // Act
        await sut.SetSyncStateAsync("my-repo", "abc123");

        // Assert
        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.Is<string>(q =>
                q.Contains("MERGE (s:SyncState {repoName: $repoName})") &&
                q.Contains("SET s.commitHash = $commitHash")),
            It.Is<Dictionary<string, object>?>(p =>
                p != null &&
                (string)p["repoName"] == "my-repo" &&
                (string)p["commitHash"] == "abc123" &&
                p.ContainsKey("updatedAt")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region CancellationToken forwarding

    [Fact]
    public async Task UpsertDocumentAsync_ForwardsCancellationToken()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        var sut = new NeptuneGraphRepository(mockClient.Object, NullLogger<NeptuneGraphRepository>.Instance);

        mockClient
            .Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("{}").RootElement.Clone());

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var document = new DocumentNode
        {
            Id = "doc-1",
            FilePath = "/docs/test.md",
            Title = "Test"
        };

        // Act
        await sut.UpsertDocumentAsync(document, token);

        // Assert
        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>?>(),
            token),
            Times.Once);
    }

    #endregion
}
