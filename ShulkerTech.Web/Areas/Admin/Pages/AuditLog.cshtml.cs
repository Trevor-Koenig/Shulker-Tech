using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Admin.Pages;

public class AuditLogModel(ApplicationDbContext db) : PageModel
{
    public List<AuditLogEntry> Entries { get; set; } = [];
    public int PageNumber { get; set; }
    public int TotalPages { get; set; }
    public string? ActionFilter { get; set; }

    private const int PageSize = 50;

    public async Task OnGetAsync(int page = 1, string? action = null)
    {
        PageNumber   = page < 1 ? 1 : page;
        ActionFilter = action;

        var query = db.AuditLog.AsQueryable();

        if (!string.IsNullOrEmpty(action))
            query = query.Where(e => e.Action == action);

        var total = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(total / (double)PageSize);
        if (PageNumber > TotalPages && TotalPages > 0)
            PageNumber = TotalPages;

        Entries = await query
            .Include(e => e.Actor)
            .OrderByDescending(e => e.OccurredAt)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }

    public static string ActionLabel(string action) => action switch
    {
        AuditAction.ArticleCreated => "Created",
        AuditAction.ArticleUpdated => "Edited",
        AuditAction.ArticleDeleted => "Deleted",
        _ => action,
    };

    public static string ActionColor(string action) => action switch
    {
        AuditAction.ArticleCreated => "var(--color-accent)",
        AuditAction.ArticleUpdated => "var(--color-crystal)",
        AuditAction.ArticleDeleted => "var(--color-redstone)",
        _ => "var(--color-muted)",
    };
}
