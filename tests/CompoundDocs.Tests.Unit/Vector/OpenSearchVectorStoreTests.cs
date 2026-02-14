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
    private readonly OpenSearchConfig _config;
    private readonly OpenSearchVectorStore _sut;
    private readonly Mock<OpenSearch.Client.IOpenSearchClient> _mockClient;
    private readonly Mock<IOpenSearchLowLevelClient> _mockLowLevel;

    public OpenSearchVectorStoreTests()
    {
        _config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        _mockClient = new Mock<OpenSearch.Client.IOpenSearchClient>();
        _mockLowLevel = new Mock<IOpenSearchLowLevelClient>();
        _mockClient.Setup(c => c.LowLevel).Returns(_mockLowLevel.Object);

        var logger = NullLogger<OpenSearchVectorStore>.Instance;
        _sut = new OpenSearchVectorStore(_mockClient.Object, _config, logger, ResiliencePipeline.Empty);
    }

    [Fact]
    public async Task IndexAsync_CallsLowLevelIndex()
    {
        SetupIndexAsync(CreateSuccessResponse("{}"));

        await _sut.IndexAsync("chunk-1", new float[] { 0.1f, 0.2f },
            new Dictionary<string, string> { ["repo"] = "test" });

        _mockLowLevel.Verify(c => c.IndexAsync<StringResponse>(
            "test-index",
            "chunk-1",
            It.IsAny<PostData>(),
            It.IsAny<IndexRequestParameters>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteByDocumentIdAsync_CallsLowLevelDeleteByQuery()
    {
        SetupDeleteByQueryAsync(CreateSuccessResponse("{}"));

        await _sut.DeleteByDocumentIdAsync("doc-1");

        _mockLowLevel.Verify(c => c.DeleteByQueryAsync<StringResponse>(
            "test-index",
            It.IsAny<PostData>(),
            It.IsAny<DeleteByQueryRequestParameters>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ParsesJsonResponse()
    {
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

        SetupSearchAsync(CreateSuccessResponse(responseJson));

        var results = await _sut.SearchAsync(new float[] { 0.1f, 0.2f }, topK: 10);

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
        SetupSearchAsync(CreateSuccessResponse("""{"hits":{"hits":[]}}"""));

        var filters = new Dictionary<string, string>
        {
            ["repo"] = "my-repo",
            ["doc_type"] = "spec"
        };

        var results = await _sut.SearchAsync(new float[] { 0.1f }, topK: 5, filters: filters);

        results.Count.ShouldBe(0);
        _mockLowLevel.Verify(c => c.SearchAsync<StringResponse>(
            "test-index",
            It.IsAny<PostData>(),
            It.IsAny<SearchRequestParameters>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_EmptyResults_ReturnsEmptyList()
    {
        SetupSearchAsync(CreateSuccessResponse("""{"hits":{"hits":[]}}"""));

        var results = await _sut.SearchAsync(new float[] { 0.1f });
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task BatchIndexAsync_CallsIndexForEachDocument()
    {
        SetupIndexAsync(CreateSuccessResponse("{}"));

        var documents = new List<VectorDocument>
        {
            new() { ChunkId = "c1", Embedding = new float[] { 0.1f } },
            new() { ChunkId = "c2", Embedding = new float[] { 0.2f } },
            new() { ChunkId = "c3", Embedding = new float[] { 0.3f } }
        };

        await _sut.BatchIndexAsync(documents);

        _mockLowLevel.Verify(c => c.IndexAsync<StringResponse>(
            "test-index",
            It.IsAny<string>(),
            It.IsAny<PostData>(),
            It.IsAny<IndexRequestParameters>(),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task IndexAsync_FailedResponse_ThrowsInvalidOperationException()
    {
        SetupIndexAsync(CreateFailedResponse("error"));

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _sut.IndexAsync("chunk-1", new float[] { 0.1f }, new Dictionary<string, string>()));
    }

    [Fact]
    public async Task SearchAsync_HitWithoutMetadata_ReturnsEmptyMetadataDictionary()
    {
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

        SetupSearchAsync(CreateSuccessResponse(responseJson));

        var results = await _sut.SearchAsync(new float[] { 0.1f, 0.2f }, topK: 5);

        results.Count.ShouldBe(1);
        results[0].ChunkId.ShouldBe("chunk-no-meta");
        results[0].Score.ShouldBe(0.90);
        results[0].Metadata.ShouldNotBeNull();
        results[0].Metadata.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_NullFilters_DoesNotAddFilterToQuery()
    {
        SetupSearchAsync(CreateSuccessResponse("""{"hits":{"hits":[]}}"""));

        var results = await _sut.SearchAsync(new float[] { 0.1f }, topK: 5, filters: null);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_EmptyFilters_DoesNotAddFilterToQuery()
    {
        SetupSearchAsync(CreateSuccessResponse("""{"hits":{"hits":[]}}"""));

        var results = await _sut.SearchAsync(new float[] { 0.1f }, topK: 5,
            filters: new Dictionary<string, string>());

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task BatchIndexAsync_EmptyCollection_MakesNoRequests()
    {
        await _sut.BatchIndexAsync(new List<VectorDocument>());

        _mockLowLevel.Verify(c => c.IndexAsync<StringResponse>(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<PostData>(),
            It.IsAny<IndexRequestParameters>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void PublicConstructor_WithIOptions_CreatesStoreWithRetryPipeline()
    {
        var mockClient = new Mock<OpenSearch.Client.IOpenSearchClient>();
        var options = Microsoft.Extensions.Options.Options.Create(_config);
        var logger = NullLogger<OpenSearchVectorStore>.Instance;

        var store = new OpenSearchVectorStore(mockClient.Object, options, logger);

        store.ShouldNotBeNull();
    }

    [Fact]
    public async Task Constructor_NullRetryPipeline_UsesEmptyPipeline()
    {
        var mockClient = new Mock<OpenSearch.Client.IOpenSearchClient>();
        var mockLowLevel = new Mock<IOpenSearchLowLevelClient>();
        mockClient.Setup(c => c.LowLevel).Returns(mockLowLevel.Object);
        mockLowLevel.Setup(c => c.IndexAsync<StringResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PostData>(),
                It.IsAny<IndexRequestParameters>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResponse("{}"));

        var logger = NullLogger<OpenSearchVectorStore>.Instance;
        var store = new OpenSearchVectorStore(mockClient.Object, _config, logger, retryPipeline: null);

        await store.IndexAsync("chunk-null", new float[] { 0.1f },
            new Dictionary<string, string> { ["key"] = "val" });

        mockLowLevel.Verify(c => c.IndexAsync<StringResponse>(
            "test-index",
            "chunk-null",
            It.IsAny<PostData>(),
            It.IsAny<IndexRequestParameters>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private void SetupIndexAsync(StringResponse response)
    {
        _mockLowLevel.Setup(c => c.IndexAsync<StringResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PostData>(),
                It.IsAny<IndexRequestParameters>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }

    private void SetupDeleteByQueryAsync(StringResponse response)
    {
        _mockLowLevel.Setup(c => c.DeleteByQueryAsync<StringResponse>(
                It.IsAny<string>(),
                It.IsAny<PostData>(),
                It.IsAny<DeleteByQueryRequestParameters>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }

    private void SetupSearchAsync(StringResponse response)
    {
        _mockLowLevel.Setup(c => c.SearchAsync<StringResponse>(
                It.IsAny<string>(),
                It.IsAny<PostData>(),
                It.IsAny<SearchRequestParameters>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }

    private static StringResponse CreateSuccessResponse(string body)
    {
        var response = new StringResponse(body);
        response.ApiCall = new ApiCallDetails { HttpStatusCode = 200, Success = true };
        return response;
    }

    private static StringResponse CreateFailedResponse(string body)
    {
        var response = new StringResponse(body);
        response.ApiCall = new ApiCallDetails { HttpStatusCode = 500, Success = false };
        return response;
    }
}
