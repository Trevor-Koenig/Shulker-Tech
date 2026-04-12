using FluentAssertions;
using Microsoft.AspNetCore.Http;
using ShulkerTech.Web.Middleware;

namespace ShulkerTech.Tests.Middleware;

/// <summary>
/// Unit-style tests for SubdomainRoutingMiddleware using DefaultHttpContext.
/// No WebApplicationFactory needed — tests the path rewriting logic directly.
/// </summary>
[Trait("Category", "Unit")]
public class SubdomainRoutingMiddlewareTests
{
    private static async Task<(string Path, bool Redirected, string? RedirectLocation)> InvokeAsync(
        string host, string path)
    {
        var middleware = new SubdomainRoutingMiddleware(_ => Task.CompletedTask);

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        context.Request.Scheme = "https";
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        // Response.Redirect() sets StatusCode = 302 and Location header on DefaultHttpContext
        bool redirected = context.Response.StatusCode is >= 300 and < 400;
        var location = context.Response.Headers.Location.ToString();
        return (context.Request.Path.Value!, redirected, string.IsNullOrEmpty(location) ? null : location);
    }

    [Fact]
    public async Task WikiSubdomain_RewritesPathWithWikiPrefix()
    {
        var (path, _, _) = await InvokeAsync("wiki.shulkertech.com", "/articles/my-article");
        path.Should().Be("/Wiki/articles/my-article");
    }

    [Fact]
    public async Task AdminSubdomain_RewritesPathWithAdminPrefix()
    {
        var (path, _, _) = await InvokeAsync("admin.shulkertech.com", "/users");
        path.Should().Be("/Admin/users");
    }

    [Fact]
    public async Task RootDomain_NoPathRewrite()
    {
        var (path, _, _) = await InvokeAsync("shulkertech.com", "/");
        path.Should().Be("/");
    }

    [Fact]
    public async Task WikiSubdomain_SetupPath_RedirectsToRootDomain()
    {
        var (_, redirected, _) = await InvokeAsync("wiki.shulkertech.com", "/setup");
        redirected.Should().BeTrue();
    }

    [Fact]
    public async Task WikiSubdomain_IdentityPath_RedirectsToRootDomain()
    {
        var (_, redirected, location) = await InvokeAsync("wiki.shulkertech.com", "/Identity/Account/Login");
        redirected.Should().BeTrue();
        location.Should().Contain("shulkertech.com");
        location.Should().NotContain("wiki.");
    }

    [Fact]
    public async Task WikiSubdomain_StaticAssetPath_PassesThrough()
    {
        // /css/ is a global path — redirects back to root domain, doesn't add prefix
        var (path, _, _) = await InvokeAsync("wiki.shulkertech.com", "/css/app.css");
        path.Should().NotStartWith("/Wiki");
    }

    [Fact]
    public async Task Localhost_GlobalPath_DoesNotRedirect()
    {
        // 'localhost' has no dot — rootHost slice would fail; middleware should not redirect
        var (_, redirected, _) = await InvokeAsync("localhost", "/setup");
        redirected.Should().BeFalse();
    }

    [Fact]
    public async Task AdminSubdomain_NestedPath_PreservesFullPath()
    {
        var (path, _, _) = await InvokeAsync("admin.shulkertech.com", "/users/edit/42");
        path.Should().Be("/Admin/users/edit/42");
    }
}
