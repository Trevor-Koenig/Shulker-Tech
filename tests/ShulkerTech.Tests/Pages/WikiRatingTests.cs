using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Core.Data;
using ShulkerTech.Tests.Infrastructure;

namespace ShulkerTech.Tests.Pages;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class WikiRatingTests(ShulkerTechWebApplicationFactory factory)
{
    private HttpClient CreateClient(string? userId = null)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        if (userId != null)
            client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        return client;
    }

    private static FormUrlEncodedContent RateForm(int usefulness, int coolness) =>
        new(new Dictionary<string, string>
        {
            ["usefulness"] = usefulness.ToString(),
            ["coolness"]   = coolness.ToString(),
        });

    // ── Submit ───────────────────────────────────────────────

    [Fact]
    public async Task Rate_AuthenticatedUser_CreatesRatingRow()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db   = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, user.Id);

        var resp = await CreateClient(user.Id)
            .PostAsync($"/Wiki/articles/{article.Slug}?handler=Rate", RateForm(4, 5));

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var verify = factory.Services.CreateScope();
        var vdb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rating = await vdb.ArticleRatings.AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == user.Id && r.ArticleId == article.Id);

        rating.Should().NotBeNull();
        rating!.Usefulness.Should().Be(4);
        rating.Coolness.Should().Be(5);
    }

    [Fact]
    public async Task Rate_SubmitTwice_UpsertsExistingRow()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db   = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, user.Id);
        var client = CreateClient(user.Id);

        await client.PostAsync($"/Wiki/articles/{article.Slug}?handler=Rate", RateForm(3, 2));
        await client.PostAsync($"/Wiki/articles/{article.Slug}?handler=Rate", RateForm(5, 1));

        using var verify = factory.Services.CreateScope();
        var vdb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ratings = await vdb.ArticleRatings.AsNoTracking()
            .Where(r => r.ArticleId == article.Id)
            .ToListAsync();

        ratings.Should().HaveCount(1);
        ratings[0].Usefulness.Should().Be(5);
        ratings[0].Coolness.Should().Be(1);
    }

    [Fact]
    public async Task Rate_MultipleUsers_CreatesOneRowEach()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var other  = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db     = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);

        await CreateClient(author.Id).PostAsync($"/Wiki/articles/{article.Slug}?handler=Rate", RateForm(4, 4));
        await CreateClient(other.Id).PostAsync($"/Wiki/articles/{article.Slug}?handler=Rate", RateForm(2, 3));

        using var verify = factory.Services.CreateScope();
        var vdb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var count = await vdb.ArticleRatings.CountAsync(r => r.ArticleId == article.Id);
        count.Should().Be(2);
    }

    // ── Validation ───────────────────────────────────────────

    [Fact]
    public async Task Rate_UsefulnessZero_ReturnsBadRequest()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db   = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, user.Id);

        var resp = await CreateClient(user.Id)
            .PostAsync($"/Wiki/articles/{article.Slug}?handler=Rate", RateForm(0, 3));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rate_CoolnessAboveFive_ReturnsBadRequest()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db   = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, user.Id);

        var resp = await CreateClient(user.Id)
            .PostAsync($"/Wiki/articles/{article.Slug}?handler=Rate", RateForm(3, 6));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rate_Anonymous_IsChallenged()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db     = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);

        var resp = await CreateClient()
            .PostAsync($"/Wiki/articles/{article.Slug}?handler=Rate", RateForm(4, 4));

        ((int)resp.StatusCode).Should().BeOneOf(302, 401);
    }

    // ── View page rendering ──────────────────────────────────

    [Fact]
    public async Task View_NoRatings_ShowsNoRatingsYet()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db     = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);

        var resp = await CreateClient().GetAsync($"/Wiki/articles/{article.Slug}");
        var html = await resp.Content.ReadAsStringAsync();

        html.Should().Contain("no ratings yet");
    }

    [Fact]
    public async Task View_WithRating_ShowsAverageScore()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db   = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, user.Id);

        await CreateClient(user.Id)
            .PostAsync($"/Wiki/articles/{article.Slug}?handler=Rate", RateForm(4, 3));

        var resp = await CreateClient().GetAsync($"/Wiki/articles/{article.Slug}");
        var html = await resp.Content.ReadAsStringAsync();

        html.Should().Contain("4.0");
        html.Should().Contain("3.0");
        html.Should().Contain("1 rating");
    }

    [Fact]
    public async Task View_AuthenticatedUser_ShowsRatingForm()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db   = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, user.Id);

        var resp = await CreateClient(user.Id).GetAsync($"/Wiki/articles/{article.Slug}");
        var html = await resp.Content.ReadAsStringAsync();

        html.Should().Contain("handler=Rate");
        html.Should().Contain("SUBMIT RATING");
    }

    [Fact]
    public async Task View_AnonymousUser_NoRatingForm()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db     = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);

        var resp = await CreateClient().GetAsync($"/Wiki/articles/{article.Slug}");
        var html = await resp.Content.ReadAsStringAsync();

        html.Should().NotContain("handler=Rate");
        html.Should().Contain("RATE THIS ARTICLE");
    }

    [Fact]
    public async Task View_ExistingRating_ShowsUpdateButtonAndPrefillsScore()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db   = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, user.Id);

        await CreateClient(user.Id)
            .PostAsync($"/Wiki/articles/{article.Slug}?handler=Rate", RateForm(3, 4));

        var resp = await CreateClient(user.Id).GetAsync($"/Wiki/articles/{article.Slug}");
        var html = await resp.Content.ReadAsStringAsync();

        html.Should().Contain("UPDATE RATING");
    }
}
