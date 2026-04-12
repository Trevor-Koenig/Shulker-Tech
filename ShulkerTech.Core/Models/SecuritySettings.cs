namespace ShulkerTech.Core.Models;

/// <summary>Single-row settings table for site-wide security policy.</summary>
public class SecuritySettings
{
    public int Id { get; set; } = 1;

    /// <summary>Comma-separated role names that must have 2FA enabled before accessing the site.</summary>
    public string RequireTwoFactorRoles { get; set; } = string.Empty;

    public IReadOnlySet<string> GetRequiredRoles() =>
        RequireTwoFactorRoles
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
