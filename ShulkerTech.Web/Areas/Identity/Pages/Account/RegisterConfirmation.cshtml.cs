using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterConfirmationModel(UserManager<ApplicationUser> userManager) : PageModel
{
    public string? Email { get; set; }

    public async Task<IActionResult> OnGetAsync(string? email)
    {
        if (email == null)
            return RedirectToPage("/Index");

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
            return NotFound();

        Email = email;
        return Page();
    }
}
