namespace ShulkerTech.Core.Models;

public class Article
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Slug { get; set; }
    /// <summary>Markdown source content. Rendered to HTML at display time via Markdig.</summary>
    public string Content { get; set; } = "";
    public bool IsPublished { get; set; }
    /// <summary>Minimum role to view. Null = inherit WikiSettings.DefaultViewRole.</summary>
    public string? ViewRole { get; set; }
    /// <summary>Minimum role to edit (non-author). Null = inherit WikiSettings.EditAnyRole.</summary>
    public string? EditRole { get; set; }
    public required string AuthorId { get; set; }
    public ApplicationUser Author { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
