using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;
using ShulkerTech.Core.Services;

namespace ShulkerTech.Web.Areas.Identity.Pages.Account;

public class RegisterModel(
    UserManager<ApplicationUser> userManager,
    IUserStore<ApplicationUser> userStore,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext db,
    IMojangService mojang,
    IEmailSender emailSender) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }
    public IList<AuthenticationScheme> ExternalLogins { get; set; } = [];

    public class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Minecraft username is required.")]
        [StringLength(16, MinimumLength = 3)]
        public string MinecraftUsername { get; set; } = string.Empty;

        [Required(ErrorMessage = "An invite code is required.")]
        public string InviteCode { get; set; } = string.Empty;
    }

    public async Task OnGetAsync(string? returnUrl = null, string? code = null)
    {
        ReturnUrl = returnUrl;
        ExternalLogins = (await signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        if (!string.IsNullOrEmpty(code))
            Input.InviteCode = code.Trim().ToUpper();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ExternalLogins = (await signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        if (!ModelState.IsValid)
            return Page();

        // Validate invite code
        var invite = await db.InviteCodes
            .FirstOrDefaultAsync(c => c.Code == Input.InviteCode.Trim().ToUpper());

        if (invite is null || !invite.IsValid)
        {
            ModelState.AddModelError(nameof(Input.InviteCode), "Invalid or expired invite code.");
            return Page();
        }

        // Verify Minecraft username with Mojang API
        var profile = await mojang.GetProfileAsync(Input.MinecraftUsername.Trim());
        if (profile is null)
        {
            ModelState.AddModelError(nameof(Input.MinecraftUsername), "Minecraft account not found. Check your username.");
            return Page();
        }

        // Ensure Minecraft account isn't already linked
        var mcTaken = await db.Users.AnyAsync(u =>
            ((ApplicationUser)u).MinecraftUuid == profile.Id);
        if (mcTaken)
        {
            ModelState.AddModelError(nameof(Input.MinecraftUsername), "This Minecraft account is already linked to another user.");
            return Page();
        }

        // Create the user
        var user = new ApplicationUser
        {
            MinecraftUsername = profile.Name,
            MinecraftUuid = profile.Id,
        };

        var emailStore = (IUserEmailStore<ApplicationUser>)userStore;
        await userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
        await emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

        var result = await userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        // Consume the invite code
        invite.UseCount++;
        await db.SaveChangesAsync();

        // Send email confirmation
        var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = Url.Page(
            "/Account/ConfirmEmail",
            pageHandler: null,
            values: new { area = "Identity", userId = user.Id, code, returnUrl },
            protocol: Request.Scheme)!;

        await emailSender.SendEmailAsync(Input.Email, "Confirm your Shulker Tech account",
            $"Welcome to Shulker Tech! Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl });
    }
}
