using System.Text.Json;
using CompoundDocs.Common.Configuration;

namespace CompoundDocs.Tests.Configuration;

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
    public void LoadGlobalConfig_WithNoConfigFile_ReturnsDefaultConfig()
    {
        // Arrange
        var configDir = Path.Combine(_tempDir, "empty-global");
        Directory.CreateDirectory(configDir);

        // Act
        var result = _sut.LoadGlobalConfig(configDir);

        // Assert
        result.ShouldNotBeNull();
        result.ConfigDirectory.ShouldBe(configDir);
        result.Postgres.ShouldNotBeNull();
        result.Ollama.ShouldNotBeNull();
    }

    [Fact]
    public void LoadGlobalConfig_WithExistingConfigFile_LoadsConfiguration()
    {
        // Arrange
        var configDir = Path.Combine(_tempDir, "existing-global");
        Directory.CreateDirectory(configDir);

        var config = new GlobalConfig
        {
            Postgres = new PostgresSettings
            {
                Host = "custom-host",
                Port = 5433,
                Database = "custom-db"
            },
            Ollama = new OllamaSettings
            {
                Host = "ollama-host",
                Port = 11435
            }
        };

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
        result.Postgres.Host.ShouldBe("custom-host");
        result.Postgres.Port.ShouldBe(5433);
        result.Postgres.Database.ShouldBe("custom-db");
        result.Ollama.Host.ShouldBe("ollama-host");
        result.Ollama.Port.ShouldBe(11435);
    }

    [Fact]
    public void LoadGlobalConfig_WithOverridePath_UsesOverridePath()
    {
        // Arrange
        var overridePath = Path.Combine(_tempDir, "override-path");
        Directory.CreateDirectory(overridePath);

        // Act
        var result = _sut.LoadGlobalConfig(overridePath);

        // Assert
        result.ConfigDirectory.ShouldBe(overridePath);
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
    public void EnsureGlobalConfigDirectory_CreatesDataAndOllamaDirectories()
    {
        // Arrange
        var configDir = Path.Combine(_tempDir, "ensure-subdirs");
        var config = new GlobalConfig { ConfigDirectory = configDir };

        // Act
        _sut.EnsureGlobalConfigDirectory(config);

        // Assert
        Directory.Exists(Path.Combine(configDir, "data", "pgdata")).ShouldBeTrue();
        Directory.Exists(Path.Combine(configDir, "ollama", "models")).ShouldBeTrue();
    }

    #endregion

    #region ExternalDocsConfig Tests (Phase 134)

    [Fact]
    public void LoadProjectConfig_WithExternalDocsConfig_LoadsSettings()
    {
        // Arrange
        var projectPath = Path.Combine(_tempDir, "external-docs-project");
        var configDir = Path.Combine(projectPath, ".csharp-compounding-docs");
        Directory.CreateDirectory(configDir);

        var config = new ProjectConfig
        {
            ProjectName = "external-docs-test",
            ExternalDocsSettings = new ExternalDocsConfig
            {
                SyncFrequencyHours = 12,
                SyncOnStartup = false,
                DefaultNamespacePrefix = "custom-prefix"
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
        result.ExternalDocsSettings.ShouldNotBeNull();
        result.ExternalDocsSettings.SyncFrequencyHours.ShouldBe(12);
        result.ExternalDocsSettings.SyncOnStartup.ShouldBeFalse();
        result.ExternalDocsSettings.DefaultNamespacePrefix.ShouldBe("custom-prefix");
    }

    [Fact]
    public void SaveProjectConfig_WithExternalDocSource_PreservesNamespacePrefix()
    {
        // Arrange
        var projectPath = Path.Combine(_tempDir, "external-source-project");
        Directory.CreateDirectory(projectPath);

        var config = new ProjectConfig
        {
            ProjectName = "external-source-test",
            ExternalDocsSettings = new ExternalDocsConfig
            {
                Sources =
                [
                    new ExternalDocSource
                    {
                        Id = "docs1",
                        Name = "External Docs",
                        Path = "/path/to/docs",
                        NamespacePrefix = "ext.docs"
                    }
                ]
            }
        };

        // Act
        _sut.SaveProjectConfig(projectPath, config);

        // Assert
        var loaded = _sut.LoadProjectConfig(projectPath);
        loaded.ExternalDocsSettings.Sources.Count.ShouldBe(1);
        loaded.ExternalDocsSettings.Sources[0].NamespacePrefix.ShouldBe("ext.docs");
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
            ExternalDocsSettings = new ExternalDocsConfig
            {
                Sources =
                [
                    new ExternalDocSource
                    {
                        Id = "external1",
                        Name = "External Docs 1",
                        Path = "/path/to/docs1",
                        Enabled = true,
                        NamespacePrefix = "ext1"
                    },
                    new ExternalDocSource
                    {
                        Id = "external2",
                        Name = "External Docs 2",
                        Path = "relative/path/docs2",
                        Enabled = false,
                        NamespacePrefix = "ext2"
                    }
                ],
                SyncFrequencyHours = 6,
                SyncOnStartup = true,
                DefaultNamespacePrefix = "default-ext"
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
        loadedConfig.ExternalDocsSettings.SyncFrequencyHours.ShouldBe(6);
        loadedConfig.ExternalDocsSettings.Sources.Count.ShouldBe(2);
        loadedConfig.ExternalDocsSettings.Sources[0].NamespacePrefix.ShouldBe("ext1");
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
