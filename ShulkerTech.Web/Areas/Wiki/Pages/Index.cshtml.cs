using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Wiki.Pages;

public class IndexModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public List<Article> Articles { get; set; } = [];
    public List<TagGroup> Tags { get; set; } = [];
    public int PublishedCount { get; set; }
    public int DraftCount { get; set; }
    public int ContributorCount { get; set; }
    public DateTime? LastUpdated { get; set; }

    public record TagGroup(int Id, string Name, string Slug, string Icon, string Color, int ArticleCount);

    /// <summary>Strips Markdown syntax and collapses whitespace for use in search data attributes.</summary>
    public static string StripMarkdown(string markdown)
    {
        var text = Regex.Replace(markdown ?? "", @"!\[.*?\]\(.*?\)", "");  // images
        text = Regex.Replace(text, @"\[([^\]]*)\]\([^)]*\)", "$1");        // links → label
        text = Regex.Replace(text, @"```[\s\S]*?```", "");                 // fenced code
        text = Regex.Replace(text, @"`[^`]*`", "");                        // inline code
        text = Regex.Replace(text, @"#{1,6}\s*", "");                      // headings
        text = Regex.Replace(text, @"[*_~>|\\]", "");                      // emphasis / misc
        text = Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    public async Task OnGetAsync()
    {
        var settings = await db.WikiSettings.FirstOrDefaultAsync() ?? new WikiSettings();

        ApplicationUser? currentUser = null;
        IList<string> userRoles = [];

        if (User.Identity?.IsAuthenticated == true)
        {
            currentUser = await userManager.GetUserAsync(User);
            if (currentUser != null)
                userRoles = await userManager.GetRolesAsync(currentUser);
        }

        var all = await db.Articles
            .Include(a => a.Author)
            .Include(a => a.Tags)
            .OrderByDescending(a => a.UpdatedAt)
            .ToListAsync();

        var isAdmin = currentUser?.IsAdmin == true;

        Articles = all.Where(a =>
        {
            if (!a.IsPublished)
            {
                if (isAdmin || (currentUser != null && currentUser.Id == a.AuthorId))
                    return true;
                var editRole = a.EditRole ?? settings.EditAnyRole;
                return currentUser != null && WikiSettings.UserSatisfies(editRole, userRoles, isAdmin);
            }

            var viewRole = a.ViewRole ?? settings.DefaultViewRole;
            return WikiSettings.UserSatisfies(viewRole, userRoles, isAdmin);
        }).ToList();

        var published = Articles.Where(a => a.IsPublished).ToList();

        PublishedCount   = published.Count;
        DraftCount       = Articles.Count(a => !a.IsPublished);
        ContributorCount = published.Select(a => a.AuthorId).Distinct().Count();
        LastUpdated      = published.Count > 0 ? published.Max(a => a.UpdatedAt) : null;

        Tags = Articles
            .Where(a => a.IsPublished)
            .SelectMany(a => a.Tags)
            .GroupBy(t => t.Id)
            .Select(g =>
            {
                var tag = g.First();
                return new TagGroup(tag.Id, tag.Name, tag.Slug, tag.Icon, tag.Color, g.Count());
            })
            .OrderBy(t => t.Name)
            .ToList();
    }
}
