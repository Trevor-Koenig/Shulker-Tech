using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Wiki.Pages;

public class IndexModel(ApplicationDbContext db) : PageModel
{
    public List<Article> Articles { get; set; } = [];

    public async Task OnGetAsync()
    {
        Articles = await db.Articles
            .Include(a => a.Author)
            .Where(a => a.IsPublished)
            .OrderByDescending(a => a.UpdatedAt)
            .ToListAsync();
    }

    public static string Excerpt(string markdown, int maxLength = 160)
    {
        // Strip Markdown syntax for a clean plain-text preview
        var text = Regex.Replace(markdown ?? "", @"[#*_`\[\]()>~]|!\[.*?\]|\[.*?\]", "").Trim();
        text = Regex.Replace(text, @"\s+", " ");
        return text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "…";
    }
}
