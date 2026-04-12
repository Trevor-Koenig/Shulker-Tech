using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Core.Data;
using ShulkerTech.Tests.Infrastructure;

namespace ShulkerTech.Tests.Pages;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class WikiArticleCreateTests(ShulkerTechWebApplicationFactory factory)
{
    private HttpClient CreateClient(string? userId = null)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        if (userId != null)
            client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        return client;
    }

    private static FormUrlEncodedContent CreateForm(
        string title = "Test Article",
        string content = "## Content\n\nSome text here.",
        bool isPublished = true)
    {
        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Title"] = title,
            ["Input.Content"] = content,
            ["Input.IsPublished"] = isPublished.ToString().ToLower(),
        });
    }

    [Fact]
    public async Task Get_Unauthenticated_RedirectsToLogin()
    {
        var response = await CreateClient().GetAsync("/Wiki/articles/create");
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("Login");
    }

    [Fact]
    public async Task Get_AuthenticatedWithoutCreateRole_ReturnsForbid()
    {
        using var scope = factory.Services.CreateScope();
        // User with no roles — default CreateRole is "Member"
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider);

        var response = await CreateClient(user.Id).GetAsync("/Wiki/articles/create");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_AuthenticatedWithCreateRole_Returns200()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");

        var response = await CreateClient(user.Id).GetAsync("/Wiki/articles/create");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_ValidArticle_CreatesArticleInDb()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var title = $"Article {Guid.NewGuid():N}";

        await CreateClient(user.Id).PostAsync("/Wiki/articles/create", CreateForm(title: title));

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await db.Articles.FirstOrDefaultAsync(a => a.Title == title);
        article.Should().NotBeNull();
    }

    [Fact]
    public async Task Post_ValidArticle_RedirectsToArticleSlug()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var title = $"My Test Article {Guid.NewGuid():N}";

        var response = await CreateClient(user.Id).PostAsync(
            "/Wiki/articles/create", CreateForm(title: title));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/articles/");
    }

    [Fact]
    public async Task Post_SlugGeneration_LowercasesAndHyphenates()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");

        await CreateClient(user.Id).PostAsync(
            "/Wiki/articles/create", CreateForm(title: "Hello World Test"));

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await db.Articles.FirstOrDefaultAsync(a => a.Title == "Hello World Test");
        article.Should().NotBeNull();
        article!.Slug.Should().Be("hello-world-test");
    }

    [Fact]
    public async Task Post_DuplicateTitle_GeneratesUniqueSlug()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Pre-seed an article with the same slug
        await TestDbHelper.CreateArticleAsync(db, user.Id,
            title: "Duplicate Title", slug: "duplicate-title");

        await CreateClient(user.Id).PostAsync(
            "/Wiki/articles/create", CreateForm(title: "Duplicate Title"));

        var articles = await db.Articles.Where(a => a.Title == "Duplicate Title").ToListAsync();
        articles.Should().HaveCount(2);
        articles.Select(a => a.Slug).Should().OnlyHaveUniqueItems();
        articles.Should().Contain(a => a.Slug == "duplicate-title-2");
    }

    [Fact]
    public async Task Post_MissingTitle_Returns200WithValidationError()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");

        var response = await CreateClient(user.Id).PostAsync(
            "/Wiki/articles/create", CreateForm(title: ""));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_EmptyContent_Returns200WithValidationError()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");

        var response = await CreateClient(user.Id).PostAsync(
            "/Wiki/articles/create", CreateForm(content: ""));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_WhitespaceCategory_StoredAsNull()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var title = $"Category Article {Guid.NewGuid():N}";

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Title"] = title,
            ["Input.Content"] = "Some content here.",
            ["Input.Category"] = "   ",
            ["Input.IsPublished"] = "true",
        });
        await CreateClient(user.Id).PostAsync("/Wiki/articles/create", form);

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await db.Articles.FirstOrDefaultAsync(a => a.Title == title);
        article.Should().NotBeNull();
        article!.Category.Should().BeNull();
    }
}
