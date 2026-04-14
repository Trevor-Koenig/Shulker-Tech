using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;
using ShulkerTech.Tests.Infrastructure;

namespace ShulkerTech.Tests.Middleware;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class Require2FAMiddlewareTests(ShulkerTechWebApplicationFactory factory)
{
    private HttpClient CreateClient(string? userId = null)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        if (userId != null)
            client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        return client;
    }

    private async Task SetRequiredRolesAsync(string roles)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = await db.SecuritySettings.FindAsync(1)
                       ?? new SecuritySettings { Id = 1 };
        settings.RequireTwoFactorRoles = roles;
        if (db.Entry(settings).State == Microsoft.EntityFrameworkCore.EntityState.Detached)
            db.SecuritySettings.Add(settings);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Anonymous_Get_PassesThrough()
    {
        // Unauthenticated requests should never be redirected by this middleware
        var response = await CreateClient().GetAsync("/");
        response.StatusCode.Should().NotBe(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Authenticated_NoRolesRequired_PassesThrough()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Admin");

        // SecuritySettings defaults to no required roles — admin should pass through
        var response = await CreateClient(user.Id).GetAsync("/");
        response.StatusCode.Should().NotBe(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Authenticated_RoleRequired_TwoFactorEnabled_PassesThrough()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Admin");
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await userManager.SetTwoFactorEnabledAsync(user, true);

        await SetRequiredRolesAsync("Admin");
        try
        {
            var response = await CreateClient(user.Id).GetAsync("/");
            response.StatusCode.Should().NotBe(HttpStatusCode.Redirect);
        }
        finally
        {
            await SetRequiredRolesAsync(string.Empty);
        }
    }

    [Fact]
    public async Task Authenticated_RoleRequired_TwoFactorDisabled_RedirectsToSetup()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Admin");
        // TwoFactorEnabled is false by default

        await SetRequiredRolesAsync("Admin");
        try
        {
            var response = await CreateClient(user.Id).GetAsync("/");
            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().Should().Contain("TwoFactorAuthentication");
        }
        finally
        {
            await SetRequiredRolesAsync(string.Empty);
        }
    }

    [Fact]
    public async Task Authenticated_DifferentRoleRequired_TwoFactorDisabled_PassesThrough()
    {
        // Member user, only Admin required — should pass through even without 2FA
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");

        await SetRequiredRolesAsync("Admin");
        try
        {
            var response = await CreateClient(user.Id).GetAsync("/");
            response.StatusCode.Should().NotBe(HttpStatusCode.Redirect);
        }
        finally
        {
            await SetRequiredRolesAsync(string.Empty);
        }
    }

    [Fact]
    public async Task Authenticated_RequestToTwoFactorSetupPage_NotRedirectedIntoLoop()
    {
        // The 2FA setup page itself must be exempt, otherwise the redirect creates an infinite loop
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Admin");

        await SetRequiredRolesAsync("Admin");
        try
        {
            var response = await CreateClient(user.Id)
                .GetAsync("/Identity/Account/Manage/TwoFactorAuthentication");
            response.StatusCode.Should().NotBe(HttpStatusCode.Redirect);
        }
        finally
        {
            await SetRequiredRolesAsync(string.Empty);
        }
    }
}
