using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Middleware;

public class Require2FAMiddleware(RequestDelegate next)
{
    private static readonly string SetupPath = "/Identity/Account/Manage/TwoFactorAuthentication";

    private static readonly HashSet<string> ExemptPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Identity/Account/Logout",
        "/Identity/Account/Manage/TwoFactorAuthentication",
        "/Identity/Account/Manage/EnableAuthenticator",
        "/Identity/Account/Manage/GenerateRecoveryCodes",
        "/Identity/Account/Manage/ResetAuthenticator",
        "/Identity/Account/Manage/Disable2fa",
        "/Identity/Account/LoginWith2fa",
        "/_framework",
        "/_blazor",
        // Static assets — must be exempt so the 2FA setup page can load its own resources
        "/css",
        "/js",
        "/lib",
        "/images",
        "/favicon.ico",
    };

    public async Task InvokeAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true && !IsExempt(context.Request.Path))
        {
            var user = await userManager.GetUserAsync(context.User);
            if (user is not null && !user.TwoFactorEnabled)
            {
                var settings = await db.SecuritySettings.FirstOrDefaultAsync() ?? new SecuritySettings();
                var requiredRoles = settings.GetRequiredRoles();

                if (requiredRoles.Count > 0)
                {
                    var userRoles = await userManager.GetRolesAsync(user);
                    if (userRoles.Any(r => requiredRoles.Contains(r)))
                    {
                        context.Response.Redirect(SetupPath);
                        return;
                    }
                }
            }
        }

        await next(context);
    }

    private static bool IsExempt(PathString path) =>
        ExemptPrefixes.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
}
