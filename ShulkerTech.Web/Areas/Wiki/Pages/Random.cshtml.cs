using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;

namespace ShulkerTech.Web.Areas.Wiki.Pages;

public class RandomModel(ApplicationDbContext db) : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        var slugs = await db.Articles
            .Where(a => a.IsPublished)
            .Select(a => a.Slug)
            .ToListAsync();

        if (slugs.Count == 0)
            return Redirect("/Wiki");

        var slug = slugs[Random.Shared.Next(slugs.Count)];
        return Redirect($"/articles/{slug}");
    }
}
