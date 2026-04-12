using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Admin.Pages.Security;

public class SettingsModel(ApplicationDbContext db) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        public bool RequireAdminTwoFactor { get; set; }
        public bool RequireModeratorTwoFactor { get; set; }
        public bool RequireMemberTwoFactor { get; set; }
    }

    public async Task OnGetAsync()
    {
        var settings = await db.SecuritySettings.FirstOrDefaultAsync() ?? new SecuritySettings();
        var roles = settings.GetRequiredRoles();
        Input = new InputModel
        {
            RequireAdminTwoFactor = roles.Contains("Admin"),
            RequireModeratorTwoFactor = roles.Contains("Moderator"),
            RequireMemberTwoFactor = roles.Contains("Member"),
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var required = new List<string>();
        if (Input.RequireAdminTwoFactor) required.Add("Admin");
        if (Input.RequireModeratorTwoFactor) required.Add("Moderator");
        if (Input.RequireMemberTwoFactor) required.Add("Member");

        var settings = await db.SecuritySettings.FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new SecuritySettings { Id = 1 };
            db.SecuritySettings.Add(settings);
        }

        settings.RequireTwoFactorRoles = string.Join(",", required);

        await db.SaveChangesAsync();
        StatusMessage = "Security settings saved.";
        return RedirectToPage();
    }
}
