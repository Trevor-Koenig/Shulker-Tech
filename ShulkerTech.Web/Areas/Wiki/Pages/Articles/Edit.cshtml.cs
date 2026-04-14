using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Wiki.Pages.Articles;

[Authorize]
public class EditModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public Article Article { get; set; } = null!;
    public bool IsAdmin { get; set; }
    public WikiSettings Settings { get; set; } = new();

    public class InputModel
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = "";

        [Required(ErrorMessage = "Article body cannot be empty.")]
        public string Content { get; set; } = "";

        public bool IsPublished { get; set; }
        public string? Category { get; set; }
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

        Input = new InputModel
        {
            Id = article.Id,
            Title = article.Title,
            Content = article.Content,
            IsPublished = article.IsPublished,
            Category = article.Category,
            ViewRole = article.ViewRole,
            EditRole = article.EditRole,
            MapUrl = article.MapUrl,
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Settings = await db.WikiSettings.FirstOrDefaultAsync() ?? new WikiSettings();

        var (article, canEdit, isAdmin) = await ResolveAsync(Input.Id);
        if (article == null || !canEdit) return Forbid();

        Article = article;
        IsAdmin = isAdmin;

        if (!ModelState.IsValid) return Page();

        article.Title = Input.Title;
        article.Content = Input.Content;
        article.IsPublished = Input.IsPublished;
        article.Category = string.IsNullOrWhiteSpace(Input.Category) ? null : Input.Category.Trim();
        article.ViewRole = string.IsNullOrEmpty(Input.ViewRole) ? null : Input.ViewRole;
        article.EditRole = string.IsNullOrEmpty(Input.EditRole) ? null : Input.EditRole;
        article.MapUrl = string.IsNullOrWhiteSpace(Input.MapUrl) ? null : Input.MapUrl.Trim();
        article.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Redirect($"/articles/{article.Slug}");
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null || !user.IsAdmin) return Forbid();

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
        var article = await db.Articles.FindAsync(id);
        if (article == null) return (null, false, false);

        var user = await userManager.GetUserAsync(User);
        if (user == null) return (article, false, false);

        var settings = await db.WikiSettings.FirstOrDefaultAsync() ?? new WikiSettings();
        var userRoles = await userManager.GetRolesAsync(user);
        var editRole = article.EditRole ?? settings.EditAnyRole;

        var canEdit = user.Id == article.AuthorId ||
                      WikiSettings.UserSatisfies(editRole, userRoles, user.IsAdmin);

        return (article, canEdit, user.IsAdmin);
    }
}
