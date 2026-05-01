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
public class HistoryModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    PermissionService permissions) : PageModel
{
    public Article Article { get; set; } = null!;
    public List<ArticleRevision> Revisions { get; set; } = [];
    public bool CanEdit { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var article = await db.Articles.FindAsync(id);
        if (article == null) return NotFound();

        var user = await userManager.GetUserAsync(User);
        if (user == null) return Forbid();

        var userRoles = await userManager.GetRolesAsync(user);

        if (article.EditRole != null)
            CanEdit = WikiSettings.UserSatisfies(article.EditRole, userRoles, user.IsAdmin);
        else
        {
            var isAuthor = user.Id == article.AuthorId;
            var editOwn = isAuthor && await permissions.HasAsync(user, userRoles, SiteResource.WikiEditOwn);
            var editAny = await permissions.HasAsync(user, userRoles, SiteResource.WikiEditAny);
            CanEdit = editOwn || editAny;
        }

        if (!CanEdit) return Forbid();

        Article = article;
        Revisions = await db.ArticleRevisions
            .Where(r => r.ArticleId == id)
            .Include(r => r.Editor)
            .OrderByDescending(r => r.EditedAt)
            .ToListAsync();

        return Page();
    }
}
