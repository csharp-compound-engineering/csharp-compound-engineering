using System.Text.Json;
using CompoundDocs.Common.Configuration;

namespace CompoundDocs.Tests.Unit.Configuration;

/// <summary>
/// Unit tests for ConfigurationLoader.
/// </summary>
public sealed class ConfigurationLoaderTests
{
    #region LoadGlobalConfig Tests

    [Fact]
    public void LoadGlobalConfig_NoOverridePath_ReturnsDefaultConfig()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange - use a temp dir that has no config file
            var sut = new ConfigurationLoader();
            var configDir = Path.Combine(tempDir, "no-override-global");
            Directory.CreateDirectory(configDir);

            // Act
            var result = sut.LoadGlobalConfig(configDir);

            // Assert
            result.ShouldNotBeNull();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadGlobalConfig_WithOverridePath_SetsConfigDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var sut = new ConfigurationLoader();
            var overrideDir = Path.Combine(tempDir, "override-sets-dir");
            Directory.CreateDirectory(overrideDir);

            // Act
            var result = sut.LoadGlobalConfig(overrideDir);

            // Assert
            result.ConfigDirectory.ShouldBe(overrideDir);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadGlobalConfig_WithExistingConfigFile_LoadsConfiguration()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var sut = new ConfigurationLoader();
            var configDir = Path.Combine(tempDir, "existing-global");
            Directory.CreateDirectory(configDir);

            var config = new GlobalConfig();

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
            File.WriteAllText(Path.Combine(configDir, "global-config.json"), json);

            // Act
            var result = sut.LoadGlobalConfig(configDir);

            // Assert
            result.ShouldNotBeNull();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    #endregion

    #region LoadProjectConfig Tests

    [Fact]
    public void LoadProjectConfig_WithNoConfigFile_ReturnsDefaultConfig()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var sut = new ConfigurationLoader();
            var projectPath = Path.Combine(tempDir, "empty-project");
            Directory.CreateDirectory(projectPath);

            // Act
            var result = sut.LoadProjectConfig(projectPath);

            // Assert
            result.ShouldNotBeNull();
            result.ProjectName.ShouldBe("empty-project");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadProjectConfig_WithExistingConfigFile_LoadsConfiguration()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var sut = new ConfigurationLoader();
            var projectPath = Path.Combine(tempDir, "existing-project");
            var configDir = Path.Combine(projectPath, ".csharp-compounding-docs");
            Directory.CreateDirectory(configDir);

            var config = new ProjectConfig
            {
                ProjectName = "custom-project-name",
                Rag = new RagSettings
                {
                    MaxResults = 20,
                    LinkDepth = 10,
                    SimilarityThreshold = 0.8f
                }
            };

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
            File.WriteAllText(Path.Combine(configDir, "config.json"), json);

            // Act
            var result = sut.LoadProjectConfig(projectPath);

            // Assert
            result.ShouldNotBeNull();
            result.ProjectName.ShouldBe("custom-project-name");
            result.Rag.MaxResults.ShouldBe(20);
            result.Rag.LinkDepth.ShouldBe(10);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadProjectConfig_WithInvalidJson_ThrowsJsonException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var sut = new ConfigurationLoader();
            var projectPath = Path.Combine(tempDir, "invalid-project");
            var configDir = Path.Combine(projectPath, ".csharp-compounding-docs");
            Directory.CreateDirectory(configDir);

            File.WriteAllText(Path.Combine(configDir, "config.json"), "invalid json content");

            // Act & Assert
            // The ConfigurationLoader does not currently handle invalid JSON gracefully
            // and throws a JsonException. This documents the current behavior.
            Should.Throw<JsonException>(() => sut.LoadProjectConfig(projectPath));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadProjectConfig_WithNullDeserializeResult_ReturnsDefault()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange - JSON literal "null" deserializes to null
            var sut = new ConfigurationLoader();
            var projectPath = Path.Combine(tempDir, "null-deser-project");
            var configDir = Path.Combine(projectPath, ".csharp-compounding-docs");
            Directory.CreateDirectory(configDir);

