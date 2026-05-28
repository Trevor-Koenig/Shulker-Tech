using Microsoft.AspNetCore.Identity;
using ShulkerTech.Core.Models;
using ShulkerTech.Web.Services;

namespace ShulkerTech.Web.Middleware;

/// <summary>
/// Guards all /Admin/* paths using RBAC. Authenticated users are checked against
/// the per-path admin resource grant. Unauthenticated users are redirected to login.
/// </summary>
public class AdminGuardMiddleware(RequestDelegate next)
{
    // Longer/more-specific prefixes must come before shorter ones.
    private static readonly (string Prefix, string Resource)[] PathResourceMap =
    [
        ("/Admin/Wiki/Settings",   SiteResource.AdminWikiSettings),
        ("/Admin/Wiki/Tags",       SiteResource.AdminWikiTags),
        ("/Admin/Wiki/Templates",  SiteResource.AdminWikiTemplates),
        ("/Admin/Wiki",            SiteResource.AdminWikiSettings),  // catch-all for /Admin/Wiki
        ("/Admin/Users",           SiteResource.AdminUsers),
        ("/Admin/Roles",           SiteResource.AdminRoles),
        ("/Admin/Security",        SiteResource.AdminSecurity),
        ("/Admin/Site/DbExport",   SiteResource.AdminDbExport),
        ("/Admin/Site",            SiteResource.AdminSiteSettings),
        ("/Admin/Invites",         SiteResource.AdminInvites),
        ("/Admin/Servers",         SiteResource.AdminServers),
        ("/Admin/Maps",            SiteResource.AdminMaps),
        ("/Admin",                 SiteResource.AdminAccess),         // dashboard catch-all
    ];

    public async Task InvokeAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        PermissionService permissions)
    {
        if (!context.Request.Path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            var adminPath = context.Request.Path.Value!["/Admin".Length..];
            if (string.IsNullOrEmpty(adminPath)) adminPath = "/";

            var returnUrl = Uri.EscapeDataString($"/admin-redirect?p={Uri.EscapeDataString(adminPath)}");
            context.Response.Redirect($"/Identity/Account/Login?ReturnUrl={returnUrl}");
            return;
        }

        var user = await userManager.GetUserAsync(context.User);
        if (user == null)
        {
            context.Response.Redirect("/Identity/Account/Login");
            return;
        }

        var resource = GetResource(context.Request.Path);
        var roles = await userManager.GetRolesAsync(user);

        if (!await permissions.CanAccessAsync(user, roles, resource, isPublicByDefault: false))
        {
            // Don't reveal the area exists — redirect to login without a return URL
            context.Response.Redirect("/Identity/Account/Login");
            return;
        }

        await next(context);
    }

    private static string GetResource(PathString path)
    {
        foreach (var (prefix, resource) in PathResourceMap)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
                return resource;
        }
        return SiteResource.AdminAccess;
    }
}
