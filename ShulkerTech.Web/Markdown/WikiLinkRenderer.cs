using Markdig.Renderers;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax.Inlines;

namespace ShulkerTech.Web.Markdown;

/// <summary>
/// Handles all Markdig LinkInline nodes (both images and hyperlinks).
///
/// Images: supports Minecraft-wiki-style figure syntax:
///   ![Caption|right|thumb](url)  → float-right thumbnail figure with caption
///   ![Caption|left|thumb](url)   → float-left thumbnail figure with caption
///   ![Caption|center](url)       → centred block figure
///   ![Caption](url)              → plain inline image
///
/// Hyperlinks: external URLs (http/https) get target="_blank" rel="noopener noreferrer".
///   Internal links (relative paths, anchor fragments) are rendered normally.
/// </summary>
public sealed class WikiLinkRenderer : LinkInlineRenderer
{
    protected override void Write(HtmlRenderer renderer, LinkInline link)
    {
        if (link.IsImage)
            WriteImage(renderer, link);
        else
            WriteHyperlink(renderer, link);
    }

    // ── Hyperlinks ────────────────────────────────────────────────────────────

    private static void WriteHyperlink(HtmlRenderer renderer, LinkInline link)
    {
        var url = link.Url ?? "";

        renderer.Write("<a href=\"");
        renderer.WriteEscapeUrl(url);
        renderer.Write("\"");

        if (!string.IsNullOrEmpty(link.Title))
        {
            renderer.Write(" title=\"");
            renderer.WriteEscape(link.Title);
            renderer.Write("\"");
        }

        if (IsExternalUrl(url))
            renderer.Write(" target=\"_blank\" rel=\"noopener noreferrer\"");

        renderer.Write(">");
        renderer.WriteChildren(link);
        renderer.Write("</a>");
    }

    private static bool IsExternalUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https";

    // ── Images ────────────────────────────────────────────────────────────────

    private static void WriteImage(HtmlRenderer renderer, LinkInline link)
    {
        var url = link.Url ?? "";
        if (!IsSafeImageUrl(url))
            return;

        var rawAlt = ExtractAltText(link);
        ParseAlt(rawAlt, out var caption, out var alignment, out var thumb);

        if (alignment is null)
        {
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

    private static bool IsSafeImageUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (url.StartsWith('/')) return true;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               uri.Scheme is "http" or "https";
    }

    private static string ExtractAltText(LinkInline link)
    {
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
