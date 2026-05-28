using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Services;

/// <summary>
/// Scoped service that checks RBAC grants. Loads all grants once per request.
/// Access is determined solely by role grants — there is no bypass flag.
/// </summary>
public class PermissionService(ApplicationDbContext db)
{
    private HashSet<(string Role, string Resource)>? _grants;
    private string? _guestRole;
    private bool _guestRoleLoaded;

    private async Task<HashSet<(string Role, string Resource)>> GetGrantsAsync()
    {
        if (_grants is not null) return _grants;
        var perms = await db.SitePermissions.ToListAsync();
        _grants = perms.Select(p => (p.RoleName, p.Resource)).ToHashSet();
        return _grants;
    }

    private async Task<string?> GetGuestRoleAsync()
    {
        if (_guestRoleLoaded) return _guestRole;
        var settings = await db.SecuritySettings.AsNoTracking().FirstOrDefaultAsync();
        _guestRole = settings?.GuestRole;
        _guestRoleLoaded = true;
        return _guestRole;
    }

    /// <summary>Returns true if any of the user's roles have an explicit grant for this resource.</summary>
    public async Task<bool> HasAsync(ApplicationUser? user, IList<string> roles, string resource)
    {
        if (user == null) return false;
        var grants = await GetGrantsAsync();
        return roles.Any(r => grants.Contains((r, resource)));
    }

    /// <summary>
    /// Returns true if the user can access a resource, honoring the public-by-default rule.
    /// <para>
    /// • No grants configured for this resource AND isPublicByDefault → true (resource is public).<br/>
    /// • The configured guest role is checked for unauthenticated users.<br/>
    /// • Otherwise the user must be authenticated and hold a role that has the grant.
    /// </para>
    /// </summary>
    public async Task<bool> CanAccessAsync(
        ApplicationUser? user,
        IList<string> roles,
        string resource,
        bool isPublicByDefault)
    {
        var grants = await GetGrantsAsync();

        // No grants configured → fall back to the resource's default visibility
        if (!grants.Any(g => g.Resource == resource))
            return isPublicByDefault;

        // Check guest role for unauthenticated users
        if (user == null)
        {
            var guestRole = await GetGuestRoleAsync();
            return guestRole != null && grants.Contains((guestRole, resource));
        }

        return roles.Any(r => grants.Contains((r, resource)));
    }
}
