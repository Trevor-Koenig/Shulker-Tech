using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace ShulkerTech.Web.Areas.Admin.Pages.Maps;

public class IndexModel(ApplicationDbContext db) : PageModel
{
    public List<MapServer> MapServers { get; set; } = [];

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        public string Label { get; set; } = string.Empty;

        [Required, Url]
        public string Url { get; set; } = string.Empty;
    }

    public async Task OnGetAsync()
    {
        MapServers = await db.MapServers.OrderBy(m => m.Label).ToListAsync();
    }

    public async Task<IActionResult> OnPostAddAsync()
    {
        if (!ModelState.IsValid)
        {
            MapServers = await db.MapServers.OrderBy(m => m.Label).ToListAsync();
            return Page();
        }

        db.MapServers.Add(new MapServer { Label = Input.Label, Url = Input.Url });
        await db.SaveChangesAsync();
        StatusMessage = $"Map server '{Input.Label}' added.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var map = await db.MapServers.FindAsync(id);
        if (map is not null)
        {
            map.IsActive = !map.IsActive;
            await db.SaveChangesAsync();
            StatusMessage = $"'{map.Label}' is now {(map.IsActive ? "active" : "inactive")}.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var map = await db.MapServers.FindAsync(id);
        if (map is not null)
        {
            db.MapServers.Remove(map);
            await db.SaveChangesAsync();
            StatusMessage = $"'{map.Label}' removed.";
        }
        return RedirectToPage();
    }
}
