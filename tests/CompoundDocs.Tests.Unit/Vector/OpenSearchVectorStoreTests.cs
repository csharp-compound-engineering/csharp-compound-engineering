using System.Text.Json;
using CompoundDocs.Common.Configuration;
using CompoundDocs.Vector;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenSearch.Net;
using Polly;

namespace CompoundDocs.Tests.Unit.Vector;

public sealed class OpenSearchVectorStoreTests
{
    [Fact]
    public async Task IndexAsync_CallsLowLevelIndex()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockClient = new Mock<OpenSearch.Client.IOpenSearchClient>();
        var mockLowLevel = new Mock<IOpenSearchLowLevelClient>();
        mockClient.Setup(c => c.LowLevel).Returns(mockLowLevel.Object);
        var sut = new OpenSearchVectorStore(mockClient.Object, config, NullLogger<OpenSearchVectorStore>.Instance, ResiliencePipeline.Empty);

        var response = new StringResponse("{}");
        response.ApiCall = new ApiCallDetails { HttpStatusCode = 200, Success = true };
        mockLowLevel.Setup(c => c.IndexAsync<StringResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PostData>(),
                It.IsAny<IndexRequestParameters>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        await sut.IndexAsync("chunk-1", new float[] { 0.1f, 0.2f },
            new Dictionary<string, string> { ["repo"] = "test" });

        // Assert
        mockLowLevel.Verify(c => c.IndexAsync<StringResponse>(
            "test-index",
            "chunk-1",
            It.IsAny<PostData>(),
            It.IsAny<IndexRequestParameters>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteByDocumentIdAsync_CallsLowLevelDeleteByQuery()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockClient = new Mock<OpenSearch.Client.IOpenSearchClient>();
        var mockLowLevel = new Mock<IOpenSearchLowLevelClient>();
        mockClient.Setup(c => c.LowLevel).Returns(mockLowLevel.Object);
        var sut = new OpenSearchVectorStore(mockClient.Object, config, NullLogger<OpenSearchVectorStore>.Instance, ResiliencePipeline.Empty);

        var response = new StringResponse("{}");
        response.ApiCall = new ApiCallDetails { HttpStatusCode = 200, Success = true };
        mockLowLevel.Setup(c => c.DeleteByQueryAsync<StringResponse>(
                It.IsAny<string>(),
                It.IsAny<PostData>(),
                It.IsAny<DeleteByQueryRequestParameters>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        await sut.DeleteByDocumentIdAsync("doc-1");

        // Assert
        mockLowLevel.Verify(c => c.DeleteByQueryAsync<StringResponse>(
            "test-index",
            It.IsAny<PostData>(),
            It.IsAny<DeleteByQueryRequestParameters>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ParsesJsonResponse()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockClient = new Mock<OpenSearch.Client.IOpenSearchClient>();
        var mockLowLevel = new Mock<IOpenSearchLowLevelClient>();
        mockClient.Setup(c => c.LowLevel).Returns(mockLowLevel.Object);
        var sut = new OpenSearchVectorStore(mockClient.Object, config, NullLogger<OpenSearchVectorStore>.Instance, ResiliencePipeline.Empty);

        var responseJson = """
        {
            "hits": {
                "hits": [
                    {
                        "_score": 0.95,
                        "_source": {
                            "chunk_id": "chunk-1",
                            "metadata": { "repo": "test-repo" }
                        }
                    },
                    {
                        "_score": 0.85,
                        "_source": {
                            "chunk_id": "chunk-2",
                            "metadata": {}
                        }
                    }
                ]
            }
        }
        """;

        var response = new StringResponse(responseJson);
        response.ApiCall = new ApiCallDetails { HttpStatusCode = 200, Success = true };
        mockLowLevel.Setup(c => c.SearchAsync<StringResponse>(
                It.IsAny<string>(),
                It.IsAny<PostData>(),
                It.IsAny<SearchRequestParameters>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

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
        var mockClient = new Mock<OpenSearch.Client.IOpenSearchClient>();
        var mockLowLevel = new Mock<IOpenSearchLowLevelClient>();
        mockClient.Setup(c => c.LowLevel).Returns(mockLowLevel.Object);
        var sut = new OpenSearchVectorStore(mockClient.Object, config, NullLogger<OpenSearchVectorStore>.Instance, ResiliencePipeline.Empty);

        var response = new StringResponse("""{"hits":{"hits":[]}}""");
        response.ApiCall = new ApiCallDetails { HttpStatusCode = 200, Success = true };
        mockLowLevel.Setup(c => c.SearchAsync<StringResponse>(
                It.IsAny<string>(),
                It.IsAny<PostData>(),
                It.IsAny<SearchRequestParameters>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var filters = new Dictionary<string, string>
        {
            ["repo"] = "my-repo",
            ["doc_type"] = "spec"
        };

        // Act
        var results = await sut.SearchAsync(new float[] { 0.1f }, topK: 5, filters: filters);

        // Assert
        results.Count.ShouldBe(0);
        mockLowLevel.Verify(c => c.SearchAsync<StringResponse>(
            "test-index",
            It.IsAny<PostData>(),
            It.IsAny<SearchRequestParameters>(),
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
        var mockClient = new Mock<OpenSearch.Client.IOpenSearchClient>();
        var mockLowLevel = new Mock<IOpenSearchLowLevelClient>();
        mockClient.Setup(c => c.LowLevel).Returns(mockLowLevel.Object);
        var sut = new OpenSearchVectorStore(mockClient.Object, config, NullLogger<OpenSearchVectorStore>.Instance, ResiliencePipeline.Empty);

        var response = new StringResponse("""{"hits":{"hits":[]}}""");
        response.ApiCall = new ApiCallDetails { HttpStatusCode = 200, Success = true };
        mockLowLevel.Setup(c => c.SearchAsync<StringResponse>(
                It.IsAny<string>(),
                It.IsAny<PostData>(),
                It.IsAny<SearchRequestParameters>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

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
        var mockClient = new Mock<OpenSearch.Client.IOpenSearchClient>();
        var mockLowLevel = new Mock<IOpenSearchLowLevelClient>();
        mockClient.Setup(c => c.LowLevel).Returns(mockLowLevel.Object);
        var sut = new OpenSearchVectorStore(mockClient.Object, config, NullLogger<OpenSearchVectorStore>.Instance, ResiliencePipeline.Empty);

        var response = new StringResponse("{}");
        response.ApiCall = new ApiCallDetails { HttpStatusCode = 200, Success = true };
        mockLowLevel.Setup(c => c.IndexAsync<StringResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PostData>(),
                It.IsAny<IndexRequestParameters>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var documents = new List<VectorDocument>
        {
            new() { ChunkId = "c1", Embedding = new float[] { 0.1f } },
            new() { ChunkId = "c2", Embedding = new float[] { 0.2f } },
            new() { ChunkId = "c3", Embedding = new float[] { 0.3f } }
        };

        // Act
        await sut.BatchIndexAsync(documents);

        // Assert
        mockLowLevel.Verify(c => c.IndexAsync<StringResponse>(
            "test-index",
            It.IsAny<string>(),
            It.IsAny<PostData>(),
            It.IsAny<IndexRequestParameters>(),
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
        var mockClient = new Mock<OpenSearch.Client.IOpenSearchClient>();
        var mockLowLevel = new Mock<IOpenSearchLowLevelClient>();
        mockClient.Setup(c => c.LowLevel).Returns(mockLowLevel.Object);
        var sut = new OpenSearchVectorStore(mockClient.Object, config, NullLogger<OpenSearchVectorStore>.Instance, ResiliencePipeline.Empty);

        var response = new StringResponse("error");
        response.ApiCall = new ApiCallDetails { HttpStatusCode = 500, Success = false };
        mockLowLevel.Setup(c => c.IndexAsync<StringResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PostData>(),
                It.IsAny<IndexRequestParameters>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

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
        var mockClient = new Mock<OpenSearch.Client.IOpenSearchClient>();
        var mockLowLevel = new Mock<IOpenSearchLowLevelClient>();
        mockClient.Setup(c => c.LowLevel).Returns(mockLowLevel.Object);
        var sut = new OpenSearchVectorStore(mockClient.Object, config, NullLogger<OpenSearchVectorStore>.Instance, ResiliencePipeline.Empty);

        var responseJson = """
        {
            "hits": {
                "hits": [
                    {
                        "_score": 0.90,
                        "_source": {
                            "chunk_id": "chunk-no-meta"
                        }
                    }
                ]
            }
        }
        """;

        var response = new StringResponse(responseJson);
        response.ApiCall = new ApiCallDetails { HttpStatusCode = 200, Success = true };
        mockLowLevel.Setup(c => c.SearchAsync<StringResponse>(
                It.IsAny<string>(),
                It.IsAny<PostData>(),
                It.IsAny<SearchRequestParameters>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

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
        var mockClient = new Mock<OpenSearch.Client.IOpenSearchClient>();
        var mockLowLevel = new Mock<IOpenSearchLowLevelClient>();
        mockClient.Setup(c => c.LowLevel).Returns(mockLowLevel.Object);
        var sut = new OpenSearchVectorStore(mockClient.Object, config, NullLogger<OpenSearchVectorStore>.Instance, ResiliencePipeline.Empty);

        var response = new StringResponse("""{"hits":{"hits":[]}}""");
        response.ApiCall = new ApiCallDetails { HttpStatusCode = 200, Success = true };
        mockLowLevel.Setup(c => c.SearchAsync<StringResponse>(
                It.IsAny<string>(),
                It.IsAny<PostData>(),
                It.IsAny<SearchRequestParameters>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

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
        var mockClient = new Mock<OpenSearch.Client.IOpenSearchClient>();
        var mockLowLevel = new Mock<IOpenSearchLowLevelClient>();
        mockClient.Setup(c => c.LowLevel).Returns(mockLowLevel.Object);
        var sut = new OpenSearchVectorStore(mockClient.Object, config, NullLogger<OpenSearchVectorStore>.Instance, ResiliencePipeline.Empty);

        var response = new StringResponse("""{"hits":{"hits":[]}}""");
        response.ApiCall = new ApiCallDetails { HttpStatusCode = 200, Success = true };
        mockLowLevel.Setup(c => c.SearchAsync<StringResponse>(
                It.IsAny<string>(),
                It.IsAny<PostData>(),
                It.IsAny<SearchRequestParameters>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

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
        var mockClient = new Mock<OpenSearch.Client.IOpenSearchClient>();
        var mockLowLevel = new Mock<IOpenSearchLowLevelClient>();
        mockClient.Setup(c => c.LowLevel).Returns(mockLowLevel.Object);
        var sut = new OpenSearchVectorStore(mockClient.Object, config, NullLogger<OpenSearchVectorStore>.Instance, ResiliencePipeline.Empty);

        // Act
        await sut.BatchIndexAsync(new List<VectorDocument>());

        // Assert
        mockLowLevel.Verify(c => c.IndexAsync<StringResponse>(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<PostData>(),
            It.IsAny<IndexRequestParameters>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void PublicConstructor_WithIOptions_CreatesStoreWithRetryPipeline()
    {
        // Arrange
        var config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        var mockClient = new Mock<OpenSearch.Client.IOpenSearchClient>();
        var options = Microsoft.Extensions.Options.Options.Create(config);
        var logger = NullLogger<OpenSearchVectorStore>.Instance;

        // Act
        var store = new OpenSearchVectorStore(mockClient.Object, options, logger);

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
        var mockClient = new Mock<OpenSearch.Client.IOpenSearchClient>();
        var mockLowLevel = new Mock<IOpenSearchLowLevelClient>();
        mockClient.Setup(c => c.LowLevel).Returns(mockLowLevel.Object);

        var response = new StringResponse("{}");
        response.ApiCall = new ApiCallDetails { HttpStatusCode = 200, Success = true };
        mockLowLevel.Setup(c => c.IndexAsync<StringResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PostData>(),
                It.IsAny<IndexRequestParameters>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var logger = NullLogger<OpenSearchVectorStore>.Instance;
        var store = new OpenSearchVectorStore(mockClient.Object, config, logger, retryPipeline: null);

        // Act
        await store.IndexAsync("chunk-null", new float[] { 0.1f },
            new Dictionary<string, string> { ["key"] = "val" });

        // Assert
        mockLowLevel.Verify(c => c.IndexAsync<StringResponse>(
            "test-index",
            "chunk-null",
            It.IsAny<PostData>(),
            It.IsAny<IndexRequestParameters>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
