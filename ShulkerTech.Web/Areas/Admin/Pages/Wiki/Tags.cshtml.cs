using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;
using ShulkerTech.Web.Services;

namespace ShulkerTech.Web.Areas.Admin.Pages.Wiki;

public class TagsModel(ApplicationDbContext db) : PageModel
{
    public List<TagRow> Tags { get; set; } = [];

    [BindProperty]
    public CreateInput Create { get; set; } = new();

    [BindProperty]
    public EditInput Edit { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public record TagRow(int Id, string Name, string Slug, string Icon, string Color, string? Description, int ArticleCount);

    public class CreateInput
    {
        [Required, MaxLength(80)]
        public string Name { get; set; } = "";

        [MaxLength(10)]
        public string? Icon { get; set; }

        [Required, MaxLength(100)]
        public string Color { get; set; } = "var(--color-accent)";

        [MaxLength(300)]
        public string? Description { get; set; }
    }

    public class EditInput
    {
        public int Id { get; set; }

        [Required, MaxLength(80)]
        public string Name { get; set; } = "";

        [MaxLength(10)]
        public string? Icon { get; set; }

        [Required, MaxLength(100)]
        public string Color { get; set; } = "var(--color-accent)";

        [MaxLength(300)]
        public string? Description { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadTagsAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        ModelState.Clear();
        TryValidateModel(Create, nameof(Create));

        if (!ModelState.IsValid)
        {
            await LoadTagsAsync();
            return Page();
        }

        var slug = await SlugHelper.GenerateUniqueSlugAsync(Create.Name, s => db.Tags.AnyAsync(t => t.Slug == s));
        db.Tags.Add(new Tag
        {
            Name = Create.Name.Trim(),
            Slug = slug,
            Icon = Create.Icon?.Trim() ?? "",
            Color = string.IsNullOrWhiteSpace(Create.Color) ? "var(--color-accent)" : Create.Color.Trim(),
            Description = string.IsNullOrWhiteSpace(Create.Description) ? null : Create.Description.Trim(),
        });
        await db.SaveChangesAsync();
        StatusMessage = $"Tag \"{Create.Name.Trim()}\" created.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditAsync()
    {
        ModelState.Clear();
        TryValidateModel(Edit, nameof(Edit));

        if (!ModelState.IsValid)
        {
            await LoadTagsAsync();
            return Page();
        }

        var tag = await db.Tags.FindAsync(Edit.Id);
        if (tag == null) return NotFound();

        tag.Name = Edit.Name.Trim();
        tag.Icon = Edit.Icon?.Trim() ?? "";
        tag.Color = string.IsNullOrWhiteSpace(Edit.Color) ? "var(--color-accent)" : Edit.Color.Trim();
        tag.Description = string.IsNullOrWhiteSpace(Edit.Description) ? null : Edit.Description.Trim();
        await db.SaveChangesAsync();
        StatusMessage = $"Tag \"{tag.Name}\" updated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var tag = await db.Tags.FindAsync(id);
        if (tag != null)
        {
            db.Tags.Remove(tag);
            await db.SaveChangesAsync();
            StatusMessage = $"Tag \"{tag.Name}\" deleted.";
        }
        return RedirectToPage();
    }

    private async Task LoadTagsAsync()
    {
        Tags = await db.Tags
            .OrderBy(t => t.Name)
            .Select(t => new TagRow(
                t.Id, t.Name, t.Slug, t.Icon, t.Color, t.Description,
                t.Articles.Count))
            .ToListAsync();
    }

}
