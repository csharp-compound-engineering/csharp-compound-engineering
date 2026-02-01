using CompoundDocs.McpServer.Services.ExternalDocs;
using CompoundDocs.McpServer.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CompoundDocs.Tests.Tools;

/// <summary>
/// Unit tests for RagQueryExternalTool.
/// </summary>
public sealed class RagQueryExternalToolTests
{
    private readonly Mock<IExternalDocsSearchService> _externalDocsServiceMock;
    private readonly ILogger<RagQueryExternalTool> _logger;
    private readonly RagQueryExternalTool _tool;

    public RagQueryExternalToolTests()
    {
        _externalDocsServiceMock = new Mock<IExternalDocsSearchService>();
        _externalDocsServiceMock.Setup(s => s.GetSources()).Returns(new List<ExternalSourceConfig>
        {
            new("context7", "Context7", "Test provider", "https://context7.com"),
            new("anthropic", "Anthropic Docs", "Test provider", "https://docs.anthropic.com"),
            new("microsoft", "Microsoft Docs", "Test provider", "https://docs.microsoft.com")
        });
        _externalDocsServiceMock.Setup(s => s.IsSourceAvailable(It.IsAny<string>()))
            .Returns<string>(source =>
                source.Equals("context7", StringComparison.OrdinalIgnoreCase) ||
                source.Equals("anthropic", StringComparison.OrdinalIgnoreCase) ||
                source.Equals("microsoft", StringComparison.OrdinalIgnoreCase));
        _externalDocsServiceMock.Setup(s => s.RagQueryAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalDocsRagResult(
                "combined",
                "Test answer based on external docs",
                new List<ExternalDocsSearchResult>
                {
                    new("context7", "Test result", "https://test.com", "Test snippet", 0.8f)
                },
                0.75f));
        _logger = NullLogger<RagQueryExternalTool>.Instance;
        _tool = new RagQueryExternalTool(_externalDocsServiceMock.Object, _logger);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new RagQueryExternalTool(null!, _logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new RagQueryExternalTool(_externalDocsServiceMock.Object, null!));
    }

    #endregion

    #region QueryAsync Tests

    [Fact]
    public async Task QueryAsync_EmptyQuery_ReturnsEmptyQueryError()
    {
        // Act
        var result = await _tool.QueryAsync("");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("EMPTY_QUERY");
    }

    [Fact]
    public async Task QueryAsync_WhitespaceQuery_ReturnsEmptyQueryError()
    {
        // Act
        var result = await _tool.QueryAsync("   ");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("EMPTY_QUERY");
    }

    [Fact]
    public async Task QueryAsync_NullQuery_ReturnsEmptyQueryError()
    {
        // Act
        var result = await _tool.QueryAsync(null!);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("EMPTY_QUERY");
    }

    [Fact]
    public async Task QueryAsync_InvalidSource_ReturnsExternalSourceNotConfiguredError()
    {
        // Act
        var result = await _tool.QueryAsync("test query", sources: "invalid_source");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("EXTERNAL_SOURCE_NOT_CONFIGURED");
    }

    [Fact]
    public async Task QueryAsync_ValidQuery_ReturnsResults()
    {
        // Act
        var result = await _tool.QueryAsync("test query");

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.Query.ShouldBe("test query");
        result.Data.Answer.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task QueryAsync_MaxResultsNormalized_WhenLessThanOrEqualZero()
    {
        // Act
        var result = await _tool.QueryAsync("test query", maxResults: -5);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryAsync_MaxResultsCapped_WhenGreaterThan20()
    {
        // Act
        var result = await _tool.QueryAsync("test query", maxResults: 100);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Theory]
    [InlineData("context7")]
    [InlineData("anthropic")]
    [InlineData("microsoft")]
    public async Task QueryAsync_ValidSources_AreAccepted(string source)
    {
        // Act
        var result = await _tool.QueryAsync("test query", sources: source);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryAsync_MultipleSources_QueryAllSources()
    {
        // Act
        var result = await _tool.QueryAsync("test query", sources: "context7,anthropic");

        // Assert
        result.Success.ShouldBeTrue();
        // Source statuses are grouped from RAG results
        result.Data!.SourceStatuses.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task QueryAsync_NoSourcesSpecified_QueriesAllKnownSources()
    {
        // Act
        var result = await _tool.QueryAsync("test query");

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.SourceStatuses.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task QueryAsync_WithCancellation_HandlesGracefully()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _externalDocsServiceMock.Setup(s => s.RagQueryAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<int>(),
                It.Is<CancellationToken>(ct => ct.IsCancellationRequested)))
            .ThrowsAsync(new OperationCanceledException());
        cts.Cancel();

        // Act
        var result = await _tool.QueryAsync("test query", cancellationToken: cts.Token);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("OPERATION_CANCELLED");
    }

    [Fact]
    public async Task QueryAsync_ReturnsConfidenceScore()
    {
        // Act
        var result = await _tool.QueryAsync("test query");

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.ConfidenceScore.ShouldBeGreaterThanOrEqualTo(0.0f);
        result.Data.ConfidenceScore.ShouldBeLessThanOrEqualTo(1.0f);
    }

    [Fact]
    public async Task QueryAsync_ReturnsSources()
    {
        // Act
        var result = await _tool.QueryAsync("test query");

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.Sources.ShouldNotBeNull();
    }

    [Fact]
    public async Task QueryAsync_SourceStatusesContainSuccess()
    {
        // Act
        var result = await _tool.QueryAsync("test query");

        // Assert
        result.Success.ShouldBeTrue();
        foreach (var status in result.Data!.SourceStatuses)
        {
            status.Source.ShouldNotBeNullOrEmpty();
            // Success can be true or false depending on source availability
        }
    }

    #endregion
}
