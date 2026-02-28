using CompoundDocs.Bedrock;
using CompoundDocs.Common.Models;
using CompoundDocs.Common.Parsing;
using CompoundDocs.Graph;
using CompoundDocs.GraphRag;
using CompoundDocs.Vector;
using Markdig.Syntax;
using Microsoft.Extensions.Logging.Abstractions;
using ExtractedEntity = CompoundDocs.GraphRag.ExtractedEntity;

namespace CompoundDocs.Tests.Unit.GraphRag;

public sealed class DocumentIngestionServiceTests
{
    private readonly Mock<IGraphRepository> _graphMock = new();
    private readonly Mock<IVectorStore> _vectorMock = new();
    private readonly Mock<IBedrockEmbeddingService> _embeddingMock = new();
    private readonly Mock<IEntityExtractor> _entityMock = new();
    private readonly Mock<IMarkdownParser> _markdownParserMock = new();
    private readonly Mock<IFrontmatterParser> _frontmatterParserMock = new();

    private DocumentIngestionService CreateService() =>
        new(
            _graphMock.Object,
            _vectorMock.Object,
            _embeddingMock.Object,
            _entityMock.Object,
            _markdownParserMock.Object,
            _frontmatterParserMock.Object,
            NullLogger<DocumentIngestionService>.Instance);

