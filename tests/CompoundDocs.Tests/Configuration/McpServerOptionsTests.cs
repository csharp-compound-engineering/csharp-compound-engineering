using CompoundDocs.McpServer.Options;

namespace CompoundDocs.Tests.Configuration;

/// <summary>
/// Unit tests for MCP Server options classes.
/// </summary>
public sealed class McpServerOptionsTests
{
    #region CompoundDocsServerOptions Tests

    [Fact]
    public void CompoundDocsServerOptions_SectionName_HasExpectedValue()
    {
        // Assert
        CompoundDocsServerOptions.SectionName.ShouldBe("McpServer");
    }

    [Fact]
    public void CompoundDocsServerOptions_DefaultServerName_IsSet()
    {
        // Arrange
        var options = new CompoundDocsServerOptions();

        // Assert
        options.ServerName.ShouldNotBeNullOrEmpty();
        options.ServerName.ShouldBe("csharp-compounding-docs");
    }

    [Fact]
    public void CompoundDocsServerOptions_DefaultServerDescription_IsSet()
    {
        // Arrange
        var options = new CompoundDocsServerOptions();

        // Assert
        options.ServerDescription.ShouldNotBeNullOrEmpty();
        options.ServerDescription.ShouldContain("MCP Server");
    }

    [Fact]
    public void CompoundDocsServerOptions_DefaultPostgres_IsNotNull()
    {
        // Arrange
        var options = new CompoundDocsServerOptions();

        // Assert
        options.Postgres.ShouldNotBeNull();
    }

    [Fact]
    public void CompoundDocsServerOptions_DefaultOllama_IsNotNull()
    {
        // Arrange
        var options = new CompoundDocsServerOptions();

        // Assert
        options.Ollama.ShouldNotBeNull();
    }

    [Fact]
    public void CompoundDocsServerOptions_CanSetServerName()
    {
        // Arrange
        var options = new CompoundDocsServerOptions();

        // Act
        options.ServerName = "custom-server";

        // Assert
        options.ServerName.ShouldBe("custom-server");
    }

    [Fact]
    public void CompoundDocsServerOptions_CanSetServerDescription()
    {
        // Arrange
        var options = new CompoundDocsServerOptions();

        // Act
        options.ServerDescription = "Custom description";

        // Assert
        options.ServerDescription.ShouldBe("Custom description");
    }

    #endregion

    #region PostgresConnectionOptions Tests

    [Fact]
    public void PostgresConnectionOptions_DefaultHost_IsLocalhost()
    {
        // Arrange
        var options = new PostgresConnectionOptions();

        // Assert
        options.Host.ShouldBe("127.0.0.1");
    }

    [Fact]
    public void PostgresConnectionOptions_DefaultPort_Is5433()
    {
        // Arrange
        var options = new PostgresConnectionOptions();

        // Assert
        options.Port.ShouldBe(5433);
    }

    [Fact]
    public void PostgresConnectionOptions_DefaultDatabase_IsCompoundingDocs()
    {
        // Arrange
        var options = new PostgresConnectionOptions();

        // Assert
        options.Database.ShouldBe("compounding_docs");
    }

    [Fact]
    public void PostgresConnectionOptions_DefaultUsername_IsCompounding()
    {
        // Arrange
        var options = new PostgresConnectionOptions();

        // Assert
        options.Username.ShouldBe("compounding");
    }

    [Fact]
    public void PostgresConnectionOptions_DefaultPassword_IsCompounding()
    {
        // Arrange
        var options = new PostgresConnectionOptions();

        // Assert
        options.Password.ShouldBe("compounding");
    }

    [Fact]
    public void PostgresConnectionOptions_GetConnectionString_ReturnsValidFormat()
    {
        // Arrange
        var options = new PostgresConnectionOptions();

        // Act
        var connectionString = options.GetConnectionString();

        // Assert
        connectionString.ShouldContain("Host=");
        connectionString.ShouldContain("Port=");
        connectionString.ShouldContain("Database=");
        connectionString.ShouldContain("Username=");
        connectionString.ShouldContain("Password=");
    }

