using CompoundDocs.Common.Configuration;

namespace CompoundDocs.Tests.Unit.Configuration;

public class ProjectConfigTests
{
    // ── ProjectConfig ───────────────────────────────────────────────────

    [Fact]
    public void Default_ProjectName_IsNull()
    {
        // Arrange
        var config = new ProjectConfig();

        // Act
        var result = config.ProjectName;

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Default_Rag_IsNotNull()
    {
        // Arrange
        var config = new ProjectConfig();

        // Act
        var result = config.Rag;

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void Default_LinkResolution_IsNotNull()
    {
        // Arrange
        var config = new ProjectConfig();

        // Act
        var result = config.LinkResolution;

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void ProjectName_CanBeSet()
    {
        // Arrange
        var config = new ProjectConfig();

        // Act
        config.ProjectName = "MyProject";

        // Assert
        config.ProjectName.ShouldBe("MyProject");
    }

    // ── RagConfig ───────────────────────────────────────────────────────

    [Fact]
    public void RagConfig_Default_ChunkSize()
    {
        // Arrange
        var config = new RagConfig();

        // Act
        var result = config.ChunkSize;

        // Assert
        result.ShouldBe(1000);
    }

    [Fact]
    public void RagConfig_Default_ChunkOverlap()
    {
        // Arrange
        var config = new RagConfig();

        // Act
        var result = config.ChunkOverlap;

        // Assert
        result.ShouldBe(200);
    }

    [Fact]
    public void RagConfig_Default_MaxResults()
    {
        // Arrange
        var config = new RagConfig();

        // Act
        var result = config.MaxResults;

        // Assert
        result.ShouldBe(10);
    }

    [Fact]
    public void RagConfig_Default_SimilarityThreshold()
    {
        // Arrange
        var config = new RagConfig();

        // Act
        var result = config.SimilarityThreshold;

        // Assert
        result.ShouldBe(0.7f);
    }

    [Fact]
    public void RagConfig_Default_LinkDepth()
    {
        // Arrange
        var config = new RagConfig();

        // Act
        var result = config.LinkDepth;

        // Assert
        result.ShouldBe(2);
    }

    // ── RagSettings ─────────────────────────────────────────────────────

    [Fact]
    public void RagSettings_Default_ChunkSize()
    {
        // Arrange
        var config = new RagSettings();

        // Act
        var result = config.ChunkSize;

        // Assert
        result.ShouldBe(1000);
    }

    [Fact]
    public void RagSettings_Default_ChunkOverlap()
    {
        // Arrange
        var config = new RagSettings();

        // Act
        var result = config.ChunkOverlap;

        // Assert
        result.ShouldBe(200);
    }

    [Fact]
    public void RagSettings_Default_MaxResults()
    {
        // Arrange
        var config = new RagSettings();

        // Act
        var result = config.MaxResults;

        // Assert
        result.ShouldBe(10);
    }

    [Fact]
    public void RagSettings_Default_SimilarityThreshold()
    {
        // Arrange
        var config = new RagSettings();

        // Act
        var result = config.SimilarityThreshold;

        // Assert
        result.ShouldBe(0.7f);
    }

    [Fact]
    public void RagSettings_Default_LinkDepth()
    {
        // Arrange
        var config = new RagSettings();

        // Act
        var result = config.LinkDepth;

        // Assert
        result.ShouldBe(2);
    }

#pragma warning disable CS0618 // Type or member is obsolete
    [Fact]
    public void RagSettings_RelevanceThreshold_Get_ReturnsSimilarityThreshold()
    {
        // Arrange
        var config = new RagSettings { SimilarityThreshold = 0.9f };

        // Act
        var result = config.RelevanceThreshold;

        // Assert
        result.ShouldBe(0.9f);
    }

    [Fact]
    public void RagSettings_RelevanceThreshold_Set_UpdatesSimilarityThreshold()
    {
        // Arrange
        var config = new RagSettings();

        // Act
        config.RelevanceThreshold = 0.5f;

        // Assert
        config.SimilarityThreshold.ShouldBe(0.5f);
    }

    [Fact]
    public void RagSettings_Default_MaxLinkedDocs()
    {
        // Arrange
        var config = new RagSettings();

        // Act
        var result = config.MaxLinkedDocs;

        // Assert
        result.ShouldBe(5);
    }
#pragma warning restore CS0618

    // ── LinkResolutionSettings ──────────────────────────────────────────

    [Fact]
    public void LinkResolutionSettings_Default_MaxDepth()
    {
        // Arrange
        var config = new LinkResolutionSettings();

        // Act
        var result = config.MaxDepth;

        // Assert
        result.ShouldBe(2);
    }

    [Fact]
    public void LinkResolutionSettings_Default_MaxLinkedDocs()
    {
        // Arrange
        var config = new LinkResolutionSettings();

        // Act
        var result = config.MaxLinkedDocs;

        // Assert
        result.ShouldBe(5);
    }
}