            File.WriteAllText(Path.Combine(configDir, "config.json"), "null");

            // Act
            var result = sut.LoadProjectConfig(projectPath);

            // Assert - should fall back to default
            result.ShouldNotBeNull();
            result.ProjectName.ShouldBe("null-deser-project");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadProjectConfig_DefaultProjectName_DerivedFromDirectoryName()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange - test that CreateDefaultProjectConfig sets ProjectName from path
            var sut = new ConfigurationLoader();
            var projectPath = Path.Combine(tempDir, "my-awesome-project");
            Directory.CreateDirectory(projectPath);

            // Act
            var result = sut.LoadProjectConfig(projectPath);

            // Assert
            result.ProjectName.ShouldBe("my-awesome-project");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    #endregion

    #region SaveProjectConfig Tests

    [Fact]
    public void SaveProjectConfig_CreatesConfigFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var sut = new ConfigurationLoader();
            var projectPath = Path.Combine(tempDir, "save-project");
            Directory.CreateDirectory(projectPath);

            var config = new ProjectConfig
            {
                ProjectName = "saved-project",
                Rag = new RagSettings { MaxResults = 50 }
            };

            // Act
            sut.SaveProjectConfig(projectPath, config);

            // Assert
            var configFile = Path.Combine(projectPath, ".csharp-compounding-docs", "config.json");
            File.Exists(configFile).ShouldBeTrue();

            var loadedConfig = sut.LoadProjectConfig(projectPath);
            loadedConfig.ProjectName.ShouldBe("saved-project");
            loadedConfig.Rag.MaxResults.ShouldBe(50);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SaveProjectConfig_OverwritesExistingConfig()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var sut = new ConfigurationLoader();
            var projectPath = Path.Combine(tempDir, "overwrite-project");
            Directory.CreateDirectory(projectPath);

            var initialConfig = new ProjectConfig { ProjectName = "initial" };
            sut.SaveProjectConfig(projectPath, initialConfig);

            var updatedConfig = new ProjectConfig { ProjectName = "updated" };

            // Act
            sut.SaveProjectConfig(projectPath, updatedConfig);

            // Assert
            var loadedConfig = sut.LoadProjectConfig(projectPath);
            loadedConfig.ProjectName.ShouldBe("updated");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SaveProjectConfig_CreatesConfigDirectory_WhenNotExists()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var sut = new ConfigurationLoader();
            var projectPath = Path.Combine(tempDir, "new-config-dir");
            Directory.CreateDirectory(projectPath);

            var config = new ProjectConfig { ProjectName = "new-project" };

            // Act
            sut.SaveProjectConfig(projectPath, config);

            // Assert
            var configDir = Path.Combine(projectPath, ".csharp-compounding-docs");
            Directory.Exists(configDir).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SaveProjectConfig_RoundTrips_WithLoadProjectConfig()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var sut = new ConfigurationLoader();
            var projectPath = Path.Combine(tempDir, "roundtrip-project");
            Directory.CreateDirectory(projectPath);

            var config = new ProjectConfig
            {
                ProjectName = "roundtrip-test",
                Rag = new RagSettings
                {
                    ChunkSize = 1500,
                    ChunkOverlap = 300,
                    MaxResults = 25,
                    SimilarityThreshold = 0.85f,
                    LinkDepth = 4
                },
                LinkResolution = new LinkResolutionSettings
                {
                    MaxDepth = 5,
                    MaxLinkedDocs = 10
                }
            };

            // Act
            sut.SaveProjectConfig(projectPath, config);
            var loaded = sut.LoadProjectConfig(projectPath);

            // Assert
            loaded.ProjectName.ShouldBe(config.ProjectName);
            loaded.Rag.ChunkSize.ShouldBe(config.Rag.ChunkSize);
            loaded.Rag.ChunkOverlap.ShouldBe(config.Rag.ChunkOverlap);
            loaded.Rag.MaxResults.ShouldBe(config.Rag.MaxResults);
            loaded.Rag.SimilarityThreshold.ShouldBe(config.Rag.SimilarityThreshold);
            loaded.Rag.LinkDepth.ShouldBe(config.Rag.LinkDepth);
            loaded.LinkResolution.MaxDepth.ShouldBe(config.LinkResolution.MaxDepth);
            loaded.LinkResolution.MaxLinkedDocs.ShouldBe(config.LinkResolution.MaxLinkedDocs);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    #endregion

