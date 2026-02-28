using CompoundDocs.Common.Configuration;

namespace CompoundDocs.Tests.Unit.Configuration;

public class GlobalConfigTests
{
    // ── GlobalConfig ────────────────────────────────────────────────

    [Fact]
    public void Default_ConfigDirectory_ContainsClaude()
    {
        // Arrange
        var config = new GlobalConfig();

        // Act
        var result = config.ConfigDirectory;

        // Assert
        result.ShouldContain(".claude");
    }

    [Fact]
    public void Default_ConfigDirectory_ContainsProjectDir()
    {
        // Arrange
        var config = new GlobalConfig();

        // Act
        var result = config.ConfigDirectory;

        // Assert
        result.ShouldContain(".csharp-compounding-docs");
    }

    [Fact]
    public void ConfigDirectory_CanBeOverridden()
    {
        // Arrange
        var config = new GlobalConfig();

        // Act
        config.ConfigDirectory = "/custom";

        // Assert
        config.ConfigDirectory.ShouldBe("/custom");
    }
}
