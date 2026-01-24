using System.Security.Claims;
using System.Text.Encodings.Web;
using CompoundDocs.McpServer.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompoundDocs.McpServer.Authentication;

public static class ApiKeyAuthenticationDefaults
{
    public const string AuthenticationScheme = "ApiKey";
}

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ApiKeyAuthenticationOptions _apiKeyOptions;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<ApiKeyAuthenticationOptions> apiKeyOptions)
        : base(options, logger, encoder)
    {
        _apiKeyOptions = apiKeyOptions.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!_apiKeyOptions.Enabled)
        {
            var bypassTicket = CreateSuccessTicket("anonymous");
            return Task.FromResult(AuthenticateResult.Success(bypassTicket));
        }

        var validKeys = _apiKeyOptions.GetValidApiKeys();
        if (validKeys.Count == 0)
        {
            return Task.FromResult(AuthenticateResult.Fail("No API keys configured on server."));
        }

        // Check X-API-Key header first
        string? apiKey = null;
        if (Request.Headers.TryGetValue(_apiKeyOptions.HeaderName, out var headerValues))
        {
            apiKey = headerValues.FirstOrDefault();
        }

        // Fall back to Authorization: Bearer <key>
        if (string.IsNullOrEmpty(apiKey))
        {
            var authorization = Request.Headers.Authorization.FirstOrDefault();
            if (authorization is not null && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                apiKey = authorization["Bearer ".Length..].Trim();
            }
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("API key is missing. Provide via X-API-Key header or Authorization: Bearer <key>."));
        }

        if (!validKeys.Contains(apiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var ticket = CreateSuccessTicket(apiKey);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private AuthenticationTicket CreateSuccessTicket(string keyIdentifier)
    {
        var claims = new[] { new Claim(ClaimTypes.Name, keyIdentifier) };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return new AuthenticationTicket(principal, Scheme.Name);
    }
}
