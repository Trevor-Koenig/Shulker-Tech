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
    public async Task Post_DoesNotCreateNewArticle()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);
        var countBefore = await db.Articles.CountAsync();

        await CreateClient(author.Id).PostAsync(
            $"/Wiki/articles/edit/{article.Id}",
            EditForm(article.Id, title: "Updated Title"));

        db.ChangeTracker.Clear();
        var countAfter = await db.Articles.CountAsync();
        countAfter.Should().Be(countBefore);
    }

    [Fact]
    public async Task Post_SubmittingMultipleTimes_DoesNotDuplicate()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);
        var countBefore = await db.Articles.CountAsync();
        var client = CreateClient(author.Id);

        await client.PostAsync($"/Wiki/articles/edit/{article.Id}", EditForm(article.Id, title: "Save 1"));
        await client.PostAsync($"/Wiki/articles/edit/{article.Id}", EditForm(article.Id, title: "Save 2"));
        await client.PostAsync($"/Wiki/articles/edit/{article.Id}", EditForm(article.Id, title: "Save 3"));

        db.ChangeTracker.Clear();
        var countAfter = await db.Articles.CountAsync();
        countAfter.Should().Be(countBefore);

        var updated = await db.Articles.FindAsync(article.Id);
        updated!.Title.Should().Be("Save 3");
    }

    [Fact]
    public async Task Post_WithZeroId_ReturnsForbid()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");

        var response = await CreateClient(author.Id).PostAsync(
            "/Wiki/articles/edit/0",
            EditForm(id: 0, title: "Should not save"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_IdMismatchBetweenRouteAndForm_UpdatesRouteArticle()
    {
        // Guards against a scenario where Input.Id in the form doesn't match the route {id}.
        // The page model uses Input.Id, so the article identified by the form wins.
        // If that article doesn't belong to the user, Forbid is returned.
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var other = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var authorArticle = await TestDbHelper.CreateArticleAsync(db, author.Id);
        var otherArticle = await TestDbHelper.CreateArticleAsync(db, other.Id);

        // POST to authorArticle's route but submit otherArticle's Id in the form body
        var response = await CreateClient(author.Id).PostAsync(
            $"/Wiki/articles/edit/{authorArticle.Id}",
            EditForm(id: otherArticle.Id, title: "Hijacked"));

        // author doesn't own otherArticle, so this must be Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        db.ChangeTracker.Clear();
        var unchanged = await db.Articles.FindAsync(otherArticle.Id);
        unchanged!.Title.Should().NotBe("Hijacked");
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
