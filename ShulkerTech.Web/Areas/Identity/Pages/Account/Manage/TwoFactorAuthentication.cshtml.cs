using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Identity.Pages.Account.Manage;

public class TwoFactorAuthenticationModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext db) : PageModel
{
    public bool HasAuthenticator { get; set; }
    public int RecoveryCodesLeft { get; set; }
    public bool Is2faEnabled { get; set; }
    public bool IsMachineRemembered { get; set; }
    public bool IsEnforced { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null)
            return NotFound();

        HasAuthenticator = await userManager.GetAuthenticatorKeyAsync(user) != null;
        Is2faEnabled = await userManager.GetTwoFactorEnabledAsync(user);
        IsMachineRemembered = await signInManager.IsTwoFactorClientRememberedAsync(user);
        RecoveryCodesLeft = await userManager.CountRecoveryCodesAsync(user);

        if (!Is2faEnabled)
        {
            var settings = await db.SecuritySettings.FirstOrDefaultAsync() ?? new SecuritySettings();
            var requiredRoles = settings.GetRequiredRoles();
            if (requiredRoles.Count > 0)
            {
                var userRoles = await userManager.GetRolesAsync(user);
                IsEnforced = userRoles.Any(r => requiredRoles.Contains(r));
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null)
            return NotFound();

        await signInManager.ForgetTwoFactorClientAsync();
        StatusMessage = "This browser has been forgotten. You will be prompted for your 2FA code on next sign in.";
        return RedirectToPage();
    }
}
