namespace ShulkerTech.Web.Middleware;

/// <summary>
/// Detects the subdomain from the Host header and rewrites the request path
/// so Razor Pages area routing picks up the correct Area.
///
/// wiki.shulkertech.com/articles  →  /Wiki/articles  →  Areas/Wiki/Pages/Articles
/// admin.shulkertech.com/users    →  /Admin/users    →  Areas/Admin/Pages/Users
/// shulkertech.com/               →  /               →  Pages/ (homepage)
/// </summary>
public class SubdomainRoutingMiddleware(RequestDelegate next)
{
    private static readonly Dictionary<string, string> SubdomainAreaMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["wiki"]  = "Wiki",
        ["admin"] = "Admin",
    };

    public async Task InvokeAsync(HttpContext context)
    {
        var host = context.Request.Host.Host;
        var subdomain = host.Split('.')[0];

        if (SubdomainAreaMap.TryGetValue(subdomain, out var area))
        {
            var areaPrefix = $"/{area}";

            // Avoid double-prefixing if middleware runs more than once
            if (!context.Request.Path.StartsWithSegments(areaPrefix, StringComparison.OrdinalIgnoreCase))
            {
                context.Request.Path = areaPrefix + context.Request.Path;
            }
        }

        await next(context);
    }
}
