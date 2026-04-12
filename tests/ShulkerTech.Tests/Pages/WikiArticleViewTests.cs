using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Core.Data;
using ShulkerTech.Tests.Infrastructure;

namespace ShulkerTech.Tests.Pages;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class WikiArticleViewTests(ShulkerTechWebApplicationFactory factory)
{
    private HttpClient CreateClient(string? userId = null)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        if (userId != null)
            client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        return client;
    }

    [Fact]
    public async Task View_PublishedArticle_NoViewRole_AnonymousCanView()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id,
            isPublished: true, viewRole: null);

        var response = await CreateClient().GetAsync($"/Wiki/articles/{article.Slug}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task View_PublishedArticle_WithViewRole_UserSatisfiesRole_Returns200()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider);
        var reader = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id,
            isPublished: true, viewRole: "Member");

        var response = await CreateClient(reader.Id).GetAsync($"/Wiki/articles/{article.Slug}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task View_PublishedArticle_WithViewRole_UserLacksRole_ReturnsNotFound()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider);
        var reader = await TestDbHelper.CreateUserAsync(scope.ServiceProvider); // no role
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id,
            isPublished: true, viewRole: "Member");

        var response = await CreateClient(reader.Id).GetAsync($"/Wiki/articles/{article.Slug}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task View_UnpublishedArticle_AsAuthor_Returns200()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id, isPublished: false);

        var response = await CreateClient(author.Id).GetAsync($"/Wiki/articles/{article.Slug}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task View_UnpublishedArticle_AsAdmin_Returns200()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider);
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id, isPublished: false);

        var response = await CreateClient(admin.Id).GetAsync($"/Wiki/articles/{article.Slug}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task View_UnpublishedArticle_AsAnonymous_ReturnsNotFound()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id, isPublished: false);

        var response = await CreateClient().GetAsync($"/Wiki/articles/{article.Slug}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task View_NonExistentSlug_ReturnsNotFound()
    {
        var response = await CreateClient().GetAsync("/Wiki/articles/this-slug-does-not-exist-xyz123");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task View_Author_CanEditFlagPresentInResponse()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id, isPublished: true);

        var response = await CreateClient(author.Id).GetAsync($"/Wiki/articles/{article.Slug}");
        var body = await response.Content.ReadAsStringAsync();
        // The page renders an edit link when CanEdit is true
        body.Should().Contain("edit");
    }

    [Fact]
    public async Task View_AnonymousUser_NoEditLinkInResponse()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id, isPublished: true);

        var response = await CreateClient().GetAsync($"/Wiki/articles/{article.Slug}");
        var body = await response.Content.ReadAsStringAsync();
        // Anonymous users should not see an edit button
        body.Should().NotContain($"/Wiki/articles/edit/{article.Id}");
    }

    [Fact]
    public async Task View_PublishedArticle_ContentRenderedAsHtml()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id,
            isPublished: true);

        var response = await CreateClient().GetAsync($"/Wiki/articles/{article.Slug}");
        var body = await response.Content.ReadAsStringAsync();
        // Markdig renders ## headings to <h2>
        body.Should().Contain("<h2");
    }
}
