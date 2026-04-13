using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Identity.Pages.Account.Manage;

public class Disable2faModel(
    UserManager<ApplicationUser> userManager,
    ILogger<Disable2faModel> logger) : PageModel
{
    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        if (!await userManager.GetTwoFactorEnabledAsync(user))
        {
            StatusMessage = "Error: 2FA is not currently enabled on this account.";
            return RedirectToPage("./TwoFactorAuthentication");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var result = await userManager.SetTwoFactorEnabledAsync(user, false);
        if (!result.Succeeded)
            throw new InvalidOperationException("Unexpected error disabling 2FA.");

        logger.LogInformation("User '{UserId}' disabled 2FA.", await userManager.GetUserIdAsync(user));
        StatusMessage = "2FA has been disabled. You can re-enable it at any time.";
        return RedirectToPage("./TwoFactorAuthentication");
    }
}
