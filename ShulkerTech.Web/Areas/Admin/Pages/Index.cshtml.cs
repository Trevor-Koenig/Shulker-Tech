using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;
using ShulkerTech.Web.Services;

namespace ShulkerTech.Web.Areas.Admin.Pages;

public class IndexModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    PermissionService permissions) : PageModel
{
    public int TotalUsers { get; set; }
    public int ActiveInviteCodes { get; set; }
    public int TotalMapServers { get; set; }
    public int ActiveMapServers { get; set; }
    public int GameServers { get; set; }
    public int ActiveSessions { get; set; }

    public HashSet<string> AccessibleResources { get; private set; } = [];

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

        var user  = await userManager.GetUserAsync(User);
        var roles = user is not null ? await userManager.GetRolesAsync(user) : [];

        string[] adminResources =
        [
            SiteResource.AdminUsers,         SiteResource.AdminRoles,
            SiteResource.AdminInvites,       SiteResource.AdminServers,
            SiteResource.AdminMaps,          SiteResource.AdminWikiSettings,
            SiteResource.AdminWikiTags,      SiteResource.AdminWikiTemplates,
            SiteResource.AdminSiteSettings,  SiteResource.AdminSecurity,
            SiteResource.AdminDbExport,      SiteResource.AdminAuditLog,
        ];

        foreach (var resource in adminResources)
            if (await permissions.CanAccessAsync(user, roles, resource, isPublicByDefault: false))
                AccessibleResources.Add(resource);
    }
}
