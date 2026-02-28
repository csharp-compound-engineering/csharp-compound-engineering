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
    // --- Happy path ---

    [Fact]
    public async Task IngestDocumentAsync_SimpleDocument_CreatesGraphAndVectorEntries()
    {
        // Arrange
        var graphMock = new Mock<IGraphRepository>();
        var vectorMock = new Mock<IVectorStore>();
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var entityMock = new Mock<IEntityExtractor>();
        var markdownParserMock = new Mock<IMarkdownParser>();
        var frontmatterParserMock = new Mock<IFrontmatterParser>();

        var body = "Some content here.";
        frontmatterParserMock.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(FrontmatterResult.NoFrontmatter(body));
        var doc = new MarkdownDocument();
        markdownParserMock.Setup(p => p.Parse(It.IsAny<string>())).Returns(doc);
        markdownParserMock.Setup(p => p.ExtractHeaders(doc))
            .Returns([new HeaderInfo(2, "Section One", "Section One", 0, 0, 20)]);
        markdownParserMock.Setup(p => p.ExtractLinks(doc)).Returns(new List<LinkInfo>());
        markdownParserMock.Setup(p => p.ExtractCodeBlocks(doc)).Returns(new List<ParsedCodeBlock>());
        markdownParserMock.Setup(p => p.ChunkByHeaders(It.IsAny<string>(), It.IsAny<int>()))
            .Returns([new ChunkInfo(0, "Section One", 0, 1, "Some content here.")]);
        embeddingMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f, 0.3f]);
        entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new DocumentIngestionService(
            graphMock.Object, vectorMock.Object, embeddingMock.Object,
            entityMock.Object, markdownParserMock.Object, frontmatterParserMock.Object,
            NullLogger<DocumentIngestionService>.Instance);
        var metadata = new DocumentIngestionMetadata
        {
            DocumentId = "repo:docs/test.md", Repository = "repo",
            FilePath = "docs/test.md", Title = "Test Document"
        };

        // Act
        await service.IngestDocumentAsync("Some content here.", metadata);

        // Assert
        graphMock.Verify(g => g.UpsertDocumentAsync(
            It.Is<DocumentNode>(d => d.Id == "repo:docs/test.md"),
            It.IsAny<CancellationToken>()), Times.Once);
        graphMock.Verify(g => g.UpsertSectionAsync(
            It.IsAny<SectionNode>(), It.IsAny<CancellationToken>()), Times.Once);
        graphMock.Verify(g => g.UpsertChunkAsync(
            It.IsAny<ChunkNode>(), It.IsAny<CancellationToken>()), Times.Once);
        vectorMock.Verify(v => v.IndexAsync(
            It.IsAny<string>(), It.IsAny<float[]>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestDocumentAsync_MultipleH2Sections_CreatesSectionNodePerH2()
    {
        // Arrange
        var graphMock = new Mock<IGraphRepository>();
        var vectorMock = new Mock<IVectorStore>();
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var entityMock = new Mock<IEntityExtractor>();
        var markdownParserMock = new Mock<IMarkdownParser>();
        var frontmatterParserMock = new Mock<IFrontmatterParser>();

        var body = "Content.";
        frontmatterParserMock.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(FrontmatterResult.NoFrontmatter(body));
        var doc = new MarkdownDocument();
        markdownParserMock.Setup(p => p.Parse(It.IsAny<string>())).Returns(doc);
        markdownParserMock.Setup(p => p.ExtractHeaders(doc))
            .Returns([
                new HeaderInfo(2, "First", "First", 0, 0, 10),
                new HeaderInfo(2, "Second", "Second", 2, 20, 30)
            ]);
        markdownParserMock.Setup(p => p.ExtractLinks(doc)).Returns(new List<LinkInfo>());
        markdownParserMock.Setup(p => p.ExtractCodeBlocks(doc)).Returns(new List<ParsedCodeBlock>());
        markdownParserMock.Setup(p => p.ChunkByHeaders(It.IsAny<string>(), It.IsAny<int>()))
            .Returns([
                new ChunkInfo(0, "First", 0, 1, "Content."),
                new ChunkInfo(1, "Second", 2, 3, "More content.")
            ]);
        embeddingMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f, 0.3f]);
        entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new DocumentIngestionService(
            graphMock.Object, vectorMock.Object, embeddingMock.Object,
            entityMock.Object, markdownParserMock.Object, frontmatterParserMock.Object,
            NullLogger<DocumentIngestionService>.Instance);
        var metadata = new DocumentIngestionMetadata
        {
            DocumentId = "repo:docs/test.md", Repository = "repo",
            FilePath = "docs/test.md", Title = "Test Document"
        };

        // Act
        await service.IngestDocumentAsync("Content.", metadata);

        // Assert
        graphMock.Verify(g => g.UpsertSectionAsync(
            It.IsAny<SectionNode>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task IngestDocumentAsync_H3SubsectionsGroupedUnderParentH2()
    {
        // Arrange
        var graphMock = new Mock<IGraphRepository>();
        var vectorMock = new Mock<IVectorStore>();
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var entityMock = new Mock<IEntityExtractor>();
        var markdownParserMock = new Mock<IMarkdownParser>();
        var frontmatterParserMock = new Mock<IFrontmatterParser>();

        var body = "Content.";
        frontmatterParserMock.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(FrontmatterResult.NoFrontmatter(body));
        var doc = new MarkdownDocument();
        markdownParserMock.Setup(p => p.Parse(It.IsAny<string>())).Returns(doc);
        markdownParserMock.Setup(p => p.ExtractHeaders(doc))
            .Returns([
                new HeaderInfo(2, "Parent", "Parent", 0, 0, 10),
                new HeaderInfo(3, "Child", "Parent > Child", 2, 20, 30)
            ]);
        markdownParserMock.Setup(p => p.ExtractLinks(doc)).Returns(new List<LinkInfo>());
        markdownParserMock.Setup(p => p.ExtractCodeBlocks(doc)).Returns(new List<ParsedCodeBlock>());
        markdownParserMock.Setup(p => p.ChunkByHeaders(It.IsAny<string>(), It.IsAny<int>()))
            .Returns([
                new ChunkInfo(0, "Parent", 0, 1, "Parent content."),
                new ChunkInfo(1, "Parent > Child", 2, 3, "Child content.")
            ]);
        embeddingMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f, 0.3f]);
        entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new DocumentIngestionService(
            graphMock.Object, vectorMock.Object, embeddingMock.Object,
            entityMock.Object, markdownParserMock.Object, frontmatterParserMock.Object,
            NullLogger<DocumentIngestionService>.Instance);
        var metadata = new DocumentIngestionMetadata
        {
            DocumentId = "repo:docs/test.md", Repository = "repo",
            FilePath = "docs/test.md", Title = "Test Document"
        };

        // Act
        await service.IngestDocumentAsync("Content.", metadata);

        // Assert
        graphMock.Verify(g => g.UpsertSectionAsync(
            It.Is<SectionNode>(s => s.Title == "Parent"),
            It.IsAny<CancellationToken>()), Times.Once);
        graphMock.Verify(g => g.UpsertChunkAsync(
            It.IsAny<ChunkNode>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task IngestDocumentAsync_PreHeaderContent_CreatesIntroSection()
    {
        // Arrange
        var graphMock = new Mock<IGraphRepository>();
        var vectorMock = new Mock<IVectorStore>();
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var entityMock = new Mock<IEntityExtractor>();
        var markdownParserMock = new Mock<IMarkdownParser>();
        var frontmatterParserMock = new Mock<IFrontmatterParser>();

        var body = "Some introduction text.\n\nSection content.";
        frontmatterParserMock.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(FrontmatterResult.NoFrontmatter(body));
        var doc = new MarkdownDocument();
        markdownParserMock.Setup(p => p.Parse(It.IsAny<string>())).Returns(doc);
        markdownParserMock.Setup(p => p.ExtractHeaders(doc))
            .Returns([new HeaderInfo(2, "Section", "Section", 2, 30, 40)]);
        markdownParserMock.Setup(p => p.ExtractLinks(doc)).Returns(new List<LinkInfo>());
        markdownParserMock.Setup(p => p.ExtractCodeBlocks(doc)).Returns(new List<ParsedCodeBlock>());
        markdownParserMock.Setup(p => p.ChunkByHeaders(It.IsAny<string>(), It.IsAny<int>()))
            .Returns([
                new ChunkInfo(0, "", 0, 1, "Some introduction text."),
                new ChunkInfo(1, "Section", 2, 3, "Section content.")
            ]);
        embeddingMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f, 0.3f]);
        entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new DocumentIngestionService(
            graphMock.Object, vectorMock.Object, embeddingMock.Object,
            entityMock.Object, markdownParserMock.Object, frontmatterParserMock.Object,
            NullLogger<DocumentIngestionService>.Instance);
        var metadata = new DocumentIngestionMetadata
        {
            DocumentId = "repo:docs/test.md", Repository = "repo",
            FilePath = "docs/test.md", Title = "Test Document"
        };

        // Act
        await service.IngestDocumentAsync("Some introduction text.\n\n## Section\n\nContent.", metadata);

        // Assert
        graphMock.Verify(g => g.UpsertSectionAsync(
            It.Is<SectionNode>(s => s.Title == "Introduction"),
            It.IsAny<CancellationToken>()), Times.Once);
        graphMock.Verify(g => g.UpsertSectionAsync(
            It.Is<SectionNode>(s => s.Title == "Section"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestDocumentAsync_NoHeaders_SingleChunkWithEntireContent()
    {
        // Arrange
        var graphMock = new Mock<IGraphRepository>();
        var vectorMock = new Mock<IVectorStore>();
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var entityMock = new Mock<IEntityExtractor>();
        var markdownParserMock = new Mock<IMarkdownParser>();
        var frontmatterParserMock = new Mock<IFrontmatterParser>();

        var body = "Just plain text with no headers at all.";
        frontmatterParserMock.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(FrontmatterResult.NoFrontmatter(body));
        var doc = new MarkdownDocument();
        markdownParserMock.Setup(p => p.Parse(It.IsAny<string>())).Returns(doc);
        markdownParserMock.Setup(p => p.ExtractHeaders(doc)).Returns(new List<HeaderInfo>());
        markdownParserMock.Setup(p => p.ExtractLinks(doc)).Returns(new List<LinkInfo>());
        markdownParserMock.Setup(p => p.ExtractCodeBlocks(doc)).Returns(new List<ParsedCodeBlock>());
        markdownParserMock.Setup(p => p.ChunkByHeaders(It.IsAny<string>(), It.IsAny<int>()))
            .Returns([new ChunkInfo(0, "", 0, 1, body)]);
        embeddingMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f, 0.3f]);
        entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new DocumentIngestionService(
            graphMock.Object, vectorMock.Object, embeddingMock.Object,
            entityMock.Object, markdownParserMock.Object, frontmatterParserMock.Object,
            NullLogger<DocumentIngestionService>.Instance);
        var metadata = new DocumentIngestionMetadata
        {
            DocumentId = "repo:docs/test.md", Repository = "repo",
            FilePath = "docs/test.md", Title = "Test Document"
        };

        // Act
        await service.IngestDocumentAsync(body, metadata);

        // Assert
        graphMock.Verify(g => g.UpsertChunkAsync(
            It.IsAny<ChunkNode>(), It.IsAny<CancellationToken>()), Times.Once);
        graphMock.Verify(g => g.UpsertSectionAsync(
            It.Is<SectionNode>(s => s.Title == "Introduction"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestDocumentAsync_FrontmatterStripped()
    {
        // Arrange
        var graphMock = new Mock<IGraphRepository>();
        var vectorMock = new Mock<IVectorStore>();
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var entityMock = new Mock<IEntityExtractor>();
        var markdownParserMock = new Mock<IMarkdownParser>();
        var frontmatterParserMock = new Mock<IFrontmatterParser>();

        var body = "Body content.";
        frontmatterParserMock.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(FrontmatterResult.Success(
                new Dictionary<string, object?> { ["title"] = "Test" }, body));
        var doc = new MarkdownDocument();
        markdownParserMock.Setup(p => p.Parse(It.IsAny<string>())).Returns(doc);
        markdownParserMock.Setup(p => p.ExtractHeaders(doc))
            .Returns(new List<HeaderInfo> { new(2, "Section", "Section", 0, 0, 10) });
        markdownParserMock.Setup(p => p.ExtractLinks(doc)).Returns(new List<LinkInfo>());
        markdownParserMock.Setup(p => p.ExtractCodeBlocks(doc)).Returns(new List<ParsedCodeBlock>());
        markdownParserMock.Setup(p => p.ChunkByHeaders(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(new List<ChunkInfo> { new(0, "Section", 0, 1, body) });
        embeddingMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f, 0.3f]);
        entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new DocumentIngestionService(
            graphMock.Object, vectorMock.Object, embeddingMock.Object,
            entityMock.Object, markdownParserMock.Object, frontmatterParserMock.Object,
            NullLogger<DocumentIngestionService>.Instance);
        var metadata = new DocumentIngestionMetadata
        {
            DocumentId = "repo:docs/test.md", Repository = "repo",
            FilePath = "docs/test.md", Title = "Test Document"
        };

        // Act
        await service.IngestDocumentAsync("---\ntitle: Test\n---\n\nBody content.", metadata);

        // Assert - chunk content should be body without frontmatter
        graphMock.Verify(g => g.UpsertChunkAsync(
            It.Is<ChunkNode>(c => !c.Content.Contains("---")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Entity extraction ---

    [Fact]
    public async Task IngestDocumentAsync_EntitiesExtracted_CreatesConceptNodesAndMentions()
    {
        // Arrange
        var graphMock = new Mock<IGraphRepository>();
        var vectorMock = new Mock<IVectorStore>();
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var entityMock = new Mock<IEntityExtractor>();
        var markdownParserMock = new Mock<IMarkdownParser>();
        var frontmatterParserMock = new Mock<IFrontmatterParser>();

        var body = "Some content about Neptune.";
        frontmatterParserMock.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(FrontmatterResult.NoFrontmatter(body));
        var doc = new MarkdownDocument();
        markdownParserMock.Setup(p => p.Parse(It.IsAny<string>())).Returns(doc);
        markdownParserMock.Setup(p => p.ExtractHeaders(doc))
            .Returns([new HeaderInfo(2, "Section", "Section", 0, 0, 10)]);
        markdownParserMock.Setup(p => p.ExtractLinks(doc)).Returns(new List<LinkInfo>());
        markdownParserMock.Setup(p => p.ExtractCodeBlocks(doc)).Returns(new List<ParsedCodeBlock>());
        markdownParserMock.Setup(p => p.ChunkByHeaders(It.IsAny<string>(), It.IsAny<int>()))
            .Returns([new ChunkInfo(0, "Section", 0, 1, "Some content about Neptune.")]);
        embeddingMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f, 0.3f]);
        entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ExtractedEntity { Name = "Amazon Neptune", Type = "Service" }
            ]);

        var service = new DocumentIngestionService(
            graphMock.Object, vectorMock.Object, embeddingMock.Object,
            entityMock.Object, markdownParserMock.Object, frontmatterParserMock.Object,
            NullLogger<DocumentIngestionService>.Instance);
        var metadata = new DocumentIngestionMetadata
        {
            DocumentId = "repo:docs/test.md", Repository = "repo",
            FilePath = "docs/test.md", Title = "Test Document"
        };

        // Act
        await service.IngestDocumentAsync("Content.", metadata);

        // Assert
        graphMock.Verify(g => g.UpsertConceptAsync(
            It.Is<ConceptNode>(c => c.Name == "Amazon Neptune"),
            It.IsAny<CancellationToken>()), Times.Once);
        graphMock.Verify(g => g.CreateRelationshipAsync(
            It.Is<GraphRelationship>(r => r.Type == "MENTIONS"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Internal links ---

    [Fact]
    public async Task IngestDocumentAsync_InternalLinks_CreatesLinksToRelationships()
    {
        // Arrange
        var graphMock = new Mock<IGraphRepository>();
        var vectorMock = new Mock<IVectorStore>();
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var entityMock = new Mock<IEntityExtractor>();
        var markdownParserMock = new Mock<IMarkdownParser>();
        var frontmatterParserMock = new Mock<IFrontmatterParser>();

        var body = "See other doc for details.";
        frontmatterParserMock.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(FrontmatterResult.NoFrontmatter(body));
        var doc = new MarkdownDocument();
        markdownParserMock.Setup(p => p.Parse(It.IsAny<string>())).Returns(doc);
        markdownParserMock.Setup(p => p.ExtractHeaders(doc))
            .Returns([new HeaderInfo(2, "Section", "Section", 0, 0, 10)]);
        markdownParserMock.Setup(p => p.ExtractLinks(doc))
            .Returns([new LinkInfo("../other.md", "other doc", 1, 5)]);
        markdownParserMock.Setup(p => p.ExtractCodeBlocks(doc)).Returns(new List<ParsedCodeBlock>());
        markdownParserMock.Setup(p => p.ChunkByHeaders(It.IsAny<string>(), It.IsAny<int>()))
            .Returns([new ChunkInfo(0, "Section", 0, 1, "See other doc for details.")]);
        embeddingMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f, 0.3f]);
        entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new DocumentIngestionService(
            graphMock.Object, vectorMock.Object, embeddingMock.Object,
            entityMock.Object, markdownParserMock.Object, frontmatterParserMock.Object,
            NullLogger<DocumentIngestionService>.Instance);
        var metadata = new DocumentIngestionMetadata
        {
            DocumentId = "repo:docs/test.md", Repository = "repo",
            FilePath = "docs/test.md", Title = "Test Document"
        };

        // Act
        await service.IngestDocumentAsync("Content.", metadata);

        // Assert
        graphMock.Verify(g => g.CreateRelationshipAsync(
            It.Is<GraphRelationship>(r => r.Type == "LINKS_TO" && r.TargetId == "repo:other.md"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Error handling ---

    [Fact]
    public async Task IngestDocumentAsync_EmbeddingFails_ContinuesWithoutVector()
    {
        // Arrange
        var graphMock = new Mock<IGraphRepository>();
        var vectorMock = new Mock<IVectorStore>();
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var entityMock = new Mock<IEntityExtractor>();
        var markdownParserMock = new Mock<IMarkdownParser>();
        var frontmatterParserMock = new Mock<IFrontmatterParser>();

        var body = "Content.";
        frontmatterParserMock.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(FrontmatterResult.NoFrontmatter(body));
        var doc = new MarkdownDocument();
        markdownParserMock.Setup(p => p.Parse(It.IsAny<string>())).Returns(doc);
        markdownParserMock.Setup(p => p.ExtractHeaders(doc))
            .Returns([new HeaderInfo(2, "Section", "Section", 0, 0, 10)]);
        markdownParserMock.Setup(p => p.ExtractLinks(doc)).Returns(new List<LinkInfo>());
        markdownParserMock.Setup(p => p.ExtractCodeBlocks(doc)).Returns(new List<ParsedCodeBlock>());
        markdownParserMock.Setup(p => p.ChunkByHeaders(It.IsAny<string>(), It.IsAny<int>()))
            .Returns([new ChunkInfo(0, "Section", 0, 1, "Content.")]);
        embeddingMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Embedding failed"));
        entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new DocumentIngestionService(
            graphMock.Object, vectorMock.Object, embeddingMock.Object,
            entityMock.Object, markdownParserMock.Object, frontmatterParserMock.Object,
            NullLogger<DocumentIngestionService>.Instance);
        var metadata = new DocumentIngestionMetadata
        {
            DocumentId = "repo:docs/test.md", Repository = "repo",
            FilePath = "docs/test.md", Title = "Test Document"
        };

        // Act
        await service.IngestDocumentAsync("Content.", metadata);

        // Assert
        graphMock.Verify(g => g.UpsertChunkAsync(
            It.IsAny<ChunkNode>(), It.IsAny<CancellationToken>()), Times.Once);
        vectorMock.Verify(v => v.IndexAsync(
            It.IsAny<string>(), It.IsAny<float[]>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestDocumentAsync_EntityExtractionFails_ContinuesWithoutConcepts()
    {
        // Arrange
        var graphMock = new Mock<IGraphRepository>();
        var vectorMock = new Mock<IVectorStore>();
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var entityMock = new Mock<IEntityExtractor>();
        var markdownParserMock = new Mock<IMarkdownParser>();
        var frontmatterParserMock = new Mock<IFrontmatterParser>();

        var body = "Content.";
        frontmatterParserMock.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(FrontmatterResult.NoFrontmatter(body));
        var doc = new MarkdownDocument();
        markdownParserMock.Setup(p => p.Parse(It.IsAny<string>())).Returns(doc);
        markdownParserMock.Setup(p => p.ExtractHeaders(doc))
            .Returns([new HeaderInfo(2, "Section", "Section", 0, 0, 10)]);
        markdownParserMock.Setup(p => p.ExtractLinks(doc)).Returns(new List<LinkInfo>());
        markdownParserMock.Setup(p => p.ExtractCodeBlocks(doc)).Returns(new List<ParsedCodeBlock>());
        markdownParserMock.Setup(p => p.ChunkByHeaders(It.IsAny<string>(), It.IsAny<int>()))
            .Returns([new ChunkInfo(0, "Section", 0, 1, "Content.")]);
        embeddingMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f, 0.3f]);
        entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Extraction failed"));

        var service = new DocumentIngestionService(
            graphMock.Object, vectorMock.Object, embeddingMock.Object,
            entityMock.Object, markdownParserMock.Object, frontmatterParserMock.Object,
            NullLogger<DocumentIngestionService>.Instance);
        var metadata = new DocumentIngestionMetadata
        {
            DocumentId = "repo:docs/test.md", Repository = "repo",
            FilePath = "docs/test.md", Title = "Test Document"
        };

        // Act
        await service.IngestDocumentAsync("Content.", metadata);

        // Assert
        graphMock.Verify(g => g.UpsertConceptAsync(
            It.IsAny<ConceptNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestDocumentAsync_VectorIndexFails_ContinuesProcessing()
    {
        // Arrange
        var graphMock = new Mock<IGraphRepository>();
        var vectorMock = new Mock<IVectorStore>();
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var entityMock = new Mock<IEntityExtractor>();
        var markdownParserMock = new Mock<IMarkdownParser>();
        var frontmatterParserMock = new Mock<IFrontmatterParser>();

        var body = "Content.";
        frontmatterParserMock.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(FrontmatterResult.NoFrontmatter(body));
        var doc = new MarkdownDocument();
        markdownParserMock.Setup(p => p.Parse(It.IsAny<string>())).Returns(doc);
        markdownParserMock.Setup(p => p.ExtractHeaders(doc))
            .Returns([
                new HeaderInfo(2, "First", "First", 0, 0, 10),
                new HeaderInfo(2, "Second", "Second", 2, 20, 30)
            ]);
        markdownParserMock.Setup(p => p.ExtractLinks(doc)).Returns(new List<LinkInfo>());
        markdownParserMock.Setup(p => p.ExtractCodeBlocks(doc)).Returns(new List<ParsedCodeBlock>());
        markdownParserMock.Setup(p => p.ChunkByHeaders(It.IsAny<string>(), It.IsAny<int>()))
            .Returns([
                new ChunkInfo(0, "First", 0, 1, "Content one."),
                new ChunkInfo(1, "Second", 2, 3, "Content two.")
            ]);
        embeddingMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f, 0.3f]);
        entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        vectorMock
            .Setup(v => v.IndexAsync(
                It.IsAny<string>(), It.IsAny<float[]>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Index failed"));

        var service = new DocumentIngestionService(
            graphMock.Object, vectorMock.Object, embeddingMock.Object,
            entityMock.Object, markdownParserMock.Object, frontmatterParserMock.Object,
            NullLogger<DocumentIngestionService>.Instance);
        var metadata = new DocumentIngestionMetadata
        {
            DocumentId = "repo:docs/test.md", Repository = "repo",
            FilePath = "docs/test.md", Title = "Test Document"
        };

        // Act
        await service.IngestDocumentAsync("Content.", metadata);

        // Assert - both chunks should still be processed in graph
        graphMock.Verify(g => g.UpsertChunkAsync(
            It.IsAny<ChunkNode>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // --- Vector metadata ---

    [Fact]
    public async Task IngestDocumentAsync_VectorMetadata_ContainsExpectedKeys()
    {
        // Arrange
        var graphMock = new Mock<IGraphRepository>();
        var vectorMock = new Mock<IVectorStore>();
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var entityMock = new Mock<IEntityExtractor>();
        var markdownParserMock = new Mock<IMarkdownParser>();
        var frontmatterParserMock = new Mock<IFrontmatterParser>();

        var body = "Content.";
        frontmatterParserMock.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(FrontmatterResult.NoFrontmatter(body));
        var doc = new MarkdownDocument();
        markdownParserMock.Setup(p => p.Parse(It.IsAny<string>())).Returns(doc);
        markdownParserMock.Setup(p => p.ExtractHeaders(doc))
            .Returns([new HeaderInfo(2, "Section", "Section", 0, 0, 10)]);
        markdownParserMock.Setup(p => p.ExtractLinks(doc)).Returns(new List<LinkInfo>());
        markdownParserMock.Setup(p => p.ExtractCodeBlocks(doc)).Returns(new List<ParsedCodeBlock>());
        markdownParserMock.Setup(p => p.ChunkByHeaders(It.IsAny<string>(), It.IsAny<int>()))
            .Returns([new ChunkInfo(0, "Section", 0, 1, "Content.")]);
        embeddingMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f, 0.3f]);
        entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        Dictionary<string, string>? capturedMetadata = null;
        vectorMock
            .Setup(v => v.IndexAsync(
                It.IsAny<string>(), It.IsAny<float[]>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, float[], Dictionary<string, string>, CancellationToken>(
                (_, _, meta, _) => capturedMetadata = meta)
            .Returns(Task.CompletedTask);

        var service = new DocumentIngestionService(
            graphMock.Object, vectorMock.Object, embeddingMock.Object,
            entityMock.Object, markdownParserMock.Object, frontmatterParserMock.Object,
            NullLogger<DocumentIngestionService>.Instance);
        var metadata = new DocumentIngestionMetadata
        {
            DocumentId = "repo:docs/test.md", Repository = "repo",
            FilePath = "docs/test.md", Title = "Test Document"
        };

        // Act
        await service.IngestDocumentAsync("Content.", metadata);

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
        var graphMock = new Mock<IGraphRepository>();
        var vectorMock = new Mock<IVectorStore>();
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var entityMock = new Mock<IEntityExtractor>();
        var markdownParserMock = new Mock<IMarkdownParser>();
        var frontmatterParserMock = new Mock<IFrontmatterParser>();

        var callOrder = new List<string>();
        vectorMock
            .Setup(v => v.DeleteByDocumentIdAsync("doc-1", It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("vector"))
            .Returns(Task.CompletedTask);
        graphMock
            .Setup(g => g.DeleteDocumentCascadeAsync("doc-1", It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("graph"))
            .Returns(Task.CompletedTask);

        var service = new DocumentIngestionService(
            graphMock.Object, vectorMock.Object, embeddingMock.Object,
            entityMock.Object, markdownParserMock.Object, frontmatterParserMock.Object,
            NullLogger<DocumentIngestionService>.Instance);

        // Act
        await service.DeleteDocumentAsync("doc-1");

        // Assert
        callOrder.ShouldBe(["vector", "graph"]);
    }

    [Fact]
    public async Task DeleteDocumentAsync_PropagatesErrors()
    {
        // Arrange
        var graphMock = new Mock<IGraphRepository>();
        var vectorMock = new Mock<IVectorStore>();
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var entityMock = new Mock<IEntityExtractor>();
        var markdownParserMock = new Mock<IMarkdownParser>();
        var frontmatterParserMock = new Mock<IFrontmatterParser>();

        vectorMock
            .Setup(v => v.DeleteByDocumentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Delete failed"));

        var service = new DocumentIngestionService(
            graphMock.Object, vectorMock.Object, embeddingMock.Object,
            entityMock.Object, markdownParserMock.Object, frontmatterParserMock.Object,
            NullLogger<DocumentIngestionService>.Instance);

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
        var graphMock = new Mock<IGraphRepository>();
        var vectorMock = new Mock<IVectorStore>();
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var entityMock = new Mock<IEntityExtractor>();
        var markdownParserMock = new Mock<IMarkdownParser>();
        var frontmatterParserMock = new Mock<IFrontmatterParser>();

        DocumentNode? capturedDoc = null;
        graphMock
            .Setup(g => g.UpsertDocumentAsync(It.IsAny<DocumentNode>(), It.IsAny<CancellationToken>()))
            .Callback<DocumentNode, CancellationToken>((d, _) => capturedDoc = d)
            .Returns(Task.CompletedTask);

        var body = "Content.";
        frontmatterParserMock.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(FrontmatterResult.NoFrontmatter(body));
        var doc = new MarkdownDocument();
        markdownParserMock.Setup(p => p.Parse(It.IsAny<string>())).Returns(doc);
        markdownParserMock.Setup(p => p.ExtractHeaders(doc))
            .Returns([new HeaderInfo(2, "Section", "Section", 0, 0, 10)]);
        markdownParserMock.Setup(p => p.ExtractLinks(doc)).Returns(new List<LinkInfo>());
        markdownParserMock.Setup(p => p.ExtractCodeBlocks(doc)).Returns(new List<ParsedCodeBlock>());
        markdownParserMock.Setup(p => p.ChunkByHeaders(It.IsAny<string>(), It.IsAny<int>()))
            .Returns([new ChunkInfo(0, "Section", 0, 1, "Content.")]);
        embeddingMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f, 0.3f]);
        entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new DocumentIngestionService(
            graphMock.Object, vectorMock.Object, embeddingMock.Object,
            entityMock.Object, markdownParserMock.Object, frontmatterParserMock.Object,
            NullLogger<DocumentIngestionService>.Instance);
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
        var graphMock = new Mock<IGraphRepository>();
        var vectorMock = new Mock<IVectorStore>();
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var entityMock = new Mock<IEntityExtractor>();
        var markdownParserMock = new Mock<IMarkdownParser>();
        var frontmatterParserMock = new Mock<IFrontmatterParser>();

        var capturedChunks = new List<ChunkNode>();
        graphMock
            .Setup(g => g.UpsertChunkAsync(It.IsAny<ChunkNode>(), It.IsAny<CancellationToken>()))
            .Callback<ChunkNode, CancellationToken>((c, _) => capturedChunks.Add(c))
            .Returns(Task.CompletedTask);

        var body = "Content.";
        frontmatterParserMock.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(FrontmatterResult.NoFrontmatter(body));
        var doc = new MarkdownDocument();
        markdownParserMock.Setup(p => p.Parse(It.IsAny<string>())).Returns(doc);
        markdownParserMock.Setup(p => p.ExtractHeaders(doc))
            .Returns([
                new HeaderInfo(2, "Alpha", "Alpha", 0, 0, 10),
                new HeaderInfo(2, "Beta", "Beta", 2, 20, 30)
            ]);
        markdownParserMock.Setup(p => p.ExtractLinks(doc)).Returns(new List<LinkInfo>());
        markdownParserMock.Setup(p => p.ExtractCodeBlocks(doc)).Returns(new List<ParsedCodeBlock>());
        markdownParserMock.Setup(p => p.ChunkByHeaders(It.IsAny<string>(), It.IsAny<int>()))
            .Returns([
                new ChunkInfo(0, "Alpha", 0, 1, "First."),
                new ChunkInfo(1, "Beta", 2, 3, "Second.")
            ]);
        embeddingMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f, 0.3f]);
        entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new DocumentIngestionService(
            graphMock.Object, vectorMock.Object, embeddingMock.Object,
            entityMock.Object, markdownParserMock.Object, frontmatterParserMock.Object,
            NullLogger<DocumentIngestionService>.Instance);
        var metadata = new DocumentIngestionMetadata
        {
            DocumentId = "repo:docs/test.md", Repository = "repo",
            FilePath = "docs/test.md", Title = "Test Document"
        };

        // Act
        await service.IngestDocumentAsync("Content.", metadata);

        // Assert
        capturedChunks.Count.ShouldBe(2);
        capturedChunks[0].Order.ShouldBe(0);
        capturedChunks[1].Order.ShouldBe(1);
    }

    [Fact]
    public async Task IngestDocumentAsync_ChunkTokenCount_IsEstimated()
    {
        // Arrange
        var graphMock = new Mock<IGraphRepository>();
        var vectorMock = new Mock<IVectorStore>();
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var entityMock = new Mock<IEntityExtractor>();
        var markdownParserMock = new Mock<IMarkdownParser>();
        var frontmatterParserMock = new Mock<IFrontmatterParser>();

        ChunkNode? capturedChunk = null;
        graphMock
            .Setup(g => g.UpsertChunkAsync(It.IsAny<ChunkNode>(), It.IsAny<CancellationToken>()))
            .Callback<ChunkNode, CancellationToken>((c, _) => capturedChunk = c)
            .Returns(Task.CompletedTask);

        var body = "Some content here.";
        frontmatterParserMock.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(FrontmatterResult.NoFrontmatter(body));
        var doc = new MarkdownDocument();
        markdownParserMock.Setup(p => p.Parse(It.IsAny<string>())).Returns(doc);
        markdownParserMock.Setup(p => p.ExtractHeaders(doc))
            .Returns([new HeaderInfo(2, "Section", "Section", 0, 0, 10)]);
        markdownParserMock.Setup(p => p.ExtractLinks(doc)).Returns(new List<LinkInfo>());
        markdownParserMock.Setup(p => p.ExtractCodeBlocks(doc)).Returns(new List<ParsedCodeBlock>());
        markdownParserMock.Setup(p => p.ChunkByHeaders(It.IsAny<string>(), It.IsAny<int>()))
            .Returns([new ChunkInfo(0, "Section", 0, 1, "Some content here.")]);
        embeddingMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f, 0.3f]);
        entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new DocumentIngestionService(
            graphMock.Object, vectorMock.Object, embeddingMock.Object,
            entityMock.Object, markdownParserMock.Object, frontmatterParserMock.Object,
            NullLogger<DocumentIngestionService>.Instance);
        var metadata = new DocumentIngestionMetadata
        {
            DocumentId = "repo:docs/test.md", Repository = "repo",
            FilePath = "docs/test.md", Title = "Test Document"
        };

        // Act
        await service.IngestDocumentAsync("Content.", metadata);

        // Assert
        capturedChunk.ShouldNotBeNull();
        capturedChunk.TokenCount.ShouldBeGreaterThan(0);
    }

    // --- Concept ID in graph ---

    [Fact]
    public async Task IngestDocumentAsync_ConceptId_IsNormalized()
    {
        // Arrange
        var graphMock = new Mock<IGraphRepository>();
        var vectorMock = new Mock<IVectorStore>();
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var entityMock = new Mock<IEntityExtractor>();
        var markdownParserMock = new Mock<IMarkdownParser>();
        var frontmatterParserMock = new Mock<IFrontmatterParser>();

        var body = "Content.";
        frontmatterParserMock.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(FrontmatterResult.NoFrontmatter(body));
        var doc = new MarkdownDocument();
        markdownParserMock.Setup(p => p.Parse(It.IsAny<string>())).Returns(doc);
        markdownParserMock.Setup(p => p.ExtractHeaders(doc))
            .Returns([new HeaderInfo(2, "Section", "Section", 0, 0, 10)]);
        markdownParserMock.Setup(p => p.ExtractLinks(doc)).Returns(new List<LinkInfo>());
        markdownParserMock.Setup(p => p.ExtractCodeBlocks(doc)).Returns(new List<ParsedCodeBlock>());
        markdownParserMock.Setup(p => p.ChunkByHeaders(It.IsAny<string>(), It.IsAny<int>()))
            .Returns([new ChunkInfo(0, "Section", 0, 1, "Content.")]);
        embeddingMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f, 0.3f]);
        entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ExtractedEntity { Name = "Amazon Neptune", Type = "Service" }
            ]);
        ConceptNode? capturedConcept = null;
        graphMock
            .Setup(g => g.UpsertConceptAsync(It.IsAny<ConceptNode>(), It.IsAny<CancellationToken>()))
            .Callback<ConceptNode, CancellationToken>((c, _) => capturedConcept = c)
            .Returns(Task.CompletedTask);

        var service = new DocumentIngestionService(
            graphMock.Object, vectorMock.Object, embeddingMock.Object,
            entityMock.Object, markdownParserMock.Object, frontmatterParserMock.Object,
            NullLogger<DocumentIngestionService>.Instance);
        var metadata = new DocumentIngestionMetadata
        {
            DocumentId = "repo:docs/test.md", Repository = "repo",
            FilePath = "docs/test.md", Title = "Test Document"
        };

        // Act
        await service.IngestDocumentAsync("Content.", metadata);

        // Assert
        capturedConcept.ShouldNotBeNull();
        capturedConcept.Id.ShouldBe("concept:amazon-neptune");
    }

    // --- Section IDs ---

    [Fact]
    public async Task IngestDocumentAsync_SectionId_UsesDocumentIdPrefix()
    {
        // Arrange
        var graphMock = new Mock<IGraphRepository>();
        var vectorMock = new Mock<IVectorStore>();
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var entityMock = new Mock<IEntityExtractor>();
        var markdownParserMock = new Mock<IMarkdownParser>();
        var frontmatterParserMock = new Mock<IFrontmatterParser>();

        var capturedSections = new List<SectionNode>();
        graphMock
            .Setup(g => g.UpsertSectionAsync(It.IsAny<SectionNode>(), It.IsAny<CancellationToken>()))
            .Callback<SectionNode, CancellationToken>((s, _) => capturedSections.Add(s))
            .Returns(Task.CompletedTask);

        var body = "Content.";
        frontmatterParserMock.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(FrontmatterResult.NoFrontmatter(body));
        var doc = new MarkdownDocument();
        markdownParserMock.Setup(p => p.Parse(It.IsAny<string>())).Returns(doc);
        markdownParserMock.Setup(p => p.ExtractHeaders(doc))
            .Returns([new HeaderInfo(2, "Getting Started", "Getting Started", 0, 0, 20)]);
        markdownParserMock.Setup(p => p.ExtractLinks(doc)).Returns(new List<LinkInfo>());
        markdownParserMock.Setup(p => p.ExtractCodeBlocks(doc)).Returns(new List<ParsedCodeBlock>());
        markdownParserMock.Setup(p => p.ChunkByHeaders(It.IsAny<string>(), It.IsAny<int>()))
            .Returns([new ChunkInfo(0, "Getting Started", 0, 1, "Content.")]);
        embeddingMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f, 0.3f]);
        entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new DocumentIngestionService(
            graphMock.Object, vectorMock.Object, embeddingMock.Object,
            entityMock.Object, markdownParserMock.Object, frontmatterParserMock.Object,
            NullLogger<DocumentIngestionService>.Instance);
        var metadata = new DocumentIngestionMetadata
        {
            DocumentId = "repo:docs/test.md", Repository = "repo",
            FilePath = "docs/test.md", Title = "Test Document"
        };

        // Act
        await service.IngestDocumentAsync("Content.", metadata);

        // Assert
        capturedSections.Count.ShouldBe(1);
        capturedSections[0].Id.ShouldBe("repo:docs/test.md:getting-started");
        capturedSections[0].DocumentId.ShouldBe("repo:docs/test.md");
    }
}
