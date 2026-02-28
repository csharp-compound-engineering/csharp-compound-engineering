using CompoundDocs.Bedrock;
using CompoundDocs.Common.Models;
using CompoundDocs.Common.Parsing;
using CompoundDocs.Graph;
using CompoundDocs.GraphRag;
using CompoundDocs.Vector;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using ExtractedEntity = CompoundDocs.GraphRag.ExtractedEntity;

namespace CompoundDocs.Tests.Unit.GraphRag;

public sealed class DocumentIngestionServiceTests
{
    private readonly Mock<IGraphRepository> _graphMock = new();
    private readonly Mock<IVectorStore> _vectorMock = new();
    private readonly Mock<IBedrockEmbeddingService> _embeddingMock = new();
    private readonly Mock<IEntityExtractor> _entityMock = new();
    private readonly MarkdownParser _markdownParser = new();
    private readonly FrontmatterParser _frontmatterParser = new();
    private readonly DocumentIngestionService _service;

    public DocumentIngestionServiceTests()
    {
        _embeddingMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f, 0.3f]);

        _entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _service = new DocumentIngestionService(
            _graphMock.Object,
            _vectorMock.Object,
            _embeddingMock.Object,
            _entityMock.Object,
            _markdownParser,
            _frontmatterParser,
            NullLogger<DocumentIngestionService>.Instance);
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
        var content = "## Section One\n\nSome content here.";
        var metadata = CreateMetadata();

        await _service.IngestDocumentAsync(content, metadata);

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
        var content = "## First\n\nContent.\n\n## Second\n\nMore content.";
        var metadata = CreateMetadata();

        await _service.IngestDocumentAsync(content, metadata);

        _graphMock.Verify(g => g.UpsertSectionAsync(
            It.IsAny<SectionNode>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task IngestDocumentAsync_H3SubsectionsGroupedUnderParentH2()
    {
        var content = "## Parent\n\n### Child\n\nChild content.";
        var metadata = CreateMetadata();

        await _service.IngestDocumentAsync(content, metadata);

        // One H2 section (H3 does not create a separate SectionNode)
        _graphMock.Verify(g => g.UpsertSectionAsync(
            It.Is<SectionNode>(s => s.Title == "Parent"),
            It.IsAny<CancellationToken>()), Times.Once);
        // Two chunks (one for ## Parent, one for ### Child)
        _graphMock.Verify(g => g.UpsertChunkAsync(
            It.IsAny<ChunkNode>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task IngestDocumentAsync_PreHeaderContent_CreatesIntroSection()
    {
        var content = "Some introduction text.\n\n## Section\n\nContent.";
        var metadata = CreateMetadata();

        await _service.IngestDocumentAsync(content, metadata);

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
        var content = "Just plain text with no headers at all.";
        var metadata = CreateMetadata();

        await _service.IngestDocumentAsync(content, metadata);

        _graphMock.Verify(g => g.UpsertChunkAsync(
            It.IsAny<ChunkNode>(), It.IsAny<CancellationToken>()), Times.Once);
        // Intro section created for content without any headers
        _graphMock.Verify(g => g.UpsertSectionAsync(
            It.Is<SectionNode>(s => s.Title == "Introduction"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestDocumentAsync_FrontmatterStripped()
    {
        var content = "---\ntitle: Test\n---\n\n## Section\n\nBody content.";
        var metadata = CreateMetadata();

        await _service.IngestDocumentAsync(content, metadata);

        // The chunk content should not contain frontmatter
        _graphMock.Verify(g => g.UpsertChunkAsync(
            It.Is<ChunkNode>(c => !c.Content.Contains("---")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Entity extraction ---

    [Fact]
    public async Task IngestDocumentAsync_EntitiesExtracted_CreatesConceptNodesAndMentions()
    {
        _entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ExtractedEntity { Name = "Amazon Neptune", Type = "Service" }
            ]);

        var content = "## Section\n\nSome content about Neptune.";
        var metadata = CreateMetadata();

        await _service.IngestDocumentAsync(content, metadata);

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
        var content = "## Section\n\nSee [other doc](../other.md) for details.";
        var metadata = CreateMetadata();

        await _service.IngestDocumentAsync(content, metadata);

        _graphMock.Verify(g => g.CreateRelationshipAsync(
            It.Is<GraphRelationship>(r => r.Type == "LINKS_TO" && r.TargetId == "repo:other.md"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Error handling ---

    [Fact]
    public async Task IngestDocumentAsync_EmbeddingFails_ContinuesWithoutVector()
    {
        _embeddingMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Embedding failed"));

        var content = "## Section\n\nContent.";
        var metadata = CreateMetadata();

        await _service.IngestDocumentAsync(content, metadata);

        // Graph operations should still succeed
        _graphMock.Verify(g => g.UpsertChunkAsync(
            It.IsAny<ChunkNode>(), It.IsAny<CancellationToken>()), Times.Once);
        // Vector index should not be called since embedding failed
        _vectorMock.Verify(v => v.IndexAsync(
            It.IsAny<string>(), It.IsAny<float[]>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestDocumentAsync_EntityExtractionFails_ContinuesWithoutConcepts()
    {
        _entityMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Extraction failed"));

        var content = "## Section\n\nContent.";
        var metadata = CreateMetadata();

        await _service.IngestDocumentAsync(content, metadata);

        _graphMock.Verify(g => g.UpsertConceptAsync(
            It.IsAny<ConceptNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestDocumentAsync_VectorIndexFails_ContinuesProcessing()
    {
        _vectorMock
            .Setup(v => v.IndexAsync(
                It.IsAny<string>(), It.IsAny<float[]>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Index failed"));

        var content = "## First\n\nContent one.\n\n## Second\n\nContent two.";
        var metadata = CreateMetadata();

        await _service.IngestDocumentAsync(content, metadata);

        // Both chunks should still be processed in graph
        _graphMock.Verify(g => g.UpsertChunkAsync(
            It.IsAny<ChunkNode>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // --- Vector metadata ---

    [Fact]
    public async Task IngestDocumentAsync_VectorMetadata_ContainsExpectedKeys()
    {
        Dictionary<string, string>? capturedMetadata = null;
        _vectorMock
            .Setup(v => v.IndexAsync(
                It.IsAny<string>(), It.IsAny<float[]>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, float[], Dictionary<string, string>, CancellationToken>(
                (_, _, meta, _) => capturedMetadata = meta)
            .Returns(Task.CompletedTask);

        var content = "## Section\n\nContent.";
        var metadata = CreateMetadata();

        await _service.IngestDocumentAsync(content, metadata);

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
        var callOrder = new List<string>();
        _vectorMock
            .Setup(v => v.DeleteByDocumentIdAsync("doc-1", It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("vector"))
            .Returns(Task.CompletedTask);
        _graphMock
            .Setup(g => g.DeleteDocumentCascadeAsync("doc-1", It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("graph"))
            .Returns(Task.CompletedTask);

        await _service.DeleteDocumentAsync("doc-1");

        callOrder.ShouldBe(["vector", "graph"]);
    }

    [Fact]
    public async Task DeleteDocumentAsync_PropagatesErrors()
    {
        _vectorMock
            .Setup(v => v.DeleteByDocumentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Delete failed"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => _service.DeleteDocumentAsync("doc-1"));
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
        // Path.GetDirectoryName("/") returns null on .NET
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
        DocumentNode? capturedDoc = null;
        _graphMock
            .Setup(g => g.UpsertDocumentAsync(It.IsAny<DocumentNode>(), It.IsAny<CancellationToken>()))
            .Callback<DocumentNode, CancellationToken>((d, _) => capturedDoc = d)
            .Returns(Task.CompletedTask);

        var content = "## Section\n\nContent.";
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

        await _service.IngestDocumentAsync(content, metadata);

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
        var capturedChunks = new List<ChunkNode>();
        _graphMock
            .Setup(g => g.UpsertChunkAsync(It.IsAny<ChunkNode>(), It.IsAny<CancellationToken>()))
            .Callback<ChunkNode, CancellationToken>((c, _) => capturedChunks.Add(c))
            .Returns(Task.CompletedTask);

        var content = "## Alpha\n\nFirst.\n\n## Beta\n\nSecond.";
        var metadata = CreateMetadata();

        await _service.IngestDocumentAsync(content, metadata);

        capturedChunks.Count.ShouldBe(2);
        capturedChunks[0].Order.ShouldBe(0);
        capturedChunks[1].Order.ShouldBe(1);
    }

    [Fact]
    public async Task IngestDocumentAsync_ChunkTokenCount_IsEstimated()
    {
        ChunkNode? capturedChunk = null;
        _graphMock
            .Setup(g => g.UpsertChunkAsync(It.IsAny<ChunkNode>(), It.IsAny<CancellationToken>()))
            .Callback<ChunkNode, CancellationToken>((c, _) => capturedChunk = c)
            .Returns(Task.CompletedTask);

        var content = "## Section\n\nSome content here.";
        var metadata = CreateMetadata();

        await _service.IngestDocumentAsync(content, metadata);

        capturedChunk.ShouldNotBeNull();
        capturedChunk.TokenCount.ShouldBeGreaterThan(0);
    }

    // --- Concept ID in graph ---

    [Fact]
    public async Task IngestDocumentAsync_ConceptId_IsNormalized()
    {
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

        var content = "## Section\n\nContent.";
        var metadata = CreateMetadata();

        await _service.IngestDocumentAsync(content, metadata);

        capturedConcept.ShouldNotBeNull();
        capturedConcept.Id.ShouldBe("concept:amazon-neptune");
    }

    // --- Section IDs ---

    [Fact]
    public async Task IngestDocumentAsync_SectionId_UsesDocumentIdPrefix()
    {
        var capturedSections = new List<SectionNode>();
        _graphMock
            .Setup(g => g.UpsertSectionAsync(It.IsAny<SectionNode>(), It.IsAny<CancellationToken>()))
            .Callback<SectionNode, CancellationToken>((s, _) => capturedSections.Add(s))
            .Returns(Task.CompletedTask);

        var content = "## Getting Started\n\nContent.";
        var metadata = CreateMetadata();

        await _service.IngestDocumentAsync(content, metadata);

        capturedSections.Count.ShouldBe(1);
        capturedSections[0].Id.ShouldBe("repo:docs/test.md:getting-started");
        capturedSections[0].DocumentId.ShouldBe("repo:docs/test.md");
    }
}
