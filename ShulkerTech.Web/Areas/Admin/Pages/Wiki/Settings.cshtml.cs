using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Admin.Pages.Wiki;

public class SettingsModel(
    ApplicationDbContext db,
    RoleManager<IdentityRole> roleManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public IList<string> AllRoles { get; set; } = [];

    public class InputModel
    {
        public string? DefaultViewRole { get; set; }
        public string CreateRole { get; set; } = "Member";
        public string EditAnyRole { get; set; } = "Moderator";
    }

    public async Task OnGetAsync()
    {
        var settings = await db.WikiSettings.FirstOrDefaultAsync() ?? new WikiSettings();
        AllRoles = roleManager.Roles.Select(r => r.Name!).OrderBy(r => r).ToList();
        Input = new InputModel
        {
            DefaultViewRole = settings.DefaultViewRole,
            CreateRole = settings.CreateRole,
            EditAnyRole = settings.EditAnyRole,
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        AllRoles = roleManager.Roles.Select(r => r.Name!).OrderBy(r => r).ToList();
        if (!ModelState.IsValid) return Page();

        var settings = await db.WikiSettings.FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new WikiSettings { Id = 1 };
            db.WikiSettings.Add(settings);
        }

        settings.DefaultViewRole = string.IsNullOrEmpty(Input.DefaultViewRole) ? null : Input.DefaultViewRole;
        settings.CreateRole = Input.CreateRole;
        settings.EditAnyRole = Input.EditAnyRole;

        await db.SaveChangesAsync();
        StatusMessage = "Wiki settings saved.";
        return RedirectToPage();
    }
}
