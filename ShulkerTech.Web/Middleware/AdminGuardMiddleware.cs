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
            // Not logged in — redirect to login with the current path as ReturnUrl so
            // Identity brings them back after a successful login.
            // SubdomainRoutingMiddleware will redirect /Identity/... to the root domain.
            var returnUrl = Uri.EscapeDataString(context.Request.Path + context.Request.QueryString);
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
