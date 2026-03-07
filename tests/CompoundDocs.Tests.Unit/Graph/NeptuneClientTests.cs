using Amazon.Runtime.Documents;
using CompoundDocs.Common.Models;
using CompoundDocs.Graph;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CompoundDocs.Tests.Unit.Graph;

public sealed class NeptuneClientTests
{
    [Fact]
    public async Task NeptuneGraphRepository_UpsertDocumentAsync_CallsClient()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        mockClient.Setup(c => c.ExecuteOpenCypherAsync(
                It.Is<string>(q => q.Contains("MERGE (d:Document")),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document(new List<Document>()));

        var logger = NullLogger<NeptuneGraphRepository>.Instance;
        var sut = new NeptuneGraphRepository(mockClient.Object, logger);

        var document = new DocumentNode
        {
            Id = "doc-1",
            FilePath = "docs/test.md",
            Title = "Test Document"
        };

        // Act
        await sut.UpsertDocumentAsync(document);

        // Assert
        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.Is<string>(q => q.Contains("MERGE (d:Document")),
            It.Is<Dictionary<string, object>?>(p =>
                p != null &&
                p["id"].ToString() == "doc-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NeptuneGraphRepository_DeleteDocumentCascadeAsync_CallsDetachDelete()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        mockClient.Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document(new List<Document>()));

        var logger = NullLogger<NeptuneGraphRepository>.Instance;
        var sut = new NeptuneGraphRepository(mockClient.Object, logger);

        // Act
        await sut.DeleteDocumentCascadeAsync("doc-1");

        // Assert
        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.Is<string>(q => q.Contains("DETACH DELETE")),
            It.Is<Dictionary<string, object>?>(p =>
                p != null &&
                p["documentId"].ToString() == "doc-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NeptuneGraphRepository_GetRelatedConceptsAsync_ParsesResults()
    {
        // Arrange
        var resultDocument = new Document(new List<Document>
        {
            new(new Dictionary<string, Document>
            {
                ["id"] = new Document("concept-1"),
                ["name"] = new Document("React"),
                ["description"] = new Document("UI library"),
                ["category"] = new Document("Framework")
            }),
            new(new Dictionary<string, Document>
            {
                ["id"] = new Document("concept-2"),
                ["name"] = new Document("Redux"),
                ["description"] = new Document("State management"),
                ["category"] = new Document("Library")
            })
        });

        var mockClient = new Mock<INeptuneClient>();
        mockClient.Setup(c => c.ExecuteOpenCypherAsync(
                It.Is<string>(q => q.Contains("RELATES_TO")),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDocument);

        var logger = NullLogger<NeptuneGraphRepository>.Instance;
        var sut = new NeptuneGraphRepository(mockClient.Object, logger);

        // Act
        var result = await sut.GetRelatedConceptsAsync("concept-0", hops: 2);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result[0].Id.ShouldBe("concept-1");
        result[0].Name.ShouldBe("React");
        result[1].Id.ShouldBe("concept-2");
    }

    [Fact]
    public async Task NeptuneGraphRepository_GetChunksByConceptAsync_ParsesResults()
    {
        // Arrange
        var resultDocument = new Document(new List<Document>
        {
            new(new Dictionary<string, Document>
            {
                ["id"] = new Document("chunk-1"),
                ["sectionId"] = new Document("section-1"),
                ["documentId"] = new Document("doc-1"),
                ["content"] = new Document("hello world"),
                ["order"] = new Document(0),
                ["tokenCount"] = new Document(2)
            })
        });

        var mockClient = new Mock<INeptuneClient>();
        mockClient.Setup(c => c.ExecuteOpenCypherAsync(
                It.Is<string>(q => q.Contains("MENTIONS")),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDocument);

        var logger = NullLogger<NeptuneGraphRepository>.Instance;
        var sut = new NeptuneGraphRepository(mockClient.Object, logger);

        // Act
        var result = await sut.GetChunksByConceptAsync("concept-1");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe("chunk-1");
        result[0].Content.ShouldBe("hello world");
    }

    [Fact]
    public async Task NeptuneGraphRepository_CreateRelationshipAsync_UsesRelationshipType()
    {
        // Arrange
        var mockClient = new Mock<INeptuneClient>();
        mockClient.Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document(new List<Document>()));

        var logger = NullLogger<NeptuneGraphRepository>.Instance;
        var sut = new NeptuneGraphRepository(mockClient.Object, logger);

        var relationship = new GraphRelationship
        {
            Type = "LINKS_TO",
            SourceId = "doc-1",
            TargetId = "doc-2"
        };

        // Act
        await sut.CreateRelationshipAsync(relationship);

        // Assert
        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.Is<string>(q => q.Contains("LINKS_TO")),
            It.IsAny<Dictionary<string, object>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NeptuneGraphRepository_UpsertSectionAsync_CallsMergeSection()
    {
        var mockClient = new Mock<INeptuneClient>();
        mockClient.Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document(new List<Document>()));

        var logger = NullLogger<NeptuneGraphRepository>.Instance;
        var sut = new NeptuneGraphRepository(mockClient.Object, logger);

        var section = new SectionNode
        {
            Id = "sec-1",
            DocumentId = "doc-1",
            Title = "Introduction"
        };

        await sut.UpsertSectionAsync(section);

        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.Is<string>(q => q.Contains("MERGE (s:Section") && q.Contains("HAS_SECTION")),
            It.Is<Dictionary<string, object>?>(p =>
                p != null &&
                p["id"].ToString() == "sec-1" &&
                p["documentId"].ToString() == "doc-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NeptuneGraphRepository_UpsertChunkAsync_CallsMergeChunk()
    {
        var mockClient = new Mock<INeptuneClient>();
        mockClient.Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document(new List<Document>()));

        var logger = NullLogger<NeptuneGraphRepository>.Instance;
        var sut = new NeptuneGraphRepository(mockClient.Object, logger);

        var chunk = new ChunkNode
        {
            Id = "chunk-1",
            SectionId = "sec-1",
            DocumentId = "doc-1",
            Content = "Test content"
        };

        await sut.UpsertChunkAsync(chunk);

        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.Is<string>(q => q.Contains("MERGE (c:Chunk") && q.Contains("HAS_CHUNK")),
            It.Is<Dictionary<string, object>?>(p =>
                p != null &&
                p["id"].ToString() == "chunk-1" &&
                p["content"].ToString() == "Test content"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NeptuneGraphRepository_UpsertConceptAsync_SerializesAliases()
    {
        Dictionary<string, object>? capturedParams = null;
        var mockClient = new Mock<INeptuneClient>();
        mockClient.Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Dictionary<string, object>?, CancellationToken>((_, p, _) => capturedParams = p)
            .ReturnsAsync(new Document(new List<Document>()));

        var logger = NullLogger<NeptuneGraphRepository>.Instance;
        var sut = new NeptuneGraphRepository(mockClient.Object, logger);

        var concept = new ConceptNode
        {
            Id = "concept-1",
            Name = "React",
            Aliases = ["ReactJS", "React.js"]
        };

        await sut.UpsertConceptAsync(concept);

        mockClient.Verify(c => c.ExecuteOpenCypherAsync(
            It.Is<string>(q => q.Contains("MERGE (c:Concept")),
            It.IsAny<Dictionary<string, object>?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        capturedParams.ShouldNotBeNull();
        var aliasesJson = capturedParams!["aliases"].ToString()!;
        aliasesJson.ShouldContain("ReactJS");
        aliasesJson.ShouldContain("React.js");
    }

    [Fact]
    public async Task NeptuneGraphRepository_GetLinkedDocumentsAsync_ParsesDocumentNodes()
    {
        var resultDocument = new Document(new List<Document>
        {
            new(new Dictionary<string, Document>
            {
                ["id"] = new Document("doc-2"),
                ["filePath"] = new Document("docs/linked.md"),
                ["title"] = new Document("Linked Doc"),
                ["docType"] = new Document("spec"),
                ["promotionLevel"] = new Document("standard"),
                ["lastUpdated"] = new Document("2024-01-01T00:00:00Z"),
                ["commitHash"] = new Document("abc123")
            })
        });

        var mockClient = new Mock<INeptuneClient>();
        mockClient.Setup(c => c.ExecuteOpenCypherAsync(
                It.Is<string>(q => q.Contains("LINKS_TO")),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDocument);

        var logger = NullLogger<NeptuneGraphRepository>.Instance;
        var sut = new NeptuneGraphRepository(mockClient.Object, logger);

        var result = await sut.GetLinkedDocumentsAsync("doc-1");

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe("doc-2");
        result[0].Title.ShouldBe("Linked Doc");
        result[0].DocType.ShouldBe("spec");
        result[0].PromotionLevel.ShouldBe("standard");
    }

    [Fact]
    public async Task NeptuneGraphRepository_GetRelatedConceptsAsync_EmptyArray_ReturnsEmptyList()
    {
        var mockClient = new Mock<INeptuneClient>();
        mockClient.Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document(new List<Document>()));

        var logger = NullLogger<NeptuneGraphRepository>.Instance;
        var sut = new NeptuneGraphRepository(mockClient.Object, logger);

        var result = await sut.GetRelatedConceptsAsync("concept-0");

        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task NeptuneGraphRepository_GetChunksByConceptAsync_NonArrayResult_ReturnsEmptyList()
    {
        var mockClient = new Mock<INeptuneClient>();
        mockClient.Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document(new Dictionary<string, Document>()));

        var logger = NullLogger<NeptuneGraphRepository>.Instance;
        var sut = new NeptuneGraphRepository(mockClient.Object, logger);

        var result = await sut.GetChunksByConceptAsync("concept-1");

        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task NeptuneGraphRepository_GetLinkedDocumentsAsync_NonArrayResult_ReturnsEmptyList()
    {
        var mockClient = new Mock<INeptuneClient>();
        mockClient.Setup(c => c.ExecuteOpenCypherAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document("not an array"));

        var logger = NullLogger<NeptuneGraphRepository>.Instance;
        var sut = new NeptuneGraphRepository(mockClient.Object, logger);

        var result = await sut.GetLinkedDocumentsAsync("doc-1");

        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }
}
