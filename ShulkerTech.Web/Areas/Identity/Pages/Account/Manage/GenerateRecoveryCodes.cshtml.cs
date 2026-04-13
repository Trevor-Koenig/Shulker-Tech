using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Identity.Pages.Account.Manage;

public class GenerateRecoveryCodesModel(
    UserManager<ApplicationUser> userManager,
    ILogger<GenerateRecoveryCodesModel> logger) : PageModel
{
    [TempData]
    public string[]? RecoveryCodes { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        if (!await userManager.GetTwoFactorEnabledAsync(user))
        {
            StatusMessage = "Error: 2FA is not enabled — enable it before generating recovery codes.";
            return RedirectToPage("./TwoFactorAuthentication");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        if (!await userManager.GetTwoFactorEnabledAsync(user))
        {
            StatusMessage = "Error: 2FA is not enabled — enable it before generating recovery codes.";
            return RedirectToPage("./TwoFactorAuthentication");
        }

        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 8);
        RecoveryCodes = codes?.ToArray();
        logger.LogInformation("User '{UserId}' generated new 2FA recovery codes.", await userManager.GetUserIdAsync(user));
        StatusMessage = "New recovery codes have been generated.";
        return RedirectToPage("./ShowRecoveryCodes");
    }
}
