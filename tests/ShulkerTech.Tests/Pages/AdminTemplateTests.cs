using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;
using ShulkerTech.Tests.Infrastructure;

namespace ShulkerTech.Tests.Pages;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class AdminTemplateTests(ShulkerTechWebApplicationFactory factory)
{
    private HttpClient CreateClient(string? userId = null)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        if (userId != null)
            client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        return client;
    }

    private static async Task<ArticleTemplate> CreateTemplateDirectlyAsync(
        IServiceProvider sp,
        string? name = null,
        bool isDefault = false)
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();

        // Clear any existing defaults first if we're adding a new one
        if (isDefault)
            await db.ArticleTemplates
                .Where(t => t.IsDefault)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsDefault, false));

        var tmpl = new ArticleTemplate
        {
            Name      = name ?? $"Test Template {Guid.NewGuid():N}",
            Content   = "## Heading\n\nSome content.",
            IsDefault = isDefault,
        };
        db.ArticleTemplates.Add(tmpl);
        await db.SaveChangesAsync();
        return tmpl;
    }

    // ── Create ───────────────────────────────────────────────

    [Fact]
    public async Task CreateTemplate_ValidInput_SavesTemplate()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);

        var name = $"My Template {Guid.NewGuid():N}";
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Create.Name"]        = name,
            ["Create.Description"] = "A handy template.",
            ["Create.Content"]     = "## Intro\n\nHello world.",
        });

        var resp = await CreateClient(admin.Id).PostAsync("/Admin/Wiki/Templates?handler=Create", form);
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var verify = factory.Services.CreateScope();
        var db   = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tmpl = await db.ArticleTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Name == name);
        tmpl.Should().NotBeNull();
        tmpl!.Description.Should().Be("A handy template.");
        tmpl.Content.Should().Be("## Intro\n\nHello world.");
        tmpl.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTemplate_WithIsDefault_MarksAsDefault()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);

        // Clear any existing defaults so there's no ambiguity
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.ArticleTemplates.Where(t => t.IsDefault)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsDefault, false));

        var name = $"New Default {Guid.NewGuid():N}";
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Create.Name"]      = name,
            ["Create.Content"]   = "## Template",
            ["Create.IsDefault"] = "true",
        });

        var resp = await CreateClient(admin.Id).PostAsync("/Admin/Wiki/Templates?handler=Create", form);
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var verify = factory.Services.CreateScope();
        var vdb  = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tmpl = await vdb.ArticleTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Name == name);
        tmpl.Should().NotBeNull();
        tmpl!.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task CreateTemplate_NewDefault_ClearsPreviousDefault()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);

        var first = await CreateTemplateDirectlyAsync(scope.ServiceProvider, isDefault: true);

        var name = $"Second Default {Guid.NewGuid():N}";
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Create.Name"]      = name,
            ["Create.Content"]   = "## Template B",
            ["Create.IsDefault"] = "true",
        });

        await CreateClient(admin.Id).PostAsync("/Admin/Wiki/Templates?handler=Create", form);

        using var verify = factory.Services.CreateScope();
        var vdb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var firstAfter = await vdb.ArticleTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == first.Id);
        firstAfter!.IsDefault.Should().BeFalse();

        var defaultCount = await vdb.ArticleTemplates.CountAsync(t => t.IsDefault);
        defaultCount.Should().Be(1);
    }

    // ── Edit ─────────────────────────────────────────────────

    [Fact]
    public async Task EditTemplate_ValidInput_UpdatesTemplate()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var tmpl  = await CreateTemplateDirectlyAsync(scope.ServiceProvider);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Edit.Id"]          = tmpl.Id.ToString(),
            ["Edit.Name"]        = "Renamed Template",
            ["Edit.Description"] = "Updated description.",
            ["Edit.Content"]     = "## New Content\n\nUpdated.",
        });

        var resp = await CreateClient(admin.Id).PostAsync("/Admin/Wiki/Templates?handler=Edit", form);
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var verify = factory.Services.CreateScope();
        var updated = await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .ArticleTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tmpl.Id);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Renamed Template");
        updated.Description.Should().Be("Updated description.");
        updated.Content.Should().Be("## New Content\n\nUpdated.");
    }

    [Fact]
    public async Task EditTemplate_SetAsDefault_ClearsPreviousDefault()
    {
        using var scope = factory.Services.CreateScope();
        var admin   = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var current = await CreateTemplateDirectlyAsync(scope.ServiceProvider, isDefault: true);
        var other   = await CreateTemplateDirectlyAsync(scope.ServiceProvider, isDefault: false);

        // Promote the other one to default
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Edit.Id"]          = other.Id.ToString(),
            ["Edit.Name"]        = other.Name,
            ["Edit.Content"]     = other.Content,
            ["Edit.IsDefault"]   = "true",
        });

        await CreateClient(admin.Id).PostAsync("/Admin/Wiki/Templates?handler=Edit", form);

        using var verify = factory.Services.CreateScope();
        var vdb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var currentAfter = await vdb.ArticleTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == current.Id);
        currentAfter!.IsDefault.Should().BeFalse();

        var otherAfter = await vdb.ArticleTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == other.Id);
        otherAfter!.IsDefault.Should().BeTrue();
    }

    // ── Delete ───────────────────────────────────────────────

    [Fact]
    public async Task DeleteTemplate_RemovesTemplate()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var tmpl  = await CreateTemplateDirectlyAsync(scope.ServiceProvider);

        var resp = await CreateClient(admin.Id)
            .PostAsync($"/Admin/Wiki/Templates?handler=Delete&id={tmpl.Id}", new FormUrlEncodedContent([]));
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var verify = factory.Services.CreateScope();
        var deleted = await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .ArticleTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tmpl.Id);
        deleted.Should().BeNull();
    }

    // ── Access control ───────────────────────────────────────

    [Fact]
    public async Task TemplatesPage_Anonymous_IsRedirected()
    {
        var resp = await CreateClient().GetAsync("/Admin/Wiki/Templates");
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task TemplatesPage_NonAdmin_IsForbidden()
    {
        using var scope = factory.Services.CreateScope();
        var member = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");

        var resp = await CreateClient(member.Id).GetAsync("/Admin/Wiki/Templates");
        // AdminGuardMiddleware redirects non-admins
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    // ── Create page template selector ────────────────────────

    [Fact]
    public async Task CreatePage_WithTemplates_RendersTemplatePicker()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        await CreateTemplateDirectlyAsync(scope.ServiceProvider, "My Fancy Template");

        var resp = await CreateClient(user.Id).GetAsync("/Wiki/articles/create");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await resp.Content.ReadAsStringAsync();

        html.Should().Contain("My Fancy Template");
        html.Should().Contain("template-load-btn");
    }
}
