using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Wiki.Pages.Articles;

[Authorize]
public class HistoryModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : PageModel
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

        var settings = await db.WikiSettings.FirstOrDefaultAsync() ?? new WikiSettings();
        var userRoles = await userManager.GetRolesAsync(user);
        var editRole = article.EditRole ?? settings.EditAnyRole;
        CanEdit = user.Id == article.AuthorId ||
                  WikiSettings.UserSatisfies(editRole, userRoles, user.IsAdmin);

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
