using Microsoft.AspNetCore.Identity;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Middleware;

/// <summary>
/// Blocks all access to the Admin area — including 404s — for unauthenticated
/// users or authenticated non-admins. Must run after UseAuthentication() so
/// that context.User is populated.
/// </summary>
public class AdminGuardMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
    {
        if (!context.Request.Path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            // Strip the /Admin prefix to recover the subdomain-relative path.
            // e.g. /Admin/users → /users, /Admin → /
            var adminPath = context.Request.Path.Value!["/Admin".Length..];
            if (string.IsNullOrEmpty(adminPath)) adminPath = "/";

            // ReturnUrl points to the bounce page which does the cross-domain redirect
            // back to admin.domain.com after a successful login.
            // Identity only accepts local ReturnUrls, so we can't pass the subdomain URL directly.
            var returnUrl = Uri.EscapeDataString($"/admin-redirect?p={Uri.EscapeDataString(adminPath)}");
            context.Response.Redirect($"/Identity/Account/Login?ReturnUrl={returnUrl}");
            return;
        }

        var user = await userManager.GetUserAsync(context.User);
        if (user?.IsAdmin != true)
        {
            // Logged in but not an admin — send to login without a return URL;
            // don't reveal the area exists.
            context.Response.Redirect("/Identity/Account/Login");
            return;
        }

        await next(context);
    }
}
