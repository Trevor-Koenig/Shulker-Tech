using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Services;

/// <summary>
/// Scoped service that checks RBAC grants. Loads all grants once per request.
/// Admins (IsAdmin == true) bypass every check.
/// </summary>
public class PermissionService(ApplicationDbContext db)
{
    private HashSet<(string role, string resource)>? _grants;

    private async Task<HashSet<(string, string)>> GetGrantsAsync()
    {
        if (_grants is not null) return _grants;
        var perms = await db.SitePermissions.ToListAsync();
        _grants = perms.Select(p => (p.RoleName, p.Resource)).ToHashSet();
        return _grants;
    }

    public async Task<bool> HasAsync(ApplicationUser? user, IList<string> roles, string resource)
    {
        if (user?.IsAdmin == true) return true;
        var grants = await GetGrantsAsync();
        return roles.Any(r => grants.Contains((r, resource)));
    }
}
