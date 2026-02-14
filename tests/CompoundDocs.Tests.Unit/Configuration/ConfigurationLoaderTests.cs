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
        result.Postgres.ShouldNotBeNull();
        result.Postgres.Host.ShouldBe("127.0.0.1");
        result.Postgres.Port.ShouldBe(5433);
        result.Ollama.ShouldNotBeNull();
        result.Ollama.Host.ShouldBe("127.0.0.1");
        result.Ollama.Port.ShouldBe(11435);
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

    [Fact]
    public void EnsureGlobalConfigDirectory_DoesNotOverwriteExistingConfig()
    {
        // Arrange - create a config file with custom values first
        var configDir = Path.Combine(_tempDir, "no-overwrite-global");
        Directory.CreateDirectory(configDir);

        var originalConfig = new GlobalConfig
        {
            ConfigDirectory = configDir,
            Postgres = new PostgresSettings { Host = "original-host", Port = 9999 }
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
            ConfigDirectory = configDir,
            Postgres = new PostgresSettings { Host = "new-host", Port = 1111 }
        };
        _sut.EnsureGlobalConfigDirectory(newConfig);

        // Assert - original config should be preserved
        var loaded = _sut.LoadGlobalConfig(configDir);
        loaded.Postgres.Host.ShouldBe("original-host");
        loaded.Postgres.Port.ShouldBe(9999);
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

    #region ApplyEnvironmentOverrides Tests

    [Fact]
    public void LoadGlobalConfig_WithPostgresEnvOverrides_AppliesValues()
    {
        // Arrange
        var configDir = Path.Combine(_tempDir, "pg-env-override");
        Directory.CreateDirectory(configDir);

        Environment.SetEnvironmentVariable("COMPOUNDING_POSTGRES_HOST", "custom-pg-host");
        Environment.SetEnvironmentVariable("COMPOUNDING_POSTGRES_PORT", "9876");
        try
        {
            // Act
            var result = _sut.LoadGlobalConfig(configDir);

            // Assert
            result.Postgres.Host.ShouldBe("custom-pg-host");
            result.Postgres.Port.ShouldBe(9876);
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPOUNDING_POSTGRES_HOST", null);
            Environment.SetEnvironmentVariable("COMPOUNDING_POSTGRES_PORT", null);
        }
    }

    [Fact]
    public void LoadGlobalConfig_WithOllamaEnvOverrides_AppliesValues()
    {
        // Arrange
        var configDir = Path.Combine(_tempDir, "ollama-env-override");
        Directory.CreateDirectory(configDir);

        Environment.SetEnvironmentVariable("COMPOUNDING_OLLAMA_HOST", "custom-ollama-host");
        Environment.SetEnvironmentVariable("COMPOUNDING_OLLAMA_MODEL", "llama3:70b");
        try
        {
            // Act
            var result = _sut.LoadGlobalConfig(configDir);

            // Assert
            result.Ollama.Host.ShouldBe("custom-ollama-host");
            result.Ollama.GenerationModel.ShouldBe("llama3:70b");
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPOUNDING_OLLAMA_HOST", null);
            Environment.SetEnvironmentVariable("COMPOUNDING_OLLAMA_MODEL", null);
        }
    }

    [Fact]
    public void LoadGlobalConfig_WithInvalidPortEnvVar_KeepsDefault()
    {
        // Arrange
        var configDir = Path.Combine(_tempDir, "invalid-port-env");
        Directory.CreateDirectory(configDir);

        Environment.SetEnvironmentVariable("COMPOUNDING_POSTGRES_PORT", "not-a-number");
        try
        {
            // Act
            var result = _sut.LoadGlobalConfig(configDir);

            // Assert - port should remain the default since int.TryParse fails
            result.Postgres.Port.ShouldBe(5433);
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPOUNDING_POSTGRES_PORT", null);
        }
    }

    [Fact]
    public void LoadGlobalConfig_WithPostgresDatabaseEnvOverride_AppliesValue()
    {
        // Arrange
        var configDir = Path.Combine(_tempDir, "pg-db-env-override");
        Directory.CreateDirectory(configDir);

        Environment.SetEnvironmentVariable("COMPOUNDING_POSTGRES_DATABASE", "custom-database");
        try
        {
            // Act
            var result = _sut.LoadGlobalConfig(configDir);

            // Assert
            result.Postgres.Database.ShouldBe("custom-database");
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPOUNDING_POSTGRES_DATABASE", null);
        }
    }

    [Fact]
    public void LoadGlobalConfig_WithPostgresUsernameEnvOverride_AppliesValue()
    {
        // Arrange
        var configDir = Path.Combine(_tempDir, "pg-user-env-override");
        Directory.CreateDirectory(configDir);

        Environment.SetEnvironmentVariable("COMPOUNDING_POSTGRES_USERNAME", "custom-user");
        try
        {
            // Act
            var result = _sut.LoadGlobalConfig(configDir);

            // Assert
            result.Postgres.Username.ShouldBe("custom-user");
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPOUNDING_POSTGRES_USERNAME", null);
        }
    }

    [Fact]
    public void LoadGlobalConfig_WithPostgresPasswordEnvOverride_AppliesValue()
    {
        // Arrange
        var configDir = Path.Combine(_tempDir, "pg-pass-env-override");
        Directory.CreateDirectory(configDir);

        Environment.SetEnvironmentVariable("COMPOUNDING_POSTGRES_PASSWORD", "secret-password");
        try
        {
            // Act
            var result = _sut.LoadGlobalConfig(configDir);

            // Assert
            result.Postgres.Password.ShouldBe("secret-password");
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPOUNDING_POSTGRES_PASSWORD", null);
        }
    }

    [Fact]
    public void LoadGlobalConfig_WithOllamaPortEnvOverride_AppliesValue()
    {
        // Arrange
        var configDir = Path.Combine(_tempDir, "ollama-port-env-override");
        Directory.CreateDirectory(configDir);

        Environment.SetEnvironmentVariable("COMPOUNDING_OLLAMA_PORT", "12345");
        try
        {
            // Act
            var result = _sut.LoadGlobalConfig(configDir);

            // Assert
            result.Ollama.Port.ShouldBe(12345);
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPOUNDING_OLLAMA_PORT", null);
        }
    }

    [Fact]
    public void LoadGlobalConfig_WithOllamaModelEnvOverride_AppliesValue()
    {
        // Arrange
        var configDir = Path.Combine(_tempDir, "ollama-model-env-override");
        Directory.CreateDirectory(configDir);

        Environment.SetEnvironmentVariable("COMPOUNDING_OLLAMA_MODEL", "mistral:7b");
        try
        {
            // Act
            var result = _sut.LoadGlobalConfig(configDir);

            // Assert
            result.Ollama.GenerationModel.ShouldBe("mistral:7b");
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPOUNDING_OLLAMA_MODEL", null);
        }
    }

    [Fact]
    public void LoadGlobalConfig_WithAllEnvOverrides_AppliesAllValues()
    {
        // Arrange
        var configDir = Path.Combine(_tempDir, "all-env-overrides");
        Directory.CreateDirectory(configDir);

        Environment.SetEnvironmentVariable("COMPOUNDING_POSTGRES_HOST", "pg-host");
        Environment.SetEnvironmentVariable("COMPOUNDING_POSTGRES_PORT", "5555");
        Environment.SetEnvironmentVariable("COMPOUNDING_POSTGRES_DATABASE", "mydb");
        Environment.SetEnvironmentVariable("COMPOUNDING_POSTGRES_USERNAME", "admin");
        Environment.SetEnvironmentVariable("COMPOUNDING_POSTGRES_PASSWORD", "pass123");
        Environment.SetEnvironmentVariable("COMPOUNDING_OLLAMA_HOST", "ollama-host");
        Environment.SetEnvironmentVariable("COMPOUNDING_OLLAMA_PORT", "7777");
        Environment.SetEnvironmentVariable("COMPOUNDING_OLLAMA_MODEL", "codellama:13b");
        try
        {
            // Act
            var result = _sut.LoadGlobalConfig(configDir);

            // Assert
            result.Postgres.Host.ShouldBe("pg-host");
            result.Postgres.Port.ShouldBe(5555);
            result.Postgres.Database.ShouldBe("mydb");
            result.Postgres.Username.ShouldBe("admin");
            result.Postgres.Password.ShouldBe("pass123");
            result.Ollama.Host.ShouldBe("ollama-host");
            result.Ollama.Port.ShouldBe(7777);
            result.Ollama.GenerationModel.ShouldBe("codellama:13b");
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPOUNDING_POSTGRES_HOST", null);
            Environment.SetEnvironmentVariable("COMPOUNDING_POSTGRES_PORT", null);
            Environment.SetEnvironmentVariable("COMPOUNDING_POSTGRES_DATABASE", null);
            Environment.SetEnvironmentVariable("COMPOUNDING_POSTGRES_USERNAME", null);
            Environment.SetEnvironmentVariable("COMPOUNDING_POSTGRES_PASSWORD", null);
            Environment.SetEnvironmentVariable("COMPOUNDING_OLLAMA_HOST", null);
            Environment.SetEnvironmentVariable("COMPOUNDING_OLLAMA_PORT", null);
            Environment.SetEnvironmentVariable("COMPOUNDING_OLLAMA_MODEL", null);
        }
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
