using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;
using ShulkerTech.Core.Services;

namespace ShulkerTech.Web.Areas.Identity.Pages.Account.Manage;

public class IndexModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext db,
    MojangService mojang) : PageModel
{
    [TempData]
    public string? StatusMessage { get; set; }

    public string? Email { get; set; }
    public string? MinecraftUsername { get; set; }
    public string? MinecraftUuid { get; set; }

    [BindProperty]
    public MinecraftInput MinecraftForm { get; set; } = new();

    public class MinecraftInput
    {
        [Required(ErrorMessage = "Minecraft username is required.")]
        [StringLength(16, MinimumLength = 3)]
        public string MinecraftUsername { get; set; } = string.Empty;
    }

    private async Task LoadAsync(ApplicationUser user)
    {
        Email = await userManager.GetEmailAsync(user);
        MinecraftUsername = user.MinecraftUsername;
        MinecraftUuid = user.MinecraftUuid;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        await LoadAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostMinecraftAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        if (!ModelState.IsValid)
        {
            await LoadAsync(user);
            return Page();
        }

        var newUsername = MinecraftForm.MinecraftUsername.Trim();

        if (string.Equals(user.MinecraftUsername, newUsername, StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "No changes made.";
            return RedirectToPage();
        }

        var profile = await mojang.GetProfileAsync(newUsername);
        if (profile is null)
        {
            ModelState.AddModelError(nameof(MinecraftForm.MinecraftUsername),
                "Minecraft account not found. Check your username.");
            await LoadAsync(user);
            return Page();
        }

        var uuidTaken = await db.Users.AnyAsync(u =>
            u.MinecraftUuid == profile.Id && u.Id != user.Id);
        if (uuidTaken)
        {
            ModelState.AddModelError(nameof(MinecraftForm.MinecraftUsername),
                "That Minecraft account is already linked to another Shulker Tech account.");
            await LoadAsync(user);
            return Page();
        }

        user.MinecraftUsername = profile.Name;
        user.MinecraftUuid = profile.Id;
        await userManager.UpdateAsync(user);
        await signInManager.RefreshSignInAsync(user);

        StatusMessage = $"Minecraft account updated to {profile.Name}.";
        return RedirectToPage();
    }
}
