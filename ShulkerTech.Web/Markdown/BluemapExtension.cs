using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace ShulkerTech.Web.Markdown;

/// <summary>
/// Markdig extension that intercepts fenced code blocks with language "map" and renders
/// them as collapsible interactive BlueMap iframes instead of code listings.
///
/// Usage in article markdown:
///   ```map
///   https://bluemap.example.com/#world:100,64,200:0:0:0:500:flat
///   ```
///
/// Multiple map blocks per article are supported; each collapses independently with
/// its open/closed state persisted in localStorage.
/// </summary>
public sealed class BluemapExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline) { }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer is not HtmlRenderer html) return;

        var existing = html.ObjectRenderers.FindExact<CodeBlockRenderer>();
        if (existing != null)
            html.ObjectRenderers.Remove(existing);

        html.ObjectRenderers.Add(new BluemapCodeBlockRenderer(existing ?? new CodeBlockRenderer()));
    }

    // ── Renderer ─────────────────────────────────────────────────────────────

    private sealed class BluemapCodeBlockRenderer(CodeBlockRenderer fallback) : HtmlObjectRenderer<CodeBlock>
    {
        protected override void Write(HtmlRenderer renderer, CodeBlock block)
        {
            if (block is FencedCodeBlock { Info: "map" } fenced)
            {
                var url = ExtractUrl(fenced);
                if (IsValidUrl(url))
                {
                    RenderMapPanel(renderer, url);
                    return;
                }
            }

            fallback.Write(renderer, block);
        }

        private static string ExtractUrl(CodeBlock block)
        {
            var lines = block.Lines;
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines.Lines[i].Slice.ToString().Trim();
                if (!string.IsNullOrEmpty(line))
                    return line;
            }
            return "";
        }

        private static bool IsValidUrl(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https";

        private static void RenderMapPanel(HtmlRenderer renderer, string url)
        {
            // Stable hex ID so the inline script can reference this specific element.
            // Safe to use GetHashCode here — only needs to be unique within one rendered page.
            var id = $"wiki-inline-map-{(uint)url.GetHashCode():x8}";

            renderer.Write($"<details id=\"{id}\" class=\"panel mb-6\" style=\"overflow:hidden;\" data-map-key=\"wiki-inline-map:");
            renderer.WriteEscape(url);
            renderer.Write("\">");

            renderer.Write("<summary style=\"padding:0.75rem 1.25rem;cursor:pointer;"
                + "font-family:var(--font-display);font-size:0.75rem;letter-spacing:0.12em;"
                + "color:var(--color-accent);list-style:none;display:flex;align-items:center;"
                + "gap:0.5rem;user-select:none;\">"
                + "<span>&#9670;</span> VIEW ON MAP</summary>");

            renderer.Write("<div style=\"height:500px;border-top:1px solid color-mix(in oklab,var(--color-accent) 15%,transparent);\">");
            renderer.Write("<iframe src=\"");
            renderer.WriteEscapeUrl(url);
            renderer.Write("\" style=\"width:100%;height:100%;border:none;\" loading=\"lazy\""
                + " title=\"BlueMap\" sandbox=\"allow-scripts allow-same-origin allow-popups\"></iframe>");
            renderer.Write("</div></details>");

            // Inline script: persists open/closed state per URL in localStorage.
            renderer.Write($"<script>(function(){{var d=document.getElementById('{id}');"
                + "if(!d)return;var k=d.getAttribute('data-map-key');"
                + "if(localStorage.getItem(k)==='true')d.open=true;"
                + "d.addEventListener('toggle',function(){{localStorage.setItem(k,d.open?'true':'false');}});"
                + "}})();</script>\n");
        }
    }
}
