using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Community.Pages.Players;

public class IndexModel(ApplicationDbContext db) : PageModel
{
    public ApplicationUser ProfileUser { get; set; } = null!;
    public List<Article> Articles { get; set; } = [];
    public long TotalPlaytimeSeconds { get; set; }
    public string? AvatarUrl { get; set; }

    public async Task<IActionResult> OnGetAsync(string username)
    {
        var uname = username.ToLower();
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.MinecraftUsername != null
                && u.MinecraftUsername.ToLower() == uname);

        if (user == null) return NotFound();

        ProfileUser = user;

        Articles = await db.Articles
            .Include(a => a.Tags)
            .Where(a => a.AuthorId == user.Id && a.IsPublished)
            .OrderByDescending(a => a.UpdatedAt)
            .ToListAsync();

        TotalPlaytimeSeconds = await db.PlayerSessions
            .Where(ps => ps.UserId == user.Id && ps.DurationSeconds.HasValue)
            .SumAsync(ps => (long)ps.DurationSeconds!);

        if (!string.IsNullOrEmpty(user.MinecraftUuid))
            AvatarUrl = $"https://api.mineatar.io/face/{user.MinecraftUuid}?scale=12";

        return Page();
    }

    public static string FormatPlaytime(long seconds)
    {
        if (seconds <= 0) return "No playtime recorded";
        var hours   = seconds / 3600;
        var minutes = (seconds % 3600) / 60;
        if (hours > 0)
            return $"{hours}h {minutes}m";
        return $"{minutes}m";
    }
}
