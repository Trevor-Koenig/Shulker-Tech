using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Core.Data;
using ShulkerTech.Tests.Infrastructure;

namespace ShulkerTech.Tests.Pages;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class WikiArticleSearchTests(ShulkerTechWebApplicationFactory factory)
{
    private HttpClient CreateClient(string? userId = null)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        if (userId != null)
            client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        return client;
    }

    [Fact]
    public async Task Search_MatchingTitle_ReturnsArticleInHtml()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var uniqueWord = $"Ender{Guid.NewGuid():N}"[..18];
        await TestDbHelper.CreateArticleAsync(db, user.Id,
            title: $"{uniqueWord} Dragon Guide",
            slug: $"search-title-{Guid.NewGuid():N}");

        var html = await CreateClient().GetStringAsync($"/Wiki?q={uniqueWord}");
        html.Should().Contain($"{uniqueWord} Dragon Guide");
    }

    [Fact]
    public async Task Search_MatchingContent_ReturnsArticleInHtml()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var uniqueWord = $"Netherite{Guid.NewGuid():N}"[..18];
        await TestDbHelper.CreateArticleAsync(db, user.Id,
            title: "A Generic Title",
            slug: $"search-content-{Guid.NewGuid():N}",
            content: $"## Mining\n\nYou can find {uniqueWord} in the nether.");

        var html = await CreateClient().GetStringAsync($"/Wiki?q={uniqueWord}");
        html.Should().Contain("A Generic Title");
    }

    [Fact]
    public async Task Search_NoMatch_ShowsNoResults()
    {
        var html = await CreateClient().GetStringAsync("/Wiki?q=xyzzy_impossible_term_99999");
        html.Should().Contain("NO MATCHING ARTICLES");
    }

    [Fact]
    public async Task Search_Returns200()
    {
        var response = await CreateClient().GetAsync("/Wiki?q=redstone");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Search_DoesNotReturnUnpublishedArticles()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var uniqueWord = $"Wither{Guid.NewGuid():N}"[..16];
        await TestDbHelper.CreateArticleAsync(db, user.Id,
            title: $"{uniqueWord} Draft Article",
            slug: $"search-draft-{Guid.NewGuid():N}",
            isPublished: false);

        var html = await CreateClient().GetStringAsync($"/Wiki?q={uniqueWord}");
        html.Should().NotContain($"{uniqueWord} Draft Article");
    }

    [Fact]
    public async Task Search_ShowsQueryInInput()
    {
        var html = await CreateClient().GetStringAsync("/Wiki?q=redstone");
        html.Should().Contain("value=\"redstone\"");
    }

    [Fact]
    public async Task Search_ShowsClearLink()
    {
        var html = await CreateClient().GetStringAsync("/Wiki?q=redstone");
        html.Should().Contain("CLEAR");
    }

    [Fact]
    public async Task NoSearch_DoesNotShowClearLink()
    {
        var html = await CreateClient().GetStringAsync("/Wiki");
        html.Should().NotContain("✕ CLEAR");
    }

    [Fact]
    public async Task Search_DoesNotReturnViewRestrictedArticles_WhenUserLacksRole()
    {
        using var scope = factory.Services.CreateScope();
        var author = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var uniqueWord = $"Secret{Guid.NewGuid():N}"[..16];
        await TestDbHelper.CreateArticleAsync(db, author.Id,
            title: $"{uniqueWord} Admin Only Article",
            slug: $"search-restricted-{Guid.NewGuid():N}",
            viewRole: "Admin");

        var html = await CreateClient().GetStringAsync($"/Wiki?q={uniqueWord}");
        html.Should().NotContain($"{uniqueWord} Admin Only Article");
    }
}
