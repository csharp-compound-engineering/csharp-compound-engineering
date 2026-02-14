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
    public void Default_Postgres_IsNotNull()
    {
        // Arrange
        var config = new GlobalConfig();

        // Act
        var result = config.Postgres;

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void Default_Ollama_IsNotNull()
    {
        // Arrange
        var config = new GlobalConfig();

        // Act
        var result = config.Ollama;

        // Assert
        result.ShouldNotBeNull();
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

    // ── PostgresSettings ────────────────────────────────────────────

    [Fact]
    public void Postgres_Default_Host()
    {
        // Arrange
        var settings = new PostgresSettings();

        // Act
        var result = settings.Host;

        // Assert
        result.ShouldBe("127.0.0.1");
    }

    [Fact]
    public void Postgres_Default_Port()
    {
        // Arrange
        var settings = new PostgresSettings();

        // Act
        var result = settings.Port;

        // Assert
        result.ShouldBe(5433);
    }

    [Fact]
    public void Postgres_Default_Database()
    {
        // Arrange
        var settings = new PostgresSettings();

        // Act
        var result = settings.Database;

        // Assert
        result.ShouldBe("compounding_docs");
    }

    [Fact]
    public void Postgres_Default_Username()
    {
        // Arrange
        var settings = new PostgresSettings();

        // Act
        var result = settings.Username;

        // Assert
        result.ShouldBe("compounding");
    }

    [Fact]
    public void Postgres_Default_Password()
    {
        // Arrange
        var settings = new PostgresSettings();

        // Act
        var result = settings.Password;

        // Assert
        result.ShouldBe("compounding");
    }

    [Fact]
    public void Postgres_GetConnectionString_DefaultValues()
    {
        // Arrange
        var settings = new PostgresSettings();

        // Act
        var result = settings.GetConnectionString();

        // Assert
        result.ShouldBe("Host=127.0.0.1;Port=5433;Database=compounding_docs;Username=compounding;Password=compounding");
    }

    [Fact]
    public void Postgres_GetConnectionString_CustomValues()
    {
        // Arrange
        var settings = new PostgresSettings
        {
            Host = "db.example.com",
            Port = 5432,
            Database = "mydb"
        };

        // Act
        var result = settings.GetConnectionString();

        // Assert
        result.ShouldContain("Host=db.example.com");
        result.ShouldContain("Port=5432");
        result.ShouldContain("Database=mydb");
    }

    // ── OllamaSettings ─────────────────────────────────────────────

    [Fact]
    public void Ollama_Default_GenerationModel()
    {
        // Arrange
        var settings = new OllamaSettings();

        // Act
        var result = settings.GenerationModel;

        // Assert
        result.ShouldBe("mistral");
    }

    [Fact]
    public void Ollama_EmbeddingModel_Static()
    {
        // Arrange & Act
        var result = OllamaSettings.EmbeddingModel;

        // Assert
        result.ShouldBe("mxbai-embed-large");
    }

    [Fact]
    public void Ollama_EmbeddingDimensions_Static()
    {
        // Arrange & Act
        var result = OllamaSettings.EmbeddingDimensions;

        // Assert
        result.ShouldBe(1024);
    }

    [Fact]
    public void Ollama_GetEndpoint_DefaultValues()
    {
        // Arrange
        var settings = new OllamaSettings();

        // Act
        var result = settings.GetEndpoint();

        // Assert
        result.ShouldBe(new Uri("http://127.0.0.1:11435"));
    }

    [Fact]
    public void Ollama_GetEndpoint_CustomValues()
    {
        // Arrange
        var settings = new OllamaSettings
        {
            Host = "ollama.local",
            Port = 9999
        };

        // Act
        var result = settings.GetEndpoint();

        // Assert
        result.ShouldBe(new Uri("http://ollama.local:9999"));
    }
}
