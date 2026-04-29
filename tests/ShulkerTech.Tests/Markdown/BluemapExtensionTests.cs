using FluentAssertions;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using ShulkerTech.Web.Markdown;

namespace ShulkerTech.Tests.Markdown;

[Trait("Category", "Unit")]
public class BluemapExtensionTests
{
    private static WikiMarkdownService BuildService() =>
        new(new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
            .Use<BluemapExtension>()
            .DisableHtml()
            .Build());

    // ── Map block renders correctly ───────────────────────────────────────────

    [Fact]
    public void MapBlock_RendersIframe()
    {
        var html = BuildService().ToHtml("```map\nhttps://bluemap.example.com/#world:0,64,0\n```");
        html.Should().Contain("<iframe");
        html.Should().Contain("https://bluemap.example.com/#world:0,64,0");
    }

    [Fact]
    public void MapBlock_RendersCollapsibleDetails()
    {
        var html = BuildService().ToHtml("```map\nhttps://bluemap.example.com/\n```");
        html.Should().Contain("<details");
        html.Should().Contain("<summary");
        html.Should().Contain("VIEW ON MAP");
    }

    [Fact]
    public void MapBlock_HasSandboxAttribute()
    {
        var html = BuildService().ToHtml("```map\nhttps://bluemap.example.com/\n```");
        html.Should().Contain("sandbox=");
        html.Should().Contain("allow-scripts");
    }

    [Fact]
    public void MapBlock_HasLocalStorageScript()
    {
        var html = BuildService().ToHtml("```map\nhttps://bluemap.example.com/\n```");
        html.Should().Contain("<script>");
        html.Should().Contain("localStorage");
    }

    [Fact]
    public void TwoMapBlocks_RenderTwoIndependentIframes()
    {
        var md = "```map\nhttps://bluemap.example.com/a\n```\n\n```map\nhttps://bluemap.example.com/b\n```";
        var html = BuildService().ToHtml(md);

        html.Should().Contain("https://bluemap.example.com/a");
        html.Should().Contain("https://bluemap.example.com/b");
        html.Split("<iframe").Length.Should().Be(3); // 2 iframes = 3 parts when split
    }

    [Fact]
    public void TwoMapBlocks_HaveDistinctIds()
    {
        var md = "```map\nhttps://bluemap.example.com/a\n```\n\n```map\nhttps://bluemap.example.com/b\n```";
        var html = BuildService().ToHtml(md);

        // Each block gets a unique id derived from its URL
        var ids = System.Text.RegularExpressions.Regex
            .Matches(html, @"id=""(wiki-inline-map-[^""]+)""")
            .Select(m => m.Groups[1].Value)
            .ToList();

        ids.Should().HaveCount(2);
        ids[0].Should().NotBe(ids[1]);
    }

    // ── Invalid URLs are not rendered ─────────────────────────────────────────

    [Fact]
    public void MapBlock_InvalidUrl_DoesNotRenderIframe()
    {
        var html = BuildService().ToHtml("```map\nnot-a-url\n```");
        html.Should().NotContain("<iframe");
    }

    [Fact]
    public void MapBlock_JavascriptUrl_DoesNotRenderIframe()
    {
        var html = BuildService().ToHtml("```map\njavascript:alert(1)\n```");
        html.Should().NotContain("<iframe");
    }

    [Fact]
    public void MapBlock_EmptyContent_DoesNotRenderIframe()
    {
        var html = BuildService().ToHtml("```map\n\n```");
        html.Should().NotContain("<iframe");
    }

    // ── Non-map code blocks still render as code ──────────────────────────────

    [Fact]
    public void RegularCodeBlock_RendersAsCode()
    {
        var html = BuildService().ToHtml("```csharp\nvar x = 1;\n```");
        html.Should().NotContain("<iframe");
        html.Should().Contain("var x = 1;");
    }

    [Fact]
    public void UnfencedCodeBlock_RendersAsCode()
    {
        var html = BuildService().ToHtml("    var x = 1;");
        html.Should().NotContain("<iframe");
        html.Should().Contain("var x = 1;");
    }
}
