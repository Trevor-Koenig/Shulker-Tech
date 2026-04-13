using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Identity.Pages.Account.Manage;

public class EnableAuthenticatorModel(
    UserManager<ApplicationUser> userManager,
    ILogger<EnableAuthenticatorModel> logger,
    UrlEncoder urlEncoder) : PageModel
{
    private const string AuthenticatorUriFormat =
        "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

    public string? SharedKey { get; set; }
    public string? AuthenticatorUri { get; set; }

    [TempData]
    public string[]? RecoveryCodes { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [StringLength(7, MinimumLength = 6)]
        [DataType(DataType.Text)]
        public string Code { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();
        await LoadSharedKeyAndQrCodeUriAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        if (!ModelState.IsValid)
        {
            await LoadSharedKeyAndQrCodeUriAsync(user);
            return Page();
        }

        var code = Input.Code.Replace(" ", "").Replace("-", "");
        var isValid = await userManager.VerifyTwoFactorTokenAsync(
            user, userManager.Options.Tokens.AuthenticatorTokenProvider, code);

        if (!isValid)
        {
            ModelState.AddModelError("Input.Code", "Verification code is invalid.");
            await LoadSharedKeyAndQrCodeUriAsync(user);
            return Page();
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);
        logger.LogInformation("User '{UserId}' enabled 2FA.", await userManager.GetUserIdAsync(user));
        StatusMessage = "Your authenticator app has been verified.";

        if (await userManager.CountRecoveryCodesAsync(user) == 0)
        {
            var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 8);
            RecoveryCodes = codes?.ToArray();
            return RedirectToPage("./ShowRecoveryCodes");
        }

        return RedirectToPage("./TwoFactorAuthentication");
    }

    private async Task LoadSharedKeyAndQrCodeUriAsync(ApplicationUser user)
    {
        var unformatted = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(unformatted))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            unformatted = await userManager.GetAuthenticatorKeyAsync(user);
        }
        SharedKey = FormatKey(unformatted!);
        var email = await userManager.GetEmailAsync(user);
        AuthenticatorUri = string.Format(AuthenticatorUriFormat,
            urlEncoder.Encode("Shulker Tech"),
            urlEncoder.Encode(email!),
            unformatted);
    }

    private static string FormatKey(string key)
    {
        var sb = new StringBuilder();
        int pos = 0;
        while (pos + 4 < key.Length)
        {
            sb.Append(key.AsSpan(pos, 4)).Append(' ');
            pos += 4;
        }
        if (pos < key.Length)
            sb.Append(key.AsSpan(pos));
        return sb.ToString().ToLowerInvariant();
    }
}
