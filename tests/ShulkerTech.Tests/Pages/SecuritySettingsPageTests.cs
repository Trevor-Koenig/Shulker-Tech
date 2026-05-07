using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;
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

    private async Task CreateRoleAsync(string roleName)
    {
        using var scope = factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync(roleName))
            await roleManager.CreateAsync(new IdentityRole(roleName));
    }

    private async Task DeleteRoleAsync(string roleName)
    {
        using var scope = factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var role = await roleManager.FindByNameAsync(roleName);
        if (role != null) await roleManager.DeleteAsync(role);
    }

    private async Task ClearSecuritySettingsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = await db.SecuritySettings.FindAsync(1);
        if (settings != null)
        {
            settings.RequireTwoFactorRoles = string.Empty;
            settings.GuestRole = null;
            await db.SaveChangesAsync();
        }
    }

    private async Task SetGuestRoleAsync(string? roleName)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = await db.SecuritySettings.FindAsync(1)
            ?? new SecuritySettings { Id = 1 };
        if (db.Entry(settings).State == Microsoft.EntityFrameworkCore.EntityState.Detached)
            db.SecuritySettings.Add(settings);
        settings.GuestRole = roleName;
        await db.SaveChangesAsync();
    }

    private async Task AddPermissionGrantAsync(string roleName, string resource)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var exists = await db.SitePermissions.AnyAsync(p => p.RoleName == roleName && p.Resource == resource);
        if (!exists)
        {
            db.SitePermissions.Add(new SitePermission { RoleName = roleName, Resource = resource });
            await db.SaveChangesAsync();
        }
    }

    private async Task RemovePermissionGrantAsync(string roleName, string resource)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var grant = await db.SitePermissions.FirstOrDefaultAsync(p => p.RoleName == roleName && p.Resource == resource);
        if (grant != null) { db.SitePermissions.Remove(grant); await db.SaveChangesAsync(); }
    }

    private static FormUrlEncodedContent GuestRoleForm(string guestRole, params string[] twoFactorRoles)
    {
        var pairs = new List<KeyValuePair<string, string>>
        {
            new("GuestRole", guestRole),
        };
        pairs.AddRange(twoFactorRoles.Select(r => new KeyValuePair<string, string>("RequiredTwoFactorRoles", r)));
        return new FormUrlEncodedContent(pairs);
    }

    private static FormUrlEncodedContent RolesForm(params string[] roles) =>
        new(roles.Select(r => new KeyValuePair<string, string>("RequiredTwoFactorRoles", r)));

    private static FormUrlEncodedContent SecurityForm(
        bool requireAdmin = false,
        bool requireModerator = false,
        bool requireMember = false)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        if (requireAdmin)     pairs.Add(new("RequiredTwoFactorRoles", "Admin"));
        if (requireModerator) pairs.Add(new("RequiredTwoFactorRoles", "Moderator"));
        if (requireMember)    pairs.Add(new("RequiredTwoFactorRoles", "Member"));
        return new FormUrlEncodedContent(pairs);
    }

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

    // ── Custom role tests ───────────────────────────────────────────────────

    [Fact]
    public async Task Post_AsAdmin_CustomRole_IsPersisted()
    {
        var roleName = $"Builder-{Guid.NewGuid():N}";
        await CreateRoleAsync(roleName);
        var adminId = await AdminUserIdAsync();

        try
        {
            var response = await CreateClient(adminId)
                .PostAsync("/Admin/Security/Settings", RolesForm(roleName));

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var settings = await db.SecuritySettings.FirstOrDefaultAsync();
            settings!.GetRequiredRoles().Should().Contain(roleName);
        }
        finally
        {
            await ClearSecuritySettingsAsync();
            await DeleteRoleAsync(roleName);
        }
    }

    [Fact]
    public async Task Get_AsAdmin_CustomRole_AppearsInPage()
    {
        var roleName = $"Scout-{Guid.NewGuid():N}";
        await CreateRoleAsync(roleName);
        var adminId = await AdminUserIdAsync();

        try
        {
            var response = await CreateClient(adminId).GetAsync("/Admin/Security/Settings");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var html = await response.Content.ReadAsStringAsync();
            // The view renders each role as a checkbox value and in a display span
            html.Should().Contain(roleName);
        }
        finally
        {
            await DeleteRoleAsync(roleName);
        }
    }

    [Fact]
    public async Task Post_AsAdmin_NonExistentRole_IsRejected()
    {
        var adminId = await AdminUserIdAsync();
        const string fakeRole = "ThisRoleDoesNotExist";

        var response = await CreateClient(adminId)
            .PostAsync("/Admin/Security/Settings", RolesForm(fakeRole));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = await db.SecuritySettings.FirstOrDefaultAsync();
        settings!.GetRequiredRoles().Should().NotContain(fakeRole);
    }

    [Fact]
    public async Task Post_AsAdmin_MixOfValidAndInvalidRoles_OnlySavesValid()
    {
        var adminId = await AdminUserIdAsync();
        const string fakeRole = "GhostRole";

        var response = await CreateClient(adminId)
            .PostAsync("/Admin/Security/Settings", RolesForm("Member", fakeRole));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        try
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var settings = await db.SecuritySettings.FirstOrDefaultAsync();
            var roles = settings!.GetRequiredRoles();
            roles.Should().Contain("Member");
            roles.Should().NotContain(fakeRole);
        }
        finally
        {
            await ClearSecuritySettingsAsync();
        }
    }

    [Fact]
    public async Task Post_AsAdmin_CustomAndStandardRoles_AllPersist()
    {
        var customRole = $"Ranger-{Guid.NewGuid():N}";
        await CreateRoleAsync(customRole);
        var adminId = await AdminUserIdAsync();

        try
        {
            var response = await CreateClient(adminId)
                .PostAsync("/Admin/Security/Settings", RolesForm("Admin", customRole));

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var roles = (await db.SecuritySettings.FirstOrDefaultAsync())!.GetRequiredRoles();
            roles.Should().Contain("Admin");
            roles.Should().Contain(customRole);
            roles.Should().NotContain("Member");
        }
        finally
        {
            await ClearSecuritySettingsAsync();
            await DeleteRoleAsync(customRole);
        }
    }

    // ── Guest role page tests ───────────────────────────────────────────────

    [Fact]
    public async Task Post_AsAdmin_SetsGuestRole_Persists()
    {
        var adminId = await AdminUserIdAsync();

        try
        {
            var response = await CreateClient(adminId)
                .PostAsync("/Admin/Security/Settings", GuestRoleForm("Member"));

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var settings = await db.SecuritySettings.FirstOrDefaultAsync();
            settings.Should().NotBeNull();
            settings!.GuestRole.Should().Be("Member");
        }
        finally
        {
            await ClearSecuritySettingsAsync();
        }
    }

    [Fact]
    public async Task Post_AsAdmin_ClearsGuestRole_SavesNull()
    {
        var adminId = await AdminUserIdAsync();
        await SetGuestRoleAsync("Member");

        try
        {
            await CreateClient(adminId)
                .PostAsync("/Admin/Security/Settings", GuestRoleForm(""));

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var settings = await db.SecuritySettings.FirstOrDefaultAsync();
            settings!.GuestRole.Should().BeNull();
        }
        finally
        {
            await ClearSecuritySettingsAsync();
        }
    }

    [Fact]
    public async Task Post_AsAdmin_NonExistentGuestRole_IsRejected()
    {
        var adminId = await AdminUserIdAsync();

        await CreateClient(adminId)
            .PostAsync("/Admin/Security/Settings", GuestRoleForm("FakeRoleThatDoesNotExist"));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = await db.SecuritySettings.FirstOrDefaultAsync();
        settings!.GuestRole.Should().BeNull();
    }

    // ── Guest role permission behaviour tests ──────────────────────────────
    // Strategy: restrict page.home (IsPublicByDefault=true) by granting it to "Member",
    // then verify anonymous access flips based on whether GuestRole is set to "Member".

    [Fact]
    public async Task Anonymous_RestrictedResource_WithMatchingGuestRole_CanAccess()
    {
        await AddPermissionGrantAsync("Member", SiteResource.PageHome);
        await SetGuestRoleAsync("Member");

        try
        {
            var response = await CreateClient().GetAsync("/");
            response.StatusCode.Should().NotBe(HttpStatusCode.Redirect);
        }
        finally
        {
            await RemovePermissionGrantAsync("Member", SiteResource.PageHome);
            await ClearSecuritySettingsAsync();
        }
    }

    [Fact]
    public async Task Anonymous_RestrictedResource_WithNoGuestRole_IsRedirectedToLogin()
    {
        await AddPermissionGrantAsync("Member", SiteResource.PageHome);
        await SetGuestRoleAsync(null);

        try
        {
            var response = await CreateClient().GetAsync("/");
            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().Should().Contain("/Identity/Account/Login");
        }
        finally
        {
            await RemovePermissionGrantAsync("Member", SiteResource.PageHome);
            await ClearSecuritySettingsAsync();
        }
    }

    [Fact]
    public async Task Anonymous_RestrictedResource_WithNonMatchingGuestRole_IsRedirectedToLogin()
    {
        // Member can access page.home; guest role is Admin (which also has access via seeded grants)
        // — but we want to verify the guest role is what's checked, not a different role.
        // Use a fresh custom role with no grants for page.home as the guest role.
        var guestOnlyRole = $"GuestOnly-{Guid.NewGuid():N}";
        await CreateRoleAsync(guestOnlyRole);
        await AddPermissionGrantAsync("Member", SiteResource.PageHome);
        await SetGuestRoleAsync(guestOnlyRole); // guest role has no page.home grant

        try
        {
            var response = await CreateClient().GetAsync("/");
            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().Should().Contain("/Identity/Account/Login");
        }
        finally
        {
            await RemovePermissionGrantAsync("Member", SiteResource.PageHome);
            await ClearSecuritySettingsAsync();
            await DeleteRoleAsync(guestOnlyRole);
        }
    }
}
