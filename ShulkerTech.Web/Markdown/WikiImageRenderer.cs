using Markdig.Renderers;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax.Inlines;

namespace ShulkerTech.Web.Markdown;

/// <summary>
/// Custom Markdig renderer for image links that supports Minecraft-wiki-style figure syntax:
///   ![Caption|right|thumb](url)   → float-right thumbnail figure with caption
///   ![Caption|left|thumb](url)    → float-left thumbnail figure with caption
///   ![Caption|center](url)        → centred block figure
///   ![Caption](url)               → plain inline image (no figure wrapper)
/// Non-image links fall through to base.Write() unchanged.
/// </summary>
public sealed class WikiImageRenderer : LinkInlineRenderer
{
    protected override void Write(HtmlRenderer renderer, LinkInline link)
    {
        if (!link.IsImage)
        {
            base.Write(renderer, link);
            return;
        }

        var url = link.Url ?? "";
        if (!IsSafeUrl(url))
            return;

        var rawAlt = ExtractAltText(link);
        ParseAlt(rawAlt, out var caption, out var alignment, out var thumb);

        if (alignment is null)
        {
            // Plain inline image — no figure wrapper
            renderer.Write("<img src=\"");
            renderer.WriteEscapeUrl(url);
            renderer.Write("\" alt=\"");
            renderer.WriteEscape(caption);
            renderer.Write("\" loading=\"lazy\" />");
            return;
        }

        var figClass = $"wiki-figure wiki-figure--{alignment}";
        if (thumb) figClass += " wiki-figure--thumb";

        renderer.Write($"<figure class=\"{figClass}\">");
        renderer.Write("<img src=\"");
        renderer.WriteEscapeUrl(url);
        renderer.Write("\" alt=\"");
        renderer.WriteEscape(caption);
        renderer.Write("\" loading=\"lazy\" />");
        if (!string.IsNullOrWhiteSpace(caption))
        {
            renderer.Write("<figcaption>");
            renderer.WriteEscape(caption);
            renderer.Write("</figcaption>");
        }
        renderer.Write("</figure>");
    }

    private static bool IsSafeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (url.StartsWith('/')) return true;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               uri.Scheme is "http" or "https";
    }

    private static string ExtractAltText(LinkInline link)
    {
        // The alt text lives in the link's child inline nodes (typically a LiteralInline).
        // Use a temp writer to avoid flushing into the live output stream.
        using var sw = new StringWriter();
        var tmp = new HtmlRenderer(sw) { EnableHtmlForInline = false };
        foreach (var child in link)
            tmp.Render(child);
        sw.Flush();
        return sw.ToString();
    }

    private static void ParseAlt(string rawAlt, out string caption, out string? alignment, out bool thumb)
    {
        alignment = null;
        thumb = false;

        var parts = rawAlt.Split('|');
        caption = parts[0].Trim();

        for (var i = 1; i < parts.Length; i++)
        {
            var part = parts[i].Trim().ToLowerInvariant();
            switch (part)
            {
                case "right" or "left" or "center":
                    alignment = part;
                    break;
                case "thumb" or "thumbnail":
                    thumb = true;
                    break;
            }
        }
    }
}
