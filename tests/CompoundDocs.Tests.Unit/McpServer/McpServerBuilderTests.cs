using CompoundDocs.McpServer;
using CompoundDocs.McpServer.Options;
using Microsoft.Extensions.DependencyInjection;

namespace CompoundDocs.Tests.Unit.McpServer;

public class McpServerBuilderTests
{
    [Fact]
    public void GetAssemblyVersion_ReturnsNonEmptyString()
    {
        // Act
        var version = McpServerBuilder.GetAssemblyVersion();

        // Assert
        version.ShouldNotBeNull();
        version.ShouldNotBeEmpty();
    }

    [Fact]
    public void GetAssemblyVersion_ReturnsValidVersionFormat()
    {
        // Act
        var version = McpServerBuilder.GetAssemblyVersion();

        // Assert
        var parts = version.Split('.');
        parts.Length.ShouldBe(3);
        foreach (var part in parts)
        {
            int.TryParse(part, out _).ShouldBe(true);
        }
    }

    [Fact]
    public void GetAssemblyVersion_ReturnsSameValueOnMultipleCalls()
    {
        // Act
        var first = McpServerBuilder.GetAssemblyVersion();
        var second = McpServerBuilder.GetAssemblyVersion();

        // Assert
        first.ShouldBe(second);
    }

    [Fact]
    public void Constructor_StoresServicesAndOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new CompoundDocsServerOptions
        {
            ServerName = "test-server"
        };

        // Act
        var builder = new McpServerBuilder(services, options);

        // Assert
        builder.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WithDefaultOptions_DoesNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new CompoundDocsServerOptions();

        // Act
        var builder = new McpServerBuilder(services, options);

        // Assert
        builder.ShouldNotBeNull();
    }

    [Fact]
    public void AddCompoundDocsMcpServer_WithOptions_ReturnsBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new CompoundDocsServerOptions();

        // Act
        var builder = services.AddCompoundDocsMcpServer(options);

        // Assert
        builder.ShouldNotBeNull();
    }

    [Fact]
    public void AddCompoundDocsMcpServer_WithIOptions_ReturnsBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = Microsoft.Extensions.Options.Options.Create(new CompoundDocsServerOptions());

        // Act
        var builder = services.AddCompoundDocsMcpServer(options);

        // Assert
        builder.ShouldNotBeNull();
    }
}
