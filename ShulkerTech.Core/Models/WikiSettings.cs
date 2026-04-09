namespace ShulkerTech.Core.Models;

/// <summary>Single-row settings table controlling wiki-wide permissions.</summary>
public class WikiSettings
{
    public int Id { get; set; } = 1;

    /// <summary>Default minimum role to view any article whose ViewRole is null. Null = public.</summary>
    public string? DefaultViewRole { get; set; } = null;

    /// <summary>Minimum role required to create new articles.</summary>
    public string CreateRole { get; set; } = "Member";

    /// <summary>Minimum role required to edit articles the user did not write.</summary>
    public string EditAnyRole { get; set; } = "Moderator";

    // ── Role hierarchy ────────────────────────────────────────────────────────

    /// <summary>Ordered role names from lowest to highest privilege.</summary>
    public static readonly string[] RoleHierarchy = ["Member", "Moderator", "Admin"];

    private static int RoleRank(string? role) =>
        role is null ? 0 : Array.IndexOf(RoleHierarchy, role) + 1;

    /// <summary>
    /// Returns true if the user satisfies <paramref name="requiredRole"/>.
    /// Admins always pass. Null requirement = public (always passes).
    /// </summary>
    public static bool UserSatisfies(string? requiredRole, IList<string> userRoles, bool isAdmin)
    {
        if (requiredRole is null) return true;
        if (isAdmin) return true;

        var required = RoleRank(requiredRole);
        var userMax = userRoles
            .Select(r => RoleRank(r))
            .DefaultIfEmpty(0)
            .Max();

        return userMax >= required;
    }
}
