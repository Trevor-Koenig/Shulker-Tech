using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Pages.Setup;

public class IndexModel(
    UserManager<ApplicationUser> userManager,
    IUserStore<ApplicationUser> userStore,
    SignInManager<ApplicationUser> signInManager,
    IConfiguration configuration) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        public string SetupCode { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        if (userManager.Users.Any())
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {

        if (!ModelState.IsValid)
            return Page();

        var expectedCode = configuration["SETUP_CODE"];
        if (string.IsNullOrEmpty(expectedCode) || Input.SetupCode != expectedCode)
        {
            ErrorMessage = "Invalid setup code.";
            return Page();
        }

        var user = new ApplicationUser { IsAdmin = true };
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

        await userManager.AddToRoleAsync(user, "Admin");

        // Confirm the email directly — the setup code already proves the admin owns
        // this deployment, so no email round-trip is needed.
        var confirmToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await userManager.ConfirmEmailAsync(user, confirmToken);

        await signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToPage("/Index");
    }
}
