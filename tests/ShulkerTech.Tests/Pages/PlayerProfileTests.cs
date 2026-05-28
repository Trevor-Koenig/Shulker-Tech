using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;
using ShulkerTech.Tests.Infrastructure;

namespace ShulkerTech.Tests.Pages;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class PlayerProfileTests(ShulkerTechWebApplicationFactory factory)
{
    // In tests there's no subdomain, so the community area is reached at /Community/players/...
    private const string PlayerBase = "/Community/players";

    private HttpClient CreateClient(string? userId = null)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        if (userId != null)
            client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        return client;
    }

    [Fact]
    public async Task Get_ExistingPlayer_Returns200()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider);

        var mcName = $"Player{Guid.NewGuid():N}"[..14];
        user.MinecraftUsername = mcName;
        await db.SaveChangesAsync();

        var response = await CreateClient().GetAsync($"{PlayerBase}/{mcName}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_UnknownPlayer_Returns404()
    {
        var response = await CreateClient().GetAsync($"{PlayerBase}/ThisPlayerDoesNotExist99999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_PlayerPage_IsCaseInsensitive()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider);

        var mcName = $"CasedPlayer{Guid.NewGuid():N}"[..14];
        user.MinecraftUsername = mcName;
        await db.SaveChangesAsync();

        var response = await CreateClient().GetAsync($"{PlayerBase}/{mcName.ToUpper()}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_PlayerPage_ShowsPublishedArticles()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");

        var mcName = $"ArticlePlayer{Guid.NewGuid():N}"[..14];
        user.MinecraftUsername = mcName;
        await db.SaveChangesAsync();

        var article = await TestDbHelper.CreateArticleAsync(db, user.Id,
            title: $"Profile Article {Guid.NewGuid():N}",
            slug: $"profile-article-{Guid.NewGuid():N}");

        var html = await CreateClient().GetStringAsync($"{PlayerBase}/{mcName}");
        html.Should().Contain(article.Title);
    }

    [Fact]
    public async Task Get_PlayerPage_DoesNotShowUnpublishedArticles()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");

        var mcName = $"DraftPlayer{Guid.NewGuid():N}"[..14];
        user.MinecraftUsername = mcName;
        await db.SaveChangesAsync();

        var draft = await TestDbHelper.CreateArticleAsync(db, user.Id,
            title: $"Draft Article {Guid.NewGuid():N}",
            slug: $"draft-article-{Guid.NewGuid():N}",
            isPublished: false);

        var html = await CreateClient().GetStringAsync($"{PlayerBase}/{mcName}");
        html.Should().NotContain(draft.Title);
    }

    [Fact]
    public async Task Get_PlayerPage_ShowsPlaytime_WhenSessionsExist()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider);

        var mcName = $"PlaytimePlayer{Guid.NewGuid():N}"[..14];
        user.MinecraftUsername = mcName;
        await db.SaveChangesAsync();

        var server = await TestDbHelper.CreateServerAsync(db);
        db.PlayerSessions.Add(new PlayerSession
        {
            UserId = user.Id,
            ServerId = server.Id,
            JoinedAt = DateTime.UtcNow.AddHours(-2),
            LeftAt = DateTime.UtcNow,
            DurationSeconds = 7200,
        });
        await db.SaveChangesAsync();

        var html = await CreateClient().GetStringAsync($"{PlayerBase}/{mcName}");
        html.Should().Contain("2H 0M");
    }

    [Fact]
    public async Task Get_PlayerPage_IsPublicWithoutAuth()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider);

        var mcName = $"PublicPlayer{Guid.NewGuid():N}"[..14];
        user.MinecraftUsername = mcName;
        await db.SaveChangesAsync();

        var response = await CreateClient().GetAsync($"{PlayerBase}/{mcName}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