    [Fact]
    public void PostgresConnectionOptions_GetConnectionString_IncludesAllProperties()
    {
        // Arrange
        var options = new PostgresConnectionOptions
        {
            Host = "db.example.com",
            Port = 5432,
            Database = "testdb",
            Username = "testuser",
            Password = "testpass"
        };

        // Act
        var connectionString = options.GetConnectionString();

        // Assert
        connectionString.ShouldContain("Host=db.example.com");
        connectionString.ShouldContain("Port=5432");
        connectionString.ShouldContain("Database=testdb");
        connectionString.ShouldContain("Username=testuser");
        connectionString.ShouldContain("Password=testpass");
    }

    [Fact]
    public void PostgresConnectionOptions_GetConnectionString_WithDefaultValues_ProducesValidConnectionString()
    {
        // Arrange
        var options = new PostgresConnectionOptions();

        // Act
        var connectionString = options.GetConnectionString();

        // Assert
        connectionString.ShouldBe("Host=127.0.0.1;Port=5433;Database=compounding_docs;Username=compounding;Password=compounding");
    }

    [Fact]
    public void PostgresConnectionOptions_CanSetHost()
    {
        // Arrange
        var options = new PostgresConnectionOptions();

        // Act
        options.Host = "custom-host";

        // Assert
        options.Host.ShouldBe("custom-host");
    }

    [Fact]
    public void PostgresConnectionOptions_CanSetPort()
    {
        // Arrange
        var options = new PostgresConnectionOptions();

        // Act
        options.Port = 5432;

        // Assert
        options.Port.ShouldBe(5432);
    }

    #endregion

    #region OllamaConnectionOptions Tests

    [Fact]
    public void OllamaConnectionOptions_DefaultHost_IsLocalhost()
    {
        // Arrange
        var options = new OllamaConnectionOptions();

        // Assert
        options.Host.ShouldBe("127.0.0.1");
    }

    [Fact]
    public void OllamaConnectionOptions_DefaultPort_Is11435()
    {
        // Arrange
        var options = new OllamaConnectionOptions();

        // Assert
        options.Port.ShouldBe(11435);
    }

    [Fact]
    public void OllamaConnectionOptions_DefaultGenerationModel_IsMistral()
    {
        // Arrange
        var options = new OllamaConnectionOptions();

        // Assert
        options.GenerationModel.ShouldBe("mistral");
    }

    [Fact]
    public void OllamaConnectionOptions_EmbeddingModel_IsMxbaiEmbedLarge()
    {
        // Assert - Static property
        OllamaConnectionOptions.EmbeddingModel.ShouldBe("mxbai-embed-large");
    }

    [Fact]
    public void OllamaConnectionOptions_EmbeddingDimensions_Is1024()
    {
        // Assert - Static property
        OllamaConnectionOptions.EmbeddingDimensions.ShouldBe(1024);
    }

    [Fact]
    public void OllamaConnectionOptions_GetEndpoint_ReturnsValidUri()
    {
        // Arrange
        var options = new OllamaConnectionOptions();

        // Act
        var endpoint = options.GetEndpoint();

        // Assert
        endpoint.ShouldNotBeNull();
        endpoint.Scheme.ShouldBe("http");
        endpoint.Host.ShouldBe("127.0.0.1");
        endpoint.Port.ShouldBe(11435);
    }

    [Fact]
    public void OllamaConnectionOptions_GetEndpoint_WithCustomValues_ReturnsCorrectUri()
    {
        // Arrange
        var options = new OllamaConnectionOptions
        {
            Host = "ollama.example.com",
            Port = 11434
        };

        // Act
        var endpoint = options.GetEndpoint();

        // Assert
        endpoint.ToString().ShouldBe("http://ollama.example.com:11434/");
    }

    [Fact]
    public void OllamaConnectionOptions_CanSetHost()
    {
        // Arrange
        var options = new OllamaConnectionOptions();

        // Act
        options.Host = "custom-ollama-host";

        // Assert
        options.Host.ShouldBe("custom-ollama-host");
    }

    [Fact]
    public void OllamaConnectionOptions_CanSetPort()
    {
        // Arrange
        var options = new OllamaConnectionOptions();

        // Act
        options.Port = 11434;

        // Assert
        options.Port.ShouldBe(11434);
    }

    [Fact]
    public void OllamaConnectionOptions_CanSetGenerationModel()
    {
        // Arrange
        var options = new OllamaConnectionOptions();

        // Act
        options.GenerationModel = "llama2";

        // Assert
        options.GenerationModel.ShouldBe("llama2");
    }

    #endregion
}
