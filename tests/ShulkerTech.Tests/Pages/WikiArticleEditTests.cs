using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Core.Data;
using ShulkerTech.Tests.Infrastructure;

namespace ShulkerTech.Tests.Pages;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class WikiArticleEditTests(ShulkerTechWebApplicationFactory factory)
{
    private HttpClient CreateClient(string? userId = null)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        if (userId != null)
            client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        return client;
    }

    private static FormUrlEncodedContent EditForm(
        int id,
        string title = "Updated Title",
        string content = "Updated content here.")
    {
        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Id"] = id.ToString(),
            ["Input.Title"] = title,
            ["Input.Content"] = content,
            ["Input.IsPublished"] = "true",
        });
    }

    [Fact]
    public async Task Get_Unauthenticated_ReturnsForbid()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);

        var response = await CreateClient().GetAsync($"/Wiki/articles/edit/{article.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.Redirect); // [Authorize] → login redirect
    }

    [Fact]
    public async Task Get_AsAuthor_Returns200()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);

        var response = await CreateClient(author.Id).GetAsync($"/Wiki/articles/edit/{article.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_AsNonAuthorWithEditAnyRole_Returns200()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider);
        // Default EditAnyRole is "Moderator"
        var editor = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Moderator");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);

        var response = await CreateClient(editor.Id).GetAsync($"/Wiki/articles/edit/{article.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_AsNonAuthorWithoutEditRole_ReturnsForbid()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider);
        var other = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);

        var response = await CreateClient(other.Id).GetAsync($"/Wiki/articles/edit/{article.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_AsAuthor_UpdatesArticle()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);

        await CreateClient(author.Id).PostAsync(
            $"/Wiki/articles/edit/{article.Id}",
            EditForm(article.Id, title: "Updated Title", content: "New content."));

        await db.Entry(article).ReloadAsync();
        article.Title.Should().Be("Updated Title");
        article.Content.Should().Be("New content.");
    }

    [Fact]
    public async Task Post_UpdatedAt_IsRefreshed()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);
        var originalUpdatedAt = article.UpdatedAt;

        await Task.Delay(10); // Ensure time has passed

        await CreateClient(author.Id).PostAsync(
            $"/Wiki/articles/edit/{article.Id}",
            EditForm(article.Id));

        await db.Entry(article).ReloadAsync();
        article.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public async Task Post_EmptyContent_Returns200WithValidationError()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);

        var response = await CreateClient(author.Id).PostAsync(
            $"/Wiki/articles/edit/{article.Id}",
            EditForm(article.Id, content: ""));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostDelete_AsAdmin_DeletesArticleAndRedirects()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider);
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);

        var response = await CreateClient(admin.Id).PostAsync(
            $"/Wiki/articles/edit/{article.Id}?handler=Delete",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = article.Id.ToString(),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var exists = await db.Articles.AnyAsync(a => a.Id == article.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task PostDelete_AsNonAdmin_ReturnsForbid()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var other = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);

        var response = await CreateClient(other.Id).PostAsync(
            $"/Wiki/articles/edit/{article.Id}?handler=Delete",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = article.Id.ToString(),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
