using CompoundDocs.McpServer.Filters;
using CompoundDocs.McpServer.Models;
using CompoundDocs.Tests.Utilities;

namespace CompoundDocs.Tests.Features;

/// <summary>
/// Unit tests for promotion level functionality including the PromotionLevel enum,
/// PromotionLevels static class, and document promotion features.
/// </summary>
public sealed class PromotionLevelTests
{
    #region PromotionLevel Enum Tests

    [Fact]
    public void PromotionLevel_Enum_HasExpectedValues()
    {
        // Assert - Verify all expected values exist
        var values = Enum.GetValues<PromotionLevel>();

        values.ShouldContain(PromotionLevel.Standard);
        values.ShouldContain(PromotionLevel.Important);
        values.ShouldContain(PromotionLevel.Critical);
    }

    [Fact]
    public void PromotionLevel_Enum_HasCorrectOrdinalValues()
    {
        // Assert - Standard < Important < Critical
        ((int)PromotionLevel.Standard).ShouldBe(0);
        ((int)PromotionLevel.Important).ShouldBe(1);
        ((int)PromotionLevel.Critical).ShouldBe(2);
    }

    [Fact]
    public void PromotionLevel_Enum_SupportsComparison()
    {
        // Assert - Can compare promotion levels
        (PromotionLevel.Standard < PromotionLevel.Important).ShouldBeTrue();
        (PromotionLevel.Important < PromotionLevel.Critical).ShouldBeTrue();
        (PromotionLevel.Standard < PromotionLevel.Critical).ShouldBeTrue();
    }

    [Theory]
    [InlineData("standard", PromotionLevel.Standard)]
    [InlineData("Standard", PromotionLevel.Standard)]
    [InlineData("STANDARD", PromotionLevel.Standard)]
    [InlineData("important", PromotionLevel.Important)]
    [InlineData("Important", PromotionLevel.Important)]
    [InlineData("critical", PromotionLevel.Critical)]
    [InlineData("Critical", PromotionLevel.Critical)]
    public void PromotionLevel_Enum_ParsesCaseInsensitively(string input, PromotionLevel expected)
    {
        // Act
        var parsed = Enum.TryParse<PromotionLevel>(input, ignoreCase: true, out var result);

        // Assert
        parsed.ShouldBeTrue();
        result.ShouldBe(expected);
    }

    #endregion

    #region PromotionLevels Static Class Tests

    [Fact]
    public void PromotionLevels_All_ContainsAllLevels()
    {
        // Assert
        PromotionLevels.All.ShouldNotBeEmpty();
        PromotionLevels.All.ShouldContain(PromotionLevels.Standard);
        PromotionLevels.All.ShouldContain(PromotionLevels.Promoted);
        PromotionLevels.All.ShouldContain(PromotionLevels.Pinned);
    }

    [Theory]
    [InlineData("standard", true)]
    [InlineData("Standard", true)]
    [InlineData("STANDARD", true)]
    [InlineData("promoted", true)]
    [InlineData("Promoted", true)]
    [InlineData("pinned", true)]
    [InlineData("Pinned", true)]
    [InlineData("invalid", false)]
    [InlineData("", false)]
    public void PromotionLevels_IsValid_ValidatesCorrectly(string level, bool expected)
    {
        // Act
        var isValid = PromotionLevels.IsValid(level);

        // Assert
        isValid.ShouldBe(expected);
    }

    [Theory]
    [InlineData("standard", 1.0f)]
    [InlineData("promoted", 1.5f)]
    [InlineData("pinned", 2.0f)]
    [InlineData("unknown", 1.0f)] // Falls back to standard
    public void PromotionLevels_GetBoostFactor_ReturnsCorrectBoost(string level, float expectedBoost)
    {
        // Act
        var boost = PromotionLevels.GetBoostFactor(level);

        // Assert
        boost.ShouldBe(expectedBoost);
    }

    [Fact]
    public void PromotionLevels_BoostFactors_AreInAscendingOrder()
    {
        // Arrange
        var standardBoost = PromotionLevels.GetBoostFactor(PromotionLevels.Standard);
        var promotedBoost = PromotionLevels.GetBoostFactor(PromotionLevels.Promoted);
        var pinnedBoost = PromotionLevels.GetBoostFactor(PromotionLevels.Pinned);

        // Assert
        standardBoost.ShouldBeLessThan(promotedBoost);
        promotedBoost.ShouldBeLessThan(pinnedBoost);
    }

