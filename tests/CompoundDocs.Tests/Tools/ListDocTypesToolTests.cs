using CompoundDocs.McpServer.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompoundDocs.Tests.Tools;

/// <summary>
/// Unit tests for ListDocTypesTool.
/// </summary>
public sealed class ListDocTypesToolTests
{
    private readonly ILogger<ListDocTypesTool> _logger;
    private readonly ListDocTypesTool _tool;

    public ListDocTypesToolTests()
    {
        _logger = NullLogger<ListDocTypesTool>.Instance;
        _tool = new ListDocTypesTool(_logger);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ListDocTypesTool(null!));
    }

    #endregion

    #region ListDocTypesAsync Tests

    [Fact]
    public async Task ListDocTypesAsync_ReturnsSuccessfulResult()
    {
        // Act
        var result = await _tool.ListDocTypesAsync();

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
    }

    [Fact]
    public async Task ListDocTypesAsync_ContainsBuiltInDocTypes()
    {
        // Act
        var result = await _tool.ListDocTypesAsync();

        // Assert
        result.Data!.DocTypes.ShouldNotBeEmpty();
        result.Data.DocTypes.ShouldContain(d => d.Name == "spec");
        result.Data.DocTypes.ShouldContain(d => d.Name == "adr");
        result.Data.DocTypes.ShouldContain(d => d.Name == "research");
        result.Data.DocTypes.ShouldContain(d => d.Name == "doc");
    }

    [Fact]
    public async Task ListDocTypesAsync_DocTypesHaveRequiredFields()
    {
        // Act
        var result = await _tool.ListDocTypesAsync();

        // Assert
        foreach (var docType in result.Data!.DocTypes)
        {
            docType.Name.ShouldNotBeNullOrEmpty();
            docType.DisplayName.ShouldNotBeNullOrEmpty();
            docType.Description.ShouldNotBeNullOrEmpty();
            docType.RequiredFields.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task ListDocTypesAsync_ContainsPromotionLevels()
    {
        // Act
        var result = await _tool.ListDocTypesAsync();

        // Assert
        result.Data!.PromotionLevels.ShouldNotBeEmpty();
        result.Data.PromotionLevels.Count.ShouldBe(3); // standard, promoted, pinned
    }

    [Fact]
    public async Task ListDocTypesAsync_PromotionLevelsHaveCorrectBoostFactors()
    {
        // Act
        var result = await _tool.ListDocTypesAsync();

        // Assert
        var standard = result.Data!.PromotionLevels.FirstOrDefault(p => p.Name == "standard");
        var promoted = result.Data.PromotionLevels.FirstOrDefault(p => p.Name == "promoted");
        var pinned = result.Data.PromotionLevels.FirstOrDefault(p => p.Name == "pinned");

        standard.ShouldNotBeNull();
        promoted.ShouldNotBeNull();
        pinned.ShouldNotBeNull();

        standard!.BoostFactor.ShouldBe(1.0f);
        promoted!.BoostFactor.ShouldBe(1.5f);
        pinned!.BoostFactor.ShouldBe(2.0f);
    }

    [Fact]
    public async Task ListDocTypesAsync_TotalCountsMatchListCounts()
    {
        // Act
        var result = await _tool.ListDocTypesAsync();

        // Assert
        result.Data!.TotalDocTypes.ShouldBe(result.Data.DocTypes.Count);
        result.Data.TotalPromotionLevels.ShouldBe(result.Data.PromotionLevels.Count);
    }

    [Fact]
    public async Task ListDocTypesAsync_AllDocTypesAreBuiltIn()
    {
        // Act
        var result = await _tool.ListDocTypesAsync();

        // Assert
        result.Data!.DocTypes.ShouldAllBe(d => d.IsBuiltIn);
    }

    [Fact]
    public async Task ListDocTypesAsync_SpecDocTypeHasTitleRequired()
    {
        // Act
        var result = await _tool.ListDocTypesAsync();

        // Assert
        var spec = result.Data!.DocTypes.First(d => d.Name == "spec");
        spec.RequiredFields.ShouldContain("title");
    }

    [Fact]
    public async Task ListDocTypesAsync_AdrDocTypeHasStatusRequired()
    {
        // Act
        var result = await _tool.ListDocTypesAsync();

        // Assert
        var adr = result.Data!.DocTypes.First(d => d.Name == "adr");
        adr.RequiredFields.ShouldContain("status");
    }

    [Fact]
    public async Task ListDocTypesAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var cts = new CancellationTokenSource();

        // Act
        var result = await _tool.ListDocTypesAsync(cts.Token);

        // Assert
        result.Success.ShouldBeTrue();
    }

    #endregion
}
