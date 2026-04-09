using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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

    public class InputModel
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = "";

        [Required(ErrorMessage = "Article body cannot be empty.")]
        public string Content { get; set; } = "";

        public bool IsPublished { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var (article, canEdit, isAdmin) = await ResolveAsync(id);
        if (article == null || !canEdit) return Forbid();

        Article = article;
        IsAdmin = isAdmin;
        Input = new InputModel
        {
            Id = article.Id,
            Title = article.Title,
            Content = article.Content,
            IsPublished = article.IsPublished,
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var (article, canEdit, _) = await ResolveAsync(Input.Id);
        if (article == null || !canEdit) return Forbid();

        article.Title = Input.Title;
        article.Content = Input.Content;
        article.IsPublished = Input.IsPublished;
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

        var isAdmin = user.IsAdmin;
        return (article, user.Id == article.AuthorId || isAdmin, isAdmin);
    }
}
