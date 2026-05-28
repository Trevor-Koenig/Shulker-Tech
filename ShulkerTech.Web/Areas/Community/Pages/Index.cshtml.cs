using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Community.Pages;

public class IndexModel(ApplicationDbContext db) : PageModel
{
    // ── Module registry ────────────────────────────────────────────────────
    // Each module has a stable string ID. The EnabledModules set controls
    // what renders. Currently all are on; a future admin toggle page will
    // persist this set to a CommunitySettings DB row.
    public static class Modules
    {
        public const string StatTiles           = "stat-tiles";
        public const string WhoIsOnline         = "who-is-online";
        public const string PlaytimeLeaderboard = "playtime-leaderboard";
        public const string MemberRoster        = "member-roster";

        public static IReadOnlyList<string> All =>
            [StatTiles, WhoIsOnline, PlaytimeLeaderboard, MemberRoster];
    }

    // Future: load from DB settings row instead of hardcoding All
    public HashSet<string> EnabledModules { get; } = new(Modules.All);
    public bool IsModuleEnabled(string moduleId) => EnabledModules.Contains(moduleId);

    // ── Stat tiles ─────────────────────────────────────────────────────────
    public int TotalMembers { get; set; }
    public int CurrentlyOnline { get; set; }
    public long TotalPlaytimeAllSeconds { get; set; }
    public int TotalArticles { get; set; }

    // ── Who's online ───────────────────────────────────────────────────────
    public List<ApplicationUser> OnlinePlayers { get; set; } = [];

    // ── Playtime leaderboard ───────────────────────────────────────────────
    public record PlaytimeStat(string UserId, string? MinecraftUsername, string? MinecraftUuid, long TotalSeconds);
    public List<PlaytimeStat> PlaytimeLeaderboard { get; set; } = [];

    // ── Member roster ──────────────────────────────────────────────────────
    public List<ApplicationUser> Members { get; set; } = [];

    public async Task OnGetAsync()
    {
        // Stats needed for tiles regardless of module toggles
        TotalMembers  = await db.Users.CountAsync();
        TotalArticles = await db.Articles.CountAsync(a => a.IsPublished);

        var onlineUserIds = await db.PlayerSessions
            .Where(ps => ps.LeftAt == null)
            .Select(ps => ps.UserId)
            .Distinct()
            .ToListAsync();
        CurrentlyOnline = onlineUserIds.Count;

        TotalPlaytimeAllSeconds = await db.PlayerSessions
            .Where(ps => ps.DurationSeconds.HasValue)
            .SumAsync(ps => ps.DurationSeconds ?? 0);

        if (IsModuleEnabled(Modules.WhoIsOnline) && onlineUserIds.Count > 0)
        {
            OnlinePlayers = await db.Users
                .Where(u => onlineUserIds.Contains(u.Id))
                .ToListAsync();
        }

        if (IsModuleEnabled(Modules.PlaytimeLeaderboard))
        {
            var ranked = await db.PlayerSessions
                .Where(ps => ps.DurationSeconds.HasValue)
                .GroupBy(ps => ps.UserId)
                .Select(g => new { UserId = g.Key, Total = g.Sum(ps => ps.DurationSeconds ?? 0) })
                .OrderByDescending(g => g.Total)
                .Take(10)
                .ToListAsync();

            if (ranked.Count > 0)
            {
                var ids  = ranked.Select(x => x.UserId).ToList();
                var users = await db.Users
                    .Where(u => ids.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id);

                PlaytimeLeaderboard = ranked
                    .Where(x => users.ContainsKey(x.UserId))
                    .Select(x => new PlaytimeStat(
                        x.UserId,
                        users[x.UserId].MinecraftUsername,
                        users[x.UserId].MinecraftUuid,
                        x.Total))
                    .ToList();
            }
        }

        if (IsModuleEnabled(Modules.MemberRoster))
        {
            Members = await db.Users
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();
        }
    }

    public static string FormatPlaytime(long seconds)
    {
        if (seconds <= 0) return "0m";
        var hours   = seconds / 3600;
        var minutes = (seconds % 3600) / 60;
        if (hours > 0) return $"{hours}h {minutes}m";
        return $"{minutes}m";
    }

    public static string AvatarUrl(string? uuid) =>
        string.IsNullOrEmpty(uuid)
            ? ""
            : $"https://api.mineatar.io/face/{uuid}?scale=4";
}
