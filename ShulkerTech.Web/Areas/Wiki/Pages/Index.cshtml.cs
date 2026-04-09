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
            .Where(a => a.IsPublished)
            .OrderByDescending(a => a.UpdatedAt)
            .ToListAsync();

        Articles = all.Where(a =>
        {
            var viewRole = a.ViewRole ?? settings.DefaultViewRole;
            return WikiSettings.UserSatisfies(viewRole, userRoles, currentUser?.IsAdmin == true);
        }).ToList();
    }

    public static string Excerpt(string markdown, int maxLength = 160)
    {
        var text = Regex.Replace(markdown ?? "", @"[#*_`\[\]()>~]|!\[.*?\]|\[.*?\]", "").Trim();
        text = Regex.Replace(text, @"\s+", " ");
        return text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "…";
    }
}
