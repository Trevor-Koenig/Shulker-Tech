using Microsoft.AspNetCore.Mvc.RazorPages;
using ShulkerTech.Web.Services;

namespace ShulkerTech.Web.Pages;

public class ServerStatusModel(ServerStatusCache cache) : PageModel
{
    public IReadOnlyCollection<CachedServerStatus> Servers { get; private set; } = [];

    public void OnGet()
    {
        Servers = cache.All;
    }
}
