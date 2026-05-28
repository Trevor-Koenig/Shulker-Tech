using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;
using ShulkerTech.Web.Services;

namespace ShulkerTech.Web.Areas.Wiki.Pages.Articles;

public class CreateModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    PermissionService permissions,
    AuditLogService auditLog) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public WikiSettings Settings { get; set; } = new();
    public List<Tag> AllTags { get; set; } = [];
    public List<ArticleTemplate> Templates { get; set; } = [];

    public class InputModel
    {
        [Required, MaxLength(200)]
        public string Title { get; set; } = "";

        [Required(ErrorMessage = "Article body cannot be empty.")]
        public string Content { get; set; } = "";

        public bool IsPublished { get; set; }
        public string TagIds { get; set; } = "";
        public string? ViewRole { get; set; }
        public string? EditRole { get; set; }

        [Url(ErrorMessage = "Must be a valid URL.")]
        [MaxLength(2000)]
        public string? MapUrl { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        Settings = await db.WikiSettings.FirstOrDefaultAsync() ?? new WikiSettings();
        AllTags = await db.Tags.OrderBy(t => t.Name).ToListAsync();
        Templates = await db.ArticleTemplates.OrderByDescending(t => t.IsDefault).ThenBy(t => t.Name).ToListAsync();

        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var userRoles = await userManager.GetRolesAsync(user);
        if (!await permissions.HasAsync(user, userRoles, SiteResource.WikiCreate))
            return Forbid();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Settings = await db.WikiSettings.FirstOrDefaultAsync() ?? new WikiSettings();
        AllTags = await db.Tags.OrderBy(t => t.Name).ToListAsync();
        Templates = await db.ArticleTemplates.OrderByDescending(t => t.IsDefault).ThenBy(t => t.Name).ToListAsync();

        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var userRoles = await userManager.GetRolesAsync(user);
        if (!await permissions.HasAsync(user, userRoles, SiteResource.WikiCreate))
            return Forbid();

        if (!ModelState.IsValid) return Page();

        var slug = await SlugHelper.GenerateUniqueSlugAsync(Input.Title, s => db.Articles.AnyAsync(a => a.Slug == s));

        var article = new Article
        {
            Title = Input.Title,
            Slug = slug,
            Content = Input.Content,
            IsPublished = Input.IsPublished,
            ViewRole = string.IsNullOrEmpty(Input.ViewRole) ? null : Input.ViewRole,
            EditRole = string.IsNullOrEmpty(Input.EditRole) ? null : Input.EditRole,
            MapUrl = string.IsNullOrWhiteSpace(Input.MapUrl) ? null : Input.MapUrl.Trim(),
            AuthorId = user.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var selectedIds = ParseTagIds(Input.TagIds);
        if (selectedIds.Count > 0)
        {
            var tags = await db.Tags.Where(t => selectedIds.Contains(t.Id)).ToListAsync();
            foreach (var tag in tags)
                article.Tags.Add(tag);
        }

        db.Articles.Add(article);
        await db.SaveChangesAsync();

        auditLog.Log(AuditAction.ArticleCreated, user.Id, article.Id, article.Title);
        await db.SaveChangesAsync();

        return Redirect($"/articles/{article.Slug}");
    }

    internal static List<int> ParseTagIds(string tagIds) =>
        (tagIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();
}
