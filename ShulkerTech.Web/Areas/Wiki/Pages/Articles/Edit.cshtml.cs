using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;
using ShulkerTech.Web.Services;

namespace ShulkerTech.Web.Areas.Wiki.Pages.Articles;

[Authorize]
public class EditModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    PermissionService permissions) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public Article Article { get; set; } = null!;
    public bool IsAdmin { get; set; }
    public WikiSettings Settings { get; set; } = new();
    public List<Tag> AllTags { get; set; } = [];

    public class InputModel
    {
        public int Id { get; set; }

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

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var (article, canEdit, isAdmin) = await ResolveAsync(id);
        if (article == null || !canEdit) return Forbid();

        Article = article;
        IsAdmin = isAdmin;
        Settings = await db.WikiSettings.FirstOrDefaultAsync() ?? new WikiSettings();
        AllTags = await db.Tags.OrderBy(t => t.Name).ToListAsync();

        Input = new InputModel
        {
            Id = article.Id,
            Title = article.Title,
            Content = article.Content,
            IsPublished = article.IsPublished,
            TagIds = string.Join(",", article.Tags.Select(t => t.Id)),
            ViewRole = article.ViewRole,
            EditRole = article.EditRole,
            MapUrl = article.MapUrl,
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Settings = await db.WikiSettings.FirstOrDefaultAsync() ?? new WikiSettings();
        AllTags = await db.Tags.OrderBy(t => t.Name).ToListAsync();

        var (article, canEdit, isAdmin) = await ResolveAsync(Input.Id);
        if (article == null || !canEdit) return Forbid();

        Article = article;
        IsAdmin = isAdmin;

        if (!ModelState.IsValid) return Page();

        var user = await userManager.GetUserAsync(User)!;
        db.ArticleRevisions.Add(new ArticleRevision
        {
            ArticleId = article.Id,
            Title     = article.Title,
            Content   = article.Content,
            MapUrl    = article.MapUrl,
            EditorId  = user!.Id,
            EditedAt  = DateTime.UtcNow,
        });

        article.Title = Input.Title;
        article.Content = Input.Content;
        article.IsPublished = Input.IsPublished;
        article.ViewRole = string.IsNullOrEmpty(Input.ViewRole) ? null : Input.ViewRole;
        article.EditRole = string.IsNullOrEmpty(Input.EditRole) ? null : Input.EditRole;
        article.MapUrl = string.IsNullOrWhiteSpace(Input.MapUrl) ? null : Input.MapUrl.Trim();
        article.UpdatedAt = DateTime.UtcNow;

        article.Tags.Clear();
        var selectedIds = CreateModel.ParseTagIds(Input.TagIds);
        if (selectedIds.Count > 0)
        {
            var tags = await db.Tags.Where(t => selectedIds.Contains(t.Id)).ToListAsync();
            foreach (var tag in tags)
                article.Tags.Add(tag);
        }

        await db.SaveChangesAsync();
        return Redirect($"/articles/{article.Slug}");
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Forbid();

        var userRoles = await userManager.GetRolesAsync(user);
        if (!await permissions.HasAsync(user, userRoles, SiteResource.WikiDelete))
            return Forbid();

        var article = await db.Articles.FindAsync(id);
        if (article != null)
        {
            db.Articles.Remove(article);
            await db.SaveChangesAsync();
        }
        return Redirect("/");
    }

    private async Task<(Article? article, bool canEdit, bool isAdmin)> ResolveAsync(int id)
    {
        var article = await db.Articles
            .Include(a => a.Tags)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (article == null) return (null, false, false);

        var user = await userManager.GetUserAsync(User);
        if (user == null) return (article, false, false);

        var userRoles = await userManager.GetRolesAsync(user);

        bool canEdit;
        if (article.EditRole != null)
        {
            // Per-article override uses the rank-based hierarchy
            canEdit = WikiSettings.UserSatisfies(article.EditRole, userRoles, user.IsAdmin);
        }
        else
        {
            // Global: author + edit_own permission, or edit_any permission
            var isAuthor = user.Id == article.AuthorId;
            var editOwn = isAuthor && await permissions.HasAsync(user, userRoles, SiteResource.WikiEditOwn);
            var editAny = await permissions.HasAsync(user, userRoles, SiteResource.WikiEditAny);
            canEdit = editOwn || editAny;
        }

        return (article, canEdit, user.IsAdmin);
    }
}
