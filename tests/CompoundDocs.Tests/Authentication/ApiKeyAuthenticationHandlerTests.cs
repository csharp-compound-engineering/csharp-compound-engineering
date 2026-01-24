using System.Text.Encodings.Web;
using CompoundDocs.McpServer.Authentication;
using CompoundDocs.McpServer.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CompoundDocs.Tests.Authentication;

public class ApiKeyAuthenticationHandlerTests
{
    private const string ValidKey1 = "test-key-1";
    private const string ValidKey2 = "test-key-2";

    private static async Task<AuthenticateResult> AuthenticateAsync(
        HttpContext httpContext,
        ApiKeyAuthenticationOptions apiKeyOptions)
    {
        var schemeOptions = new AuthenticationSchemeOptions();
        var optionsMonitor = new TestOptionsMonitor(schemeOptions);
        var loggerFactory = NullLoggerFactory.Instance;
        var encoder = UrlEncoder.Default;
        var apiKeyOptionsWrapper = Microsoft.Extensions.Options.Options.Create(apiKeyOptions);

        var handler = new ApiKeyAuthenticationHandler(
            optionsMonitor, loggerFactory, encoder, apiKeyOptionsWrapper);

        var scheme = new AuthenticationScheme(
            ApiKeyAuthenticationDefaults.AuthenticationScheme,
            null,
            typeof(ApiKeyAuthenticationHandler));

        await handler.InitializeAsync(scheme, httpContext);
        return await handler.AuthenticateAsync();
    }

    [Fact]
    public async Task ValidKeyInXApiKeyHeader_ReturnsSuccess()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-API-Key"] = ValidKey1;

        var options = new ApiKeyAuthenticationOptions { ApiKeys = $"{ValidKey1},{ValidKey2}" };
        var result = await AuthenticateAsync(context, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidKeyInAuthorizationBearerHeader_ReturnsSuccess()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {ValidKey2}";

        var options = new ApiKeyAuthenticationOptions { ApiKeys = $"{ValidKey1},{ValidKey2}" };
        var result = await AuthenticateAsync(context, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task MissingKey_ReturnsFail()
    {
        var context = new DefaultHttpContext();

        var options = new ApiKeyAuthenticationOptions { ApiKeys = ValidKey1 };
        var result = await AuthenticateAsync(context, options);

        result.Succeeded.ShouldBeFalse();
        result.Failure!.Message.ShouldContain("missing");
    }

    [Fact]
    public async Task InvalidKey_ReturnsFail()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-API-Key"] = "wrong-key";

        var options = new ApiKeyAuthenticationOptions { ApiKeys = ValidKey1 };
        var result = await AuthenticateAsync(context, options);

        result.Succeeded.ShouldBeFalse();
        result.Failure!.Message.ShouldContain("Invalid");
    }

    [Fact]
    public async Task EmptyKey_ReturnsFail()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-API-Key"] = "";

        var options = new ApiKeyAuthenticationOptions { ApiKeys = ValidKey1 };
        var result = await AuthenticateAsync(context, options);

        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task MultipleValidKeys_AnyWorks()
    {
        var options = new ApiKeyAuthenticationOptions { ApiKeys = $"{ValidKey1},{ValidKey2}" };

        var context1 = new DefaultHttpContext();
        context1.Request.Headers["X-API-Key"] = ValidKey1;
        var result1 = await AuthenticateAsync(context1, options);

        var context2 = new DefaultHttpContext();
        context2.Request.Headers["X-API-Key"] = ValidKey2;
        var result2 = await AuthenticateAsync(context2, options);

        result1.Succeeded.ShouldBeTrue();
        result2.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task AuthDisabled_BypassesValidation()
    {
        var context = new DefaultHttpContext();

        var options = new ApiKeyAuthenticationOptions { Enabled = false };
        var result = await AuthenticateAsync(context, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task EnabledButNoKeysConfigured_ReturnsFail()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["X-API-Key"] = "any-key";

        var options = new ApiKeyAuthenticationOptions { Enabled = true, ApiKeys = "" };

        // Act
        var result = await AuthenticateAsync(context, options);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Failure!.Message.ShouldContain("No API keys configured");
    }

    [Fact]
    public async Task EnabledButWhitespaceOnlyKeys_ReturnsFail()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["X-API-Key"] = "any-key";

        var options = new ApiKeyAuthenticationOptions { Enabled = true, ApiKeys = "   " };

        // Act
        var result = await AuthenticateAsync(context, options);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Failure!.Message.ShouldContain("No API keys configured");
    }

    [Theory]
    [InlineData("key1,key2,key3", 3)]
    [InlineData("key1 , key2 , key3", 3)]
    [InlineData("  key1  ,  key2  ", 2)]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData("single-key", 1)]
    [InlineData("key1,,key2", 2)]
    public void GetValidApiKeys_ParsesCorrectly(string input, int expectedCount)
    {
        var options = new ApiKeyAuthenticationOptions { ApiKeys = input };
        var keys = options.GetValidApiKeys();
        keys.Count.ShouldBe(expectedCount);
    }

    private sealed class TestOptionsMonitor : IOptionsMonitor<AuthenticationSchemeOptions>
    {
        public TestOptionsMonitor(AuthenticationSchemeOptions currentValue)
        {
            CurrentValue = currentValue;
        }

        public AuthenticationSchemeOptions CurrentValue { get; }

        public AuthenticationSchemeOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<AuthenticationSchemeOptions, string?> listener) => null;
    }
}
