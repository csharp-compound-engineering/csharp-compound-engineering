using CompoundDocs.McpServer.Options;
using Shouldly;

namespace CompoundDocs.Tests.Unit.Options;

public class CompoundDocsServerOptionsTests
{
    [Fact]
    public void SectionName_ShouldBe_McpServer()
    {
        CompoundDocsServerOptions.SectionName.ShouldBe("McpServer");
    }

    [Fact]
    public void DefaultServerName_ShouldBe_CsharpCompoundingDocs()
    {
        var options = new CompoundDocsServerOptions();
        options.ServerName.ShouldBe("csharp-compounding-docs");
    }

    [Fact]
    public void DefaultServerDescription_ShouldContain_GraphRAG()
    {
        var options = new CompoundDocsServerOptions();
        options.ServerDescription.ShouldContain("GraphRAG");
    }

    [Fact]
    public void DefaultPort_ShouldBe_8080()
    {
        var options = new CompoundDocsServerOptions();
        options.Port.ShouldBe(8080);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        var options = new CompoundDocsServerOptions
        {
            ServerName = "custom-server",
            ServerDescription = "A custom description",
            Port = 8080
        };

        options.ServerName.ShouldBe("custom-server");
        options.ServerDescription.ShouldBe("A custom description");
        options.Port.ShouldBe(8080);
    }
}

public class ApiKeyAuthenticationOptionsTests
{
    [Fact]
    public void DefaultApiKeys_ShouldBeEmptyString()
    {
        var options = new ApiKeyAuthenticationOptions();
        options.ApiKeys.ShouldBe(string.Empty);
    }

    [Fact]
    public void DefaultHeaderName_ShouldBe_XApiKey()
    {
        var options = new ApiKeyAuthenticationOptions();
        options.HeaderName.ShouldBe("X-API-Key");
    }

    [Fact]
    public void DefaultEnabled_ShouldBeTrue()
    {
        var options = new ApiKeyAuthenticationOptions();
        options.Enabled.ShouldBeTrue();
    }

    [Fact]
    public void GetValidApiKeys_WithEmptyString_ShouldReturnEmptySet()
    {
        var options = new ApiKeyAuthenticationOptions { ApiKeys = "" };
        var keys = options.GetValidApiKeys();
        keys.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("  ")]
    [InlineData("\t")]
    public void GetValidApiKeys_WithWhitespace_ShouldReturnEmptySet(string whitespace)
    {
        var options = new ApiKeyAuthenticationOptions { ApiKeys = whitespace };
        var keys = options.GetValidApiKeys();
        keys.ShouldBeEmpty();
    }

    [Fact]
    public void GetValidApiKeys_WithOnlySpaces_ShouldReturnEmptySet()
    {
        var options = new ApiKeyAuthenticationOptions { ApiKeys = "   " };
        var keys = options.GetValidApiKeys();
        keys.ShouldBeEmpty();
    }

    [Fact]
    public void GetValidApiKeys_WithSingleKey_ShouldReturnSetWithOneEntry()
    {
        var options = new ApiKeyAuthenticationOptions { ApiKeys = "my-secret-key" };
        var keys = options.GetValidApiKeys();
        keys.Count.ShouldBe(1);
        keys.ShouldContain("my-secret-key");
    }

    [Fact]
    public void GetValidApiKeys_WithCommaSeparatedKeys_ShouldReturnAllKeys()
    {
        var options = new ApiKeyAuthenticationOptions { ApiKeys = "key1,key2,key3" };
        var keys = options.GetValidApiKeys();
        keys.Count.ShouldBe(3);
        keys.ShouldContain("key1");
        keys.ShouldContain("key2");
        keys.ShouldContain("key3");
    }

    [Fact]
    public void GetValidApiKeys_ShouldTrimWhitespaceAroundKeys()
    {
        var options = new ApiKeyAuthenticationOptions { ApiKeys = " key1 , key2 , key3 " };
        var keys = options.GetValidApiKeys();
        keys.Count.ShouldBe(3);
        keys.ShouldContain("key1");
        keys.ShouldContain("key2");
        keys.ShouldContain("key3");
    }

    [Fact]
    public void GetValidApiKeys_ShouldRemoveEmptyEntries_FromDoubleCommas()
    {
        var options = new ApiKeyAuthenticationOptions { ApiKeys = "key1,,key2,,,key3" };
        var keys = options.GetValidApiKeys();
        keys.Count.ShouldBe(3);
        keys.ShouldContain("key1");
        keys.ShouldContain("key2");
        keys.ShouldContain("key3");
    }

    [Fact]
    public void GetValidApiKeys_ShouldBeCaseSensitive()
    {
        var options = new ApiKeyAuthenticationOptions { ApiKeys = "MyKey,mykey,MYKEY" };
        var keys = options.GetValidApiKeys();
        keys.Count.ShouldBe(3);
        keys.ShouldContain("MyKey");
        keys.ShouldContain("mykey");
        keys.ShouldContain("MYKEY");
    }
}
