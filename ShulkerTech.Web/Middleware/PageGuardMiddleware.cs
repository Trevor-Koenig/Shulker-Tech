using Microsoft.AspNetCore.Identity;
using ShulkerTech.Core.Models;
using ShulkerTech.Web.Services;

namespace ShulkerTech.Web.Middleware;

/// <summary>
/// Guards main-site pages and the wiki area using the RBAC permission system.
/// Resources that are public-by-default remain accessible to everyone until an admin
/// explicitly grants the resource to specific roles — at that point only those roles
/// (or IsAdmin users) can access it.
/// Runs after SubdomainRoutingMiddleware so paths already have area prefixes applied.
/// </summary>
public class PageGuardMiddleware(RequestDelegate next)
{
    // Exact path → resource (case-insensitive)
    private static readonly Dictionary<string, string> ExactResourceMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "/",             SiteResource.PageHome },
            { "/Index",        SiteResource.PageHome },
            { "/ServerStatus", SiteResource.PageServerStatus },
            { "/Privacy",      SiteResource.PagePrivacy },
        };

    // Prefix → resource — checked longest-prefix-first; wiki area covers all /Wiki/* paths
    private static readonly (string Prefix, string Resource)[] PrefixResourceMap =
    [
        ("/Wiki",    SiteResource.WikiView),
        ("/players", SiteResource.PagePlayers),
    ];

    // Pre-build a lookup so we don't iterate SiteResource.All on every request
    private static readonly Dictionary<string, ResourceInfo> ResourceInfoMap =
        SiteResource.All.ToDictionary(r => r.Key);

    public async Task InvokeAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        PermissionService permissions)
    {
        var path = context.Request.Path;

        if (!TryGetResource(path, out var resourceKey))
        {
            await next(context);
            return;
        }

        var isPublicByDefault = ResourceInfoMap.TryGetValue(resourceKey, out var info) && info.IsPublicByDefault;

        ApplicationUser? user = null;
        IList<string> roles = Array.Empty<string>();

        if (context.User.Identity?.IsAuthenticated == true)
        {
            user = await userManager.GetUserAsync(context.User);
            if (user != null)
                roles = await userManager.GetRolesAsync(user);
        }

        if (await permissions.CanAccessAsync(user, roles, resourceKey, isPublicByDefault))
        {
            await next(context);
            return;
        }

        // Not allowed — redirect unauthenticated users to login, others to access denied
        if (context.User.Identity?.IsAuthenticated != true)
        {
            var returnUrl = Uri.EscapeDataString(path.Value ?? "/");
            context.Response.Redirect($"/Identity/Account/Login?ReturnUrl={returnUrl}");
        }
        else
        {
            context.Response.Redirect("/Identity/Account/AccessDenied");
        }
    }

    private static bool TryGetResource(PathString path, out string resource)
    {
        var value = path.Value ?? "";

        if (ExactResourceMap.TryGetValue(value, out resource!))
            return true;

        foreach (var (prefix, res) in PrefixResourceMap)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                resource = res;
                return true;
            }
        }

        resource = "";
        return false;
    }
}
