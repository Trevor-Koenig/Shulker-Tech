using FluentAssertions;
using ShulkerTech.Web.Areas.Wiki.Pages;

namespace ShulkerTech.Tests.Services;

[Trait("Category", "Unit")]
public class WikiIndexStripMarkdownTests
{
    private static string Strip(string md) => IndexModel.StripMarkdown(md);

    [Fact]
    public void StripMarkdown_RemovesImageSyntax()
    {
        // The image token is removed and surrounding whitespace collapsed to one space
        Strip("Hello ![alt text](image.png) world").Should().Be("Hello world");
    }

    [Fact]
    public void StripMarkdown_ConvertsLinksToLabelOnly()
    {
        Strip("See [the docs](https://example.com) here").Should().Be("See the docs here");
    }

    [Fact]
    public void StripMarkdown_RemovesFencedCodeBlocks()
    {
        var md = "Before\n```csharp\nvar x = 1;\n```\nAfter";
        Strip(md).Should().NotContain("var x = 1;");
        Strip(md).Should().Contain("Before");
        Strip(md).Should().Contain("After");
    }

    [Fact]
    public void StripMarkdown_RemovesInlineCode()
    {
        // Inline code (including its content) is completely removed
        var result = Strip("Use `dotnet test` to run");
        result.Should().NotContain("dotnet test");
        result.Should().Contain("Use").And.Contain("to run");
    }

    [Fact]
    public void StripMarkdown_RemovesHeadingMarkers()
    {
        Strip("## Section Title").Should().Be("Section Title");
        Strip("# Top Level").Should().Be("Top Level");
        Strip("### Deep").Should().Be("Deep");
    }

    [Fact]
    public void StripMarkdown_RemovesEmphasisCharacters()
    {
        Strip("This is **bold** and _italic_").Should().NotContain("*").And.NotContain("_");
        Strip("This is **bold** and _italic_").Should().Contain("bold").And.Contain("italic");
    }

    [Fact]
    public void StripMarkdown_CollapsesMultipleWhitespace()
    {
        Strip("Hello     world").Should().Be("Hello world");
    }

    [Fact]
    public void StripMarkdown_NullInput_ReturnsEmptyString()
    {
        // The implementation uses `markdown ?? ""` so null should not throw
        var act = () => Strip(null!);
        act.Should().NotThrow();
        Strip(null!).Should().BeEmpty();
    }
}
