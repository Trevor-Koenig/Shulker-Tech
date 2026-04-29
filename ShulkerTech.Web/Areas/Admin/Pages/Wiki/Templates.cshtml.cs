using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Admin.Pages.Wiki;

[Authorize]
public class TemplatesModel(ApplicationDbContext db) : PageModel
{
    public List<ArticleTemplate> Templates { get; set; } = [];

    [BindProperty]
    public CreateInput Create { get; set; } = new();

    [BindProperty]
    public EditInput Edit { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class CreateInput
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = "";

        [MaxLength(300)]
        public string? Description { get; set; }

        public string Content { get; set; } = "";

        public bool IsDefault { get; set; }
    }

    public class EditInput
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = "";

        [MaxLength(300)]
        public string? Description { get; set; }

        public string Content { get; set; } = "";

        public bool IsDefault { get; set; }
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostCreateAsync()
    {
        ModelState.Clear();
        TryValidateModel(Create, nameof(Create));

        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }

        if (Create.IsDefault)
            await ClearDefaultAsync();

        db.ArticleTemplates.Add(new ArticleTemplate
        {
            Name        = Create.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(Create.Description) ? null : Create.Description.Trim(),
            Content     = Create.Content,
            IsDefault   = Create.IsDefault,
        });
        await db.SaveChangesAsync();
        StatusMessage = $"Template \"{Create.Name.Trim()}\" created.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditAsync()
    {
        ModelState.Clear();
        TryValidateModel(Edit, nameof(Edit));

        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }

        var tmpl = await db.ArticleTemplates.FindAsync(Edit.Id);
        if (tmpl == null) return NotFound();

        if (Edit.IsDefault && !tmpl.IsDefault)
            await ClearDefaultAsync();

        tmpl.Name        = Edit.Name.Trim();
        tmpl.Description = string.IsNullOrWhiteSpace(Edit.Description) ? null : Edit.Description.Trim();
        tmpl.Content     = Edit.Content;
        tmpl.IsDefault   = Edit.IsDefault;
        tmpl.UpdatedAt   = DateTime.UtcNow;
        await db.SaveChangesAsync();
        StatusMessage = $"Template \"{tmpl.Name}\" updated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var tmpl = await db.ArticleTemplates.FindAsync(id);
        if (tmpl != null)
        {
            db.ArticleTemplates.Remove(tmpl);
            await db.SaveChangesAsync();
            StatusMessage = $"Template \"{tmpl.Name}\" deleted.";
        }
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Templates = await db.ArticleTemplates.OrderBy(t => t.Name).ToListAsync();
    }

    private async Task ClearDefaultAsync()
    {
        await db.ArticleTemplates
            .Where(t => t.IsDefault)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsDefault, false));
    }
}
