using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace ShulkerTech.Tests.Infrastructure;

/// <summary>
/// No-op antiforgery implementation for integration tests.
/// Skips CSRF token validation so test POsts don't need to first fetch a form token.
/// </summary>
internal sealed class NoOpAntiforgery : IAntiforgery
{
    private static readonly AntiforgeryTokenSet _tokens =
        new("request-token", "cookie-token", "form-field-name", "header-name");

    public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => _tokens;
    public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => _tokens;
    public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
    public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
}
