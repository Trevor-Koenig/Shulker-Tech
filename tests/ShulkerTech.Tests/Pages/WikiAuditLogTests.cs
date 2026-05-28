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
public class WikiAuditLogTests(ShulkerTechWebApplicationFactory factory)
{
    private HttpClient CreateClient(string? userId = null)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        if (userId != null)
            client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        return client;
    }

    private static FormUrlEncodedContent EditForm(int id, string title = "Updated Title", string content = "Updated content.")
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
    public async Task CreateArticle_WritesAuditEntry_WithCreatedAction()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var title = $"Audit Create {Guid.NewGuid():N}";

        await CreateClient(user.Id).PostAsync("/Wiki/articles/create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Title"] = title,
                ["Input.Content"] = "## Section\n\nContent here.",
                ["Input.IsPublished"] = "true",
            }));

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entry = await db.AuditLog
            .FirstOrDefaultAsync(e => e.Action == AuditAction.ArticleCreated && e.ActorId == user.Id && e.ArticleTitle == title);

        entry.Should().NotBeNull();
        entry!.ArticleId.Should().BePositive();
    }

    [Fact]
    public async Task EditArticle_WritesAuditEntry_WithUpdatedAction()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, user.Id, slug: $"audit-edit-{Guid.NewGuid():N}");

        await CreateClient(user.Id).PostAsync($"/Wiki/articles/edit/{article.Id}", EditForm(article.Id));

        var entry = await db.AuditLog
            .FirstOrDefaultAsync(e => e.Action == AuditAction.ArticleUpdated && e.ArticleId == article.Id);

        entry.Should().NotBeNull();
        entry!.ActorId.Should().Be(user.Id);
    }

    [Fact]
    public async Task DeleteArticle_WritesAuditEntry_WithDeletedAction()
    {
        using var scope = factory.Services.CreateScope();
        var mod = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Moderator");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var article = await TestDbHelper.CreateArticleAsync(db, mod.Id,
            title: $"To Delete {Guid.NewGuid():N}",
            slug: $"audit-delete-{Guid.NewGuid():N}");
        var articleId = article.Id;
        var articleTitle = article.Title;

        await CreateClient(mod.Id).PostAsync(
            $"/Wiki/articles/edit/{articleId}?handler=Delete",
            new FormUrlEncodedContent([]));

        var entry = await db.AuditLog
            .FirstOrDefaultAsync(e => e.Action == AuditAction.ArticleDeleted && e.ArticleTitle == articleTitle);

        entry.Should().NotBeNull();
        entry!.ActorId.Should().Be(mod.Id);
    }

    [Fact]
    public async Task AuditLogAdminPage_AdminUser_Returns200()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true, role: "Admin");

        var response = await CreateClient(admin.Id).GetAsync("/Admin/AuditLog");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AuditLogAdminPage_Unauthenticated_RedirectsToLogin()
    {
        var response = await CreateClient().GetAsync("/Admin/AuditLog");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
    }

    [Fact]
    public async Task AuditLogAdminPage_ContainsEntries_InHtml()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true, role: "Admin");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.AuditLog.Add(new AuditLogEntry
        {
            Action = AuditAction.ArticleCreated,
            ActorId = admin.Id,
            ArticleTitle = "Audit Page Test Article",
            OccurredAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var html = await CreateClient(admin.Id).GetStringAsync("/Admin/AuditLog");
        html.Should().Contain("Audit Page Test Article");
    }
}
