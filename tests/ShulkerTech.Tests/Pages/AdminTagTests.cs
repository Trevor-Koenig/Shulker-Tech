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
public class AdminTagTests(ShulkerTechWebApplicationFactory factory)
{
    private HttpClient AdminClient(string userId)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        return client;
    }

    private async Task<Tag> CreateTagDirectlyAsync(IServiceProvider sp, string name, string color = "var(--color-accent)")
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var slug = name.ToLowerInvariant().Replace(" ", "-") + "-" + Guid.NewGuid().ToString("N")[..6];
        var tag = new Tag { Name = name, Slug = slug, Icon = "🧪", Color = color };
        db.Tags.Add(tag);
        await db.SaveChangesAsync();
        return tag;
    }

    // ── Create ───────────────────────────────────────────────

    [Fact]
    public async Task CreateTag_ValidInput_SavesTag()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var name = $"Test Tag {Guid.NewGuid():N}";
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Create.Name"]  = name,
            ["Create.Icon"]  = "⚗️",
            ["Create.Color"] = "#ff6b35",
        });

        var resp = await AdminClient(admin.Id).PostAsync("/Admin/Wiki/Tags?handler=Create", form);
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var tag = await db.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Name == name);
        tag.Should().NotBeNull();
        tag!.Icon.Should().Be("⚗️");
        tag.Color.Should().Be("#ff6b35");
        tag.Slug.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateTag_WithoutIcon_SavesEmptyIcon()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var name = $"No Icon Tag {Guid.NewGuid():N}";
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Create.Name"]  = name,
            ["Create.Color"] = "var(--color-accent)",
        });

        var resp = await AdminClient(admin.Id).PostAsync("/Admin/Wiki/Tags?handler=Create", form);
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var tag = await db.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Name == name);
        tag.Should().NotBeNull();
        tag!.Icon.Should().Be("");
    }

    // ── Edit ─────────────────────────────────────────────────

    [Fact]
    public async Task EditTag_ValidInput_UpdatesTag()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tag = await CreateTagDirectlyAsync(scope.ServiceProvider, $"Edit Me {Guid.NewGuid():N}");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Edit.Id"]    = tag.Id.ToString(),
            ["Edit.Name"]  = "Renamed Tag",
            ["Edit.Icon"]  = "🔧",
            ["Edit.Color"] = "#aabbcc",
            ["Edit.Description"] = "Updated description",
        });

        var resp = await AdminClient(admin.Id).PostAsync("/Admin/Wiki/Tags?handler=Edit", form);
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var verify = factory.Services.CreateScope();
        var updated = await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tag.Id);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Renamed Tag");
        updated.Icon.Should().Be("🔧");
        updated.Color.Should().Be("#aabbcc");
        updated.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task EditTag_ClearIcon_SavesEmptyIcon()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);

        var tag = await CreateTagDirectlyAsync(scope.ServiceProvider, $"Has Icon {Guid.NewGuid():N}");
        tag.Icon.Should().Be("🧪");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Edit.Id"]    = tag.Id.ToString(),
            ["Edit.Name"]  = tag.Name,
            ["Edit.Icon"]  = "",
            ["Edit.Color"] = tag.Color,
        });

        var resp = await AdminClient(admin.Id).PostAsync("/Admin/Wiki/Tags?handler=Edit", form);
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var verify = factory.Services.CreateScope();
        var updated = await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tag.Id);
        updated!.Icon.Should().Be("");
    }

    [Fact]
    public async Task EditTag_DoesNotAffectSlug()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);

        var tag = await CreateTagDirectlyAsync(scope.ServiceProvider, $"Slug Fixed {Guid.NewGuid():N}");
        var originalSlug = tag.Slug;

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Edit.Id"]    = tag.Id.ToString(),
            ["Edit.Name"]  = "Completely Different Name",
            ["Edit.Color"] = tag.Color,
        });

        await AdminClient(admin.Id).PostAsync("/Admin/Wiki/Tags?handler=Edit", form);

        using var verify = factory.Services.CreateScope();
        var updated = await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tag.Id);
        updated!.Slug.Should().Be(originalSlug);
    }

    // ── Delete ───────────────────────────────────────────────

    [Fact]
    public async Task DeleteTag_RemovesTag()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);

        var tag = await CreateTagDirectlyAsync(scope.ServiceProvider, $"Delete Me {Guid.NewGuid():N}");

        var resp = await AdminClient(admin.Id)
            .PostAsync($"/Admin/Wiki/Tags?handler=Delete&id={tag.Id}", new FormUrlEncodedContent([]));
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var verify = factory.Services.CreateScope();
        var deleted = await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tag.Id);
        deleted.Should().BeNull();
    }
}
