using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html.Inlines;

namespace ShulkerTech.Web.Markdown;

public sealed class WikiMarkdownService(MarkdownPipeline pipeline)
{
    public string ToHtml(string markdown)
    {
        var document = Markdig.Markdown.Parse(markdown, pipeline);
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        pipeline.Setup(renderer);
        renderer.ObjectRenderers.TryRemove<LinkInlineRenderer>();
        renderer.ObjectRenderers.Add(new WikiLinkRenderer());
        renderer.Render(document);
        writer.Flush();
        return writer.ToString();
    }
}
