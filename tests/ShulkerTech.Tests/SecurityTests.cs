using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;
using ShulkerTech.Tests.Infrastructure;

namespace ShulkerTech.Tests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class SecurityTests(ShulkerTechWebApplicationFactory factory)
{
    private HttpClient AnonClient() =>
        factory.CreateClient(new() { AllowAutoRedirect = false });

    private HttpClient AuthClient(string userId)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        return client;
    }

    // ── Security headers ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("/")]
    [InlineData("/Wiki")]
    [InlineData("/Privacy")]
    public async Task Response_IncludesSecurityHeaders(string path)
    {
        var response = await AnonClient().GetAsync(path);

        response.Headers.TryGetValues("X-Content-Type-Options", out var xct);
        response.Headers.TryGetValues("X-Frame-Options", out var xfo);
        response.Headers.TryGetValues("Referrer-Policy", out var rp);

        xct?.FirstOrDefault().Should().Be("nosniff",        $"X-Content-Type-Options must be set on {path}");
        xfo?.FirstOrDefault().Should().Be("SAMEORIGIN",     $"X-Frame-Options must be set on {path}");
        rp?.FirstOrDefault().Should().Be("strict-origin-when-cross-origin", $"Referrer-Policy must be set on {path}");
    }

    // ── RBAC sub-resource isolation ───────────────────────────────────────────

    [Fact]
    public async Task AdminSubResource_UserWithOnlyAdminAccess_CanReachDashboard()
    {
        var (userId, _) = await CreateLimitedAdminAsync(SiteResource.AdminAccess);
        var response = await AuthClient(userId).GetAsync("/Admin");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminSubResource_UserWithOnlyAdminAccess_IsBlockedFromAllSubPages()
    {
        var (userId, _) = await CreateLimitedAdminAsync(SiteResource.AdminAccess);
        var client = AuthClient(userId);

        string[] subPaths =
        [
            "/Admin/Users", "/Admin/Roles", "/Admin/Invites",
            "/Admin/Servers", "/Admin/Maps", "/Admin/Security/Settings",
            "/Admin/Site/Settings", "/Admin/Site/DbExport",
            "/Admin/Wiki/Settings", "/Admin/Wiki/Tags", "/Admin/Wiki/Templates",
        ];

        foreach (var path in subPaths)
        {
            var response = await client.GetAsync(path);
            response.StatusCode.Should().Be(HttpStatusCode.Redirect,
                $"a user with only admin.access should be blocked from {path}");
            response.Headers.Location?.ToString().Should().Contain("/Identity/Account/Login",
                $"the block on {path} should redirect to login, not reveal the page exists");
        }
    }

    [Fact]
    public async Task AdminSubResource_GranularGrant_OnlyOpensMatchingPage()
    {
        // User has admin.access + admin.users — can reach /Admin and /Admin/Users
        // but must still be blocked from /Admin/Roles
        var (userId, _) = await CreateLimitedAdminAsync(
            SiteResource.AdminAccess, SiteResource.AdminUsers);

        var client = AuthClient(userId);

        (await client.GetAsync("/Admin")).StatusCode
            .Should().Be(HttpStatusCode.OK, "admin.access grants the dashboard");

        (await client.GetAsync("/Admin/Users")).StatusCode
            .Should().Be(HttpStatusCode.OK, "admin.users grants the users page");

        (await client.GetAsync("/Admin/Roles")).StatusCode
            .Should().Be(HttpStatusCode.Redirect, "no admin.roles grant — must be blocked");
    }

    // ── Wiki information disclosure ───────────────────────────────────────────

    [Fact]
    public async Task WikiEdit_Unauthenticated_SameResponseForValidAndInvalidId()
    {
        using var scope = factory.Services.CreateScope();
        var db     = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);

        var validResp   = await AnonClient().GetAsync($"/articles/edit?id={article.Id}");
        var invalidResp = await AnonClient().GetAsync("/articles/edit?id=999999");

        validResp.StatusCode.Should().Be(invalidResp.StatusCode,
            "unauthenticated users must not be able to distinguish valid from invalid article IDs on the edit page");
    }

    [Fact]
    public async Task WikiHistory_Unauthenticated_SameResponseForValidAndInvalidId()
    {
        using var scope = factory.Services.CreateScope();
        var db     = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);

        var validResp   = await AnonClient().GetAsync($"/articles/history?id={article.Id}");
        var invalidResp = await AnonClient().GetAsync("/articles/history?id=999999");

        validResp.StatusCode.Should().Be(invalidResp.StatusCode,
            "unauthenticated users must not be able to distinguish valid from invalid article IDs on the history page");
    }

    // ── Path traversal protection ─────────────────────────────────────────────

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..%2F..%2F..%2Fetc%2Fpasswd")]
    [InlineData("../../secrets.sql.gz")]
    public async Task SetupRestore_PathTraversalAttempt_DoesNotCrash(string maliciousPath)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["setupCode"]  = "test-setup-code",
            ["backupFile"] = maliciousPath,
        });

        var response = await AnonClient().PostAsync("/Setup/Restore?handler=Existing", form);

        ((int)response.StatusCode).Should().NotBe(500,
            $"path traversal attempt '{maliciousPath}' must not cause a server error");
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("../../secrets.sql.gz")]
    public async Task SetupRestore_PathTraversalAttempt_DoesNotLeakFileContents(string maliciousPath)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["setupCode"]  = "test-setup-code",
            ["backupFile"] = maliciousPath,
        });

        // Follow redirects so we can read the page body
        var client = factory.CreateClient();
        var response = await client.PostAsync("/Setup/Restore?handler=Existing", form);
        var body = await response.Content.ReadAsStringAsync();

        // If the traversal were allowed, /etc/passwd content would appear; it must not
        body.Should().NotContain("root:x:",
            $"path traversal attempt '{maliciousPath}' must not return filesystem file contents");
        body.Should().NotContain("/bin/bash",
            $"path traversal attempt '{maliciousPath}' must not return filesystem file contents");
    }

    // ── XSS / HTML injection via Markdown ─────────────────────────────────────

    [Fact]
    public async Task WikiView_ScriptTagInContent_TagsAreNotExecutable()
    {
        using var scope = factory.Services.CreateScope();
        var db     = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id,
            title: "XSS Test Article",
            slug: $"xss-script-{Guid.NewGuid():N}",
            content: "## Safe heading\n\n<script>alert('xss')</script>\n\nNormal content.");

        var member = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var html = await AuthClient(member.Id).GetStringAsync($"/Wiki/articles/{article.Slug}");

        // Markdig DisableHtml() HTML-encodes raw HTML rather than stripping it, so
        // <script> becomes &lt;script&gt; — unexecutable by the browser.
        html.Should().NotContain("<script>alert(",
            "raw executable script tags must be HTML-encoded, not passed through as live HTML");
    }

    [Fact]
    public async Task WikiView_IframeInContent_IsStrippedFromRenderedHtml()
    {
        using var scope = factory.Services.CreateScope();
        var db     = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id,
            title: "Iframe Test Article",
            slug: $"xss-iframe-{Guid.NewGuid():N}",
            content: "## Content\n\n<iframe src=\"https://evil.example.com\"></iframe>\n\nText after.");

        var member = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var html = await AuthClient(member.Id).GetStringAsync($"/Wiki/articles/{article.Slug}");

        html.Should().NotContain("<iframe", "raw iframe tags must be stripped by the Markdown pipeline");
    }

    [Fact]
    public async Task WikiView_OnClickAttributeInContent_IsStrippedFromRenderedHtml()
    {
        using var scope = factory.Services.CreateScope();
        var db     = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id,
            title: "Event Handler Test Article",
            slug: $"xss-onclick-{Guid.NewGuid():N}",
            content: "## Content\n\n<p onclick=\"alert('xss')\">Click me</p>");

        var member = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var html = await AuthClient(member.Id).GetStringAsync($"/Wiki/articles/{article.Slug}");

        html.Should().NotContain("onclick=\"alert", "inline event handlers must be stripped by the Markdown pipeline");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<(string UserId, string RoleName)> CreateLimitedAdminAsync(params string[] resources)
    {
        using var scope = factory.Services.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        var roleName = $"Ltd_{Guid.NewGuid():N}"[..20];
        await roleManager.CreateAsync(new IdentityRole(roleName));

        db.SitePermissions.AddRange(resources.Select(r =>
            new SitePermission { RoleName = roleName, Resource = r }));
        await db.SaveChangesAsync();

        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: roleName);
        return (user.Id, roleName);
    }
}
