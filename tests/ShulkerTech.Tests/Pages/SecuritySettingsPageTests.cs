using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Core.Data;
using ShulkerTech.Tests.Infrastructure;

namespace ShulkerTech.Tests.Pages;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class SecuritySettingsPageTests(ShulkerTechWebApplicationFactory factory)
{
    private HttpClient CreateClient(string? userId = null)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        if (userId != null)
            client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        return client;
    }

    private async Task<string> AdminUserIdAsync()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true, role: "Admin");
        return user.Id;
    }

    private async Task<string> MemberUserIdAsync()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        return user.Id;
    }

    private static FormUrlEncodedContent SecurityForm(
        bool requireAdmin = false,
        bool requireModerator = false,
        bool requireMember = false) =>
        new(new Dictionary<string, string>
        {
            ["Input.RequireAdminTwoFactor"] = requireAdmin.ToString().ToLower(),
            ["Input.RequireModeratorTwoFactor"] = requireModerator.ToString().ToLower(),
            ["Input.RequireMemberTwoFactor"] = requireMember.ToString().ToLower(),
        });

    [Fact]
    public async Task Get_Unauthenticated_Redirects()
    {
        var response = await CreateClient().GetAsync("/Admin/Security/Settings");
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Get_AsNonAdmin_Redirects()
    {
        var userId = await MemberUserIdAsync();
        var response = await CreateClient(userId).GetAsync("/Admin/Security/Settings");
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Get_AsAdmin_Returns200()
    {
        var userId = await AdminUserIdAsync();
        var response = await CreateClient(userId).GetAsync("/Admin/Security/Settings");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_AsAdmin_PersistsSelectedRoles()
    {
        var userId = await AdminUserIdAsync();

        var response = await CreateClient(userId)
            .PostAsync("/Admin/Security/Settings", SecurityForm(requireAdmin: true, requireModerator: true));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        try
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var settings = await db.SecuritySettings.FirstOrDefaultAsync();
            settings.Should().NotBeNull();
            var roles = settings!.GetRequiredRoles();
            roles.Should().Contain("Admin");
            roles.Should().Contain("Moderator");
            roles.Should().NotContain("Member");
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var settings = await db.SecuritySettings.FindAsync(1);
            if (settings != null) { settings.RequireTwoFactorRoles = string.Empty; await db.SaveChangesAsync(); }
        }
    }

    [Fact]
    public async Task Post_AsAdmin_UncheckingAll_ClearsRoles()
    {
        var userId = await AdminUserIdAsync();

        await CreateClient(userId)
            .PostAsync("/Admin/Security/Settings", SecurityForm(requireAdmin: false));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = await db.SecuritySettings.FirstOrDefaultAsync();
        settings!.GetRequiredRoles().Should().BeEmpty();
    }

    [Fact]
    public async Task Post_AsNonAdmin_Redirects()
    {
        var userId = await MemberUserIdAsync();
        var response = await CreateClient(userId)
            .PostAsync("/Admin/Security/Settings", SecurityForm(requireAdmin: true));
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }
}
