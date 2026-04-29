using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Core.Data;
using ShulkerTech.Tests.Infrastructure;

namespace ShulkerTech.Tests.Pages;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class WikiPhase4Tests(ShulkerTechWebApplicationFactory factory)
{
    private HttpClient CreateClient(string? userId = null)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        if (userId != null)
            client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        return client;
    }

    // ── Random ──────────────────────────────────────────────

    [Fact]
    public async Task Random_WithPublishedArticles_RedirectsToArticle()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var slug = $"random-target-{Guid.NewGuid():N}";

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Title"] = $"Random Target {slug}",
            ["Input.Content"] = "Some content.",
            ["Input.IsPublished"] = "true",
        });
        var createResp = await CreateClient(user.Id).PostAsync("/Wiki/articles/create", form);
        createResp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var response = await CreateClient().GetAsync("/Wiki/random");
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("/articles/");
    }

    // ── Favorites ────────────────────────────────────────────

    [Fact]
    public async Task Favorite_AuthenticatedUser_CanFavoriteArticle()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var title = $"Fav Article {Guid.NewGuid():N}";

        var createForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Title"] = title,
            ["Input.Content"] = "Content.",
            ["Input.IsPublished"] = "true",
        });
        var createResp = await CreateClient(user.Id).PostAsync("/Wiki/articles/create", createForm);
        createResp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var article = await db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Title == title);
        article.Should().NotBeNull();

        // Toggle favorite on
        var favResp = await CreateClient(user.Id)
            .PostAsync($"/Wiki/articles/{article!.Slug}?handler=Favorite", new FormUrlEncodedContent([]));
        favResp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var fav = await verifyDb.ArticleFavorites
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.UserId == user.Id && f.ArticleId == article.Id);
        fav.Should().NotBeNull();
    }

    [Fact]
    public async Task Favorite_Toggle_RemovesFavorite()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var title = $"Unfav Article {Guid.NewGuid():N}";

        var createForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Title"] = title,
            ["Input.Content"] = "Content.",
            ["Input.IsPublished"] = "true",
        });
        await CreateClient(user.Id).PostAsync("/Wiki/articles/create", createForm);

        var article = await db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Title == title);
        article.Should().NotBeNull();

        var client = CreateClient(user.Id);

        // Add favorite
        await client.PostAsync($"/Wiki/articles/{article!.Slug}?handler=Favorite", new FormUrlEncodedContent([]));

        // Remove favorite (second toggle)
        await client.PostAsync($"/Wiki/articles/{article.Slug}?handler=Favorite", new FormUrlEncodedContent([]));

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var fav = await verifyDb.ArticleFavorites
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.UserId == user.Id && f.ArticleId == article.Id);
        fav.Should().BeNull();
    }

    [Fact]
    public async Task Favorite_Anonymous_IsChallenged()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var title = $"Anon Fav {Guid.NewGuid():N}";

        var createForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Title"] = title,
            ["Input.Content"] = "Content.",
            ["Input.IsPublished"] = "true",
        });
        await CreateClient(user.Id).PostAsync("/Wiki/articles/create", createForm);

        var article = await db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Title == title);
        article.Should().NotBeNull();

        // Anonymous client — no X-Test-User-Id
        var resp = await CreateClient().PostAsync(
            $"/Wiki/articles/{article!.Slug}?handler=Favorite",
            new FormUrlEncodedContent([]));

        // Challenge returns redirect to login (302) or 401
        ((int)resp.StatusCode).Should().BeOneOf(302, 401);
    }

    [Fact]
    public async Task WikiIndex_ShowsFavoritesSection_ForLoggedInUser()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var title = $"Index Fav {Guid.NewGuid():N}";

        var createForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Title"] = title,
            ["Input.Content"] = "Content.",
            ["Input.IsPublished"] = "true",
        });
        await CreateClient(user.Id).PostAsync("/Wiki/articles/create", createForm);

        var article = await db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Title == title);
        article.Should().NotBeNull();

        // Favorite the article
        await CreateClient(user.Id).PostAsync(
            $"/Wiki/articles/{article!.Slug}?handler=Favorite",
            new FormUrlEncodedContent([]));

        // Index should show MY FAVORITES section with this article
        var indexResp = await CreateClient(user.Id).GetAsync("/Wiki");
        var html = await indexResp.Content.ReadAsStringAsync();
        html.Should().Contain("MY FAVORITES");
        html.Should().Contain(title);
    }
}
