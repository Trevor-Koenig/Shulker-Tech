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

    // Paths that belong to the root app and redirect to the root domain when
    // accessed from a subdomain (login, setup, etc.).
    private static readonly string[] GlobalPaths =
    [
        "/setup",
        "/Account",
        "/Identity",
    ];

    // Static asset paths — served directly from whichever host the browser
    // requests them from. No redirect, no area-prefix. The same wwwroot is
    // shared by all subdomains so the files are always reachable.
    private static readonly string[] StaticPaths =
    [
        "/css",
        "/js",
        "/lib",
        "/images",
        "/favicon.ico",
        "/api",
        "/uploads",
    ];

    // Paths that skip area-prefixing but are served locally (no cross-domain redirect).
    // Used so the root-level 404 page is reachable from any subdomain during re-execution.
    private static readonly string[] LocalPassthroughPaths = ["/404"];

    public async Task InvokeAsync(HttpContext context)
    {
        var host = context.Request.Host.Host;
        var subdomain = host.Split('.')[0];
        var path = context.Request.Path.Value ?? string.Empty;

        // Static assets — served directly, no redirect, no area-prefix.
        if (HasPrefix(path, StaticPaths))
        {
            await next(context);
            return;
        }

        // Global paths (login, setup, etc.) always live on the root domain.
        // Redirect subdomains to root so relative redirects within those pages
        // don't stay on the wrong host.
        if (HasPrefix(path, GlobalPaths))
        {
            // Guard against hosts with no dot (e.g. "localhost") where slicing would throw
            var rootHost = subdomain.Length < host.Length ? host[(subdomain.Length + 1)..] : string.Empty;
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

        // Passthrough paths skip area-prefixing but stay on the current host.
        if (HasExact(path, LocalPassthroughPaths))
        {
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

    private static bool HasPrefix(string path, string[] prefixes)
    {
        foreach (var prefix in prefixes)
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static bool HasExact(string path, string[] candidates)
    {
        foreach (var candidate in candidates)
            if (path.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
