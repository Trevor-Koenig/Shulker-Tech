namespace ShulkerTech.Core.Models;

/// <summary>All permission keys used by the RBAC system.</summary>
public static class SiteResource
{
    public const string WikiCreate  = "wiki.create";
    public const string WikiEditOwn = "wiki.edit_own";
    public const string WikiEditAny = "wiki.edit_any";
    public const string WikiDelete  = "wiki.delete";

    public static readonly ResourceInfo[] All =
    [
        new(WikiCreate,  "Wiki", "Create Articles",    "Can create new wiki articles"),
        new(WikiEditOwn, "Wiki", "Edit Own Articles",  "Can edit articles they authored"),
        new(WikiEditAny, "Wiki", "Edit Any Article",   "Can edit articles written by others"),
        new(WikiDelete,  "Wiki", "Delete Articles",    "Can delete any article"),
    ];
}

public record ResourceInfo(string Key, string Group, string DisplayName, string Description);
