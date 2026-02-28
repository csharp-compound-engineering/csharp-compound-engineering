using CompoundDocs.Bedrock;
using CompoundDocs.Common.Configuration;
using CompoundDocs.Common.Models;
using CompoundDocs.Graph;
using CompoundDocs.GraphRag;
using CompoundDocs.Vector;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace CompoundDocs.Tests.Unit.GraphRag;

public sealed class GraphRagPipelineQueryTests
{
    #region Happy path

    [Fact]
    public async Task QueryAsync_HappyPath_ReturnsCompleteResult()
    {
        // Arrange
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new() { ChunkId = "chunk-1", Score = 0.9, Metadata = new Dictionary<string, string> { ["document_id"] = "doc-1", ["file_path"] = "/docs/guide.md", ["repository"] = "my-repo" } },
                new() { ChunkId = "chunk-2", Score = 0.85, Metadata = new Dictionary<string, string> { ["document_id"] = "doc-1", ["file_path"] = "/docs/guide.md", ["repository"] = "my-repo" } }
            });
        graphMock
            .Setup(g => g.GetChunksByIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkNode>
            {
                new() { Id = "chunk-1", SectionId = "sec-1", DocumentId = "doc-1", Content = "First chunk", Order = 0, TokenCount = 3 },
                new() { Id = "chunk-2", SectionId = "sec-1", DocumentId = "doc-1", Content = "Second chunk", Order = 0, TokenCount = 3 }
            });
        graphMock
            .Setup(g => g.GetConceptsByChunkIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConceptNode>
            {
                new() { Id = "c1", Name = "DependencyInjection" },
                new() { Id = "c2", Name = "GraphRAG" }
            });
        graphMock
            .Setup(g => g.GetLinkedDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentNode>());
        llmMock
            .Setup(l => l.GenerateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<BedrockMessage>>(), It.IsAny<ModelTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Synthesized answer about DI and GraphRAG.");

        // Act
        var result = await sut.QueryAsync("How does DI work?");

        // Assert
        result.Answer.ShouldBe("Synthesized answer about DI and GraphRAG.");
        result.Sources.Count.ShouldBe(2);
        result.Sources[0].ChunkId.ShouldBe("chunk-1");
        result.Sources[0].DocumentId.ShouldBe("doc-1");
        result.Sources[0].FilePath.ShouldBe("/docs/guide.md");
        result.Sources[0].Repository.ShouldBe("my-repo");
        result.Sources[0].RelevanceScore.ShouldBe(0.9);
        result.Sources[1].ChunkId.ShouldBe("chunk-2");
        result.RelatedConcepts.ShouldBe(["DependencyInjection", "GraphRAG"]);
        result.Confidence.ShouldBeGreaterThan(0);
    }

    #endregion

    #region No vector results

    [Fact]
    public async Task QueryAsync_NoVectorResults_ReturnsEmptyAnswerAndZeroConfidence()
    {
        // Arrange
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>());

        // Act
        var result = await sut.QueryAsync("Unknown query");

        // Assert
        result.Answer.ShouldBe("No relevant documents found for your query.");
        result.Confidence.ShouldBe(0);
        result.Sources.ShouldBeEmpty();
        result.RelatedConcepts.ShouldBeEmpty();

        llmMock.Verify(
            l => l.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<BedrockMessage>>(),
                It.IsAny<ModelTier>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region MinRelevanceScore filtering

    [Fact]
    public async Task QueryAsync_ResultsBelowMinScore_FilteredOut()
    {
        // Arrange
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new() { ChunkId = "chunk-1", Score = 0.9, Metadata = new Dictionary<string, string> { ["document_id"] = "doc-1", ["file_path"] = "/path", ["repository"] = "repo" } },
                new() { ChunkId = "chunk-2", Score = 0.5, Metadata = new Dictionary<string, string> { ["document_id"] = "doc-1", ["file_path"] = "/path", ["repository"] = "repo" } }
            });
        graphMock
            .Setup(g => g.GetChunksByIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkNode> { new() { Id = "chunk-1", SectionId = "sec-1", DocumentId = "doc-1", Content = "chunk content", Order = 0, TokenCount = 3 } });
        graphMock
            .Setup(g => g.GetConceptsByChunkIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConceptNode>());
        graphMock
            .Setup(g => g.GetLinkedDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentNode>());
        llmMock
            .Setup(l => l.GenerateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<BedrockMessage>>(), It.IsAny<ModelTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Answer");

        // Act
        var result = await sut.QueryAsync("query");

        // Assert — chunk-2 with score 0.5 should be filtered (default min=0.7)
        result.Sources.Count.ShouldBe(1);
        result.Sources[0].ChunkId.ShouldBe("chunk-1");
    }

    [Fact]
    public async Task QueryAsync_AllResultsBelowMinScore_ReturnsEmptyAnswer()
    {
        // Arrange
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new() { ChunkId = "chunk-1", Score = 0.3, Metadata = new Dictionary<string, string>() },
                new() { ChunkId = "chunk-2", Score = 0.5, Metadata = new Dictionary<string, string>() }
            });

        // Act
        var result = await sut.QueryAsync("query");

        // Assert
        result.Answer.ShouldBe("No relevant documents found for your query.");
        result.Confidence.ShouldBe(0);

        llmMock.Verify(
            l => l.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<BedrockMessage>>(),
                It.IsAny<ModelTier>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Options override config

    [Fact]
    public async Task QueryAsync_OptionsOverrideConfigDefaults()
    {
        // Arrange
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new() { ChunkId = "chunk-1", Score = 0.5, Metadata = new Dictionary<string, string> { ["document_id"] = "doc-1", ["file_path"] = "/path", ["repository"] = "repo" } }
            });
        graphMock
            .Setup(g => g.GetChunksByIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkNode> { new() { Id = "chunk-1", SectionId = "sec-1", DocumentId = "doc-1", Content = "chunk content", Order = 0, TokenCount = 3 } });
        graphMock
            .Setup(g => g.GetConceptsByChunkIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConceptNode>());
        graphMock
            .Setup(g => g.GetLinkedDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentNode>());
        llmMock
            .Setup(l => l.GenerateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<BedrockMessage>>(), It.IsAny<ModelTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Answer");

        var options = new GraphRagOptions
        {
            MaxChunks = 3,
            MinRelevanceScore = 0.4 // Lower than default 0.7 — chunk-1 at 0.5 should pass
        };

        // Act
        var result = await sut.QueryAsync("query", options);

        // Assert — chunk-1 at 0.5 passes the 0.4 threshold
        result.Sources.Count.ShouldBe(1);

        vectorMock.Verify(v => v.SearchAsync(
            testEmbedding,
            3,
            It.IsAny<Dictionary<string, string>?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task QueryAsync_NullOptions_UsesConfigDefaults()
    {
        // Arrange
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>());

        // Act
        await sut.QueryAsync("query", null);

        // Assert — should use config default maxChunks=10
        vectorMock.Verify(v => v.SearchAsync(
            testEmbedding,
            10,
            null,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Filters

    [Fact]
    public async Task QueryAsync_RepositoryFilter_PassedToSearch()
    {
        // Arrange
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>());

        var options = new GraphRagOptions { RepositoryFilter = "my-repo" };

        // Act
        await sut.QueryAsync("query", options);

        // Assert
        vectorMock.Verify(v => v.SearchAsync(
            testEmbedding,
            It.IsAny<int>(),
            It.Is<Dictionary<string, string>?>(f =>
                f != null &&
                f["repository"] == "my-repo" &&
                f.Count == 1),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task QueryAsync_DocTypeFilter_PassedToSearch()
    {
        // Arrange
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>());

        var options = new GraphRagOptions { DocTypeFilter = "guide" };

        // Act
        await sut.QueryAsync("query", options);

        // Assert
        vectorMock.Verify(v => v.SearchAsync(
            testEmbedding,
            It.IsAny<int>(),
            It.Is<Dictionary<string, string>?>(f =>
                f != null &&
                f["doc_type"] == "guide" &&
                f.Count == 1),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task QueryAsync_BothFilters_BothPassedToSearch()
    {
        // Arrange
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>());

        var options = new GraphRagOptions
        {
            RepositoryFilter = "my-repo",
            DocTypeFilter = "guide"
        };

        // Act
        await sut.QueryAsync("query", options);

        // Assert
        vectorMock.Verify(v => v.SearchAsync(
            testEmbedding,
            It.IsAny<int>(),
            It.Is<Dictionary<string, string>?>(f =>
                f != null &&
                f["repository"] == "my-repo" &&
                f["doc_type"] == "guide" &&
                f.Count == 2),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task QueryAsync_NoFilters_NullPassedToSearch()
    {
        // Arrange
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>());

        var options = new GraphRagOptions(); // No filters set

        // Act
        await sut.QueryAsync("query", options);

        // Assert
        vectorMock.Verify(v => v.SearchAsync(
            testEmbedding,
            It.IsAny<int>(),
            null,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Graph enrichment failures

    [Fact]
    public async Task QueryAsync_ConceptEnrichmentFails_PipelineContinues()
    {
        // Arrange
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new() { ChunkId = "chunk-1", Score = 0.9, Metadata = new Dictionary<string, string> { ["document_id"] = "doc-1", ["file_path"] = "/path", ["repository"] = "repo" } }
            });
        graphMock
            .Setup(g => g.GetChunksByIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkNode> { new() { Id = "chunk-1", SectionId = "sec-1", DocumentId = "doc-1", Content = "chunk content", Order = 0, TokenCount = 3 } });
        graphMock
            .Setup(g => g.GetLinkedDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentNode>());
        llmMock
            .Setup(l => l.GenerateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<BedrockMessage>>(), It.IsAny<ModelTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Answer without concepts");
        graphMock
            .Setup(g => g.GetConceptsByChunkIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Neptune down"));

        // Act
        var result = await sut.QueryAsync("query");

        // Assert — pipeline continues, answer is produced
        result.Answer.ShouldBe("Answer without concepts");
        result.RelatedConcepts.ShouldBeEmpty();
        result.Sources.Count.ShouldBe(1);
    }

    [Fact]
    public async Task QueryAsync_LinkedDocEnrichmentFails_PipelineContinues()
    {
        // Arrange
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new() { ChunkId = "chunk-1", Score = 0.9, Metadata = new Dictionary<string, string> { ["document_id"] = "doc-1", ["file_path"] = "/path", ["repository"] = "repo" } }
            });
        graphMock
            .Setup(g => g.GetChunksByIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkNode> { new() { Id = "chunk-1", SectionId = "sec-1", DocumentId = "doc-1", Content = "chunk content", Order = 0, TokenCount = 3 } });
        graphMock
            .Setup(g => g.GetConceptsByChunkIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConceptNode> { new() { Id = "c1", Name = "Concept" } });
        llmMock
            .Setup(l => l.GenerateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<BedrockMessage>>(), It.IsAny<ModelTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Answer with concept");
        graphMock
            .Setup(g => g.GetLinkedDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Neptune down"));

        // Act
        var result = await sut.QueryAsync("query");

        // Assert — pipeline continues
        result.Answer.ShouldBe("Answer with concept");
        result.RelatedConcepts.ShouldBe(["Concept"]);
    }

    #endregion

    #region Confidence computation

    [Fact]
    public async Task QueryAsync_Confidence_CorrectFormula()
    {
        // Arrange — 2 results out of 10 requested, scores 0.9 and 0.8
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new() { ChunkId = "chunk-1", Score = 0.9, Metadata = new Dictionary<string, string> { ["document_id"] = "doc-1", ["file_path"] = "/path", ["repository"] = "repo" } },
                new() { ChunkId = "chunk-2", Score = 0.8, Metadata = new Dictionary<string, string> { ["document_id"] = "doc-1", ["file_path"] = "/path", ["repository"] = "repo" } }
            });
        graphMock
            .Setup(g => g.GetChunksByIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkNode>
            {
                new() { Id = "chunk-1", SectionId = "sec-1", DocumentId = "doc-1", Content = "chunk content", Order = 0, TokenCount = 3 },
                new() { Id = "chunk-2", SectionId = "sec-1", DocumentId = "doc-1", Content = "chunk content", Order = 0, TokenCount = 3 }
            });
        graphMock
            .Setup(g => g.GetConceptsByChunkIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConceptNode>());
        graphMock
            .Setup(g => g.GetLinkedDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentNode>());
        llmMock
            .Setup(l => l.GenerateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<BedrockMessage>>(), It.IsAny<ModelTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Answer");

        // Act
        var result = await sut.QueryAsync("query");

        // Assert: avg=0.85, coverage=2/10=0.2, confidence=0.85*0.2=0.17
        var expectedConfidence = 0.85 * (2.0 / 10);
        result.Confidence.ShouldBe(expectedConfidence, tolerance: 0.001);
    }

    [Fact]
    public async Task QueryAsync_Confidence_ScaledDownWithFewerResults()
    {
        // Arrange — 1 result out of 5 requested
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new() { ChunkId = "chunk-1", Score = 0.9, Metadata = new Dictionary<string, string> { ["document_id"] = "doc-1", ["file_path"] = "/path", ["repository"] = "repo" } }
            });
        graphMock
            .Setup(g => g.GetChunksByIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkNode> { new() { Id = "chunk-1", SectionId = "sec-1", DocumentId = "doc-1", Content = "chunk content", Order = 0, TokenCount = 3 } });
        graphMock
            .Setup(g => g.GetConceptsByChunkIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConceptNode>());
        graphMock
            .Setup(g => g.GetLinkedDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentNode>());
        llmMock
            .Setup(l => l.GenerateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<BedrockMessage>>(), It.IsAny<ModelTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Answer");

        var options = new GraphRagOptions { MaxChunks = 5 };

        // Act
        var result = await sut.QueryAsync("query", options);

        // Assert: avg=0.9, coverage=1/5=0.2, confidence=0.9*0.2=0.18
        var expectedConfidence = 0.9 * (1.0 / 5);
        result.Confidence.ShouldBe(expectedConfidence, tolerance: 0.001);
    }

    #endregion

    #region LLM interaction

    [Fact]
    public async Task QueryAsync_LlmCalledWithSonnetTier()
    {
        // Arrange
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new() { ChunkId = "chunk-1", Score = 0.9, Metadata = new Dictionary<string, string> { ["document_id"] = "doc-1", ["file_path"] = "/path", ["repository"] = "repo" } }
            });
        graphMock
            .Setup(g => g.GetChunksByIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkNode> { new() { Id = "chunk-1", SectionId = "sec-1", DocumentId = "doc-1", Content = "chunk content", Order = 0, TokenCount = 3 } });
        graphMock
            .Setup(g => g.GetConceptsByChunkIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConceptNode>());
        graphMock
            .Setup(g => g.GetLinkedDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentNode>());
        llmMock
            .Setup(l => l.GenerateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<BedrockMessage>>(), It.IsAny<ModelTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Answer");

        // Act
        await sut.QueryAsync("query");

        // Assert
        llmMock.Verify(l => l.GenerateAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<BedrockMessage>>(),
            ModelTier.Sonnet,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task QueryAsync_SystemPromptContainsRagInstructions()
    {
        // Arrange
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new() { ChunkId = "chunk-1", Score = 0.9, Metadata = new Dictionary<string, string> { ["document_id"] = "doc-1", ["file_path"] = "/path", ["repository"] = "repo" } }
            });
        graphMock
            .Setup(g => g.GetChunksByIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkNode> { new() { Id = "chunk-1", SectionId = "sec-1", DocumentId = "doc-1", Content = "chunk content", Order = 0, TokenCount = 3 } });
        graphMock
            .Setup(g => g.GetConceptsByChunkIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConceptNode>());
        graphMock
            .Setup(g => g.GetLinkedDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentNode>());
        llmMock
            .Setup(l => l.GenerateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<BedrockMessage>>(), It.IsAny<ModelTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Answer");

        // Act
        await sut.QueryAsync("query");

        // Assert
        llmMock.Verify(l => l.GenerateAsync(
            It.Is<string>(s =>
                s.Contains("documentation assistant") &&
                s.Contains("provided context")),
            It.IsAny<IReadOnlyList<BedrockMessage>>(),
            It.IsAny<ModelTier>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task QueryAsync_UserMessageContainsChunkContentAndMetadata()
    {
        // Arrange
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new() { ChunkId = "chunk-1", Score = 0.9, Metadata = new Dictionary<string, string> { ["document_id"] = "doc-1", ["file_path"] = "/docs/guide.md", ["repository"] = "repo" } }
            });
        graphMock
            .Setup(g => g.GetChunksByIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkNode> { new() { Id = "chunk-1", SectionId = "sec-1", DocumentId = "doc-1", Content = "Important content about DI", Order = 0, TokenCount = 6 } });
        graphMock
            .Setup(g => g.GetConceptsByChunkIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConceptNode>());
        graphMock
            .Setup(g => g.GetLinkedDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentNode>());
        llmMock
            .Setup(l => l.GenerateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<BedrockMessage>>(), It.IsAny<ModelTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Answer");

        // Act
        await sut.QueryAsync("How does DI work?");

        // Assert
        llmMock.Verify(l => l.GenerateAsync(
            It.IsAny<string>(),
            It.Is<IReadOnlyList<BedrockMessage>>(msgs =>
                msgs.Count == 1 &&
                msgs[0].Role == "user" &&
                msgs[0].Content.Contains("Important content about DI") &&
                msgs[0].Content.Contains("/docs/guide.md") &&
                msgs[0].Content.Contains("How does DI work?")),
            It.IsAny<ModelTier>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Sources mapping

    [Fact]
    public async Task QueryAsync_SourcesMappedFromVectorResults()
    {
        // Arrange
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new() { ChunkId = "chunk-1", Score = 0.9, Metadata = new Dictionary<string, string> { ["document_id"] = "doc-1", ["file_path"] = "/docs/a.md", ["repository"] = "repo-a" } },
                new() { ChunkId = "chunk-2", Score = 0.8, Metadata = new Dictionary<string, string> { ["document_id"] = "doc-2", ["file_path"] = "/docs/b.md", ["repository"] = "repo-b" } }
            });
        graphMock
            .Setup(g => g.GetChunksByIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkNode>
            {
                new() { Id = "chunk-1", SectionId = "sec-1", DocumentId = "doc-1", Content = "chunk content", Order = 0, TokenCount = 3 },
                new() { Id = "chunk-2", SectionId = "sec-1", DocumentId = "doc-1", Content = "chunk content", Order = 0, TokenCount = 3 }
            });
        graphMock
            .Setup(g => g.GetConceptsByChunkIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConceptNode>());
        graphMock
            .Setup(g => g.GetLinkedDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentNode>());
        llmMock
            .Setup(l => l.GenerateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<BedrockMessage>>(), It.IsAny<ModelTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Answer");

        // Act
        var result = await sut.QueryAsync("query");

        // Assert
        result.Sources.Count.ShouldBe(2);

        result.Sources[0].DocumentId.ShouldBe("doc-1");
        result.Sources[0].ChunkId.ShouldBe("chunk-1");
        result.Sources[0].Repository.ShouldBe("repo-a");
        result.Sources[0].FilePath.ShouldBe("/docs/a.md");
        result.Sources[0].RelevanceScore.ShouldBe(0.9);

        result.Sources[1].DocumentId.ShouldBe("doc-2");
        result.Sources[1].ChunkId.ShouldBe("chunk-2");
        result.Sources[1].Repository.ShouldBe("repo-b");
        result.Sources[1].FilePath.ShouldBe("/docs/b.md");
        result.Sources[1].RelevanceScore.ShouldBe(0.8);
    }

    #endregion

    #region CancellationToken forwarding

    [Fact]
    public async Task QueryAsync_CancellationTokenForwardedToAllCalls()
    {
        // Arrange
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new() { ChunkId = "chunk-1", Score = 0.9, Metadata = new Dictionary<string, string> { ["document_id"] = "doc-1", ["file_path"] = "/path", ["repository"] = "repo" } }
            });
        graphMock
            .Setup(g => g.GetChunksByIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkNode> { new() { Id = "chunk-1", SectionId = "sec-1", DocumentId = "doc-1", Content = "chunk content", Order = 0, TokenCount = 3 } });
        graphMock
            .Setup(g => g.GetConceptsByChunkIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConceptNode>());
        graphMock
            .Setup(g => g.GetLinkedDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentNode>());
        llmMock
            .Setup(l => l.GenerateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<BedrockMessage>>(), It.IsAny<ModelTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Answer");

        // Act
        await sut.QueryAsync("query", null, token);

        // Assert
        embeddingMock.Verify(e => e.GenerateEmbeddingAsync("query", token), Times.Once);
        vectorMock.Verify(v => v.SearchAsync(
            It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), token),
            Times.Once);
        graphMock.Verify(g => g.GetChunksByIdsAsync(It.IsAny<IReadOnlyList<string>>(), token), Times.Once);
        graphMock.Verify(g => g.GetConceptsByChunkIdsAsync(It.IsAny<IReadOnlyList<string>>(), token), Times.Once);
        llmMock.Verify(l => l.GenerateAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyList<BedrockMessage>>(), It.IsAny<ModelTier>(), token),
            Times.Once);
    }

    #endregion

    #region CrossRepoLinks disabled

    [Fact]
    public async Task QueryAsync_CrossRepoLinksDisabled_SkipsLinkedDocLookup()
    {
        // Arrange
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new() { ChunkId = "chunk-1", Score = 0.9, Metadata = new Dictionary<string, string> { ["document_id"] = "doc-1", ["file_path"] = "/path", ["repository"] = "repo" } }
            });
        graphMock
            .Setup(g => g.GetChunksByIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkNode> { new() { Id = "chunk-1", SectionId = "sec-1", DocumentId = "doc-1", Content = "chunk content", Order = 0, TokenCount = 3 } });
        graphMock
            .Setup(g => g.GetConceptsByChunkIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConceptNode>());
        llmMock
            .Setup(l => l.GenerateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<BedrockMessage>>(), It.IsAny<ModelTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Answer");

        var options = new GraphRagOptions { UseCrossRepoLinks = false };

        // Act
        await sut.QueryAsync("query", options);

        // Assert
        graphMock.Verify(
            g => g.GetLinkedDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Cross-repo entity resolution

    [Fact]
    public async Task QueryAsync_CrossRepoResolution_EnrichesRelatedConcepts()
    {
        // Arrange
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new() { ChunkId = "chunk-1", Score = 0.9, Metadata = new Dictionary<string, string> { ["document_id"] = "doc-1", ["file_path"] = "/path", ["repository"] = "repo-a" } }
            });
        graphMock
            .Setup(g => g.GetChunksByIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkNode> { new() { Id = "chunk-1", SectionId = "sec-1", DocumentId = "doc-1", Content = "chunk content", Order = 0, TokenCount = 3 } });
        graphMock
            .Setup(g => g.GetConceptsByChunkIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConceptNode> { new() { Id = "c1", Name = "LocalConcept" } });
        graphMock
            .Setup(g => g.GetLinkedDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentNode>());
        llmMock
            .Setup(l => l.GenerateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<BedrockMessage>>(), It.IsAny<ModelTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Answer");
        resolverMock
            .Setup(r => r.ResolveAsync("LocalConcept", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResolvedEntity
            {
                ConceptId = "c1",
                Name = "LocalConcept",
                Repository = "repo-b",
                RelatedConceptIds = ["c2"],
                RelatedConceptNames = ["CrossRepoConcept"]
            });

        // Act
        var result = await sut.QueryAsync("query");

        // Assert — should include both the original and cross-repo concept
        result.RelatedConcepts.ShouldContain("LocalConcept");
        result.RelatedConcepts.ShouldContain("CrossRepoConcept");
    }

    [Fact]
    public async Task QueryAsync_CrossRepoResolution_SameRepo_DoesNotEnrich()
    {
        // Arrange
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new() { ChunkId = "chunk-1", Score = 0.9, Metadata = new Dictionary<string, string> { ["document_id"] = "doc-1", ["file_path"] = "/path", ["repository"] = "repo-a" } }
            });
        graphMock
            .Setup(g => g.GetChunksByIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkNode> { new() { Id = "chunk-1", SectionId = "sec-1", DocumentId = "doc-1", Content = "chunk content", Order = 0, TokenCount = 3 } });
        graphMock
            .Setup(g => g.GetConceptsByChunkIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConceptNode> { new() { Id = "c1", Name = "LocalConcept" } });
        graphMock
            .Setup(g => g.GetLinkedDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentNode>());
        llmMock
            .Setup(l => l.GenerateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<BedrockMessage>>(), It.IsAny<ModelTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Answer");
        resolverMock
            .Setup(r => r.ResolveAsync("LocalConcept", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResolvedEntity
            {
                ConceptId = "c1",
                Name = "LocalConcept",
                Repository = "repo-a", // Same repo as vector results
                RelatedConceptIds = ["c2"],
                RelatedConceptNames = ["ShouldNotAppear"]
            });

        // Act
        var result = await sut.QueryAsync("query");

        // Assert — same repo, so cross-repo names should NOT be added
        result.RelatedConcepts.ShouldContain("LocalConcept");
        result.RelatedConcepts.ShouldNotContain("ShouldNotAppear");
    }

    [Fact]
    public async Task QueryAsync_CrossRepoResolution_ExceptionSwallowed()
    {
        // Arrange
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new() { ChunkId = "chunk-1", Score = 0.9, Metadata = new Dictionary<string, string> { ["document_id"] = "doc-1", ["file_path"] = "/path", ["repository"] = "repo-a" } }
            });
        graphMock
            .Setup(g => g.GetChunksByIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkNode> { new() { Id = "chunk-1", SectionId = "sec-1", DocumentId = "doc-1", Content = "chunk content", Order = 0, TokenCount = 3 } });
        graphMock
            .Setup(g => g.GetConceptsByChunkIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConceptNode> { new() { Id = "c1", Name = "ErrorConcept" } });
        graphMock
            .Setup(g => g.GetLinkedDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentNode>());
        llmMock
            .Setup(l => l.GenerateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<BedrockMessage>>(), It.IsAny<ModelTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Answer");
        resolverMock
            .Setup(r => r.ResolveAsync("ErrorConcept", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Resolver down"));

        // Act
        var result = await sut.QueryAsync("query");

        // Assert — pipeline continues despite resolver error
        result.Answer.ShouldBe("Answer");
        result.RelatedConcepts.ShouldBe(["ErrorConcept"]);
    }

    [Fact]
    public async Task QueryAsync_CrossRepoResolution_NullResult_SkipsEnrichment()
    {
        // Arrange
        var embeddingMock = new Mock<IBedrockEmbeddingService>();
        var vectorMock = new Mock<IVectorStore>();
        var graphMock = new Mock<IGraphRepository>();
        var llmMock = new Mock<IBedrockLlmService>();
        var resolverMock = new Mock<ICrossRepoEntityResolver>();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var config = new CompoundDocsCloudConfig
        {
            GraphRag = new GraphRagConfig
            {
                MaxChunksPerQuery = 10, MinRelevanceScore = 0.7,
                MaxTraversalSteps = 5, UseCrossRepoLinks = true
            }
        };
        var sut = new GraphRagPipeline(
            embeddingMock.Object, vectorMock.Object, graphMock.Object, llmMock.Object,
            resolverMock.Object, MsOptions.Create(config), NullLogger<GraphRagPipeline>.Instance);

        embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);
        vectorMock
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new() { ChunkId = "chunk-1", Score = 0.9, Metadata = new Dictionary<string, string> { ["document_id"] = "doc-1", ["file_path"] = "/path", ["repository"] = "repo-a" } }
            });
        graphMock
            .Setup(g => g.GetChunksByIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkNode> { new() { Id = "chunk-1", SectionId = "sec-1", DocumentId = "doc-1", Content = "chunk content", Order = 0, TokenCount = 3 } });
        graphMock
            .Setup(g => g.GetConceptsByChunkIdsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConceptNode> { new() { Id = "c1", Name = "UnknownConcept" } });
        graphMock
            .Setup(g => g.GetLinkedDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentNode>());
        llmMock
            .Setup(l => l.GenerateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<BedrockMessage>>(), It.IsAny<ModelTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Answer");
        resolverMock
            .Setup(r => r.ResolveAsync("UnknownConcept", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResolvedEntity?)null);

        // Act
        var result = await sut.QueryAsync("query");

        // Assert
        result.RelatedConcepts.ShouldBe(["UnknownConcept"]);
    }

    #endregion

    #region Static helpers

    [Fact]
    public void BuildSystemPrompt_ContainsRagInstructions()
    {
        var prompt = GraphRagPipeline.BuildSystemPrompt();

        prompt.ShouldContain("documentation assistant");
        prompt.ShouldContain("provided context");
        prompt.ShouldContain("Do not make up information");
    }

    [Fact]
    public void FormatChunkContext_FormatsChunksWithMetadata()
    {
        var chunks = new List<ChunkNode>
        {
            new() { Id = "chunk-1", SectionId = "sec-1", DocumentId = "doc-1", Content = "Content of chunk one", Order = 0, TokenCount = 5 }
        };
        var vectorResults = new List<VectorSearchResult>
        {
            new() { ChunkId = "chunk-1", Score = 0.9, Metadata = new Dictionary<string, string> { ["file_path"] = "/docs/guide.md" } }
        };

        var formatted = GraphRagPipeline.FormatChunkContext(chunks, vectorResults);

        formatted.ShouldContain("## Context");
        formatted.ShouldContain("/docs/guide.md");
        formatted.ShouldContain("Content of chunk one");
        formatted.ShouldContain("0.90");
    }

    [Fact]
    public void FormatChunkContext_MissingFilePath_DefaultsToEmpty()
    {
        var chunks = new List<ChunkNode>
        {
            new() { Id = "chunk-1", SectionId = "sec-1", DocumentId = "doc-1", Content = "Content without path", Order = 0, TokenCount = 5 }
        };
        var vectorResults = new List<VectorSearchResult>
        {
            new()
            {
                ChunkId = "chunk-1",
                Score = 0.85,
                Metadata = new Dictionary<string, string>
                {
                    ["document_id"] = "doc-1"
                    // no file_path key
                }
            }
        };

        var formatted = GraphRagPipeline.FormatChunkContext(chunks, vectorResults);

        formatted.ShouldContain("## Context");
        formatted.ShouldContain("Content without path");
        formatted.ShouldContain("### Source:  (relevance: 0.85)");
    }

    [Fact]
    public void ComputeConfidence_CorrectFormula()
    {
        var scores = new List<double> { 0.9, 0.8 };

        var confidence = GraphRagPipeline.ComputeConfidence(scores, 10);

        // avg=0.85, coverage=2/10=0.2, confidence=0.17
        confidence.ShouldBe(0.85 * 0.2, tolerance: 0.001);
    }

    [Fact]
    public void ComputeConfidence_EmptyScores_ReturnsZero()
    {
        var confidence = GraphRagPipeline.ComputeConfidence([], 10);

        confidence.ShouldBe(0);
    }

    [Fact]
    public void ComputeConfidence_MoreResultsThanRequested_CapsAtOne()
    {
        // If somehow more results than requested
        var scores = new List<double> { 0.9, 0.8, 0.7 };

        var confidence = GraphRagPipeline.ComputeConfidence(scores, 2);

        // avg=0.8, coverage=min(1, 3/2)=1.0, confidence=0.8
        var expected = (0.9 + 0.8 + 0.7) / 3.0 * 1.0;
        confidence.ShouldBe(expected, tolerance: 0.001);
    }

    #endregion
}
