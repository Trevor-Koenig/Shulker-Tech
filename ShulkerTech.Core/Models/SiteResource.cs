namespace ShulkerTech.Core.Models;

/// <summary>All permission keys used by the RBAC system.</summary>
public static class SiteResource
{
    // ── Pages (main site) ──────────────────────────────────────────────────
    public const string PageHome         = "page.home";
    public const string PageServerStatus = "page.server_status";
    public const string PagePrivacy      = "page.privacy";
    public const string PagePlayers      = "page.players";

    // ── Wiki ───────────────────────────────────────────────────────────────
    public const string WikiView    = "wiki.view";
    public const string WikiCreate  = "wiki.create";
    public const string WikiEditOwn = "wiki.edit_own";
    public const string WikiEditAny = "wiki.edit_any";
    public const string WikiDelete  = "wiki.delete";

    // ── Admin ──────────────────────────────────────────────────────────────
    public const string AdminAccess        = "admin.access";
    public const string AdminUsers         = "admin.users";
    public const string AdminRoles         = "admin.roles";
    public const string AdminSecurity      = "admin.security";
    public const string AdminSiteSettings  = "admin.site_settings";
    public const string AdminWikiSettings  = "admin.wiki_settings";
    public const string AdminWikiTags      = "admin.wiki_tags";
    public const string AdminWikiTemplates = "admin.wiki_templates";
    public const string AdminInvites       = "admin.invites";
    public const string AdminServers       = "admin.servers";
    public const string AdminMaps          = "admin.maps";
    public const string AdminAuditLog      = "admin.audit_log";
    public const string AdminDbExport      = "admin.db_export";

    public static readonly ResourceInfo[] All =
    [
        // Pages — public by default; admin can restrict by granting roles
        new(PageHome,         "Pages", "Home Page",        "Access the site home page",          IsPublicByDefault: true),
        new(PageServerStatus, "Pages", "Server Status",    "View the server status page",        IsPublicByDefault: true),
        new(PagePrivacy,      "Pages", "Privacy Policy",   "View the privacy policy page",       IsPublicByDefault: true),
        new(PagePlayers,      "Pages", "Player Profiles",  "View public player profile pages",   IsPublicByDefault: true),

        // Wiki — view is public by default; write actions require explicit grants
        new(WikiView,    "Wiki", "View Articles",     "View published wiki articles",       IsPublicByDefault: true),
        new(WikiCreate,  "Wiki", "Create Articles",   "Create new wiki articles",           IsPublicByDefault: false),
        new(WikiEditOwn, "Wiki", "Edit Own Articles", "Edit articles they authored",        IsPublicByDefault: false),
        new(WikiEditAny, "Wiki", "Edit Any Article",  "Edit articles written by others",    IsPublicByDefault: false),
        new(WikiDelete,  "Wiki", "Delete Articles",   "Delete any wiki article",            IsPublicByDefault: false),

        // Admin — never public; IsAdmin bypasses all checks
        new(AdminAccess,        "Admin", "Admin Dashboard",      "Access the admin dashboard",         IsPublicByDefault: false),
        new(AdminUsers,         "Admin", "User Management",      "View and edit user accounts",        IsPublicByDefault: false),
        new(AdminRoles,         "Admin", "Roles & Permissions",  "Manage roles and permission grants", IsPublicByDefault: false),
        new(AdminSecurity,      "Admin", "Security Settings",    "Configure 2FA requirements",         IsPublicByDefault: false),
        new(AdminSiteSettings,  "Admin", "Site Settings",        "Edit site-wide settings",            IsPublicByDefault: false),
        new(AdminWikiSettings,  "Admin", "Wiki Settings",        "Configure wiki behavior",            IsPublicByDefault: false),
        new(AdminWikiTags,      "Admin", "Wiki Tags",            "Create and manage wiki tags",        IsPublicByDefault: false),
        new(AdminWikiTemplates, "Admin", "Wiki Templates",       "Manage article templates",           IsPublicByDefault: false),
        new(AdminInvites,       "Admin", "Invite Codes",         "Create and manage invite codes",     IsPublicByDefault: false),
        new(AdminServers,       "Admin", "Server Management",    "Configure Minecraft servers",        IsPublicByDefault: false),
        new(AdminMaps,          "Admin", "Map Management",       "Manage BlueMap configurations",      IsPublicByDefault: false),
        new(AdminAuditLog,      "Admin", "Audit Log",            "View the admin audit log",            IsPublicByDefault: false),
        new(AdminDbExport,      "Admin", "Database Export",      "Download a full database export",     IsPublicByDefault: false),
    ];
}

public record ResourceInfo(
    string Key,
    string Group,
    string DisplayName,
    string Description,
    bool IsPublicByDefault = false);
