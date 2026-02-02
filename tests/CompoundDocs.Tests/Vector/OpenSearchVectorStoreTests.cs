using System.Net;
using System.Text;
using System.Text.Json;
using CompoundDocs.Common.Configuration;
using CompoundDocs.Vector;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;

namespace CompoundDocs.Tests.Vector;

public sealed class OpenSearchVectorStoreTests
{
    private readonly OpenSearchConfig _config;
    private readonly OpenSearchVectorStore _sut;
    private readonly MockHttpMessageHandler _handler;

    public OpenSearchVectorStoreTests()
    {
        _config = new OpenSearchConfig
        {
            CollectionEndpoint = "https://opensearch.example.com",
            IndexName = "test-index"
        };
        _handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(_handler);
        var logger = NullLogger<OpenSearchVectorStore>.Instance;
        _sut = new OpenSearchVectorStore(httpClient, _config, logger, ResiliencePipeline.Empty);
    }

    [Fact]
    public async Task IndexAsync_SendsPutToCorrectUrl()
    {
        _handler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK);

        await _sut.IndexAsync("chunk-1", new float[] { 0.1f, 0.2f },
            new Dictionary<string, string> { ["repo"] = "test" });

        _handler.LastRequest.ShouldNotBeNull();
        _handler.LastRequest!.Method.ShouldBe(HttpMethod.Put);
        _handler.LastRequest.RequestUri!.ToString()
            .ShouldBe("https://opensearch.example.com/test-index/_doc/chunk-1");
    }

    [Fact]
    public async Task IndexAsync_SendsCorrectBody()
    {
        _handler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK);

        await _sut.IndexAsync("chunk-1", new float[] { 0.5f },
            new Dictionary<string, string> { ["key"] = "val" });

        var body = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("chunk_id").GetString().ShouldBe("chunk-1");
        doc.RootElement.GetProperty("metadata").GetProperty("key").GetString().ShouldBe("val");
    }

    [Fact]
    public async Task DeleteByDocumentIdAsync_SendsPostDeleteByQuery()
    {
        _handler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK);

        await _sut.DeleteByDocumentIdAsync("doc-1");

        _handler.LastRequest.ShouldNotBeNull();
        _handler.LastRequest!.Method.ShouldBe(HttpMethod.Post);
        _handler.LastRequest.RequestUri!.ToString()
            .ShouldBe("https://opensearch.example.com/test-index/_delete_by_query");

        var body = await _handler.LastRequest.Content!.ReadAsStringAsync();
        body.ShouldContain("document_id");
        body.ShouldContain("doc-1");
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

        _handler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };

        var results = await _sut.SearchAsync(new float[] { 0.1f, 0.2f }, topK: 10);

        results.Count.ShouldBe(2);
        results[0].ChunkId.ShouldBe("chunk-1");
        results[0].Score.ShouldBe(0.95);
        results[0].Metadata["repo"].ShouldBe("test-repo");
        results[1].ChunkId.ShouldBe("chunk-2");
        results[1].Score.ShouldBe(0.85);
    }

    [Fact]
    public async Task SearchAsync_WithFilters_AddsTermsToQuery()
    {
        var responseJson = """{"hits":{"hits":[]}}""";
        _handler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };

        var filters = new Dictionary<string, string>
        {
            ["repo"] = "my-repo",
            ["doc_type"] = "spec"
        };

        var results = await _sut.SearchAsync(new float[] { 0.1f }, topK: 5, filters: filters);

        results.Count.ShouldBe(0);
        var body = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        body.ShouldContain("metadata.repo");
        body.ShouldContain("my-repo");
        body.ShouldContain("metadata.doc_type");
    }

    [Fact]
    public async Task SearchAsync_EmptyResults_ReturnsEmptyList()
    {
        var responseJson = """{"hits":{"hits":[]}}""";
        _handler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };

        var results = await _sut.SearchAsync(new float[] { 0.1f });
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task BatchIndexAsync_CallsIndexForEachDocument()
    {
        var requestCount = 0;
        _handler.ResponseFactory = _ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        };

        var documents = new List<VectorDocument>
        {
            new() { ChunkId = "c1", Embedding = new float[] { 0.1f } },
            new() { ChunkId = "c2", Embedding = new float[] { 0.2f } },
            new() { ChunkId = "c3", Embedding = new float[] { 0.3f } }
        };

        await _sut.BatchIndexAsync(documents);

        requestCount.ShouldBe(3);
    }

    [Fact]
    public async Task IndexAsync_HttpError_Propagates()
    {
        _handler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

        await Should.ThrowAsync<HttpRequestException>(async () =>
            await _sut.IndexAsync("chunk-1", new float[] { 0.1f }, new Dictionary<string, string>()));
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public Func<HttpRequestMessage, HttpResponseMessage> ResponseFactory { get; set; }
            = _ => new HttpResponseMessage(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Read content before returning so it's available for assertions
            if (request.Content != null)
            {
                await request.Content.LoadIntoBufferAsync();
            }
            LastRequest = request;
            return ResponseFactory(request);
        }
    }
}
