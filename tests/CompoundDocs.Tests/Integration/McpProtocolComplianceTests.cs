using System.Text.Json;
using System.Text.Json.Serialization;
using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.SemanticKernel;
using CompoundDocs.McpServer.Services.DocumentProcessing;
using CompoundDocs.McpServer.Session;
using CompoundDocs.McpServer.Tools;
using CompoundDocs.Tests.Utilities;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.Tests.Integration;

/// <summary>
/// Tests for MCP (Model Context Protocol) compliance.
/// Verifies JSON-RPC message format, tool registration, error responses, and session lifecycle.
/// </summary>
public sealed class McpProtocolComplianceTests : TestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    #region Tool Response Format Tests

    [Fact]
    public void ToolResponse_Success_ShouldHaveCorrectJsonFormat()
    {
        // Arrange
        var response = ToolResponse<TestData>.Ok(new TestData { Value = "test" });

        // Act
        var json = JsonSerializer.Serialize(response, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<JsonElement>(json);

        // Assert
        deserialized.TryGetProperty("success", out var successProp).ShouldBeTrue();
        successProp.GetBoolean().ShouldBeTrue();

        deserialized.TryGetProperty("data", out var dataProp).ShouldBeTrue();
        dataProp.TryGetProperty("value", out var valueProp).ShouldBeTrue();
        valueProp.GetString().ShouldBe("test");

        // Error should not be present on success
        deserialized.TryGetProperty("error", out _).ShouldBeFalse();
    }

    [Fact]
    public void ToolResponse_Failure_ShouldHaveCorrectJsonFormat()
    {
        // Arrange
        var response = ToolResponse<TestData>.Fail("Something went wrong", "ERROR_CODE");

        // Act
        var json = JsonSerializer.Serialize(response, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<JsonElement>(json);

        // Assert
        deserialized.TryGetProperty("success", out var successProp).ShouldBeTrue();
        successProp.GetBoolean().ShouldBeFalse();

        deserialized.TryGetProperty("error", out var errorProp).ShouldBeTrue();
        errorProp.GetString().ShouldBe("Something went wrong");

        deserialized.TryGetProperty("error_code", out var codeProp).ShouldBeTrue();
        codeProp.GetString().ShouldBe("ERROR_CODE");

        // Data should not be present on failure
        deserialized.TryGetProperty("data", out _).ShouldBeFalse();
    }

    [Fact]
    public void ToolResponse_FromToolError_ShouldPreserveCodeAndMessage()
    {
        // Arrange
        var toolError = ToolErrors.NoActiveProject;

        // Act
        var response = ToolResponse<TestData>.Fail(toolError);

        // Assert
        response.Success.ShouldBeFalse();
        response.Error.ShouldBe(toolError.Message);
        response.ErrorCode.ShouldBe(toolError.Code);
    }

    [Fact]
    public void NonGenericToolResponse_Success_ShouldHaveCorrectFormat()
    {
        // Arrange
        var response = ToolResponse.Ok("Operation completed successfully");

        // Act
        var json = JsonSerializer.Serialize(response, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<JsonElement>(json);

        // Assert
        deserialized.TryGetProperty("success", out var successProp).ShouldBeTrue();
        successProp.GetBoolean().ShouldBeTrue();

        deserialized.TryGetProperty("message", out var messageProp).ShouldBeTrue();
        messageProp.GetString().ShouldBe("Operation completed successfully");
    }

    #endregion

    #region Error Response Format Tests

    [Theory]
    [InlineData("NO_ACTIVE_PROJECT", "No project is currently active. Use 'activate_project' tool first.")]
    [InlineData("EMPTY_QUERY", "Query cannot be empty.")]
    [InlineData("OPERATION_CANCELLED", "The operation was cancelled.")]
    public void StandardErrors_ShouldHaveConsistentFormat(string expectedCode, string expectedMessage)
    {
        // Arrange & Act
        ToolError error = expectedCode switch
        {
            "NO_ACTIVE_PROJECT" => ToolErrors.NoActiveProject,
            "EMPTY_QUERY" => ToolErrors.EmptyQuery,
            "OPERATION_CANCELLED" => ToolErrors.OperationCancelled,
            _ => throw new ArgumentException($"Unknown error code: {expectedCode}")
        };

        // Assert
        error.Code.ShouldBe(expectedCode);
        error.Message.ShouldBe(expectedMessage);
    }

    [Theory]
    [InlineData("file_path")]
    [InlineData("project_path")]
    [InlineData("query")]
    public void MissingParameterError_ShouldIncludeParameterName(string parameterName)
    {
        // Act
        var error = ToolErrors.MissingParameter(parameterName);

        // Assert
        error.Code.ShouldBe("MISSING_PARAMETER");
        error.Message.ShouldContain(parameterName);
    }

    [Theory]
    [InlineData("invalid_level")]
    [InlineData("ultra")]
    [InlineData("highest")]
    public void InvalidPromotionLevelError_ShouldIncludeLevelValue(string level)
    {
        // Act
        var error = ToolErrors.InvalidPromotionLevel(level);

        // Assert
        error.Code.ShouldBe("INVALID_PROMOTION_LEVEL");
        error.Message.ShouldContain(level);
        error.Message.ShouldContain("standard");
        error.Message.ShouldContain("important");
        error.Message.ShouldContain("critical");
    }

    [Theory]
    [InlineData("unknown_type")]
    [InlineData("readme")]
    public void InvalidDocTypeError_ShouldIncludeDocType(string docType)
    {
        // Act
        var error = ToolErrors.InvalidDocType(docType);

        // Assert
        error.Code.ShouldBe("INVALID_DOC_TYPE");
        error.Message.ShouldContain(docType);
    }

    #endregion

    #region Tool Registration and Discovery Tests

    [Fact]
    public void SemanticSearchTool_ShouldHaveRequiredAttributes()
    {
        // Arrange
        var toolType = typeof(SemanticSearchTool);
        var method = toolType.GetMethod("SearchAsync");

        // Assert - Look for the real MCP attribute from ModelContextProtocol.Server
        var mcpToolTypeAttr = toolType.GetCustomAttributes(inherit: false)
            .FirstOrDefault(a => a.GetType().Name == "McpServerToolTypeAttribute");
        mcpToolTypeAttr.ShouldNotBeNull("SemanticSearchTool should have McpServerToolTypeAttribute");

        method.ShouldNotBeNull();
        var mcpToolAttr = method!.GetCustomAttributes(inherit: false)
            .FirstOrDefault(a => a.GetType().Name == "McpServerToolAttribute");
        mcpToolAttr.ShouldNotBeNull("SearchAsync should have McpServerToolAttribute");
    }

    [Fact]
    public void RagQueryTool_ShouldHaveRequiredAttributes()
    {
        // Arrange
        var toolType = typeof(RagQueryTool);
        var method = toolType.GetMethod("QueryAsync");

        // Assert - Look for the real MCP attribute from ModelContextProtocol.Server
        var mcpToolTypeAttr = toolType.GetCustomAttributes(inherit: false)
            .FirstOrDefault(a => a.GetType().Name == "McpServerToolTypeAttribute");
        mcpToolTypeAttr.ShouldNotBeNull("RagQueryTool should have McpServerToolTypeAttribute");

        method.ShouldNotBeNull();
        var mcpToolAttr = method!.GetCustomAttributes(inherit: false)
            .FirstOrDefault(a => a.GetType().Name == "McpServerToolAttribute");
        mcpToolAttr.ShouldNotBeNull("QueryAsync should have McpServerToolAttribute");
    }

    [Fact]
    public void IndexDocumentTool_ShouldHaveRequiredAttributes()
    {
        // Arrange
        var toolType = typeof(IndexDocumentTool);
        var method = toolType.GetMethod("IndexDocumentAsync");

        // Assert - Look for the real MCP attribute from ModelContextProtocol.Server
        var mcpToolTypeAttr = toolType.GetCustomAttributes(inherit: false)
            .FirstOrDefault(a => a.GetType().Name == "McpServerToolTypeAttribute");
        mcpToolTypeAttr.ShouldNotBeNull("IndexDocumentTool should have McpServerToolTypeAttribute");

        method.ShouldNotBeNull();
    }

    [Fact]
    public void UpdatePromotionLevelTool_ShouldHaveRequiredAttributes()
    {
        // Arrange
        var toolType = typeof(UpdatePromotionLevelTool);
        var method = toolType.GetMethod("UpdatePromotionLevelAsync");

        // Assert - Look for the real MCP attribute from ModelContextProtocol.Server
        var mcpToolTypeAttr = toolType.GetCustomAttributes(inherit: false)
            .FirstOrDefault(a => a.GetType().Name == "McpServerToolTypeAttribute");
        mcpToolTypeAttr.ShouldNotBeNull("UpdatePromotionLevelTool should have McpServerToolTypeAttribute");

        method.ShouldNotBeNull();
    }

    [Fact]
    public void ActivateProjectTool_ShouldHaveRequiredAttributes()
    {
        // Arrange
        var toolType = typeof(ActivateProjectTool);
        var method = toolType.GetMethod("ActivateProjectAsync");

        // Assert - Look for the real MCP attribute from ModelContextProtocol.Server
        var mcpToolTypeAttr = toolType.GetCustomAttributes(inherit: false)
            .FirstOrDefault(a => a.GetType().Name == "McpServerToolTypeAttribute");
        mcpToolTypeAttr.ShouldNotBeNull("ActivateProjectTool should have McpServerToolTypeAttribute");

        method.ShouldNotBeNull();
    }

    #endregion

    #region Session Lifecycle Tests

    [Fact]
    public async Task SessionLifecycle_Initialize_SearchAsync_Shutdown_ShouldSucceed()
    {
        // Arrange - Initialize phase
        var sessionContext = CreateLooseMock<ISessionContext>();
        var embeddingService = CreateLooseMock<IEmbeddingService>();
        var documentRepository = CreateLooseMock<IDocumentRepository>();
        var logger = CreateLooseMock<ILogger<SemanticSearchTool>>().Object;

        sessionContext.Setup(s => s.IsProjectActive).Returns(false);

        var searchTool = new SemanticSearchTool(
            documentRepository.Object,
            embeddingService.Object,
            sessionContext.Object,
            logger);

        // Act - Tool call before activation should fail
        var preActivationResult = await searchTool.SearchAsync("test", limit: 10);

        // Assert - Pre-activation
        preActivationResult.Success.ShouldBeFalse();
        preActivationResult.ErrorCode.ShouldBe("NO_ACTIVE_PROJECT");

        // Arrange - Post activation
        sessionContext.Setup(s => s.IsProjectActive).Returns(true);
        sessionContext.Setup(s => s.TenantKey).Returns("test:main:hash");

        embeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestEmbedding());

        documentRepository
            .Setup(r => r.SearchAsync(
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchResult>());

        // Act - Tool call after activation should succeed
        var postActivationResult = await searchTool.SearchAsync("test", limit: 10);

        // Assert - Post-activation
        postActivationResult.Success.ShouldBeTrue();

        // Shutdown - Verify no exceptions on deactivation
        sessionContext.Object.DeactivateProject();
    }

    [Fact]
    public async Task CancellationToken_WhenCancelled_ShouldReturnOperationCancelled()
    {
        // Arrange
        var sessionContext = CreateLooseMock<ISessionContext>();
        var embeddingService = CreateLooseMock<IEmbeddingService>();
        var documentRepository = CreateLooseMock<IDocumentRepository>();
        var logger = CreateLooseMock<ILogger<SemanticSearchTool>>().Object;

        sessionContext.Setup(s => s.IsProjectActive).Returns(true);
        sessionContext.Setup(s => s.TenantKey).Returns("test:main:hash");

        var cts = new CancellationTokenSource();
        cts.Cancel();

        embeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var searchTool = new SemanticSearchTool(
            documentRepository.Object,
            embeddingService.Object,
            sessionContext.Object,
            logger);

        // Act
        var result = await searchTool.SearchAsync("test", limit: 10, cancellationToken: cts.Token);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("OPERATION_CANCELLED");
    }

    #endregion

    #region Result Serialization Tests

    [Fact]
    public void SemanticSearchResult_ShouldSerializeWithCorrectPropertyNames()
    {
        // Arrange
        var result = new SemanticSearchResult
        {
            Query = "test query",
            TotalResults = 5,
            Documents = new List<DocumentMatch>
            {
                new DocumentMatch
                {
                    FilePath = "docs/test.md",
                    Title = "Test Document",
                    DocType = "spec",
                    PromotionLevel = "standard",
                    RelevanceScore = 0.95f,
                    ContentSnippet = "Test content..."
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(result, JsonOptions);
        var element = JsonSerializer.Deserialize<JsonElement>(json);

        // Assert - Check snake_case property names
        element.TryGetProperty("query", out _).ShouldBeTrue();
        element.TryGetProperty("total_results", out _).ShouldBeTrue();
        element.TryGetProperty("documents", out var docs).ShouldBeTrue();

        var firstDoc = docs.EnumerateArray().First();
        firstDoc.TryGetProperty("file_path", out _).ShouldBeTrue();
        firstDoc.TryGetProperty("doc_type", out _).ShouldBeTrue();
        firstDoc.TryGetProperty("promotion_level", out _).ShouldBeTrue();
        firstDoc.TryGetProperty("relevance_score", out _).ShouldBeTrue();
        firstDoc.TryGetProperty("content_snippet", out _).ShouldBeTrue();
    }

    [Fact]
    public void RagQueryResult_ShouldSerializeWithCorrectPropertyNames()
    {
        // Arrange
        var result = new RagQueryResult
        {
            Query = "test query",
            Answer = "Test answer",
            Sources = new List<RagSource>
            {
                new RagSource
                {
                    FilePath = "docs/source.md",
                    Title = "Source Doc",
                    DocType = "spec",
                    RelevanceScore = 0.9f
                }
            },
            Chunks = null,
            ConfidenceScore = 0.85f
        };

        // Act
        var json = JsonSerializer.Serialize(result, JsonOptions);
        var element = JsonSerializer.Deserialize<JsonElement>(json);

        // Assert
        element.TryGetProperty("query", out _).ShouldBeTrue();
        element.TryGetProperty("answer", out _).ShouldBeTrue();
        element.TryGetProperty("sources", out _).ShouldBeTrue();
        element.TryGetProperty("confidence_score", out _).ShouldBeTrue();

        // Chunks should be omitted when null
        element.TryGetProperty("chunks", out _).ShouldBeFalse();
    }

    [Fact]
    public void IndexDocumentResult_ShouldSerializeWithCorrectPropertyNames()
    {
        // Arrange
        var result = new IndexDocumentResult
        {
            FilePath = "docs/test.md",
            DocumentId = "doc-123",
            Title = "Test Document",
            DocType = "spec",
            ChunkCount = 3,
            Warnings = new List<string> { "Warning 1" },
            Message = "Success"
        };

        // Act
        var json = JsonSerializer.Serialize(result, JsonOptions);
        var element = JsonSerializer.Deserialize<JsonElement>(json);

        // Assert
        element.TryGetProperty("file_path", out _).ShouldBeTrue();
        element.TryGetProperty("document_id", out _).ShouldBeTrue();
        element.TryGetProperty("doc_type", out _).ShouldBeTrue();
        element.TryGetProperty("chunk_count", out _).ShouldBeTrue();
    }

    #endregion

    #region Input Validation Tests

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(-100, 10)]
    public async Task SemanticSearch_InvalidLimit_ShouldDefaultToValidValue(int invalidLimit, int expectedLimit)
    {
        // Arrange
        var sessionContext = CreateLooseMock<ISessionContext>();
        var embeddingService = CreateLooseMock<IEmbeddingService>();
        var documentRepository = CreateLooseMock<IDocumentRepository>();
        var logger = CreateLooseMock<ILogger<SemanticSearchTool>>().Object;

        sessionContext.Setup(s => s.IsProjectActive).Returns(true);
        sessionContext.Setup(s => s.TenantKey).Returns("test:main:hash");

        embeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestEmbedding());

        int capturedLimit = 0;
        documentRepository
            .Setup(r => r.SearchAsync(
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<ReadOnlyMemory<float>, string, int, float, string?, CancellationToken>(
                (_, _, limit, _, _, _) => capturedLimit = limit)
            .ReturnsAsync(new List<SearchResult>());

        var searchTool = new SemanticSearchTool(
            documentRepository.Object,
            embeddingService.Object,
            sessionContext.Object,
            logger);

        // Act
        await searchTool.SearchAsync("test", limit: invalidLimit);

        // Assert
        capturedLimit.ShouldBe(expectedLimit);
    }

    [Theory]
    [InlineData(150, 100)]
    [InlineData(1000, 100)]
    public async Task SemanticSearch_ExcessiveLimit_ShouldBeCapped(int excessiveLimit, int expectedLimit)
    {
        // Arrange
        var sessionContext = CreateLooseMock<ISessionContext>();
        var embeddingService = CreateLooseMock<IEmbeddingService>();
        var documentRepository = CreateLooseMock<IDocumentRepository>();
        var logger = CreateLooseMock<ILogger<SemanticSearchTool>>().Object;

        sessionContext.Setup(s => s.IsProjectActive).Returns(true);
        sessionContext.Setup(s => s.TenantKey).Returns("test:main:hash");

        embeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestEmbedding());

        int capturedLimit = 0;
        documentRepository
            .Setup(r => r.SearchAsync(
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<ReadOnlyMemory<float>, string, int, float, string?, CancellationToken>(
                (_, _, limit, _, _, _) => capturedLimit = limit)
            .ReturnsAsync(new List<SearchResult>());

        var searchTool = new SemanticSearchTool(
            documentRepository.Object,
            embeddingService.Object,
            sessionContext.Object,
            logger);

        // Act
        await searchTool.SearchAsync("test", limit: excessiveLimit);

        // Assert
        capturedLimit.ShouldBe(expectedLimit);
    }

    #endregion

    #region Helper Methods

    private static ReadOnlyMemory<float> CreateTestEmbedding(int dimensions = 1024)
    {
        var vector = new float[dimensions];
        var random = new Random(42);
        for (int i = 0; i < dimensions; i++)
        {
            vector[i] = (float)random.NextDouble();
        }
        return new ReadOnlyMemory<float>(vector);
    }

    #endregion

    #region Test Types

    private sealed class TestData
    {
        [JsonPropertyName("value")]
        public required string Value { get; init; }
    }

    #endregion
}
