using FluentAssertions;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using ShulkerTech.Web.Markdown;

namespace ShulkerTech.Tests.Markdown;

/// <summary>Unit tests for WikiMarkdownService — runs against the real pipeline without a DB.</summary>
[Trait("Category", "Unit")]
public class WikiMarkdownServiceTests
{
    private static WikiMarkdownService BuildService() =>
        new(new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
            .DisableHtml()
            .Build());

    // ── External hyperlinks ───────────────────────────────────────────────────

    [Fact]
    public void ExternalLink_HasTargetBlankAndNoOpener()
    {
        var html = BuildService().ToHtml("[Minecraft Wiki](https://minecraft.wiki)");
        html.Should().Contain("target=\"_blank\"");
        html.Should().Contain("rel=\"noopener noreferrer\"");
        html.Should().Contain("href=\"https://minecraft.wiki\"");
    }

    [Fact]
    public void ExternalHttpLink_HasTargetBlank()
    {
        var html = BuildService().ToHtml("[Example](http://example.com)");
        html.Should().Contain("target=\"_blank\"");
        html.Should().Contain("rel=\"noopener noreferrer\"");
    }

    [Fact]
    public void InternalRelativeLink_NoTargetBlank()
    {
        var html = BuildService().ToHtml("[Home](/wiki/articles/home)");
        html.Should().NotContain("target=\"_blank\"");
        html.Should().Contain("href=\"/wiki/articles/home\"");
    }

    [Fact]
    public void AnchorLink_NoTargetBlank()
    {
        var html = BuildService().ToHtml("[Jump](#my-section)");
        html.Should().NotContain("target=\"_blank\"");
        html.Should().Contain("href=\"#my-section\"");
    }

    [Fact]
    public void Link_WithTitle_TitleAttributePreserved()
    {
        var html = BuildService().ToHtml("[Click](https://example.com \"My Title\")");
        html.Should().Contain("title=\"My Title\"");
    }

    // ── Header anchor IDs (GitHub-style) ─────────────────────────────────────

    [Fact]
    public void Heading_GeneratesLowercaseHyphenatedId()
    {
        var html = BuildService().ToHtml("## My Section");
        html.Should().Contain("id=\"my-section\"");
    }

    [Fact]
    public void Heading_WithSpecialChars_StripsNonAlphanumeric()
    {
        var html = BuildService().ToHtml("## Farms & Builds");
        // GitHub-style: strips & and collapses
        html.Should().Contain("id=\"");
        html.Should().Contain("<h2");
    }

    [Fact]
    public void AnchorLink_MatchesGeneratedHeadingId()
    {
        var svc = BuildService();
        var headingHtml = svc.ToHtml("## My Section");
        var linkHtml = svc.ToHtml("[Jump](#my-section)");

        headingHtml.Should().Contain("id=\"my-section\"");
        linkHtml.Should().Contain("href=\"#my-section\"");
    }

    // ── Images ────────────────────────────────────────────────────────────────

    [Fact]
    public void ExternalImage_RendersImgTag()
    {
        var html = BuildService().ToHtml("![Alt text](https://example.com/img.png)");
        html.Should().Contain("<img");
        html.Should().Contain("https://example.com/img.png");
        html.Should().Contain("alt=\"Alt text\"");
    }

    [Fact]
    public void Image_WithRightThumb_RendersFigure()
    {
        var html = BuildService().ToHtml("![Caption|right|thumb](/uploads/wiki/test.png)");
        html.Should().Contain("<figure");
        html.Should().Contain("wiki-figure--right");
        html.Should().Contain("wiki-figure--thumb");
        html.Should().Contain("<figcaption>Caption</figcaption>");
    }

    [Fact]
    public void Image_WithCenter_RendersFigure()
    {
        var html = BuildService().ToHtml("![Caption|center](/uploads/wiki/test.png)");
        html.Should().Contain("<figure");
        html.Should().Contain("wiki-figure--center");
    }

    [Fact]
    public void Image_PlainSyntax_RendersInlineImg()
    {
        var html = BuildService().ToHtml("![Alt](/uploads/wiki/test.png)");
        html.Should().Contain("<img");
        html.Should().NotContain("<figure");
    }
}
