using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Admin.Pages.Site;

public class SettingsModel(ApplicationDbContext db) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        public string HeroTagline { get; set; } = string.Empty;

        public string BuildCardTitle { get; set; } = string.Empty;
        public string BuildCardBody { get; set; } = string.Empty;

        public string ExploreCardTitle { get; set; } = string.Empty;
        public string ExploreCardBody { get; set; } = string.Empty;

        public string ConnectCardTitle { get; set; } = string.Empty;
        public string ConnectCardBody { get; set; } = string.Empty;
    }

    public async Task OnGetAsync()
    {
        var settings = await db.SiteSettings.FirstOrDefaultAsync() ?? new SiteSettings();
        Input = new InputModel
        {
            HeroTagline = settings.HeroTagline,
            BuildCardTitle = settings.BuildCardTitle,
            BuildCardBody = settings.BuildCardBody,
            ExploreCardTitle = settings.ExploreCardTitle,
            ExploreCardBody = settings.ExploreCardBody,
            ConnectCardTitle = settings.ConnectCardTitle,
            ConnectCardBody = settings.ConnectCardBody,
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var settings = await db.SiteSettings.FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new SiteSettings { Id = 1 };
            db.SiteSettings.Add(settings);
        }

        settings.HeroTagline = Input.HeroTagline;
        settings.BuildCardTitle = Input.BuildCardTitle;
        settings.BuildCardBody = Input.BuildCardBody;
        settings.ExploreCardTitle = Input.ExploreCardTitle;
        settings.ExploreCardBody = Input.ExploreCardBody;
        settings.ConnectCardTitle = Input.ConnectCardTitle;
        settings.ConnectCardBody = Input.ConnectCardBody;

        await db.SaveChangesAsync();
        StatusMessage = "Site settings saved.";
        return RedirectToPage();
    }
}
