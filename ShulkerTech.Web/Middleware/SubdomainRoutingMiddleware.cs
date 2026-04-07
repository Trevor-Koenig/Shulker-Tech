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
    public static readonly Dictionary<string, string> SubdomainAreaMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["wiki"]  = "Wiki",
        ["admin"] = "Admin",
    };

    // Paths that are served from the root app regardless of subdomain.
    // These are never prefixed with an area path.
    private static readonly string[] GlobalPaths =
    [
        "/setup",
        "/Account",
        "/css/",
        "/js/",
        "/favicon.ico",
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        var host = context.Request.Host.Host;
        var subdomain = host.Split('.')[0];

        // Global paths (setup, login, etc.) always live on the root domain.
        // Redirect to root if accessed from a subdomain so relative redirects
        // within those pages don't stay on the wrong subdomain.
        var path = context.Request.Path.Value ?? string.Empty;
        if (GlobalPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            var rootHost = host[(subdomain.Length + 1)..];
            if (rootHost.Contains('.'))
            {
                var port = context.Request.Host.Port;
                var portSuffix = port.HasValue ? $":{port}" : string.Empty;
                context.Response.Redirect($"{context.Request.Scheme}://{rootHost}{portSuffix}{context.Request.Path}{context.Request.QueryString}");
                return;
            }

            await next(context);
            return;
        }

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
