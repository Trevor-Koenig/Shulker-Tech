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
    public bool IsFavorited { get; set; }

    // Ratings
    public double AvgUsefulness { get; set; }
    public double AvgCoolness { get; set; }
    public int RatingCount { get; set; }
    public byte? UserUsefulness { get; set; }
    public byte? UserCoolness { get; set; }

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

        if (!article.IsPublished)
        {
            var canSeeUnpublished = viewer != null &&
                (viewer.Id == article.AuthorId ||
                 viewer.IsAdmin ||
                 WikiSettings.UserSatisfies(editRole, userRoles, viewer.IsAdmin));
            if (!canSeeUnpublished)
                return NotFound();
        }

        var viewRole = article.ViewRole ?? settings.DefaultViewRole;
        if (!WikiSettings.UserSatisfies(viewRole, userRoles, viewer?.IsAdmin == true))
            return NotFound();

        Article = article;
        ContentHtml = wikiMarkdown.ToHtml(article.Content);

        CanEdit = viewer != null &&
                  (viewer.Id == article.AuthorId ||
                   WikiSettings.UserSatisfies(editRole, userRoles, viewer.IsAdmin));

        if (viewer != null)
            IsFavorited = await db.ArticleFavorites
                .AnyAsync(f => f.UserId == viewer.Id && f.ArticleId == article.Id);

        // Load ratings
        var ratings = await db.ArticleRatings
            .Where(r => r.ArticleId == article.Id)
            .ToListAsync();

        RatingCount = ratings.Count;
        if (RatingCount > 0)
        {
            AvgUsefulness = ratings.Average(r => (double)r.Usefulness);
            AvgCoolness   = ratings.Average(r => (double)r.Coolness);
        }

        if (viewer != null)
        {
            var mine = ratings.FirstOrDefault(r => r.UserId == viewer.Id);
            UserUsefulness = mine?.Usefulness;
            UserCoolness   = mine?.Coolness;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostFavoriteAsync(string slug)
    {
        var viewer = await userManager.GetUserAsync(User);
        if (viewer == null) return Challenge();

        var article = await db.Articles.FirstOrDefaultAsync(a => a.Slug == slug);
        if (article == null) return NotFound();

        var existing = await db.ArticleFavorites
            .FirstOrDefaultAsync(f => f.UserId == viewer.Id && f.ArticleId == article.Id);

        if (existing != null)
            db.ArticleFavorites.Remove(existing);
        else
            db.ArticleFavorites.Add(new ArticleFavorite { UserId = viewer.Id, ArticleId = article.Id });

        await db.SaveChangesAsync();
        return Redirect($"/articles/{article.Slug}");
    }

    public async Task<IActionResult> OnPostRateAsync(string slug, byte usefulness, byte coolness)
    {
        if (usefulness < 1 || usefulness > 5 || coolness < 1 || coolness > 5)
            return BadRequest();

        var viewer = await userManager.GetUserAsync(User);
        if (viewer == null) return Challenge();

        var article = await db.Articles.FirstOrDefaultAsync(a => a.Slug == slug);
        if (article == null) return NotFound();

        var existing = await db.ArticleRatings
            .FirstOrDefaultAsync(r => r.UserId == viewer.Id && r.ArticleId == article.Id);

        if (existing != null)
        {
            existing.Usefulness = usefulness;
            existing.Coolness   = coolness;
            existing.UpdatedAt  = DateTime.UtcNow;
        }
        else
        {
            db.ArticleRatings.Add(new ArticleRating
            {
                UserId    = viewer.Id,
                ArticleId = article.Id,
                Usefulness = usefulness,
                Coolness   = coolness,
            });
        }

        await db.SaveChangesAsync();
        return Redirect($"/articles/{article.Slug}#rating-widget");
    }
}
