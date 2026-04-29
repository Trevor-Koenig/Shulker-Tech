using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;
using ShulkerTech.Web.Markdown;

namespace ShulkerTech.Web.Areas.Wiki.Pages.Articles;

[Authorize]
public class RevisionModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    WikiMarkdownService wikiMarkdown) : PageModel
{
    public ArticleRevision Revision { get; set; } = null!;
    public Article Article { get; set; } = null!;
    public string ContentHtml { get; set; } = "";

    // Diff
    [BindProperty(SupportsGet = true)]
    public string Compare { get; set; } = "current";
    public string CompareLabel { get; set; } = "";
    public List<DiffLine> DiffLines { get; set; } = [];

    public record DiffLine(DiffPiece Piece, bool SeparatorBefore)
    {
        public string Prefix => Piece.Type switch
        {
            ChangeType.Inserted => "+ ",
            ChangeType.Deleted  => "− ",
            _                   => "  ",
        };
        public string Background => Piece.Type switch
        {
            ChangeType.Inserted => "color-mix(in oklab, #4ade80 10%, transparent)",
            ChangeType.Deleted  => "color-mix(in oklab, var(--color-redstone) 12%, transparent)",
            _                   => "transparent",
        };
        public string Color => Piece.Type switch
        {
            ChangeType.Inserted => "#4ade80",
            ChangeType.Deleted  => "var(--color-redstone)",
            _                   => "var(--color-muted)",
        };
    }

    // All siblings for the compare dropdown (excludes this revision)
    public List<ArticleRevision> SiblingRevisions { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var (revision, article, canEdit) = await ResolveAsync(id);
        if (revision == null || article == null || !canEdit) return Forbid();

        Revision = revision;
        Article = article;
        ContentHtml = wikiMarkdown.ToHtml(revision.Content);

        SiblingRevisions = await db.ArticleRevisions
            .Where(r => r.ArticleId == article.Id && r.Id != id)
            .OrderByDescending(r => r.EditedAt)
            .ToListAsync();

        BuildDiff(revision, article);
        return Page();
    }

    public async Task<IActionResult> OnPostRestoreAsync(int id)
    {
        var (revision, article, canEdit) = await ResolveAsync(id);
        if (revision == null || article == null || !canEdit) return Forbid();

        var user = await userManager.GetUserAsync(User);

        db.ArticleRevisions.Add(new ArticleRevision
        {
            ArticleId = article.Id,
            Title     = article.Title,
            Content   = article.Content,
            MapUrl    = article.MapUrl,
            EditorId  = user!.Id,
            EditedAt  = DateTime.UtcNow,
        });

        article.Title     = revision.Title;
        article.Content   = revision.Content;
        article.MapUrl    = revision.MapUrl;
        article.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Redirect($"/articles/{article.Slug}");
    }

    private const int ContextLines = 3;

    private void BuildDiff(ArticleRevision revision, Article article)
    {
        string compareContent;

        if (Compare == "current" || !int.TryParse(Compare, out var compareId))
        {
            compareContent = article.Content;
            CompareLabel   = "current version";
        }
        else
        {
            var sibling = SiblingRevisions.FirstOrDefault(r => r.Id == compareId);
            if (sibling == null)
            {
                compareContent = article.Content;
                CompareLabel   = "current version";
            }
            else
            {
                compareContent = sibling.Content;
                CompareLabel   = sibling.EditedAt.ToString("MMM d, yyyy · HH:mm UTC");
            }
        }

        // Diff: revision (old) → compare target (new)
        var raw = InlineDiffBuilder.Diff(revision.Content, compareContent).Lines;

        // Collect indices of changed lines
        var visible = new HashSet<int>();
        for (var i = 0; i < raw.Count; i++)
        {
            if (raw[i].Type is ChangeType.Inserted or ChangeType.Deleted)
            {
                for (var j = Math.Max(0, i - ContextLines); j <= Math.Min(raw.Count - 1, i + ContextLines); j++)
                    visible.Add(j);
            }
        }

        // Build windowed list with separator markers for collapsed sections
        var lastVisible = -2;
        foreach (var i in Enumerable.Range(0, raw.Count).Where(visible.Contains))
        {
            DiffLines.Add(new DiffLine(raw[i], SeparatorBefore: lastVisible >= 0 && i > lastVisible + 1));
            lastVisible = i;
        }
    }

    private async Task<(ArticleRevision? revision, Article? article, bool canEdit)> ResolveAsync(int revisionId)
    {
        var revision = await db.ArticleRevisions
            .Include(r => r.Editor)
            .FirstOrDefaultAsync(r => r.Id == revisionId);

        if (revision == null) return (null, null, false);

        var article = await db.Articles.FindAsync(revision.ArticleId);
        if (article == null) return (null, null, false);

        var user = await userManager.GetUserAsync(User);
        if (user == null) return (revision, article, false);

        var settings = await db.WikiSettings.FirstOrDefaultAsync() ?? new WikiSettings();
        var userRoles = await userManager.GetRolesAsync(user);
        var editRole = article.EditRole ?? settings.EditAnyRole;
        var canEdit = user.Id == article.AuthorId ||
                      WikiSettings.UserSatisfies(editRole, userRoles, user.IsAdmin);

        return (revision, article, canEdit);
    }
}
