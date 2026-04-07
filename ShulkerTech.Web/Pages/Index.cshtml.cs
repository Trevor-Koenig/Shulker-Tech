using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;

namespace ShulkerTech.Web.Pages;

public class IndexModel(ApplicationDbContext db) : PageModel
{
    public string? MapUrl { get; set; }

    public async Task OnGetAsync()
    {
        var active = await db.MapServers
            .Where(m => m.IsActive)
            .Select(m => m.Url)
            .ToListAsync();

        if (active.Count > 0)
            MapUrl = active[Random.Shared.Next(active.Count)];
    }
}
