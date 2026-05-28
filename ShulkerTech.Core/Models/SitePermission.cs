namespace ShulkerTech.Core.Models;

public class SitePermission
{
    public int Id { get; set; }

    /// <summary>Identity role name this grant applies to (e.g. "Member").</summary>
    public string RoleName { get; set; } = "";

    /// <summary>Resource key from <see cref="SiteResource"/> (e.g. "wiki.create").</summary>
    public string Resource { get; set; } = "";
}
