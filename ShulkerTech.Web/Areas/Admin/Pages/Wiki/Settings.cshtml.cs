using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Admin.Pages.Wiki;

public class SettingsModel(ApplicationDbContext db) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        public string? DefaultViewRole { get; set; }
    }

    public async Task OnGetAsync()
    {
        var settings = await db.WikiSettings.FirstOrDefaultAsync() ?? new WikiSettings();
        Input = new InputModel
        {
            DefaultViewRole = settings.DefaultViewRole,
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var settings = await db.WikiSettings.FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new WikiSettings { Id = 1 };
            db.WikiSettings.Add(settings);
        }

        settings.DefaultViewRole = string.IsNullOrEmpty(Input.DefaultViewRole) ? null : Input.DefaultViewRole;

        await db.SaveChangesAsync();
        StatusMessage = "Wiki settings saved.";
        return RedirectToPage();
    }
}
