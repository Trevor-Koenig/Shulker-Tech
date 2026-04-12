using Microsoft.AspNetCore.Identity;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Middleware;

/// <summary>
/// Redirects all requests to /setup when no users exist in the database.
/// Once the first admin account is created, this middleware becomes a no-op.
/// Works across all subdomains since it runs before subdomain routing.
/// </summary>
public class FirstRunMiddleware(RequestDelegate next)
{
    private static readonly string[] PassthroughPaths =
    [
        "/setup",
        "/favicon.ico",
        "/css/",
        "/js/",
        "/images/",
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip static assets and the setup page itself
        var path = context.Request.Path.Value ?? string.Empty;
        if (PassthroughPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var hasUsers = userManager.Users.Any();

        if (!hasUsers)
        {
            // /setup is in SubdomainRoutingMiddleware's GlobalPaths list so it won't
            // be rewritten to /Wiki/setup or /Admin/setup — safe to redirect to /setup
            // from any subdomain.
            context.Response.Redirect("/setup");
            return;
        }

        await next(context);
    }
}