    #endregion

    #region CompoundDocument Promotion Tests

    [Fact]
    public void CompoundDocument_DefaultPromotionLevel_IsStandard()
    {
        // Arrange & Act
        var document = new CompoundDocument();

        // Assert
        document.PromotionLevel.ShouldBe(PromotionLevels.Standard);
    }

    [Fact]
    public void CompoundDocument_PromotionLevel_CanBeSet()
    {
        // Arrange
        var document = new CompoundDocument
        {
            PromotionLevel = PromotionLevels.Promoted
        };

        // Assert
        document.PromotionLevel.ShouldBe(PromotionLevels.Promoted);
    }

    [Theory]
    [InlineData("standard")]
    [InlineData("promoted")]
    [InlineData("pinned")]
    public void CompoundDocument_PromotionLevel_AcceptsValidValues(string level)
    {
        // Arrange & Act
        var document = new CompoundDocument
        {
            PromotionLevel = level
        };

        // Assert
        document.PromotionLevel.ShouldBe(level);
    }

    #endregion

    #region TestDocumentBuilder Promotion Tests

    [Fact]
    public void TestDocumentBuilder_AsStandard_SetsCorrectLevel()
    {
        // Arrange & Act
        var document = TestDocumentBuilder.Create()
            .AsStandard()
            .Build();

        // Assert
        document.PromotionLevel.ShouldBe(PromotionLevels.Standard);
    }

    [Fact]
    public void TestDocumentBuilder_AsPromoted_SetsCorrectLevel()
    {
        // Arrange & Act
        var document = TestDocumentBuilder.Create()
            .AsPromoted()
            .Build();

        // Assert
        document.PromotionLevel.ShouldBe(PromotionLevels.Promoted);
    }

    [Fact]
    public void TestDocumentBuilder_AsPinned_SetsCorrectLevel()
    {
        // Arrange & Act
        var document = TestDocumentBuilder.Create()
            .AsPinned()
            .Build();

        // Assert
        document.PromotionLevel.ShouldBe(PromotionLevels.Pinned);
    }

    [Fact]
    public void TestDocumentBuilder_WithPromotionLevel_SetsCustomLevel()
    {
        // Arrange & Act
        var document = TestDocumentBuilder.Create()
            .WithPromotionLevel("custom-level")
            .Build();

        // Assert
        document.PromotionLevel.ShouldBe("custom-level");
    }

    #endregion

    #region DocumentChunk Promotion Tests

    [Fact]
    public void DocumentChunk_DefaultPromotionLevel_IsStandard()
    {
        // Arrange & Act
        var chunk = new DocumentChunk();

        // Assert
        chunk.PromotionLevel.ShouldBe(PromotionLevels.Standard);
    }

    [Fact]
    public void DocumentChunk_CreateFromParent_InheritsPromotionLevel()
    {
        // Arrange
        var parent = TestDocumentBuilder.Create()
            .AsPromoted()
            .Build();

        // Act
        var chunk = DocumentChunk.CreateFromParent(
            parent,
            headerPath: "## Section",
            content: "Chunk content",
            startLine: 1,
            endLine: 10);

        // Assert
        chunk.PromotionLevel.ShouldBe(PromotionLevels.Promoted);
    }

    [Fact]
    public void DocumentChunk_CreateFromParent_InheritsPinnedLevel()
    {
        // Arrange
        var parent = TestDocumentBuilder.Create()
            .AsPinned()
            .Build();

        // Act
        var chunk = DocumentChunk.CreateFromParent(
            parent,
            headerPath: "## Section",
            content: "Critical content",
            startLine: 1,
            endLine: 10);

        // Assert
        chunk.PromotionLevel.ShouldBe(PromotionLevels.Pinned);
    }

    #endregion

    #region Search Boost Calculation Tests

