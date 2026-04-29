using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;
using ShulkerTech.Web.Markdown;

namespace ShulkerTech.Web.Areas.Wiki.Pages.Articles;

public class ViewModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    WikiMarkdownService wikiMarkdown) : PageModel
{
    public Article Article { get; set; } = null!;
    public string ContentHtml { get; set; } = "";
    public bool CanEdit { get; set; }

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var article = await db.Articles
            .Include(a => a.Author)
            .Include(a => a.Tags)
            .FirstOrDefaultAsync(a => a.Slug == slug);

        if (article == null) return NotFound();

        var settings = await db.WikiSettings.FirstOrDefaultAsync() ?? new WikiSettings();
        ApplicationUser? viewer = null;
        IList<string> userRoles = [];

        if (User.Identity?.IsAuthenticated == true)
        {
            viewer = await userManager.GetUserAsync(User);
            if (viewer != null)
                userRoles = await userManager.GetRolesAsync(viewer);
        }

        var editRole = article.EditRole ?? settings.EditAnyRole;

        // Unpublished: author, admin, or anyone who satisfies the edit role
        if (!article.IsPublished)
        {
            var canSeeUnpublished = viewer != null &&
                (viewer.Id == article.AuthorId ||
                 viewer.IsAdmin ||
                 WikiSettings.UserSatisfies(editRole, userRoles, viewer.IsAdmin));
            if (!canSeeUnpublished)
                return NotFound();
        }

        // ViewRole check
        var viewRole = article.ViewRole ?? settings.DefaultViewRole;
        if (!WikiSettings.UserSatisfies(viewRole, userRoles, viewer?.IsAdmin == true))
            return NotFound();

        Article = article;
        ContentHtml = wikiMarkdown.ToHtml(article.Content);

        // CanEdit: author, or satisfies article EditRole / global EditAnyRole
        CanEdit = viewer != null &&
                  (viewer.Id == article.AuthorId ||
                   WikiSettings.UserSatisfies(editRole, userRoles, viewer.IsAdmin));

        return Page();
    }
}
