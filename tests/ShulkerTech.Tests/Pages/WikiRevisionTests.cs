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
public class WikiRevisionTests(ShulkerTechWebApplicationFactory factory)
{
    private HttpClient CreateClient(string? userId = null)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        if (userId != null)
            client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        return client;
    }

    private static FormUrlEncodedContent EditForm(int id, string title = "Updated", string content = "Updated content.")
        => new(new Dictionary<string, string>
        {
            ["Input.Id"]          = id.ToString(),
            ["Input.Title"]       = title,
            ["Input.Content"]     = content,
            ["Input.IsPublished"] = "true",
        });

    // ── Edit creates revision ──────────────────────────────────────────────

    [Fact]
    public async Task Edit_CreatesRevision_WithPreviousContent()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id,
            title: "Original Title", slug: $"original-{Guid.NewGuid():N}");
        var originalContent = article.Content;

        await CreateClient(author.Id).PostAsync(
            $"/Wiki/articles/edit/{article.Id}",
            EditForm(article.Id, title: "New Title", content: "New content."));

        var revision = await db.ArticleRevisions
            .FirstOrDefaultAsync(r => r.ArticleId == article.Id);

        revision.Should().NotBeNull();
        revision!.Title.Should().Be("Original Title");
        revision.Content.Should().Be(originalContent);
    }

    [Fact]
    public async Task Edit_MultipleEdits_CreatesOneRevisionPerEdit()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);
        var client = CreateClient(author.Id);

        await client.PostAsync($"/Wiki/articles/edit/{article.Id}", EditForm(article.Id, title: "Edit 1"));
        await client.PostAsync($"/Wiki/articles/edit/{article.Id}", EditForm(article.Id, title: "Edit 2"));
        await client.PostAsync($"/Wiki/articles/edit/{article.Id}", EditForm(article.Id, title: "Edit 3"));

        var count = await db.ArticleRevisions.CountAsync(r => r.ArticleId == article.Id);
        count.Should().Be(3);
    }

    [Fact]
    public async Task Edit_Revision_RecordsEditorId()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);

        await CreateClient(author.Id).PostAsync(
            $"/Wiki/articles/edit/{article.Id}",
            EditForm(article.Id, title: "Edited"));

        var revision = await db.ArticleRevisions.FirstAsync(r => r.ArticleId == article.Id);
        revision.EditorId.Should().Be(author.Id);
    }

    // ── History page ───────────────────────────────────────────────────────

    [Fact]
    public async Task History_Unauthenticated_ReturnsForbid()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);

        var response = await CreateClient().GetAsync($"/Wiki/articles/history/{article.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.Redirect); // [Authorize] → login
    }

    [Fact]
    public async Task History_AsAuthor_Returns200()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);

        var response = await CreateClient(author.Id).GetAsync($"/Wiki/articles/history/{article.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task History_AsNonEditor_ReturnsForbid()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider);
        var other = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);

        var response = await CreateClient(other.Id).GetAsync($"/Wiki/articles/history/{article.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Revision view page ─────────────────────────────────────────────────

    [Fact]
    public async Task RevisionView_AsAuthor_Returns200()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);

        await CreateClient(author.Id).PostAsync(
            $"/Wiki/articles/edit/{article.Id}",
            EditForm(article.Id, title: "New Title"));

        var revision = await db.ArticleRevisions.FirstAsync(r => r.ArticleId == article.Id);
        var response = await CreateClient(author.Id).GetAsync($"/Wiki/articles/revision/{revision.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RevisionView_AsNonEditor_ReturnsForbid()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider);
        var other = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);

        await CreateClient(author.Id).PostAsync(
            $"/Wiki/articles/edit/{article.Id}",
            EditForm(article.Id, title: "New Title"));

        var revision = await db.ArticleRevisions.FirstAsync(r => r.ArticleId == article.Id);
        var response = await CreateClient(other.Id).GetAsync($"/Wiki/articles/revision/{revision.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Restore ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Restore_RestoresContentToArticle()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id,
            title: "Original", slug: $"restore-test-{Guid.NewGuid():N}");
        var client = CreateClient(author.Id);

        // Edit the article — saves "Original" as a revision
        await client.PostAsync(
            $"/Wiki/articles/edit/{article.Id}",
            EditForm(article.Id, title: "Overwritten", content: "Overwritten content."));

        // Restore the original revision
        var revision = await db.ArticleRevisions.FirstAsync(r => r.ArticleId == article.Id);
        await client.PostAsync($"/Wiki/articles/revision/{revision.Id}?handler=Restore",
            new FormUrlEncodedContent([]));

        db.ChangeTracker.Clear();
        var restored = await db.Articles.FindAsync(article.Id);
        restored!.Title.Should().Be("Original");
    }

    [Fact]
    public async Task Restore_CreatesNewRevisionOfCurrentState()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);
        var client = CreateClient(author.Id);

        // Edit to create revision 1
        await client.PostAsync(
            $"/Wiki/articles/edit/{article.Id}",
            EditForm(article.Id, title: "After Edit"));

        var countBefore = await db.ArticleRevisions.CountAsync(r => r.ArticleId == article.Id);

        // Restore revision 1 — should create revision 2 (snapshot of "After Edit")
        var revision = await db.ArticleRevisions.FirstAsync(r => r.ArticleId == article.Id);
        await client.PostAsync($"/Wiki/articles/revision/{revision.Id}?handler=Restore",
            new FormUrlEncodedContent([]));

        var countAfter = await db.ArticleRevisions.CountAsync(r => r.ArticleId == article.Id);
        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task Restore_AsNonEditor_ReturnsForbid()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider);
        var other = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);

        // Create a revision via direct DB insert (author has no edit role)
        var revision = new ArticleRevision
        {
            ArticleId = article.Id,
            Title     = article.Title,
            Content   = article.Content,
            EditorId  = author.Id,
            EditedAt  = DateTime.UtcNow,
        };
        db.ArticleRevisions.Add(revision);
        await db.SaveChangesAsync();

        var response = await CreateClient(other.Id).PostAsync(
            $"/Wiki/articles/revision/{revision.Id}?handler=Restore",
            new FormUrlEncodedContent([]));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Cascade delete ─────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteArticle_CascadesRevisions()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var admin  = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, author.Id);

        // Create a revision
        await CreateClient(author.Id).PostAsync(
            $"/Wiki/articles/edit/{article.Id}",
            EditForm(article.Id, title: "Edited"));

        var revisionId = (await db.ArticleRevisions.FirstAsync(r => r.ArticleId == article.Id)).Id;

        // Admin deletes the article
        await CreateClient(admin.Id).PostAsync(
            $"/Wiki/articles/edit/{article.Id}?handler=Delete",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = article.Id.ToString() }));

        db.ChangeTracker.Clear();
        var revisionExists = await db.ArticleRevisions.AnyAsync(r => r.Id == revisionId);
        revisionExists.Should().BeFalse();
    }
}