    [Theory]
    [InlineData(0.8f, "standard", 0.8f)]   // 0.8 * 1.0 = 0.8
    [InlineData(0.8f, "promoted", 1.2f)]   // 0.8 * 1.5 = 1.2
    [InlineData(0.8f, "pinned", 1.6f)]     // 0.8 * 2.0 = 1.6
    public void PromotionBoost_CalculatesCorrectBoostedScore(
        float baseScore,
        string promotionLevel,
        float expectedBoostedScore)
    {
        // Arrange
        var boostFactor = PromotionLevels.GetBoostFactor(promotionLevel);

        // Act
        var boostedScore = baseScore * boostFactor;

        // Assert
        boostedScore.ShouldBe(expectedBoostedScore, 0.001f);
    }

    [Fact]
    public void PromotionBoost_PinnedDocument_RanksHigherThanStandard()
    {
        // Arrange
        var standardScore = 0.9f * PromotionLevels.GetBoostFactor(PromotionLevels.Standard);
        var pinnedScore = 0.7f * PromotionLevels.GetBoostFactor(PromotionLevels.Pinned);

        // Act & Assert - Even with lower base score, pinned ranks higher
        pinnedScore.ShouldBeGreaterThan(standardScore);
    }

    [Fact]
    public void PromotionBoost_PromotedDocument_RanksHigherThanStandard()
    {
        // Arrange
        var standardScore = 0.8f * PromotionLevels.GetBoostFactor(PromotionLevels.Standard);
        var promotedScore = 0.6f * PromotionLevels.GetBoostFactor(PromotionLevels.Promoted);

        // Act & Assert - Promoted with lower base still ranks higher
        promotedScore.ShouldBeGreaterThan(standardScore);
    }

    #endregion

    #region TenantFilterCriteria Promotion Tests

    [Fact]
    public void TenantFilterCriteria_WithPromotionLevel_SetsLevel()
    {
        // Arrange
        var sessionContextMock = new Mock<CompoundDocs.McpServer.Session.ISessionContext>();
        sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);
        sessionContextMock.Setup(s => s.ProjectName).Returns("test-project");
        sessionContextMock.Setup(s => s.ActiveBranch).Returns("main");
        sessionContextMock.Setup(s => s.PathHash).Returns("abc123");

        // Act
        var filter = TenantFilterCriteria.FromSessionContext(
            sessionContextMock.Object,
            PromotionLevel.Important);

        // Assert
        filter.PromotionLevel.ShouldBe(PromotionLevel.Important);
    }

    [Fact]
    public void TenantFilterCriteria_WithMinimumPromotionLevel_SetsMinLevel()
    {
        // Arrange
        var sessionContextMock = new Mock<CompoundDocs.McpServer.Session.ISessionContext>();
        sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);
        sessionContextMock.Setup(s => s.ProjectName).Returns("test-project");
        sessionContextMock.Setup(s => s.ActiveBranch).Returns("main");
        sessionContextMock.Setup(s => s.PathHash).Returns("abc123");

        // Act
        var filter = TenantFilterCriteria.FromSessionContextWithMinimumPromotion(
            sessionContextMock.Object,
            PromotionLevel.Important);

        // Assert
        filter.MinimumPromotionLevel.ShouldBe(PromotionLevel.Important);
    }

    [Theory]
    [InlineData(PromotionLevel.Standard, 3)]  // All levels
    [InlineData(PromotionLevel.Important, 2)] // Important and Critical
    [InlineData(PromotionLevel.Critical, 1)]  // Only Critical
    public void TenantFilterCriteria_GetPromotionLevelsAtOrAboveMinimum_ReturnsCorrectCount(
        PromotionLevel minimum,
        int expectedCount)
    {
        // Arrange
        var sessionContextMock = new Mock<CompoundDocs.McpServer.Session.ISessionContext>();
        sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);
        sessionContextMock.Setup(s => s.ProjectName).Returns("test-project");
        sessionContextMock.Setup(s => s.ActiveBranch).Returns("main");
        sessionContextMock.Setup(s => s.PathHash).Returns("abc123");

        var filter = TenantFilterCriteria.FromSessionContextWithMinimumPromotion(
            sessionContextMock.Object,
            minimum);

        // Act
        var levels = filter.GetPromotionLevelsAtOrAboveMinimum();

        // Assert
        levels.Count.ShouldBe(expectedCount);
    }

    #endregion
}
