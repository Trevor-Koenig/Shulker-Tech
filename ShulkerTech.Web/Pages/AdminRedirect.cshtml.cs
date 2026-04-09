using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Pages;

/// <summary>
/// Bounce page that redirects authenticated admins back to the admin subdomain
/// after a successful login. Needed because Identity's ReturnUrl only accepts
/// local (same-origin) URLs, so we can't redirect straight to admin.domain.com.
/// </summary>
[Authorize]
public class AdminRedirectModel(UserManager<ApplicationUser> userManager) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string? p)
    {
        var user = await userManager.GetUserAsync(User);
        if (user?.IsAdmin != true)
            return Redirect("/");

        var host = Request.Host.Host;
        var portSuffix = Request.Host.Port.HasValue ? $":{Request.Host.Port}" : "";
        var path = string.IsNullOrWhiteSpace(p) ? "/" : p;
        if (!path.StartsWith('/')) path = "/" + path;

        return Redirect($"{Request.Scheme}://admin.{host}{portSuffix}{path}");
    }
}
