using Markdig;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Wiki.Pages.Articles;

public class ViewModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    MarkdownPipeline pipeline) : PageModel
{
    public Article Article { get; set; } = null!;
    public string ContentHtml { get; set; } = "";
    public bool CanEdit { get; set; }

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var article = await db.Articles
            .Include(a => a.Author)
            .FirstOrDefaultAsync(a => a.Slug == slug);

        if (article == null) return NotFound();

        // Unpublished articles are only visible to the author or admins
        if (!article.IsPublished)
        {
            var viewer = await userManager.GetUserAsync(User);
            if (viewer == null || (viewer.Id != article.AuthorId && !viewer.IsAdmin))
                return NotFound();
        }

        Article = article;
        ContentHtml = Markdown.ToHtml(article.Content, pipeline);

        if (User.Identity?.IsAuthenticated == true)
        {
            var currentUser = await userManager.GetUserAsync(User);
            CanEdit = currentUser != null &&
                      (currentUser.Id == article.AuthorId || currentUser.IsAdmin);
        }

        return Page();
    }
}