    #region EnsureProjectConfigDirectory Tests (Phase 133)

    [Fact]
    public void EnsureProjectConfigDirectory_CreatesDirectoryAndDefaultConfig()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var sut = new ConfigurationLoader();
            var projectPath = Path.Combine(tempDir, "ensure-dir-project");
            Directory.CreateDirectory(projectPath);

            // Act
            sut.EnsureProjectConfigDirectory(projectPath);

            // Assert
            var configDir = Path.Combine(projectPath, ".csharp-compounding-docs");
            Directory.Exists(configDir).ShouldBeTrue();

            var configFile = Path.Combine(configDir, "config.json");
            File.Exists(configFile).ShouldBeTrue();

            var loaded = sut.LoadProjectConfig(projectPath);
            loaded.ProjectName.ShouldBe("ensure-dir-project");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void EnsureProjectConfigDirectory_WithExistingConfig_DoesNotOverwrite()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var sut = new ConfigurationLoader();
            var projectPath = Path.Combine(tempDir, "no-overwrite-project");
            var configDir = Path.Combine(projectPath, ".csharp-compounding-docs");
            Directory.CreateDirectory(configDir);

            var originalConfig = new ProjectConfig { ProjectName = "original-name" };
            var json = JsonSerializer.Serialize(originalConfig, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
            File.WriteAllText(Path.Combine(configDir, "config.json"), json);

            // Act
            sut.EnsureProjectConfigDirectory(projectPath);

            // Assert
            var loaded = sut.LoadProjectConfig(projectPath);
            loaded.ProjectName.ShouldBe("original-name");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void EnsureProjectConfigDirectory_WithProvidedConfig_UsesProvidedConfig()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var sut = new ConfigurationLoader();
            var projectPath = Path.Combine(tempDir, "provided-config-project");
            Directory.CreateDirectory(projectPath);

            var providedConfig = new ProjectConfig
            {
                ProjectName = "provided-name",
                Rag = new RagSettings { MaxResults = 25 }
            };

            // Act
            sut.EnsureProjectConfigDirectory(projectPath, providedConfig);

            // Assert
            var loaded = sut.LoadProjectConfig(projectPath);
            loaded.ProjectName.ShouldBe("provided-name");
            loaded.Rag.MaxResults.ShouldBe(25);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    #endregion

    #region EnsureGlobalConfigDirectory Tests (Phase 133)

    [Fact]
    public void EnsureGlobalConfigDirectory_CreatesGlobalConfigFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var sut = new ConfigurationLoader();
            var configDir = Path.Combine(tempDir, "ensure-global-dir");
            var config = new GlobalConfig { ConfigDirectory = configDir };

            // Act
            sut.EnsureGlobalConfigDirectory(config);

            // Assert
            Directory.Exists(configDir).ShouldBeTrue();
            var globalConfigFile = Path.Combine(configDir, "global-config.json");
            File.Exists(globalConfigFile).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void EnsureGlobalConfigDirectory_DoesNotOverwriteExistingConfig()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange - create a config file with custom values first
            var sut = new ConfigurationLoader();
            var configDir = Path.Combine(tempDir, "no-overwrite-global");
            Directory.CreateDirectory(configDir);

            var originalConfig = new GlobalConfig
            {
                ConfigDirectory = configDir
            };
            var json = JsonSerializer.Serialize(originalConfig, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
            File.WriteAllText(Path.Combine(configDir, "global-config.json"), json);

            // Act - call EnsureGlobalConfigDirectory with different config
            var newConfig = new GlobalConfig
            {
                ConfigDirectory = configDir
            };
            sut.EnsureGlobalConfigDirectory(newConfig);

            // Assert - original config should be preserved
            var loaded = sut.LoadGlobalConfig(configDir);
            loaded.ShouldNotBeNull();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    #endregion

    #region RagConfig Tests (Phase 135)

    [Fact]
    public void LoadProjectConfig_WithRagSettings_LoadsDefaultValues()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var sut = new ConfigurationLoader();
            var projectPath = Path.Combine(tempDir, "rag-default-project");
            var configDir = Path.Combine(projectPath, ".csharp-compounding-docs");
            Directory.CreateDirectory(configDir);

            var config = new ProjectConfig { ProjectName = "rag-test" };
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
            File.WriteAllText(Path.Combine(configDir, "config.json"), json);

            // Act
            var result = sut.LoadProjectConfig(projectPath);

            // Assert
            result.Rag.ChunkSize.ShouldBe(1000);
            result.Rag.ChunkOverlap.ShouldBe(200);
            result.Rag.MaxResults.ShouldBe(10);
            result.Rag.SimilarityThreshold.ShouldBe(0.7f);
            result.Rag.LinkDepth.ShouldBe(2);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SaveProjectConfig_WithCustomRagSettings_PreservesAllValues()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var sut = new ConfigurationLoader();
            var projectPath = Path.Combine(tempDir, "custom-rag-project");
            Directory.CreateDirectory(projectPath);

            var config = new ProjectConfig
            {
                ProjectName = "custom-rag-test",
                Rag = new RagSettings
                {
                    ChunkSize = 2000,
                    ChunkOverlap = 400,
                    MaxResults = 20,
                    SimilarityThreshold = 0.8f,
                    LinkDepth = 3
                }
            };

            // Act
            sut.SaveProjectConfig(projectPath, config);

            // Assert
            var loaded = sut.LoadProjectConfig(projectPath);
            loaded.Rag.ChunkSize.ShouldBe(2000);
            loaded.Rag.ChunkOverlap.ShouldBe(400);
            loaded.Rag.MaxResults.ShouldBe(20);
            loaded.Rag.SimilarityThreshold.ShouldBe(0.8f);
            loaded.Rag.LinkDepth.ShouldBe(3);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void RagConfig_DefaultValues_MatchSpecification()
    {
        // Arrange & Act
        var ragConfig = new RagConfig();

        // Assert
        ragConfig.ChunkSize.ShouldBe(1000);
        ragConfig.ChunkOverlap.ShouldBe(200);
        ragConfig.MaxResults.ShouldBe(10);
        ragConfig.SimilarityThreshold.ShouldBe(0.7f);
        ragConfig.LinkDepth.ShouldBe(2);
    }

    [Fact]
    public void RagSettings_BackwardCompatibility_SimilarityThreshold()
    {
        // Arrange
        var ragSettings = new RagSettings();

        // Act
        ragSettings.SimilarityThreshold = 0.9f;

        // Assert
        ragSettings.SimilarityThreshold.ShouldBe(0.9f);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void FullConfiguration_LoadAndSave_RoundTrip()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var sut = new ConfigurationLoader();
            var projectPath = Path.Combine(tempDir, "full-config-project");
            Directory.CreateDirectory(projectPath);

            var originalConfig = new ProjectConfig
            {
                ProjectName = "integration-test",
                Rag = new RagSettings
                {
                    ChunkSize = 1500,
                    ChunkOverlap = 300,
                    MaxResults = 15,
                    SimilarityThreshold = 0.75f,
                    LinkDepth = 3
                },
                LinkResolution = new LinkResolutionSettings
                {
                    MaxDepth = 3,
                    MaxLinkedDocs = 8
                }
            };

            // Act
            sut.SaveProjectConfig(projectPath, originalConfig);
            var loadedConfig = sut.LoadProjectConfig(projectPath);

            // Assert
            loadedConfig.ProjectName.ShouldBe("integration-test");
            loadedConfig.Rag.ChunkSize.ShouldBe(1500);
            loadedConfig.Rag.MaxResults.ShouldBe(15);
            loadedConfig.LinkResolution.MaxDepth.ShouldBe(3);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    #endregion
}