    private void SetupDefaultMocks(string body, List<ChunkInfo>? chunks = null, List<HeaderInfo>? headers = null, List<LinkInfo>? links = null)
    {
        _frontmatterParserMock.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(FrontmatterResult.NoFrontmatter(body));

        var doc = new MarkdownDocument();
        _markdownParserMock.Setup(p => p.Parse(It.IsAny<string>())).Returns(doc);
        _markdownParserMock.Setup(p => p.ExtractHeaders(doc)).Returns(headers ?? []);
        _markdownParserMock.Setup(p => p.ExtractLinks(doc)).Returns(links ?? []);
        _markdownParserMock.Setup(p => p.ExtractCodeBlocks(doc)).Returns(new List<ParsedCodeBlock>());
        _markdownParserMock.Setup(p => p.ChunkByHeaders(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(chunks ?? [new ChunkInfo(0, "", 0, 1, body)]);

        _embeddingMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f, 0.3f]);

        _entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private static DocumentIngestionMetadata CreateMetadata(string docId = "repo:docs/test.md") => new()
    {
        DocumentId = docId,
        Repository = "repo",
        FilePath = "docs/test.md",
        Title = "Test Document"
    };

    // --- Happy path ---

    [Fact]
    public async Task IngestDocumentAsync_SimpleDocument_CreatesGraphAndVectorEntries()
    {
        // Arrange
        SetupDefaultMocks("Some content here.",
            chunks: [new ChunkInfo(0, "Section One", 0, 1, "Some content here.")],
            headers: [new HeaderInfo(2, "Section One", "Section One", 0, 0, 20)]);
        var service = CreateService();

        // Act
        await service.IngestDocumentAsync("Some content here.", CreateMetadata());

        // Assert
        _graphMock.Verify(g => g.UpsertDocumentAsync(
            It.Is<DocumentNode>(d => d.Id == "repo:docs/test.md"),
            It.IsAny<CancellationToken>()), Times.Once);
        _graphMock.Verify(g => g.UpsertSectionAsync(
            It.IsAny<SectionNode>(), It.IsAny<CancellationToken>()), Times.Once);
        _graphMock.Verify(g => g.UpsertChunkAsync(
            It.IsAny<ChunkNode>(), It.IsAny<CancellationToken>()), Times.Once);
        _vectorMock.Verify(v => v.IndexAsync(
            It.IsAny<string>(), It.IsAny<float[]>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestDocumentAsync_MultipleH2Sections_CreatesSectionNodePerH2()
    {
        // Arrange
        SetupDefaultMocks("Content.",
            chunks:
            [
                new ChunkInfo(0, "First", 0, 1, "Content."),
                new ChunkInfo(1, "Second", 2, 3, "More content.")
            ],
            headers:
            [
                new HeaderInfo(2, "First", "First", 0, 0, 10),
                new HeaderInfo(2, "Second", "Second", 2, 20, 30)
            ]);
        var service = CreateService();

        // Act
        await service.IngestDocumentAsync("Content.", CreateMetadata());

        // Assert
        _graphMock.Verify(g => g.UpsertSectionAsync(
            It.IsAny<SectionNode>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task IngestDocumentAsync_H3SubsectionsGroupedUnderParentH2()
    {
        // Arrange
        SetupDefaultMocks("Content.",
            chunks:
            [
                new ChunkInfo(0, "Parent", 0, 1, "Parent content."),
                new ChunkInfo(1, "Parent > Child", 2, 3, "Child content.")
            ],
            headers:
            [
                new HeaderInfo(2, "Parent", "Parent", 0, 0, 10),
                new HeaderInfo(3, "Child", "Parent > Child", 2, 20, 30)
            ]);
        var service = CreateService();

        // Act
        await service.IngestDocumentAsync("Content.", CreateMetadata());

        // Assert
        _graphMock.Verify(g => g.UpsertSectionAsync(
            It.Is<SectionNode>(s => s.Title == "Parent"),
            It.IsAny<CancellationToken>()), Times.Once);
        _graphMock.Verify(g => g.UpsertChunkAsync(
            It.IsAny<ChunkNode>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task IngestDocumentAsync_PreHeaderContent_CreatesIntroSection()
    {
        // Arrange
        SetupDefaultMocks("Some introduction text.\n\nSection content.",
            chunks:
            [
                new ChunkInfo(0, "", 0, 1, "Some introduction text."),
                new ChunkInfo(1, "Section", 2, 3, "Section content.")
            ],
            headers: [new HeaderInfo(2, "Section", "Section", 2, 30, 40)]);
        var service = CreateService();

        // Act
        await service.IngestDocumentAsync("Some introduction text.\n\n## Section\n\nContent.", CreateMetadata());

        // Assert
        _graphMock.Verify(g => g.UpsertSectionAsync(
            It.Is<SectionNode>(s => s.Title == "Introduction"),
            It.IsAny<CancellationToken>()), Times.Once);
        _graphMock.Verify(g => g.UpsertSectionAsync(
            It.Is<SectionNode>(s => s.Title == "Section"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestDocumentAsync_NoHeaders_SingleChunkWithEntireContent()
    {
        // Arrange
        var body = "Just plain text with no headers at all.";
        SetupDefaultMocks(body,
            chunks: [new ChunkInfo(0, "", 0, 1, body)],
            headers: []);
        var service = CreateService();

        // Act
        await service.IngestDocumentAsync(body, CreateMetadata());

        // Assert
        _graphMock.Verify(g => g.UpsertChunkAsync(
            It.IsAny<ChunkNode>(), It.IsAny<CancellationToken>()), Times.Once);
        _graphMock.Verify(g => g.UpsertSectionAsync(
            It.Is<SectionNode>(s => s.Title == "Introduction"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestDocumentAsync_FrontmatterStripped()
    {
        // Arrange
        var body = "Body content.";
        _frontmatterParserMock.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(FrontmatterResult.Success(
                new Dictionary<string, object?> { ["title"] = "Test" }, body));

        var doc = new MarkdownDocument();
        _markdownParserMock.Setup(p => p.Parse(It.IsAny<string>())).Returns(doc);
        _markdownParserMock.Setup(p => p.ExtractHeaders(doc))
            .Returns(new List<HeaderInfo> { new(2, "Section", "Section", 0, 0, 10) });
        _markdownParserMock.Setup(p => p.ExtractLinks(doc)).Returns(new List<LinkInfo>());
        _markdownParserMock.Setup(p => p.ExtractCodeBlocks(doc)).Returns(new List<ParsedCodeBlock>());
        _markdownParserMock.Setup(p => p.ChunkByHeaders(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(new List<ChunkInfo> { new(0, "Section", 0, 1, body) });
        _embeddingMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f, 0.3f]);
        _entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var service = CreateService();

        // Act
        await service.IngestDocumentAsync("---\ntitle: Test\n---\n\nBody content.", CreateMetadata());

        // Assert - chunk content should be body without frontmatter
        _graphMock.Verify(g => g.UpsertChunkAsync(
            It.Is<ChunkNode>(c => !c.Content.Contains("---")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Entity extraction ---

    [Fact]
    public async Task IngestDocumentAsync_EntitiesExtracted_CreatesConceptNodesAndMentions()
    {
        // Arrange
        SetupDefaultMocks("Some content about Neptune.",
            chunks: [new ChunkInfo(0, "Section", 0, 1, "Some content about Neptune.")],
            headers: [new HeaderInfo(2, "Section", "Section", 0, 0, 10)]);
        _entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ExtractedEntity { Name = "Amazon Neptune", Type = "Service" }
            ]);
        var service = CreateService();

        // Act
        await service.IngestDocumentAsync("Content.", CreateMetadata());

        // Assert
        _graphMock.Verify(g => g.UpsertConceptAsync(
            It.Is<ConceptNode>(c => c.Name == "Amazon Neptune"),
            It.IsAny<CancellationToken>()), Times.Once);
        _graphMock.Verify(g => g.CreateRelationshipAsync(
            It.Is<GraphRelationship>(r => r.Type == "MENTIONS"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Internal links ---

    [Fact]
    public async Task IngestDocumentAsync_InternalLinks_CreatesLinksToRelationships()
    {
        // Arrange
        SetupDefaultMocks("See other doc for details.",
            chunks: [new ChunkInfo(0, "Section", 0, 1, "See other doc for details.")],
            headers: [new HeaderInfo(2, "Section", "Section", 0, 0, 10)],
            links: [new LinkInfo("../other.md", "other doc", 1, 5)]);
        var service = CreateService();

        // Act
        await service.IngestDocumentAsync("Content.", CreateMetadata());

        // Assert
        _graphMock.Verify(g => g.CreateRelationshipAsync(
            It.Is<GraphRelationship>(r => r.Type == "LINKS_TO" && r.TargetId == "repo:other.md"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Error handling ---

    [Fact]
    public async Task IngestDocumentAsync_EmbeddingFails_ContinuesWithoutVector()
    {
        // Arrange
        SetupDefaultMocks("Content.",
            chunks: [new ChunkInfo(0, "Section", 0, 1, "Content.")],
            headers: [new HeaderInfo(2, "Section", "Section", 0, 0, 10)]);
        _embeddingMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Embedding failed"));
        var service = CreateService();

        // Act
        await service.IngestDocumentAsync("Content.", CreateMetadata());

        // Assert
        _graphMock.Verify(g => g.UpsertChunkAsync(
            It.IsAny<ChunkNode>(), It.IsAny<CancellationToken>()), Times.Once);
        _vectorMock.Verify(v => v.IndexAsync(
            It.IsAny<string>(), It.IsAny<float[]>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestDocumentAsync_EntityExtractionFails_ContinuesWithoutConcepts()
    {
        // Arrange
        SetupDefaultMocks("Content.",
            chunks: [new ChunkInfo(0, "Section", 0, 1, "Content.")],
            headers: [new HeaderInfo(2, "Section", "Section", 0, 0, 10)]);
        _entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Extraction failed"));
        var service = CreateService();

        // Act
        await service.IngestDocumentAsync("Content.", CreateMetadata());

        // Assert
        _graphMock.Verify(g => g.UpsertConceptAsync(
            It.IsAny<ConceptNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestDocumentAsync_VectorIndexFails_ContinuesProcessing()
    {
        // Arrange
        SetupDefaultMocks("Content.",
            chunks:
            [
                new ChunkInfo(0, "First", 0, 1, "Content one."),
                new ChunkInfo(1, "Second", 2, 3, "Content two.")
            ],
            headers:
            [
                new HeaderInfo(2, "First", "First", 0, 0, 10),
                new HeaderInfo(2, "Second", "Second", 2, 20, 30)
            ]);
        _vectorMock
            .Setup(v => v.IndexAsync(
                It.IsAny<string>(), It.IsAny<float[]>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Index failed"));
        var service = CreateService();

        // Act
        await service.IngestDocumentAsync("Content.", CreateMetadata());

        // Assert - both chunks should still be processed in graph
        _graphMock.Verify(g => g.UpsertChunkAsync(
            It.IsAny<ChunkNode>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // --- Vector metadata ---

    [Fact]
    public async Task IngestDocumentAsync_VectorMetadata_ContainsExpectedKeys()
    {
        // Arrange
        Dictionary<string, string>? capturedMetadata = null;
        SetupDefaultMocks("Content.",
            chunks: [new ChunkInfo(0, "Section", 0, 1, "Content.")],
            headers: [new HeaderInfo(2, "Section", "Section", 0, 0, 10)]);
        _vectorMock
            .Setup(v => v.IndexAsync(
                It.IsAny<string>(), It.IsAny<float[]>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, float[], Dictionary<string, string>, CancellationToken>(
                (_, _, meta, _) => capturedMetadata = meta)
            .Returns(Task.CompletedTask);
        var service = CreateService();

        // Act
        await service.IngestDocumentAsync("Content.", CreateMetadata());

        // Assert
        capturedMetadata.ShouldNotBeNull();
        capturedMetadata.ShouldContainKey("document_id");
        capturedMetadata.ShouldContainKey("section_id");
        capturedMetadata.ShouldContainKey("chunk_id");
        capturedMetadata.ShouldContainKey("file_path");
        capturedMetadata.ShouldContainKey("repository");
        capturedMetadata.ShouldContainKey("header_path");
        capturedMetadata["document_id"].ShouldBe("repo:docs/test.md");
        capturedMetadata["repository"].ShouldBe("repo");
    }

    // --- Delete ---

    [Fact]
    public async Task DeleteDocumentAsync_CallsVectorDeleteThenGraphCascade()
    {
        // Arrange
        var callOrder = new List<string>();
        _vectorMock
            .Setup(v => v.DeleteByDocumentIdAsync("doc-1", It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("vector"))
            .Returns(Task.CompletedTask);
        _graphMock
            .Setup(g => g.DeleteDocumentCascadeAsync("doc-1", It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("graph"))
            .Returns(Task.CompletedTask);
        var service = CreateService();

        // Act
        await service.DeleteDocumentAsync("doc-1");

        // Assert
        callOrder.ShouldBe(["vector", "graph"]);
    }

    [Fact]
    public async Task DeleteDocumentAsync_PropagatesErrors()
    {
        // Arrange
        _vectorMock
            .Setup(v => v.DeleteByDocumentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Delete failed"));
        var service = CreateService();

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => service.DeleteDocumentAsync("doc-1"));
    }

    // --- NormalizeConceptId ---

    [Theory]
    [InlineData("Amazon Neptune", "concept:amazon-neptune")]
    [InlineData("C# Language", "concept:c-language")]
    [InlineData("  spaces  ", "concept:spaces")]
    [InlineData("Under_Score", "concept:underscore")]
    [InlineData("Multiple   Spaces", "concept:multiple-spaces")]
    [InlineData("Special!@#Characters", "concept:specialcharacters")]
    [InlineData("already-normalized", "concept:already-normalized")]
    public void NormalizeConceptId_VariousCases(string input, string expected)
    {
        DocumentIngestionService.NormalizeConceptId(input).ShouldBe(expected);
    }

    // --- ResolveRelativeLink ---

    [Fact]
    public void ResolveRelativeLink_SiblingFile()
    {
        var result = DocumentIngestionService.ResolveRelativeLink("docs/guide.md", "other.md");
        result.ShouldBe("docs/other.md");
    }

    [Fact]
    public void ResolveRelativeLink_ParentDirectory()
    {
        var result = DocumentIngestionService.ResolveRelativeLink("docs/sub/page.md", "../other.md");
        result.ShouldBe("docs/other.md");
    }

    [Fact]
    public void ResolveRelativeLink_FragmentOnly_ReturnsNull()
    {
        var result = DocumentIngestionService.ResolveRelativeLink("docs/guide.md", "#section");
        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveRelativeLink_FragmentStripped()
    {
        var result = DocumentIngestionService.ResolveRelativeLink("docs/guide.md", "other.md#section");
        result.ShouldBe("docs/other.md");
    }

    [Fact]
    public void ResolveRelativeLink_EmptyUrl_ReturnsNull()
    {
        var result = DocumentIngestionService.ResolveRelativeLink("docs/guide.md", "");
        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveRelativeLink_FileInRoot()
    {
        var result = DocumentIngestionService.ResolveRelativeLink("readme.md", "other.md");
        result.ShouldBe("other.md");
    }

    [Fact]
    public void ResolveRelativeLink_CurrentDirPrefix_Resolved()
    {
        var result = DocumentIngestionService.ResolveRelativeLink("docs/guide.md", "./other.md");
        result.ShouldBe("docs/other.md");
    }

    [Fact]
    public void ResolveRelativeLink_RootSourcePath_HandlesNullGetDirectoryName()
    {
        var result = DocumentIngestionService.ResolveRelativeLink("/", "other.md");
        result.ShouldBe("other.md");
    }

    // --- EstimateTokenCount ---

    [Fact]
    public void EstimateTokenCount_ReturnsLengthDividedByFour()
    {
        DocumentIngestionService.EstimateTokenCount("12345678").ShouldBe(2);
    }

    // --- DocumentNode properties ---

    [Fact]
    public async Task IngestDocumentAsync_DocumentNodeProperties_SetFromMetadata()
    {
        // Arrange
        DocumentNode? capturedDoc = null;
        _graphMock
            .Setup(g => g.UpsertDocumentAsync(It.IsAny<DocumentNode>(), It.IsAny<CancellationToken>()))
            .Callback<DocumentNode, CancellationToken>((d, _) => capturedDoc = d)
            .Returns(Task.CompletedTask);
        SetupDefaultMocks("Content.",
            chunks: [new ChunkInfo(0, "Section", 0, 1, "Content.")],
            headers: [new HeaderInfo(2, "Section", "Section", 0, 0, 10)]);
        var service = CreateService();
        var metadata = new DocumentIngestionMetadata
        {
            DocumentId = "repo:docs/test.md",
            Repository = "repo",
            FilePath = "docs/test.md",
            Title = "Test Doc",
            DocType = "guide",
            PromotionLevel = "published",
            CommitHash = "abc123"
        };

        // Act
        await service.IngestDocumentAsync("Content.", metadata);

        // Assert
        capturedDoc.ShouldNotBeNull();
        capturedDoc.Id.ShouldBe("repo:docs/test.md");
        capturedDoc.FilePath.ShouldBe("docs/test.md");
        capturedDoc.Title.ShouldBe("Test Doc");
        capturedDoc.DocType.ShouldBe("guide");
        capturedDoc.PromotionLevel.ShouldBe("published");
        capturedDoc.CommitHash.ShouldBe("abc123");
    }

    // --- Chunk content and ordering ---

    [Fact]
    public async Task IngestDocumentAsync_ChunkOrder_MatchesHeaderOrder()
    {
        // Arrange
        var capturedChunks = new List<ChunkNode>();
        _graphMock
            .Setup(g => g.UpsertChunkAsync(It.IsAny<ChunkNode>(), It.IsAny<CancellationToken>()))
            .Callback<ChunkNode, CancellationToken>((c, _) => capturedChunks.Add(c))
            .Returns(Task.CompletedTask);
        SetupDefaultMocks("Content.",
            chunks:
            [
                new ChunkInfo(0, "Alpha", 0, 1, "First."),
                new ChunkInfo(1, "Beta", 2, 3, "Second.")
            ],
            headers:
            [
                new HeaderInfo(2, "Alpha", "Alpha", 0, 0, 10),
                new HeaderInfo(2, "Beta", "Beta", 2, 20, 30)
            ]);
        var service = CreateService();

        // Act
        await service.IngestDocumentAsync("Content.", CreateMetadata());

        // Assert
        capturedChunks.Count.ShouldBe(2);
        capturedChunks[0].Order.ShouldBe(0);
        capturedChunks[1].Order.ShouldBe(1);
    }

    [Fact]
    public async Task IngestDocumentAsync_ChunkTokenCount_IsEstimated()
    {
        // Arrange
        ChunkNode? capturedChunk = null;
        _graphMock
            .Setup(g => g.UpsertChunkAsync(It.IsAny<ChunkNode>(), It.IsAny<CancellationToken>()))
            .Callback<ChunkNode, CancellationToken>((c, _) => capturedChunk = c)
            .Returns(Task.CompletedTask);
        SetupDefaultMocks("Some content here.",
            chunks: [new ChunkInfo(0, "Section", 0, 1, "Some content here.")],
            headers: [new HeaderInfo(2, "Section", "Section", 0, 0, 10)]);
        var service = CreateService();

        // Act
        await service.IngestDocumentAsync("Content.", CreateMetadata());

        // Assert
        capturedChunk.ShouldNotBeNull();
        capturedChunk.TokenCount.ShouldBeGreaterThan(0);
    }

    // --- Concept ID in graph ---

    [Fact]
    public async Task IngestDocumentAsync_ConceptId_IsNormalized()
    {
        // Arrange
        SetupDefaultMocks("Content.",
            chunks: [new ChunkInfo(0, "Section", 0, 1, "Content.")],
            headers: [new HeaderInfo(2, "Section", "Section", 0, 0, 10)]);
        _entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ExtractedEntity { Name = "Amazon Neptune", Type = "Service" }
            ]);
        ConceptNode? capturedConcept = null;
        _graphMock
            .Setup(g => g.UpsertConceptAsync(It.IsAny<ConceptNode>(), It.IsAny<CancellationToken>()))
            .Callback<ConceptNode, CancellationToken>((c, _) => capturedConcept = c)
            .Returns(Task.CompletedTask);
        var service = CreateService();

        // Act
        await service.IngestDocumentAsync("Content.", CreateMetadata());

        // Assert
        capturedConcept.ShouldNotBeNull();
        capturedConcept.Id.ShouldBe("concept:amazon-neptune");
    }

    // --- Section IDs ---

    [Fact]
    public async Task IngestDocumentAsync_SectionId_UsesDocumentIdPrefix()
    {
        // Arrange
        var capturedSections = new List<SectionNode>();
        _graphMock
            .Setup(g => g.UpsertSectionAsync(It.IsAny<SectionNode>(), It.IsAny<CancellationToken>()))
            .Callback<SectionNode, CancellationToken>((s, _) => capturedSections.Add(s))
            .Returns(Task.CompletedTask);
        SetupDefaultMocks("Content.",
            chunks: [new ChunkInfo(0, "Getting Started", 0, 1, "Content.")],
            headers: [new HeaderInfo(2, "Getting Started", "Getting Started", 0, 0, 20)]);
        var service = CreateService();

        // Act
        await service.IngestDocumentAsync("Content.", CreateMetadata());

        // Assert
        capturedSections.Count.ShouldBe(1);
        capturedSections[0].Id.ShouldBe("repo:docs/test.md:getting-started");
        capturedSections[0].DocumentId.ShouldBe("repo:docs/test.md");
    }
}
