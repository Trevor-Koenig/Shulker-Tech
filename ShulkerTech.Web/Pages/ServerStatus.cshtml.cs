using Microsoft.AspNetCore.Mvc.RazorPages;
using ShulkerTech.Web.Services;

namespace ShulkerTech.Web.Pages;

public class ServerStatusModel(ServerStatusCache cache, ServerStatsCache statsCache) : PageModel
{
    public IReadOnlyCollection<CachedServerStatus> Servers { get; private set; } = [];
    public IReadOnlyDictionary<int, CachedServerStats> Stats { get; private set; } =
        new Dictionary<int, CachedServerStats>();

    public void OnGet()
    {
        Servers = cache.All;
        Stats = Servers
            .Select(s => statsCache.Get(s.ServerId))
            .OfType<CachedServerStats>()
            .ToDictionary(s => s.ServerId);
    }
}
