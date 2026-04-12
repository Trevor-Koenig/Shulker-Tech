using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Pages;

public class IndexModel(ApplicationDbContext db) : PageModel
{
    public string? MapUrl { get; set; }
    public SiteSettings Site { get; set; } = new();

    public async Task OnGetAsync()
    {
        var active = await db.MapServers
            .Where(m => m.IsActive)
            .Select(m => m.Url)
            .ToListAsync();

        if (active.Count > 0)
            MapUrl = active[Random.Shared.Next(active.Count)];

        Site = await db.SiteSettings.FirstOrDefaultAsync() ?? new SiteSettings();
    }
}
