using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Core.Data;
using ShulkerTech.Tests.Infrastructure;

namespace ShulkerTech.Tests.Pages;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class WikiTagTests(ShulkerTechWebApplicationFactory factory)
{
    private HttpClient CreateClient(string? userId = null)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        if (userId != null)
            client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        return client;
    }

    [Fact]
    public async Task CreateArticle_WithTagIds_SavesTagRelationship()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var title = $"Tagged Article {Guid.NewGuid():N}";

        // Tag IDs 1 (Getting Started) and 3 (Survival) are seeded
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Title"] = title,
            ["Input.Content"] = "## Content\n\nSome text here.",
            ["Input.TagIds"] = "1,3",
            ["Input.IsPublished"] = "true",
        });
        var response = await CreateClient(user.Id).PostAsync("/Wiki/articles/create", form);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var article = await db.Articles
            .Include(a => a.Tags)
            .FirstOrDefaultAsync(a => a.Title == title);
        article.Should().NotBeNull();
        article!.Tags.Should().HaveCount(2);
        article.Tags.Select(t => t.Name).Should().BeEquivalentTo(new[] { "Getting Started", "Survival" });
    }

    [Fact]
    public async Task CreateArticle_WithNoTagIds_CreatesArticleSuccessfully()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var title = $"Untagged Article {Guid.NewGuid():N}";

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Title"] = title,
            ["Input.Content"] = "## Content\n\nSome text here.",
            ["Input.IsPublished"] = "true",
        });
        var response = await CreateClient(user.Id).PostAsync("/Wiki/articles/create", form);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var article = await db.Articles
            .Include(a => a.Tags)
            .FirstOrDefaultAsync(a => a.Title == title);
        article.Should().NotBeNull();
        article!.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task EditArticle_ChangingTags_UpdatesTagRelationship()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var title = $"Edit Tags Article {Guid.NewGuid():N}";
        var createForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Title"] = title,
            ["Input.Content"] = "## Content\n\nSome text here.",
            ["Input.TagIds"] = "1",
            ["Input.IsPublished"] = "true",
        });
        var createResponse = await CreateClient(user.Id).PostAsync("/Wiki/articles/create", createForm);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var article = await db.Articles.FirstOrDefaultAsync(a => a.Title == title);
        article.Should().NotBeNull();

        // Edit to use different tags (Server Info=2, Redstone=4)
        var editForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Id"] = article!.Id.ToString(),
            ["Input.Title"] = article.Title,
            ["Input.Content"] = article.Content,
            ["Input.TagIds"] = "2,4",
            ["Input.IsPublished"] = "true",
        });
        var editResponse = await CreateClient(user.Id)
            .PostAsync($"/Wiki/articles/edit/{article.Id}", editForm);
        editResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var updated = await db.Articles
            .Include(a => a.Tags)
            .FirstOrDefaultAsync(a => a.Id == article.Id);
        updated!.Tags.Should().HaveCount(2);
        updated.Tags.Select(t => t.Name).Should().BeEquivalentTo(new[] { "Server Info", "Redstone" });
    }

    [Fact]
    public async Task SeedTags_ArePresent()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tags = await db.Tags.ToListAsync();

        tags.Should().HaveCountGreaterThanOrEqualTo(12);
        tags.Select(t => t.Slug).Should().Contain(new[]
        {
            "getting-started", "server-info", "survival", "redstone",
            "farms", "building", "events", "community",
            "rules", "lore", "economy", "pvp"
        });
    }

    [Fact]
    public async Task WikiIndex_RendersTagPills_WhenArticlesHaveTags()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var title = $"Tag Index Article {Guid.NewGuid():N}";

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Title"] = title,
            ["Input.Content"] = "## Content\n\nSome text here.",
            ["Input.TagIds"] = "1",
            ["Input.IsPublished"] = "true",
        });
        var createResp = await CreateClient(user.Id).PostAsync("/Wiki/articles/create", form);
        createResp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var response = await CreateClient().GetAsync("/Wiki");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("data-tag-slug");
    }
}
