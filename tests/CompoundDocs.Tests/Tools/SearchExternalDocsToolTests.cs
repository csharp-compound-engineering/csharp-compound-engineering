using CompoundDocs.McpServer.Services.ExternalDocs;
using CompoundDocs.McpServer.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CompoundDocs.Tests.Tools;

/// <summary>
/// Unit tests for SearchExternalDocsTool.
/// </summary>
public sealed class SearchExternalDocsToolTests
{
    private readonly Mock<IExternalDocsSearchService> _externalDocsServiceMock;
    private readonly ILogger<SearchExternalDocsTool> _logger;
    private readonly SearchExternalDocsTool _tool;

    public SearchExternalDocsToolTests()
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
        _externalDocsServiceMock.Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExternalDocsSearchResult>
            {
                new("context7", "Test result", "https://test.com", "Test snippet", 0.8f)
            });
        _logger = NullLogger<SearchExternalDocsTool>.Instance;
        _tool = new SearchExternalDocsTool(_externalDocsServiceMock.Object, _logger);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new SearchExternalDocsTool(null!, _logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new SearchExternalDocsTool(_externalDocsServiceMock.Object, null!));
    }

    #endregion

    #region SearchAsync Tests

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsEmptyQueryError()
    {
        // Act
        var result = await _tool.SearchAsync("");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("EMPTY_QUERY");
    }

    [Fact]
    public async Task SearchAsync_WhitespaceQuery_ReturnsEmptyQueryError()
    {
        // Act
        var result = await _tool.SearchAsync("   ");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("EMPTY_QUERY");
    }

    [Fact]
    public async Task SearchAsync_NullQuery_ReturnsEmptyQueryError()
    {
        // Act
        var result = await _tool.SearchAsync(null!);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("EMPTY_QUERY");
    }

    [Fact]
    public async Task SearchAsync_InvalidSource_ReturnsExternalSourceNotConfiguredError()
    {
        // Act
        var result = await _tool.SearchAsync("test query", sources: "invalid_source");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("EXTERNAL_SOURCE_NOT_CONFIGURED");
    }

    [Fact]
    public async Task SearchAsync_ValidQuery_ReturnsResults()
    {
        // Act
        var result = await _tool.SearchAsync("test query");

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.Query.ShouldBe("test query");
        result.Data.Results.ShouldNotBeNull();
    }

    [Fact]
    public async Task SearchAsync_LimitNormalized_WhenLessThanOrEqualZero()
    {
        // Act
        var result = await _tool.SearchAsync("test query", limit: -5);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task SearchAsync_LimitCapped_WhenGreaterThan50()
    {
        // Act
        var result = await _tool.SearchAsync("test query", limit: 100);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Theory]
    [InlineData("context7")]
    [InlineData("anthropic")]
    [InlineData("microsoft")]
    public async Task SearchAsync_ValidSources_AreAccepted(string source)
    {
        // Act
        var result = await _tool.SearchAsync("test query", sources: source);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task SearchAsync_MultipleSources_SearchesAll()
    {
        // Act
        var result = await _tool.SearchAsync("test query", sources: "context7,anthropic");

        // Assert
        result.Success.ShouldBeTrue();
        // Results are grouped by source from search results
        result.Data!.SourceResults.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task SearchAsync_NoSourcesSpecified_SearchesAllKnownSources()
    {
        // Act
        var result = await _tool.SearchAsync("test query");

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.SourceResults.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task SearchAsync_WithCancellation_HandlesGracefully()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _externalDocsServiceMock.Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<int>(),
                It.Is<CancellationToken>(ct => ct.IsCancellationRequested)))
            .ThrowsAsync(new OperationCanceledException());
        cts.Cancel();

        // Act
        var result = await _tool.SearchAsync("test query", cancellationToken: cts.Token);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("OPERATION_CANCELLED");
    }

    #endregion

    #region ListSourcesAsync Tests

    [Fact]
    public async Task ListSourcesAsync_ReturnsSuccessfulResult()
    {
        // Act
        var result = await _tool.ListSourcesAsync();

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
    }

    [Fact]
    public async Task ListSourcesAsync_ContainsKnownSources()
    {
        // Act
        var result = await _tool.ListSourcesAsync();

        // Assert
        result.Data!.Sources.ShouldNotBeEmpty();
        result.Data.Sources.ShouldContain(s => s.Name == "context7");
        result.Data.Sources.ShouldContain(s => s.Name == "anthropic");
        result.Data.Sources.ShouldContain(s => s.Name == "microsoft");
    }

    [Fact]
    public async Task ListSourcesAsync_SourcesHaveRequiredFields()
    {
        // Act
        var result = await _tool.ListSourcesAsync();

        // Assert
        foreach (var source in result.Data!.Sources)
        {
            source.Name.ShouldNotBeNullOrEmpty();
            source.DisplayName.ShouldNotBeNullOrEmpty();
            source.Description.ShouldNotBeNullOrEmpty();
            source.BaseUrl.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task ListSourcesAsync_TotalSourcesMatchesListCount()
    {
        // Act
        var result = await _tool.ListSourcesAsync();

        // Assert
        result.Data!.TotalSources.ShouldBe(result.Data.Sources.Count);
    }

    #endregion
}
