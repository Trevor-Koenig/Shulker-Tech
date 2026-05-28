using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Admin.Pages.Security;

public class SettingsModel(
    ApplicationDbContext db,
    RoleManager<IdentityRole> roleManager) : PageModel
{
    /// <summary>All roles that exist in the system, ordered alphabetically.</summary>
    public List<string> AllRoles { get; set; } = [];

    /// <summary>Role names currently required to have 2FA enabled.</summary>
    [BindProperty]
    public List<string> RequiredTwoFactorRoles { get; set; } = [];

    /// <summary>Role treated as "guest" for unauthenticated permission checks. Empty string means none.</summary>
    [BindProperty]
    public string GuestRole { get; set; } = "";

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        AllRoles = await roleManager.Roles
            .Select(r => r.Name!)
            .OrderBy(n => n)
            .ToListAsync();

        var settings = await db.SecuritySettings.FirstOrDefaultAsync() ?? new SecuritySettings();
        RequiredTwoFactorRoles = [.. settings.GetRequiredRoles()];
        GuestRole = settings.GuestRole ?? "";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var validRoles = await roleManager.Roles.Select(r => r.Name!).ToListAsync();

        var selected = (RequiredTwoFactorRoles ?? [])
            .Where(r => validRoles.Contains(r, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(r => r)
            .ToList();

        var guestRole = validRoles.FirstOrDefault(r =>
            r.Equals(GuestRole, StringComparison.OrdinalIgnoreCase));

        var settings = await db.SecuritySettings.FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new SecuritySettings { Id = 1 };
            db.SecuritySettings.Add(settings);
        }

        settings.RequireTwoFactorRoles = string.Join(",", selected);
        settings.GuestRole = guestRole;
        await db.SaveChangesAsync();

        StatusMessage = "Security settings saved.";
        return RedirectToPage();
    }
}
