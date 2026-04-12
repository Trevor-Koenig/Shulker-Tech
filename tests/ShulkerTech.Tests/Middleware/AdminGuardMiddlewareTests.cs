using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Tests.Infrastructure;

namespace ShulkerTech.Tests.Middleware;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class AdminGuardMiddlewareTests(ShulkerTechWebApplicationFactory factory)
{
    private HttpClient CreateClient() => factory.CreateClient(
        new() { AllowAutoRedirect = false });

    [Fact]
    public async Task Unauthenticated_RequestToAdmin_RedirectsToLogin()
    {
        var response = await CreateClient().GetAsync("/Admin");
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/Identity/Account/Login");
    }

    [Fact]
    public async Task Unauthenticated_ReturnUrl_ContainsAdminRedirectParam()
    {
        var response = await CreateClient().GetAsync("/Admin");
        response.Headers.Location!.ToString().Should().Contain("admin-redirect");
    }

    [Fact]
    public async Task NonAdmin_RequestToAdmin_Redirects()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: false);

        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", user.Id);
        var response = await client.GetAsync("/Admin");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/Identity/Account/Login");
    }

    [Fact]
    public async Task NonAdmin_RedirectResponse_HasNoReturnUrl()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: false);

        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", user.Id);
        var response = await client.GetAsync("/Admin");

        // Non-admin redirect should not include a return URL (don't reveal admin exists)
        response.Headers.Location!.ToString().Should().NotContain("ReturnUrl");
    }

    [Fact]
    public async Task Admin_RequestToAdminIndex_Returns200()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider,
            isAdmin: true, role: "Admin");

        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", user.Id);
        var response = await client.GetAsync("/Admin");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_RequestToAdminNestedPath_Returns200()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider,
            isAdmin: true, role: "Admin");

        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", user.Id);
        var response = await client.GetAsync("/Admin/Users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Unauthenticated_RequestToRootPath_PassesThrough()
    {
        var response = await CreateClient().GetAsync("/");
        // Home page should not be caught by AdminGuard
        response.StatusCode.Should().NotBe(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Unauthenticated_RequestToNonAdminPath_NotIntercepted()
    {
        var response = await CreateClient().GetAsync("/Privacy");
        // AdminGuard only fires on /Admin — non-admin paths should pass through
        var location = response.Headers.Location?.ToString() ?? "";
        location.Should().NotContain("admin-redirect");
    }
}
