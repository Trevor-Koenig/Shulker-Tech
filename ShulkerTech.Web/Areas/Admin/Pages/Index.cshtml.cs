using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Admin.Pages;

public class IndexModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public int TotalUsers { get; set; }
    public int ActiveInviteCodes { get; set; }
    public int TotalMapServers { get; set; }
    public int ActiveMapServers { get; set; }
    public int GameServers { get; set; }
    public int ActiveSessions { get; set; }

    public async Task OnGetAsync()
    {
        TotalUsers = await userManager.Users.CountAsync();

        var now = DateTime.UtcNow;
        ActiveInviteCodes = await db.InviteCodes.CountAsync(c =>
            !c.IsRevoked &&
            c.UseCount < c.MaxUses &&
            (c.ExpiresAt == null || c.ExpiresAt > now));

        TotalMapServers  = await db.MapServers.CountAsync();
        ActiveMapServers = await db.MapServers.CountAsync(m => m.IsActive);

        GameServers    = await db.MinecraftServers.CountAsync(s => s.IsActive);
        ActiveSessions = await db.PlayerSessions.CountAsync(s => s.LeftAt == null);
    }
}
