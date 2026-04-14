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
        pipeline.Setup(renderer);               // registers all default renderers first
        renderer.ObjectRenderers.TryRemove<LinkInlineRenderer>();
        renderer.ObjectRenderers.Add(new WikiImageRenderer());
        renderer.Render(document);
        writer.Flush();
        return writer.ToString();
    }
}
