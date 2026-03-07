using CompoundDocs.Common.Configuration;
using CompoundDocs.Vector;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenSearch.Client;
using Polly;

namespace CompoundDocs.Tests.Unit.Vector;

public sealed class OpenSearchVectorStoreTests
{
    [Fact]
    public async Task IndexAsync_CallsHighLevelIndex()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockClient = new Mock<IOpenSearchClient>();
        var mockFactory = new Mock<IOpenSearchClientFactory>();
        mockFactory.Setup(f => f.GetClient()).Returns(mockClient.Object);
        var sut = new OpenSearchVectorStore(mockFactory.Object, config, NullLogger<OpenSearchVectorStore>.Instance, ResiliencePipeline.Empty);

        var mockResponse = new Mock<IndexResponse>();
        mockResponse.Setup(r => r.IsValid).Returns(true);
        mockClient.Setup(c => c.IndexAsync(
            It.IsAny<OpenSearchChunkDocument>(),
            It.IsAny<Func<IndexDescriptor<OpenSearchChunkDocument>, IIndexRequest<OpenSearchChunkDocument>>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(mockResponse.Object);

        // Act
        await sut.IndexAsync("chunk-1", new float[] { 0.1f, 0.2f },
            new Dictionary<string, string> { ["repo"] = "test" });

        // Assert
        mockClient.Verify(c => c.IndexAsync(
            It.IsAny<OpenSearchChunkDocument>(),
            It.IsAny<Func<IndexDescriptor<OpenSearchChunkDocument>, IIndexRequest<OpenSearchChunkDocument>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteByDocumentIdAsync_CallsHighLevelDeleteByQuery()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockClient = new Mock<IOpenSearchClient>();
        var mockFactory = new Mock<IOpenSearchClientFactory>();
        mockFactory.Setup(f => f.GetClient()).Returns(mockClient.Object);
        var sut = new OpenSearchVectorStore(mockFactory.Object, config, NullLogger<OpenSearchVectorStore>.Instance, ResiliencePipeline.Empty);

        var mockResponse = new Mock<DeleteByQueryResponse>();
        mockResponse.Setup(r => r.IsValid).Returns(true);
        mockClient.Setup(c => c.DeleteByQueryAsync<OpenSearchChunkDocument>(
            It.IsAny<Func<DeleteByQueryDescriptor<OpenSearchChunkDocument>, IDeleteByQueryRequest>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(mockResponse.Object);

        // Act
        await sut.DeleteByDocumentIdAsync("doc-1");

        // Assert
        mockClient.Verify(c => c.DeleteByQueryAsync<OpenSearchChunkDocument>(
            It.IsAny<Func<DeleteByQueryDescriptor<OpenSearchChunkDocument>, IDeleteByQueryRequest>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ReturnsDeserializedResults()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockClient = new Mock<IOpenSearchClient>();
        var mockFactory = new Mock<IOpenSearchClientFactory>();
        mockFactory.Setup(f => f.GetClient()).Returns(mockClient.Object);
        var sut = new OpenSearchVectorStore(mockFactory.Object, config, NullLogger<OpenSearchVectorStore>.Instance, ResiliencePipeline.Empty);

        var mockHit1 = new Mock<IHit<OpenSearchChunkDocument>>();
        mockHit1.Setup(h => h.Score).Returns(0.95);
        mockHit1.Setup(h => h.Source).Returns(new OpenSearchChunkDocument
        {
            ChunkId = "chunk-1",
            Metadata = new Dictionary<string, string> { ["repo"] = "test-repo" }
        });

        var mockHit2 = new Mock<IHit<OpenSearchChunkDocument>>();
        mockHit2.Setup(h => h.Score).Returns(0.85);
        mockHit2.Setup(h => h.Source).Returns(new OpenSearchChunkDocument
        {
            ChunkId = "chunk-2",
            Metadata = new Dictionary<string, string>()
        });

        var mockResponse = new Mock<ISearchResponse<OpenSearchChunkDocument>>();
        mockResponse.Setup(r => r.IsValid).Returns(true);
        mockResponse.Setup(r => r.Hits).Returns(new[] { mockHit1.Object, mockHit2.Object });

        mockClient.Setup(c => c.SearchAsync(
            It.IsAny<Func<SearchDescriptor<OpenSearchChunkDocument>, ISearchRequest>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(mockResponse.Object);

        // Act
        var results = await sut.SearchAsync(new float[] { 0.1f, 0.2f }, topK: 10);

        // Assert
        results.Count.ShouldBe(2);
        results[0].ChunkId.ShouldBe("chunk-1");
        results[0].Score.ShouldBe(0.95);
        results[0].Metadata["repo"].ShouldBe("test-repo");
        results[1].ChunkId.ShouldBe("chunk-2");
        results[1].Score.ShouldBe(0.85);
    }

    [Fact]
    public async Task SearchAsync_WithFilters_CallsSearch()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockClient = new Mock<IOpenSearchClient>();
        var mockFactory = new Mock<IOpenSearchClientFactory>();
        mockFactory.Setup(f => f.GetClient()).Returns(mockClient.Object);
        var sut = new OpenSearchVectorStore(mockFactory.Object, config, NullLogger<OpenSearchVectorStore>.Instance, ResiliencePipeline.Empty);

        var mockResponse = new Mock<ISearchResponse<OpenSearchChunkDocument>>();
        mockResponse.Setup(r => r.IsValid).Returns(true);
        mockResponse.Setup(r => r.Hits).Returns(Array.Empty<IHit<OpenSearchChunkDocument>>());

        mockClient.Setup(c => c.SearchAsync(
            It.IsAny<Func<SearchDescriptor<OpenSearchChunkDocument>, ISearchRequest>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(mockResponse.Object);

        var filters = new Dictionary<string, string>
        {
            ["repo"] = "my-repo",
            ["doc_type"] = "spec"
        };

        // Act
        var results = await sut.SearchAsync(new float[] { 0.1f }, topK: 5, filters: filters);

        // Assert
        results.Count.ShouldBe(0);
        mockClient.Verify(c => c.SearchAsync(
            It.IsAny<Func<SearchDescriptor<OpenSearchChunkDocument>, ISearchRequest>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_EmptyResults_ReturnsEmptyList()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockClient = new Mock<IOpenSearchClient>();
        var mockFactory = new Mock<IOpenSearchClientFactory>();
        mockFactory.Setup(f => f.GetClient()).Returns(mockClient.Object);
        var sut = new OpenSearchVectorStore(mockFactory.Object, config, NullLogger<OpenSearchVectorStore>.Instance, ResiliencePipeline.Empty);

        var mockResponse = new Mock<ISearchResponse<OpenSearchChunkDocument>>();
        mockResponse.Setup(r => r.IsValid).Returns(true);
        mockResponse.Setup(r => r.Hits).Returns(Array.Empty<IHit<OpenSearchChunkDocument>>());

        mockClient.Setup(c => c.SearchAsync(
            It.IsAny<Func<SearchDescriptor<OpenSearchChunkDocument>, ISearchRequest>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(mockResponse.Object);

        // Act
        var results = await sut.SearchAsync(new float[] { 0.1f });

        // Assert
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task BatchIndexAsync_CallsIndexForEachDocument()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockClient = new Mock<IOpenSearchClient>();
        var mockFactory = new Mock<IOpenSearchClientFactory>();
        mockFactory.Setup(f => f.GetClient()).Returns(mockClient.Object);
        var sut = new OpenSearchVectorStore(mockFactory.Object, config, NullLogger<OpenSearchVectorStore>.Instance, ResiliencePipeline.Empty);

        var mockResponse = new Mock<IndexResponse>();
        mockResponse.Setup(r => r.IsValid).Returns(true);
        mockClient.Setup(c => c.IndexAsync(
            It.IsAny<OpenSearchChunkDocument>(),
            It.IsAny<Func<IndexDescriptor<OpenSearchChunkDocument>, IIndexRequest<OpenSearchChunkDocument>>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(mockResponse.Object);

        var documents = new List<VectorDocument>
        {
            new() { ChunkId = "c1", Embedding = new float[] { 0.1f } },
            new() { ChunkId = "c2", Embedding = new float[] { 0.2f } },
            new() { ChunkId = "c3", Embedding = new float[] { 0.3f } }
        };

        // Act
        await sut.BatchIndexAsync(documents);

        // Assert
        mockClient.Verify(c => c.IndexAsync(
            It.IsAny<OpenSearchChunkDocument>(),
            It.IsAny<Func<IndexDescriptor<OpenSearchChunkDocument>, IIndexRequest<OpenSearchChunkDocument>>>(),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task IndexAsync_FailedResponse_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockClient = new Mock<IOpenSearchClient>();
        var mockFactory = new Mock<IOpenSearchClientFactory>();
        mockFactory.Setup(f => f.GetClient()).Returns(mockClient.Object);
        var sut = new OpenSearchVectorStore(mockFactory.Object, config, NullLogger<OpenSearchVectorStore>.Instance, ResiliencePipeline.Empty);

        var mockResponse = new Mock<IndexResponse>();
        mockResponse.Setup(r => r.IsValid).Returns(false);
        mockClient.Setup(c => c.IndexAsync(
            It.IsAny<OpenSearchChunkDocument>(),
            It.IsAny<Func<IndexDescriptor<OpenSearchChunkDocument>, IIndexRequest<OpenSearchChunkDocument>>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(mockResponse.Object);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await sut.IndexAsync("chunk-1", new float[] { 0.1f }, new Dictionary<string, string>()));
    }

    [Fact]
    public async Task SearchAsync_HitWithoutMetadata_ReturnsEmptyMetadataDictionary()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockClient = new Mock<IOpenSearchClient>();
        var mockFactory = new Mock<IOpenSearchClientFactory>();
        mockFactory.Setup(f => f.GetClient()).Returns(mockClient.Object);
        var sut = new OpenSearchVectorStore(mockFactory.Object, config, NullLogger<OpenSearchVectorStore>.Instance, ResiliencePipeline.Empty);

        var mockHit = new Mock<IHit<OpenSearchChunkDocument>>();
        mockHit.Setup(h => h.Score).Returns(0.90);
        mockHit.Setup(h => h.Source).Returns(new OpenSearchChunkDocument
        {
            ChunkId = "chunk-no-meta",
            Metadata = null!
        });

        var mockResponse = new Mock<ISearchResponse<OpenSearchChunkDocument>>();
        mockResponse.Setup(r => r.IsValid).Returns(true);
        mockResponse.Setup(r => r.Hits).Returns(new[] { mockHit.Object });

        mockClient.Setup(c => c.SearchAsync(
            It.IsAny<Func<SearchDescriptor<OpenSearchChunkDocument>, ISearchRequest>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(mockResponse.Object);

        // Act
        var results = await sut.SearchAsync(new float[] { 0.1f, 0.2f }, topK: 5);

        // Assert
        results.Count.ShouldBe(1);
        results[0].ChunkId.ShouldBe("chunk-no-meta");
        results[0].Score.ShouldBe(0.90);
        results[0].Metadata.ShouldNotBeNull();
        results[0].Metadata.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_NullFilters_DoesNotAddFilterToQuery()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockClient = new Mock<IOpenSearchClient>();
        var mockFactory = new Mock<IOpenSearchClientFactory>();
        mockFactory.Setup(f => f.GetClient()).Returns(mockClient.Object);
        var sut = new OpenSearchVectorStore(mockFactory.Object, config, NullLogger<OpenSearchVectorStore>.Instance, ResiliencePipeline.Empty);

        var mockResponse = new Mock<ISearchResponse<OpenSearchChunkDocument>>();
        mockResponse.Setup(r => r.IsValid).Returns(true);
        mockResponse.Setup(r => r.Hits).Returns(Array.Empty<IHit<OpenSearchChunkDocument>>());

        mockClient.Setup(c => c.SearchAsync(
            It.IsAny<Func<SearchDescriptor<OpenSearchChunkDocument>, ISearchRequest>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(mockResponse.Object);

        // Act
        var results = await sut.SearchAsync(new float[] { 0.1f }, topK: 5, filters: null);

        // Assert
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_EmptyFilters_DoesNotAddFilterToQuery()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockClient = new Mock<IOpenSearchClient>();
        var mockFactory = new Mock<IOpenSearchClientFactory>();
        mockFactory.Setup(f => f.GetClient()).Returns(mockClient.Object);
        var sut = new OpenSearchVectorStore(mockFactory.Object, config, NullLogger<OpenSearchVectorStore>.Instance, ResiliencePipeline.Empty);

        var mockResponse = new Mock<ISearchResponse<OpenSearchChunkDocument>>();
        mockResponse.Setup(r => r.IsValid).Returns(true);
        mockResponse.Setup(r => r.Hits).Returns(Array.Empty<IHit<OpenSearchChunkDocument>>());

        mockClient.Setup(c => c.SearchAsync(
            It.IsAny<Func<SearchDescriptor<OpenSearchChunkDocument>, ISearchRequest>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(mockResponse.Object);

        // Act
        var results = await sut.SearchAsync(new float[] { 0.1f }, topK: 5,
            filters: new Dictionary<string, string>());

        // Assert
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task BatchIndexAsync_EmptyCollection_MakesNoRequests()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockClient = new Mock<IOpenSearchClient>();
        var mockFactory = new Mock<IOpenSearchClientFactory>();
        mockFactory.Setup(f => f.GetClient()).Returns(mockClient.Object);
        var sut = new OpenSearchVectorStore(mockFactory.Object, config, NullLogger<OpenSearchVectorStore>.Instance, ResiliencePipeline.Empty);

        // Act
        await sut.BatchIndexAsync(new List<VectorDocument>());

        // Assert
        mockClient.Verify(c => c.IndexAsync(
            It.IsAny<OpenSearchChunkDocument>(),
            It.IsAny<Func<IndexDescriptor<OpenSearchChunkDocument>, IIndexRequest<OpenSearchChunkDocument>>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void PublicConstructor_WithIOptionsMonitor_CreatesStoreWithRetryPipeline()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockFactory = new Mock<IOpenSearchClientFactory>();
        var mockOptionsMonitor = new Mock<Microsoft.Extensions.Options.IOptionsMonitor<OpenSearchConfig>>();
        mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(config);
        var logger = NullLogger<OpenSearchVectorStore>.Instance;

        // Act
        var store = new OpenSearchVectorStore(mockFactory.Object, mockOptionsMonitor.Object, logger);

        // Assert
        store.ShouldNotBeNull();
    }

    [Fact]
    public async Task Constructor_NullRetryPipeline_UsesEmptyPipeline()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockClient = new Mock<IOpenSearchClient>();
        var mockFactory = new Mock<IOpenSearchClientFactory>();
        mockFactory.Setup(f => f.GetClient()).Returns(mockClient.Object);

        var mockResponse = new Mock<IndexResponse>();
        mockResponse.Setup(r => r.IsValid).Returns(true);
        mockClient.Setup(c => c.IndexAsync(
            It.IsAny<OpenSearchChunkDocument>(),
            It.IsAny<Func<IndexDescriptor<OpenSearchChunkDocument>, IIndexRequest<OpenSearchChunkDocument>>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(mockResponse.Object);

        var logger = NullLogger<OpenSearchVectorStore>.Instance;
        var store = new OpenSearchVectorStore(mockFactory.Object, config, logger, retryPipeline: null);

        // Act
        await store.IndexAsync("chunk-null", new float[] { 0.1f },
            new Dictionary<string, string> { ["key"] = "val" });

        // Assert
        mockClient.Verify(c => c.IndexAsync(
            It.IsAny<OpenSearchChunkDocument>(),
            It.IsAny<Func<IndexDescriptor<OpenSearchChunkDocument>, IIndexRequest<OpenSearchChunkDocument>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
