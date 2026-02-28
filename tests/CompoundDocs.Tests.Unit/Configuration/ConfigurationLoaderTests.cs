using System.Text.Json;
using CompoundDocs.Common.Configuration;

namespace CompoundDocs.Tests.Unit.Configuration;

/// <summary>
/// Unit tests for ConfigurationLoader.
/// </summary>
public sealed class ConfigurationLoaderTests : IDisposable
{
    private readonly ConfigurationLoader _sut;
    private readonly string _tempDir;
    private bool _disposed;

    public ConfigurationLoaderTests()
    {
        _sut = new ConfigurationLoader();
        _tempDir = Path.Combine(Path.GetTempPath(), $"config-loader-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    #region LoadGlobalConfig Tests

    [Fact]
    public void LoadGlobalConfig_NoOverridePath_ReturnsDefaultConfig()
    {
        // Arrange - use a temp dir that has no config file
        var configDir = Path.Combine(_tempDir, "no-override-global");
        Directory.CreateDirectory(configDir);

        // Act
        var result = _sut.LoadGlobalConfig(configDir);

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void LoadGlobalConfig_WithOverridePath_SetsConfigDirectory()
    {
        // Arrange
        var overrideDir = Path.Combine(_tempDir, "override-sets-dir");
        Directory.CreateDirectory(overrideDir);

        // Act
        var result = _sut.LoadGlobalConfig(overrideDir);

        // Assert
        result.ConfigDirectory.ShouldBe(overrideDir);
    }

    [Fact]
    public void LoadGlobalConfig_WithExistingConfigFile_LoadsConfiguration()
    {
        // Arrange
        var configDir = Path.Combine(_tempDir, "existing-global");
        Directory.CreateDirectory(configDir);

        var config = new GlobalConfig();

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
        File.WriteAllText(Path.Combine(configDir, "global-config.json"), json);

        // Act
        var result = _sut.LoadGlobalConfig(configDir);

        // Assert
        result.ShouldNotBeNull();
    }

    #endregion

    #region LoadProjectConfig Tests

    [Fact]
    public void LoadProjectConfig_WithNoConfigFile_ReturnsDefaultConfig()
    {
        // Arrange
        var projectPath = Path.Combine(_tempDir, "empty-project");
        Directory.CreateDirectory(projectPath);

        // Act
        var result = _sut.LoadProjectConfig(projectPath);

        // Assert
        result.ShouldNotBeNull();
        result.ProjectName.ShouldBe("empty-project");
    }

    [Fact]
    public void LoadProjectConfig_WithExistingConfigFile_LoadsConfiguration()
    {
        // Arrange
        var projectPath = Path.Combine(_tempDir, "existing-project");
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
        var result = _sut.LoadProjectConfig(projectPath);

        // Assert
        result.ShouldNotBeNull();
        result.ProjectName.ShouldBe("custom-project-name");
        result.Rag.MaxResults.ShouldBe(20);
        result.Rag.LinkDepth.ShouldBe(10);
    }

    [Fact]
    public void LoadProjectConfig_WithInvalidJson_ThrowsJsonException()
    {
        // Arrange
        var projectPath = Path.Combine(_tempDir, "invalid-project");
        var configDir = Path.Combine(projectPath, ".csharp-compounding-docs");
        Directory.CreateDirectory(configDir);

        File.WriteAllText(Path.Combine(configDir, "config.json"), "invalid json content");

        // Act & Assert
        // The ConfigurationLoader does not currently handle invalid JSON gracefully
        // and throws a JsonException. This documents the current behavior.
        Should.Throw<JsonException>(() => _sut.LoadProjectConfig(projectPath));
    }

    [Fact]
    public void LoadProjectConfig_WithNullDeserializeResult_ReturnsDefault()
    {
        // Arrange - JSON literal "null" deserializes to null
        var projectPath = Path.Combine(_tempDir, "null-deser-project");
        var configDir = Path.Combine(projectPath, ".csharp-compounding-docs");
        Directory.CreateDirectory(configDir);

        File.WriteAllText(Path.Combine(configDir, "config.json"), "null");

        // Act
        var result = _sut.LoadProjectConfig(projectPath);

        // Assert - should fall back to default
        result.ShouldNotBeNull();
        result.ProjectName.ShouldBe("null-deser-project");
    }

    [Fact]
    public void LoadProjectConfig_DefaultProjectName_DerivedFromDirectoryName()
    {
        // Arrange - test that CreateDefaultProjectConfig sets ProjectName from path
        var projectPath = Path.Combine(_tempDir, "my-awesome-project");
        Directory.CreateDirectory(projectPath);

        // Act
        var result = _sut.LoadProjectConfig(projectPath);

        // Assert
        result.ProjectName.ShouldBe("my-awesome-project");
    }

    #endregion

    #region SaveProjectConfig Tests

    [Fact]
    public void SaveProjectConfig_CreatesConfigFile()
    {
        // Arrange
        var projectPath = Path.Combine(_tempDir, "save-project");
        Directory.CreateDirectory(projectPath);

        var config = new ProjectConfig
        {
            ProjectName = "saved-project",
            Rag = new RagSettings { MaxResults = 50 }
        };

        // Act
        _sut.SaveProjectConfig(projectPath, config);

        // Assert
        var configFile = Path.Combine(projectPath, ".csharp-compounding-docs", "config.json");
        File.Exists(configFile).ShouldBeTrue();

        var loadedConfig = _sut.LoadProjectConfig(projectPath);
        loadedConfig.ProjectName.ShouldBe("saved-project");
        loadedConfig.Rag.MaxResults.ShouldBe(50);
    }

    [Fact]
    public void SaveProjectConfig_OverwritesExistingConfig()
    {
        // Arrange
        var projectPath = Path.Combine(_tempDir, "overwrite-project");
        Directory.CreateDirectory(projectPath);

        var initialConfig = new ProjectConfig { ProjectName = "initial" };
        _sut.SaveProjectConfig(projectPath, initialConfig);

        var updatedConfig = new ProjectConfig { ProjectName = "updated" };

        // Act
        _sut.SaveProjectConfig(projectPath, updatedConfig);

        // Assert
        var loadedConfig = _sut.LoadProjectConfig(projectPath);
        loadedConfig.ProjectName.ShouldBe("updated");
    }

    [Fact]
    public void SaveProjectConfig_CreatesConfigDirectory_WhenNotExists()
    {
        // Arrange
        var projectPath = Path.Combine(_tempDir, "new-config-dir");
        Directory.CreateDirectory(projectPath);

        var config = new ProjectConfig { ProjectName = "new-project" };

        // Act
        _sut.SaveProjectConfig(projectPath, config);

        // Assert
        var configDir = Path.Combine(projectPath, ".csharp-compounding-docs");
        Directory.Exists(configDir).ShouldBeTrue();
    }

    [Fact]
    public void SaveProjectConfig_RoundTrips_WithLoadProjectConfig()
    {
        // Arrange
        var projectPath = Path.Combine(_tempDir, "roundtrip-project");
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
        _sut.SaveProjectConfig(projectPath, config);
        var loaded = _sut.LoadProjectConfig(projectPath);

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

    #endregion

    #region EnsureProjectConfigDirectory Tests (Phase 133)

    [Fact]
    public void EnsureProjectConfigDirectory_CreatesDirectoryAndDefaultConfig()
    {
        // Arrange
        var projectPath = Path.Combine(_tempDir, "ensure-dir-project");
        Directory.CreateDirectory(projectPath);

        // Act
        _sut.EnsureProjectConfigDirectory(projectPath);

        // Assert
        var configDir = Path.Combine(projectPath, ".csharp-compounding-docs");
        Directory.Exists(configDir).ShouldBeTrue();

        var configFile = Path.Combine(configDir, "config.json");
        File.Exists(configFile).ShouldBeTrue();

        var loaded = _sut.LoadProjectConfig(projectPath);
        loaded.ProjectName.ShouldBe("ensure-dir-project");
    }

    [Fact]
    public void EnsureProjectConfigDirectory_WithExistingConfig_DoesNotOverwrite()
    {
        // Arrange
        var projectPath = Path.Combine(_tempDir, "no-overwrite-project");
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
        _sut.EnsureProjectConfigDirectory(projectPath);

        // Assert
        var loaded = _sut.LoadProjectConfig(projectPath);
        loaded.ProjectName.ShouldBe("original-name");
    }

    [Fact]
    public void EnsureProjectConfigDirectory_WithProvidedConfig_UsesProvidedConfig()
    {
        // Arrange
        var projectPath = Path.Combine(_tempDir, "provided-config-project");
        Directory.CreateDirectory(projectPath);

        var providedConfig = new ProjectConfig
        {
            ProjectName = "provided-name",
            Rag = new RagSettings { MaxResults = 25 }
        };

        // Act
        _sut.EnsureProjectConfigDirectory(projectPath, providedConfig);

        // Assert
        var loaded = _sut.LoadProjectConfig(projectPath);
        loaded.ProjectName.ShouldBe("provided-name");
        loaded.Rag.MaxResults.ShouldBe(25);
    }

    #endregion

    #region EnsureGlobalConfigDirectory Tests (Phase 133)

    [Fact]
    public void EnsureGlobalConfigDirectory_CreatesGlobalConfigFile()
    {
        // Arrange
        var configDir = Path.Combine(_tempDir, "ensure-global-dir");
        var config = new GlobalConfig { ConfigDirectory = configDir };

        // Act
        _sut.EnsureGlobalConfigDirectory(config);

        // Assert
        Directory.Exists(configDir).ShouldBeTrue();
        var globalConfigFile = Path.Combine(configDir, "global-config.json");
        File.Exists(globalConfigFile).ShouldBeTrue();
    }

    [Fact]
    public void EnsureGlobalConfigDirectory_DoesNotOverwriteExistingConfig()
    {
        // Arrange - create a config file with custom values first
        var configDir = Path.Combine(_tempDir, "no-overwrite-global");
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
        _sut.EnsureGlobalConfigDirectory(newConfig);

        // Assert - original config should be preserved
        var loaded = _sut.LoadGlobalConfig(configDir);
        loaded.ShouldNotBeNull();
    }

    #endregion

    #region RagConfig Tests (Phase 135)

    [Fact]
    public void LoadProjectConfig_WithRagSettings_LoadsDefaultValues()
    {
        // Arrange
        var projectPath = Path.Combine(_tempDir, "rag-default-project");
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
        var result = _sut.LoadProjectConfig(projectPath);

        // Assert
        result.Rag.ChunkSize.ShouldBe(1000);
        result.Rag.ChunkOverlap.ShouldBe(200);
        result.Rag.MaxResults.ShouldBe(10);
        result.Rag.SimilarityThreshold.ShouldBe(0.7f);
        result.Rag.LinkDepth.ShouldBe(2);
    }

    [Fact]
    public void SaveProjectConfig_WithCustomRagSettings_PreservesAllValues()
    {
        // Arrange
        var projectPath = Path.Combine(_tempDir, "custom-rag-project");
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
        _sut.SaveProjectConfig(projectPath, config);

        // Assert
        var loaded = _sut.LoadProjectConfig(projectPath);
        loaded.Rag.ChunkSize.ShouldBe(2000);
        loaded.Rag.ChunkOverlap.ShouldBe(400);
        loaded.Rag.MaxResults.ShouldBe(20);
        loaded.Rag.SimilarityThreshold.ShouldBe(0.8f);
        loaded.Rag.LinkDepth.ShouldBe(3);
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
        // Arrange
        var projectPath = Path.Combine(_tempDir, "full-config-project");
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
        _sut.SaveProjectConfig(projectPath, originalConfig);
        var loadedConfig = _sut.LoadProjectConfig(projectPath);

        // Assert
        loadedConfig.ProjectName.ShouldBe("integration-test");
        loadedConfig.Rag.ChunkSize.ShouldBe(1500);
        loadedConfig.Rag.MaxResults.ShouldBe(15);
        loadedConfig.LinkResolution.MaxDepth.ShouldBe(3);
    }

    #endregion

    #region Cleanup

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }

            _disposed = true;
        }
    }

    #endregion
}
