using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Core.Data;
using ShulkerTech.Tests.Infrastructure;

namespace ShulkerTech.Tests.Pages;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class WikiTocTests(ShulkerTechWebApplicationFactory factory)
{
    private HttpClient CreateClient(string? userId = null)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        if (userId != null)
            client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        return client;
    }

    [Fact]
    public async Task ArticleWithMultipleH2Headings_RendersTocSidebar()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var slug = $"toc-test-{Guid.NewGuid():N}";

        await TestDbHelper.CreateArticleAsync(db, user.Id,
            title: "TOC Test Article",
            slug: slug,
            content: "## First Section\n\nParagraph one.\n\n## Second Section\n\nParagraph two.\n\n## Third Section\n\nParagraph three.");

        var html = await CreateClient().GetStringAsync($"/Wiki/articles/{slug}");
        html.Should().Contain("toc-sidebar");
        html.Should().Contain("toc-nav");
    }

    [Fact]
    public async Task ArticleWithMultipleH2Headings_TocLinksPointToHeadingIds()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var slug = $"toc-links-{Guid.NewGuid():N}";

        await TestDbHelper.CreateArticleAsync(db, user.Id,
            title: "TOC Links Article",
            slug: slug,
            content: "## Overview\n\nIntro text.\n\n## Installation\n\nInstall steps.");

        var html = await CreateClient().GetStringAsync($"/Wiki/articles/{slug}");
        html.Should().Contain("href=\"#overview\"");
        html.Should().Contain("href=\"#installation\"");
    }

    [Fact]
    public async Task ArticleWithH3Headings_TocEntriesHaveTocH3Class()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var slug = $"toc-h3-{Guid.NewGuid():N}";

        await TestDbHelper.CreateArticleAsync(db, user.Id,
            title: "TOC H3 Article",
            slug: slug,
            content: "## Main Section\n\n### Sub Section\n\nContent.\n\n## Another Section\n\nMore content.");

        var html = await CreateClient().GetStringAsync($"/Wiki/articles/{slug}");
        html.Should().Contain("toc-h3");
    }

    [Fact]
    public async Task ArticleWithOneHeading_DoesNotRenderTocSidebar()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var slug = $"toc-single-{Guid.NewGuid():N}";

        await TestDbHelper.CreateArticleAsync(db, user.Id,
            title: "Short Article",
            slug: slug,
            content: "## Only One Section\n\nJust a single heading.");

        var html = await CreateClient().GetStringAsync($"/Wiki/articles/{slug}");
        html.Should().NotContain("toc-sidebar");
    }

    [Fact]
    public async Task ArticleWithNoHeadings_DoesNotRenderTocSidebar()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var slug = $"toc-none-{Guid.NewGuid():N}";

        await TestDbHelper.CreateArticleAsync(db, user.Id,
            title: "No Headings Article",
            slug: slug,
            content: "Just a plain paragraph with no headings at all.");

        var html = await CreateClient().GetStringAsync($"/Wiki/articles/{slug}");
        html.Should().NotContain("toc-sidebar");
    }
}
